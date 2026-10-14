using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Goggles3.Core;

namespace Goggles3.Windows;

public partial class MainWindow : Window
{
    private FfmpegVideoSession? session;
    private readonly DispatcherTimer stats = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly WriteableBitmap bitmap = new(1920,1080,96,96,PixelFormats.Bgr32,null);
    private double lastFrame, rate;
    private int frames, pendingUi;
    private bool recording, saving, busy, closing, fullscreen, replay;
    private long generation;
    private Rect savedBounds;
    private WindowState savedState;
    private bool savedTopmost;
    private readonly Queue<string> log = [];
    private string? currentRecordingPath;

    public MainWindow()
    {
        InitializeComponent();
        stats.Tick += (_,_) => RefreshStats(); stats.Start();
        Loaded += (_,_) =>
        {
            if (SystemParameters.WorkArea.Width >= MinWidth) Width = Math.Min(Width,SystemParameters.WorkArea.Width);
            if (SystemParameters.WorkArea.Height >= MinHeight) Height = Math.Min(Height,SystemParameters.WorkArea.Height);
            Left = SystemParameters.WorkArea.Left + Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
            Top = SystemParameters.WorkArea.Top + Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
            string[] args = Environment.GetCommandLineArgs();
            int replayIndex = Array.IndexOf(args, "--replay");
            if (replayIndex >= 0 && replayIndex + 1 < args.Length) StartReplay(args[replayIndex + 1]);
            ConnectButton.Focus();
        };
        var diagnosticMenu = new System.Windows.Controls.ContextMenu();
        var item = new System.Windows.Controls.MenuItem { Header = "Diagnóstico" };
        item.Click += (_,_) => MessageBox.Show(this, string.Join(Environment.NewLine, log), "Diagnóstico USB");
        diagnosticMenu.Items.Add(item); Hud.ContextMenu = diagnosticMenu;
    }

    private async void ConnectClick(object sender, RoutedEventArgs e)
    {
        if (busy || closing) return;
        busy = true; ConnectButton.IsEnabled = false;
        try
        {
            if (session != null) { await DisconnectAsync(); return; }
            Status.Text = "Abriendo conexión con las gafas…";
            var address = await AdapterLocator.FindAsync();
            if (closing) return;
            if (address == null)
            {
                if (MessageBox.Show(this, "La conexión USB necesita preparar el adaptador RNDIS de las Goggles 3. Se añadirá 192.168.60.1/24 solo a ese adaptador; no se cambiarán Wi-Fi, Ethernet, DNS ni la puerta de enlace. Windows pedirá permiso de administrador. ¿Prepararlo ahora?",
                    "Preparar conexión USB", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                { Status.Text = "Conexión cancelada"; return; }
                await AdapterLocator.PrepareAsync();
                address = await AdapterLocator.FindAsync();
                if (closing) return;
                if (address == null) throw new IOException("No se detecta el adaptador RNDIS de las Goggles 3. Consulta Tutorial.");
            }
            replay = false;
            var active = CreateSession(); active.Start(new UdpVideoReceiver(address));
        }
        catch (Exception error) { AddLog(error.Message); await DisconnectAsync(); Status.Text = error.Message; }
        finally { busy = false; if (!closing) ConnectButton.IsEnabled = true; }
    }

    private string DecoderPath => Path.Combine(AppContext.BaseDirectory,"ffmpeg","ffmpeg.exe");

    private FfmpegVideoSession CreateSession()
    {
        if (!File.Exists(DecoderPath)) throw new FileNotFoundException("Falta el decodificador. Extrae todo el ZIP, no solo Goggle 3 LiveView.exe.");
        long token = ++generation;
        var active = new FfmpegVideoSession(DecoderPath); session = active;
        frames = 0; rate = 0; lastFrame = clock.Elapsed.TotalSeconds;
        active.Frame += pixels =>
        {
            if (Interlocked.CompareExchange(ref pendingUi,1,0) != 0) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                try
                {
                    if (token != generation || closing) return;
                    bitmap.WritePixels(new Int32Rect(0,0,1920,1080), pixels,1920*4,0);
                    Video.Source = bitmap; Idle.Visibility = Visibility.Collapsed;
                    frames++; lastFrame = clock.Elapsed.TotalSeconds;
                    Status.Text = replay ? "Prueba local · captura grabada, no USB en directo" : "En directo · imagen recibida por USB";
                    StatusDot.Fill = (Brush)FindResource("Mint");
                    RecordButton.IsEnabled = !saving;
                }
                finally { Interlocked.Exchange(ref pendingUi,0); }
            }));
        };
        active.Bitrate += value => Dispatcher.BeginInvoke(new Action(() => { if (token == generation) rate = value; }));
        active.Log += text => Dispatcher.BeginInvoke(new Action(() => { if (token == generation) AddLog(text); }));
        active.Ended += message => Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (token != generation || closing) return;
            await DisconnectAsync(); Status.Text = message;
        }));
        active.RecordingEnded += result => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (result.Path != currentRecordingPath) return;
            recording = false; saving = false; RecordLabel.Text = "Grabar"; RecordDot.Fill = Brushes.White;
            RecordingNotice.Visibility = Visibility.Visible;
            RecordingText.Foreground = Brushes.LightGray;
            RecordingText.Text = result.Error ?? $"Guardado en Vídeos/Goggle 3 LiveView: {Path.GetFileName(result.Path)}" +
                (result.Dropped > 0 ? $" · {result.Dropped} fotogramas omitidos por carga" : "");
            RecordButton.IsEnabled = session != null && Video.Source != null;
        }));
        ConnectLabel.Text = "Desconectar";
        ConnectGlyph.Data = Geometry.Parse("M 0,0 L 12,0 L 12,12 L 0,12 Z");
        IdleTitle.Text = "Esperando imagen"; IdleHint.Text = "Comprueba LiveView y los ajustes de las gafas.";
        return active;
    }

    private void StartReplay(string path)
    {
        try { replay = true; CreateSession().StartReplay(path); }
        catch (Exception error) { Status.Text = error.Message; AddLog(error.Message); }
    }

    private async Task DisconnectAsync()
    {
        generation++; FfmpegVideoSession? active = session; session = null;
        if (recording) { recording = false; saving = true; RecordLabel.Text = "Guardando…"; }
        if (active != null) await active.DisposeAsync();
        Video.Source = null; Idle.Visibility = Visibility.Visible;
        Status.Text = "Desconectado"; StatusDot.Fill = new SolidColorBrush(Color.FromArgb(96,255,255,255));
        ConnectLabel.Text = "Conectar"; ConnectGlyph.Data = Geometry.Parse("M 7,0 L 0,8 L 5,8 L 3,15 L 11,6 L 6,6 Z");
        IdleTitle.Text = "Tu visión, en una pantalla más grande"; IdleHint.Text = "Conecta las Goggles 3 por USB-C y pulsa Conectar.";
        RecordButton.IsEnabled = false; Fps.Text = Resolution.Text = Bitrate.Text = "—";
        frames = 0; rate = 0;
    }

    private void RefreshStats()
    {
        if (session == null) return;
        if (clock.Elapsed.TotalSeconds - lastFrame > 3)
        {
            Status.Text = "Esperando imagen · comprueba LiveView en las gafas";
            Video.Source = null; Idle.Visibility = Visibility.Visible;
            RecordButton.IsEnabled = false; Fps.Text = Resolution.Text = Bitrate.Text = "—";
            StatusDot.Fill = Brushes.Gray;
        }
        else if (Video.Source != null)
        {
            Fps.Text = frames.ToString(); Resolution.Text = "1920×1080";
            Bitrate.Text = replay || rate <= 0 ? "—" : $"{rate:F1} Mbps";
        }
        frames = 0;
    }

    private async void RecordClick(object sender, RoutedEventArgs e)
    {
        FfmpegVideoSession? active = session;
        if (active == null || saving) return;
        try
        {
            if (recording)
            {
                recording = false; saving = true; RecordButton.IsEnabled = false;
                RecordLabel.Text = "Guardando…"; await active.StopRecordingAsync();
            }
            else
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"Goggle 3 LiveView");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory,$"Goggle3LiveView_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Guid.NewGuid().ToString()[..4]}.mp4");
                currentRecordingPath = path; active.StartRecording(path); recording = true;
                RecordLabel.Text = "Detener"; RecordDot.Fill = Brushes.Tomato;
                RecordingText.Foreground = Brushes.Tomato; RecordingText.Text = "Grabando · " + Path.GetFileName(path);
                RecordingNotice.Visibility = Visibility.Visible;
            }
        }
        catch (Exception error) { RecordingText.Text = error.Message; RecordingNotice.Visibility = Visibility.Visible; AddLog(error.Message); }
    }

    private void FullscreenClick(object sender, RoutedEventArgs e) => SetFullscreen(true);
    private void SetFullscreen(bool enable)
    {
        if (enable == fullscreen) return;
        fullscreen = enable;
        if (enable)
        {
            savedBounds = RestoreBounds; savedState = WindowState; savedTopmost = Topmost;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(this).Handle,2),ref info))
            { fullscreen = false; return; }
            Matrix transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Point origin = transform.Transform(new Point(info.Monitor.Left,info.Monitor.Top));
            Point extent = transform.Transform(new Point(info.Monitor.Right,info.Monitor.Bottom));
            WindowChrome.SetWindowChrome(this,null); WindowState = WindowState.Normal; ResizeMode = ResizeMode.NoResize;
            Left = origin.X; Top = origin.Y; Width = extent.X-origin.X; Height = extent.Y-origin.Y; Topmost = true;
            CaptionRow.Height = new GridLength(0); CaptionControls.Visibility = Hud.Visibility = Visibility.Collapsed;
        }
        else
        {
            Topmost = savedTopmost; ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight = 28, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(14) });
            Left = savedBounds.Left; Top = savedBounds.Top; Width = savedBounds.Width; Height = savedBounds.Height;
            WindowState = savedState; CaptionRow.Height = new GridLength(28); CaptionControls.Visibility = Hud.Visibility = Visibility.Visible;
        }
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && fullscreen) { SetFullscreen(false); e.Handled = true; }
        else if (e.Key == Key.F11) { SetFullscreen(!fullscreen); e.Handled = true; }
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; closing = true; ConnectButton.IsEnabled = RecordButton.IsEnabled = false;
        stats.Stop(); await DisconnectAsync(); Close();
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void AddLog(string text)
    {
        while (log.Count >= 100) log.Dequeue();
        log.Enqueue(DateTime.Now.ToString("HH:mm:ss") + " " + text);
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Goggle 3 LiveView");
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "diagnostic.txt"), log);
        }
        catch (IOException) { /* Diagnostics must not interrupt the viewer. */ }
        catch (UnauthorizedAccessException) { }
    }
    private void TutorialClick(object sender, RoutedEventArgs e) => new TutorialWindow { Owner = this }.ShowDialog();

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
