using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Goggles3.Core;

namespace Goggles3.Multi.Windows;

public sealed class MainWindow : Window
{
    internal static readonly Brush Mint = new SolidColorBrush(Color.FromRgb(48, 219, 207));
    internal static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(145, 157, 161));
    private readonly SlotAssignments assignments = new();
    private readonly Mosaic mosaic = new();
    private readonly Tile[] tiles;
    private readonly Grid root = new(), body = new(), panels = new();
    private readonly StackPanel caption = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock count = Text("", 11, Mint), notice = Text("", 11, Muted);
    private readonly Button connectAll = Button("", true), disconnectAll = Button(""), recordMosaic = Button(""), tutorial = Button(""), language = Button("ES");
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch watch = Stopwatch.StartNew();
    private UsbAdapter[] devices = [];
    private bool spanish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";
    private bool discoveryRunning, closing, stopAllBusy, mosaicSaving, full;
    private double lastDiscovery = -3, lastStats;
    private MosaicRecording? recording;
    private Task? mosaicFinalization;
    private Rect previousBounds;
    private WindowState previousState;
    private bool previousTopmost;
    private readonly string encoder = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
    private string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Goggle 3 LiveView Multi");
    internal string T(string en, string es) => spanish ? es : en;

    public MainWindow()
    {
        Title = "Goggle 3 LiveView Multi";
        Width = Math.Min(1200, SystemParameters.WorkArea.Width * 0.94);
        Height = Math.Min(820, SystemParameters.WorkArea.Height * 0.94);
        MinWidth = 840; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new LinearGradientBrush(Color.FromRgb(10, 30, 36), Colors.Black, 30);
        Foreground = Brushes.White; FontFamily = new FontFamily("Segoe UI");
        Icon = new BitmapImage(new Uri("pack://application:,,,/AppIcon.png"));
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, Chrome());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        root.RowDefinitions.Add(new RowDefinition());
        Content = new Viewbox { Child = root, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        SizeChanged += (_, _) =>
        {
            root.Width = full ? ActualWidth : Math.Max(1040, ActualWidth);
            root.Height = full ? ActualHeight : Math.Max(720, ActualHeight);
        };
        root.Children.Add(caption);
        foreach (var item in new[] { ("—", (Action)(() => WindowState = WindowState.Minimized)), ("□", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), ("×", () => Close()) })
        {
            var button = Button(item.Item1); button.Padding = new Thickness(12, 2, 12, 2);
            button.Click += (_, _) => item.Item2(); WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            caption.Children.Add(button);
        }
        body.Margin = new Thickness(20, 0, 20, 16); Grid.SetRow(body, 1); root.Children.Add(body);
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
        body.RowDefinitions.Add(new RowDefinition());
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
        var header = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        var branding = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        branding.Children.Add(Text("G O G G L E  3  L I V E V I E W", 15, Brushes.White, true));
        branding.Children.Add(Text("M U L T I  V I E W", 9, Mint, true));
        header.Children.Add(branding); count.HorizontalAlignment = HorizontalAlignment.Right; count.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(count); body.Children.Add(header);
        panels.RowDefinitions.Add(new RowDefinition()); panels.RowDefinitions.Add(new RowDefinition());
        panels.ColumnDefinitions.Add(new ColumnDefinition()); panels.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(panels, 1); body.Children.Add(panels);
        tiles = Enumerable.Range(0, 4).Select(i => new Tile(this, i)).ToArray();
        foreach (var tile in tiles) { Grid.SetRow(tile.Surface, tile.Index / 2); Grid.SetColumn(tile.Surface, tile.Index % 2); panels.Children.Add(tile.Surface); }
        notice.Margin = new Thickness(6, 5, 6, 5); notice.TextWrapping = TextWrapping.Wrap; Grid.SetRow(notice, 2); body.Children.Add(notice);
        var bar = new DockPanel();
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right); bar.Children.Add(left);
        left.Children.Add(connectAll); left.Children.Add(tutorial); left.Children.Add(recordMosaic);
        var fullscreen = Button("⛶"); fullscreen.ToolTip = "Fullscreen · F11"; AutomationProperties.SetName(fullscreen, "Fullscreen mosaic");
        left.Children.Add(fullscreen);
        right.Children.Add(language); right.Children.Add(disconnectAll);
        var footer = Rounded(bar, new SolidColorBrush(Color.FromRgb(19, 24, 26)), 18); footer.Padding = new Thickness(10, 6, 10, 6);
        Grid.SetRow(footer, 3); body.Children.Add(footer);
        connectAll.Click += async (_, _) =>
        {
            connectAll.IsEnabled = false;
            foreach (var tile in tiles) if (tile.Session == null && !tile.Busy && assignments[tile.Index] != null) await Connect(tile);
            Refresh();
        };
        disconnectAll.Click += async (_, _) => await StopAll();
        recordMosaic.Click += async (_, _) => await ToggleMosaic();
        tutorial.Click += (_, _) => Tutorial();
        language.Click += (_, _) =>
        {
            spanish = !spanish;
            foreach (var tile in tiles) if (tile.Name.Text.StartsWith("Goggles ") || tile.Name.Text.StartsWith("Gafas ")) tile.Name.Text = T("Goggles ", "Gafas ") + (tile.Index + 1);
            RefreshChoices(); Refresh();
        };
        fullscreen.Click += (_, _) => Fullscreen(null);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && full) { Fullscreen(null); e.Handled = true; }
            else if (e.Key == Key.F11) { Fullscreen(null); e.Handled = true; }
        };
        Closing += OnClosing;
        timer.Tick += Tick; timer.Start(); RefreshChoices(); Refresh();
    }
    private static WindowChrome Chrome() => new() { CaptionHeight = 28, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(14) };
    internal static TextBlock Text(string value, double size, Brush color, bool bold = false) => new()
    { Text = value, FontSize = size, Foreground = color, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
    internal static Button Button(string label, bool primary = false) => new()
    { Content = label, Margin = new Thickness(3, 0, 3, 0), Background = primary ? Mint : new SolidColorBrush(Color.FromRgb(38, 43, 46)), Foreground = primary ? Brushes.Black : Brushes.White };
    internal static Border Rounded(UIElement child, Brush background, double radius = 14) => new()
    { Child = child, Background = background, CornerRadius = new CornerRadius(radius), BorderBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), BorderThickness = new Thickness(1) };

    private async void Tick(object? sender, EventArgs args)
    {
        double now = watch.Elapsed.TotalSeconds;
        foreach (var tile in tiles)
        {
            var frame = Volatile.Read(ref tile.Latest);
            if (frame != null && frame.Generation == tile.Generation && !ReferenceEquals(frame, tile.Displayed))
            {
                tile.Bitmap ??= new WriteableBitmap(1920, 1080, 96, 96, PixelFormats.Bgr32, null);
                tile.Bitmap.WritePixels(new Int32Rect(0, 0, 1920, 1080), frame.Pixels, 1920 * 4, 0);
                tile.Video.Source = tile.Bitmap; tile.Displayed = frame;
                tile.LastFrame = frame.Timestamp; tile.HasVideo = true;
            }
            if (tile.HasVideo && Stopwatch.GetElapsedTime(Interlocked.Read(ref tile.LastFrame)).TotalSeconds >= 3)
            { tile.HasVideo = false; tile.Video.Source = null; tile.Displayed = null; Volatile.Write(ref tile.Latest, null); }
        }
        if (now - lastStats >= 1)
        {
            foreach (var tile in tiles) tile.Fps = Interlocked.Exchange(ref tile.Frames, 0) / (now - lastStats);
            lastStats = now; Refresh();
        }
        if (!closing && !discoveryRunning && now - lastDiscovery >= 2)
        {
            discoveryRunning = true; lastDiscovery = now;
            try
            {
                var discovered = await Task.Run(Adapters.Find);
                if (closing) return;
                bool changed = !devices.SequenceEqual(discovered);
                devices = discovered;
                assignments.Discover(devices.Select(d => d.Id));
                if (changed) RefreshChoices();
                foreach (var tile in tiles)
                    if (tile.Session != null && !tile.Busy && !devices.Any(d => d.Id == assignments[tile.Index])) await Disconnect(tile);
                Refresh();
            }
            catch (Exception error) { notice.Text = error.Message; }
            finally { discoveryRunning = false; }
        }
    }
    private void RefreshChoices()
    {
        foreach (var tile in tiles)
        {
            tile.UpdatingChoices = true;
            string? selected = assignments[tile.Index];
            var choices = new List<Choice> { new(null, T("Unassigned", "Sin asignar")) };
            choices.AddRange(devices.Where(d => assignments.Available(d.Id, tile.Index)).Select(d => new Choice(d.Id, d.Label)));
            if (selected != null && !choices.Any(c => c.Id == selected)) choices.Add(new Choice(selected, T("USB disconnected", "USB desconectado")));
            tile.Source.ItemsSource = choices; tile.Source.SelectedItem = choices.FirstOrDefault(c => c.Id == selected) ?? choices[0];
            tile.UpdatingChoices = false;
        }
    }
    private void Refresh()
    {
        count.Text = $"●   {devices.Length} " + T("USB goggles", "gafas USB");
        connectAll.Content = "ϟ  " + T("Connect all", "Conectar todas");
        connectAll.IsEnabled = !closing && !stopAllBusy && tiles.Any(t => t.Session == null && !t.Busy && devices.Any(d => d.Id == assignments[t.Index]));
        disconnectAll.Content = "■  " + T("Disconnect all", "Desconectar todas");
        disconnectAll.IsEnabled = !closing && !stopAllBusy && tiles.Any(t => t.Session != null || t.Busy);
        tutorial.Content = "ⓘ  Tutorial"; language.Content = spanish ? "EN" : "ES";
        recordMosaic.Content = mosaicSaving ? T("Saving…", "Guardando…") : recording != null ? "■  " + T("Stop mosaic", "Parar mosaico") : "◉  " + T("Record mosaic", "Grabar mosaico");
        recordMosaic.IsEnabled = !closing && !mosaicSaving && (recording != null || mosaic.HasFrames());
        foreach (var tile in tiles)
        {
            bool running = tile.Session != null;
            tile.Source.IsEnabled = !running && !tile.Busy && !closing;
            tile.Connect.Content = tile.Busy ? T("Please wait…", "Espera…") : running ? T("Disconnect", "Desconectar") : "ϟ  " + T("Connect", "Conectar");
            tile.Connect.IsEnabled = !closing && !tile.Busy && (running || devices.Any(d => d.Id == assignments[tile.Index]));
            tile.Record.Content = tile.Saving ? T("Saving…", "Guardando…") : tile.Recording ? "■  " + T("Stop", "Parar") : "◉  " + T("Record", "Grabar");
            tile.Record.IsEnabled = !closing && !tile.Busy && !tile.Saving && (tile.Recording || (running && tile.HasVideo));
            tile.Placeholder.Visibility = tile.HasVideo ? Visibility.Collapsed : Visibility.Visible;
            tile.IdleTitle.Text = running ? T("Waiting for video", "Esperando vídeo") : T("Your view, on another screen", "Tu visión, en otra pantalla");
            tile.IdleSubtitle.Text = running ? T("Enable LiveView sharing on the goggles.", "Activa LiveView en las gafas.") : T("Select goggles for this channel.", "Elige las gafas de este canal.");
            string status = tile.Busy ? T("Connecting / saving…", "Conectando / guardando…") : tile.HasVideo ? T("Live", "En directo") : running ? T("Waiting for LiveView", "Esperando LiveView") : T("Ready to connect", "Listo para conectar");
            tile.Status.Text = "●  " + (tile.Error ?? status); tile.Status.ToolTip = tile.Error;
            tile.Status.Foreground = tile.Error != null ? Brushes.Salmon : tile.HasVideo ? Mint : Muted;
            tile.Metrics.Text = tile.HasVideo ? $"{tile.Fps:0}    1920×1080    {Volatile.Read(ref tile.Bitrate):0.0} Mb/s" : "—        —        —";
            tile.MetricLabels.Text = "FPS     " + T("RESOLUTION", "RESOLUCIÓN") + "     BITRATE";
        }
    }
    private async Task Connect(Tile tile)
    {
        if (tile.Busy || closing || tile.Session != null) return;
        string? id = assignments[tile.Index]; if (id == null) return;
        long attempt = ++tile.Attempt;
        tile.Busy = true; tile.Error = null; Refresh();
        try
        {
            var device = (await Task.Run(Adapters.Find)).SingleOrDefault(d => d.Id == id) ?? throw new IOException(T("Reconnect this USB device.", "Reconecta este USB."));
            if (device.Problem != 0) throw new IOException(T("Windows USB error: ", "Error USB de Windows: ") + device.Problem);
            if (device.Address == null)
            {
                notice.Text = T("Approve the Windows prompt to configure this goggles USB connection.", "Autoriza el aviso de Windows para configurar el USB de estas gafas.");
                await Adapters.PrepareAsync(id);
                for (int retry = 0; retry < 20; retry++)
                {
                    if (closing || tile.Attempt != attempt) return;
                    device = (await Task.Run(Adapters.Find)).SingleOrDefault(d => d.Id == id) ?? throw new IOException("USB disconnected.");
                    if (device.Address != null) break;
                    await Task.Delay(250);
                }
            }
            if (closing || tile.Attempt != attempt) return;
            if (device.Address == null) throw new IOException(T("USB address unavailable. Reconnect and try again.", "Dirección USB no disponible. Reconecta e inténtalo de nuevo."));
            if (!File.Exists(encoder)) throw new FileNotFoundException("ffmpeg.exe is missing. Extract the complete ZIP.");
            var session = new FfmpegVideoSession(encoder);
            long generation = mosaic.Reset(tile.Index); tile.Generation = generation; tile.Session = session;
            session.Frame += pixels =>
            {
                if (Volatile.Read(ref tile.Generation) != generation) return;
                mosaic.Update(tile.Index, generation, pixels);
                Volatile.Write(ref tile.Latest, new DecodedFrame(generation, Stopwatch.GetTimestamp(), pixels));
                Interlocked.Increment(ref tile.Frames);
            };
            session.Bitrate += rate => { if (Volatile.Read(ref tile.Generation) == generation) Volatile.Write(ref tile.Bitrate, rate); };
            session.Ended += error => Dispatcher.BeginInvoke(async () =>
            {
                if (tile.Session != session) return;
                tile.Error = error; await Disconnect(tile);
            });
            session.RecordingEnded += result => Dispatcher.BeginInvoke(() =>
            {
                if (tile.Generation != generation) return;
                tile.Recording = false; tile.Saving = false; Saved(result); Refresh();
            });
            session.Start(new UdpVideoReceiver(device.Address, interfaceIndex: device.Index));
            notice.Text = "";
        }
        catch (Exception error)
        {
            tile.Error = error.Message;
            if (tile.Session != null) await Disconnect(tile);
        }
        finally { tile.Busy = false; Refresh(); }
    }
    private async Task Disconnect(Tile tile)
    {
        var session = tile.Session; if (session == null) return;
        tile.Busy = true; tile.Session = null; tile.HasVideo = false;
        tile.Generation = mosaic.Reset(tile.Index); Volatile.Write(ref tile.Latest, null);
        tile.Video.Source = null; tile.Displayed = null; tile.Bitmap = null; Refresh();
        try { await session.DisposeAsync(); }
        catch (Exception error) { tile.Error = error.Message; }
        finally { tile.Busy = false; tile.Recording = tile.Saving = false; Refresh(); }
    }
    private async Task StopAll()
    {
        if (stopAllBusy) return;
        stopAllBusy = true; Refresh();
        foreach (var tile in tiles) tile.Attempt++;
        await StopMosaic();
        await Task.WhenAll(tiles.Select(Disconnect));
        stopAllBusy = false; Refresh();
    }
    private string RecordingPath(string label)
    {
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, $"{label}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.mp4");
    }
    private async Task ToggleRecord(Tile tile)
    {
        var session = tile.Session; if (session == null || tile.Saving) return;
        try
        {
            if (tile.Recording) { tile.Saving = true; Refresh(); await session.StopRecordingAsync(); tile.Recording = false; }
            else { session.StartRecording(RecordingPath($"Channel-{tile.Index + 1}")); tile.Recording = true; }
        }
        catch (Exception error) { tile.Error = error.Message; }
        finally { tile.Saving = false; Refresh(); }
    }
    private async Task ToggleMosaic()
    {
        if (recording != null) { await StopMosaic(); return; }
        if (!mosaic.HasFrames()) return;
        try
        {
            recording = new MosaicRecording(mosaic, encoder, RecordingPath("Mosaic"), result =>
                Dispatcher.BeginInvoke(async () => { Saved(result); if (recording != null) await StopMosaic(); Refresh(); }));
            notice.Text = T("Recording mosaic · 1920×1080 · 30 FPS", "Grabando mosaico · 1920×1080 · 30 FPS");
        }
        catch (Exception error) { notice.Text = error.Message; }
        Refresh();
    }
    private async Task StopMosaic()
    {
        var active = recording; recording = null;
        if (active == null) { if (mosaicFinalization != null) await mosaicFinalization; return; }
        mosaicSaving = true; Refresh();
        var saving = active.DisposeAsync().AsTask(); mosaicFinalization = saving;
        try { await saving; } catch (Exception error) { notice.Text = error.Message; }
        finally { mosaicSaving = false; mosaicFinalization = null; Refresh(); }
    }
    private void Saved(RecordingResult result)
    {
        notice.Text = result.Error != null ? T("Recording error: ", "Error de grabación: ") + result.Error :
            T("Saved: ", "Guardado: ") + result.Path + (result.Dropped > 0 ? T($" · {result.Dropped} dropped frames", $" · {result.Dropped} fotogramas omitidos") : "");
    }
    private void Fullscreen(int? focused)
    {
        if (!full)
        {
            previousBounds = new Rect(Left, Top, Width, Height); previousState = WindowState; previousTopmost = Topmost;
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor)) return;
            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var origin = transform.Transform(new Point(monitor.Monitor.Left, monitor.Monitor.Top));
            var end = transform.Transform(new Point(monitor.Monitor.Right, monitor.Monitor.Bottom));
            WindowState = WindowState.Normal; WindowChrome.SetWindowChrome(this, null); ResizeMode = ResizeMode.NoResize;
            MinWidth = MinHeight = 0; Left = origin.X; Top = origin.Y; Width = end.X - origin.X; Height = end.Y - origin.Y; Topmost = true;
        }
        else
        {
            Topmost = previousTopmost; ResizeMode = ResizeMode.CanResize; WindowChrome.SetWindowChrome(this, Chrome());
            MinWidth = 840; MinHeight = 560; Left = previousBounds.Left; Top = previousBounds.Top; Width = previousBounds.Width; Height = previousBounds.Height; WindowState = previousState;
        }
        full = !full;
        root.Width = full ? ActualWidth : Math.Max(1040, ActualWidth);
        root.Height = full ? ActualHeight : Math.Max(720, ActualHeight);
        root.RowDefinitions[0].Height = new GridLength(full ? 0 : 28); caption.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        body.Margin = full ? new Thickness(0) : new Thickness(20, 0, 20, 16);
        body.RowDefinitions[0].Height = new GridLength(full ? 0 : 70);
        body.RowDefinitions[3].Height = new GridLength(full ? 0 : 60);
        foreach (UIElement child in body.Children) if (child != panels) child.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        foreach (var tile in tiles)
        {
            tile.Overlay.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
            tile.Surface.Margin = new Thickness(full ? 1 : 4);
            tile.Surface.CornerRadius = new CornerRadius(full ? 0 : 14);
            tile.Surface.Visibility = full && focused != null && focused != tile.Index ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetRow(tile.Surface, full && focused == tile.Index ? 0 : tile.Index / 2); Grid.SetColumn(tile.Surface, full && focused == tile.Index ? 0 : tile.Index % 2);
            Grid.SetRowSpan(tile.Surface, full && focused == tile.Index ? 2 : 1); Grid.SetColumnSpan(tile.Surface, full && focused == tile.Index ? 2 : 1);
        }
    }
    private void Tutorial()
    {
        var stack = new StackPanel { Margin = new Thickness(26) };
        stack.Children.Add(Text(T("Connect your goggles", "Conecta tus gafas"), 22, Brushes.White, true));
        string content = T(
            "1. Connect each pair of DJI Goggles 3 with a USB data cable. Use a powered hub if needed.\n\n2. On each pair: Settings → About → OTG Wired Connection.\n\n3. In the shortcut menu, enable liveview sharing to a mobile device via Wi-Fi. Windows receives the video over USB; no Wi-Fi connection is needed.\n\n4. Without an aircraft, enable Camera View Recording in Settings → Camera → Advanced Camera Settings.\n\n5. Select the USB connection for each channel, name each pilot and click Connect. Approve the Windows USB setup prompt when requested.\n\nRecord saves one channel. Record mosaic saves all four views to a 1920×1080 MP4 at 30 FPS. Both can run together. Files are saved in Videos / Goggle 3 LiveView Multi. Stop recording and wait for Saved before closing.\n\nUse ⛶ for a clean fullscreen view; Esc or F11 exits. If there is no image, disconnect that channel, restart LiveView sharing on the goggles and reconnect. Close other viewers using those goggles. Always use the goggles as your primary display when flying.",
            "1. Conecta cada DJI Goggles 3 con un cable USB de datos. Usa un hub con alimentación si es necesario.\n\n2. En cada par de gafas: Configuración → Acerca de → Conexión por cable OTG.\n\n3. En el menú de acceso directo, activa la compartición de la vista en directo con un dispositivo móvil por Wi-Fi. Windows recibe el vídeo por USB; no necesita conectarse a esa red Wi-Fi.\n\n4. Sin aeronave, activa Grabación de la vista de cámara en Configuración → Cámara → Configuración avanzada de la cámara.\n\n5. Selecciona el USB de cada canal, pon el nombre del piloto y pulsa Conectar. Autoriza la configuración USB de Windows cuando se solicite.\n\nGrabar guarda un canal. Grabar mosaico guarda las cuatro vistas en un MP4 de 1920×1080 a 30 FPS. Ambas grabaciones pueden funcionar a la vez. Los archivos se guardan en Vídeos / Goggle 3 LiveView Multi. Para la grabación y espera a Guardado antes de cerrar.\n\nUsa ⛶ para ver la imagen a pantalla completa; Esc o F11 permite salir. Si no hay imagen, desconecta ese canal, reinicia LiveView en las gafas y reconecta. Cierra otros visores que estén usando esas gafas. Utiliza siempre las gafas como pantalla principal para volar.");
        var text = Text(content, 13, Brushes.LightGray); text.TextWrapping = TextWrapping.Wrap; text.Margin = new Thickness(0, 18, 0, 18); stack.Children.Add(text);
        var manual = Button(T("Official DJI manual ↗", "Manual oficial DJI ↗"));
        manual.Click += (_, _) => Process.Start(new ProcessStartInfo("https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf") { UseShellExecute = true });
        stack.Children.Add(manual);
        var folder = Button(T("Open recordings folder", "Abrir carpeta de grabaciones")); folder.Margin = new Thickness(3, 10, 3, 0);
        folder.Click += (_, _) => { Directory.CreateDirectory(Folder); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Folder + "\"") { UseShellExecute = true }); }; stack.Children.Add(folder);
        new Window { Title = "Goggle 3 LiveView Multi · Tutorial", Owner = this, Width = 680, Height = 730, MinWidth = 500, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(15, 26, 30)), Content = new ScrollViewer { Content = stack } }.ShowDialog();
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; closing = true; timer.Stop(); Refresh(); await StopAll(); Close();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private sealed record Choice(string? Id, string Label) { public override string ToString() => Label; }
    private sealed record DecodedFrame(long Generation, long Timestamp, byte[] Pixels);
    private sealed class Tile
    {
        public readonly int Index;
        public readonly Border Surface;
        public readonly Grid Overlay = new();
        public readonly Image Video = new() { Stretch = Stretch.Uniform };
        public readonly StackPanel Placeholder = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        public readonly TextBlock IdleTitle = Text("", 15, Brushes.White, true), IdleSubtitle = Text("", 11, Muted), Status = Text("", 11, Muted), Metrics = Text("—", 10, Brushes.White), MetricLabels = Text("", 7, Muted);
        public readonly TextBox Name;
        public readonly ComboBox Source = new() { Width = 154, FontSize = 11 };
        public readonly Button Connect = Button("", true), Record = Button("");
        public FfmpegVideoSession? Session;
        public WriteableBitmap? Bitmap;
        public DecodedFrame? Latest, Displayed;
        public bool Busy, Recording, Saving, UpdatingChoices;
        public volatile bool HasVideo;
        public long Generation, Frames, LastFrame, Attempt;
        public double Fps, Bitrate;
        public string? Error;
        public Tile(MainWindow owner, int index)
        {
            Index = index;
            var grid = new Grid { ClipToBounds = true };
            Surface = Rounded(grid, new LinearGradientBrush(Color.FromRgb(8, 25, 31), Colors.Black, 30)); Surface.Margin = new Thickness(4);
            grid.Children.Add(Video); grid.Children.Add(Placeholder); grid.Children.Add(Overlay);
            var goggles = new System.Windows.Shapes.Path { Width = 46, Height = 28, Stretch = Stretch.Uniform, Fill = new SolidColorBrush(Color.FromArgb(45, 48, 219, 207)), Stroke = Mint, StrokeThickness = 1, Data = Geometry.Parse("M 2 14 C 2 2 13 1 25 1 C 37 1 48 2 48 14 C 48 27 39 30 30 23 C 27 20 23 20 20 23 C 11 30 2 27 2 14 Z"), Margin = new Thickness(0, 0, 0, 14) };
            Placeholder.Children.Add(goggles); Placeholder.Children.Add(IdleTitle); IdleTitle.TextAlignment = TextAlignment.Center;
            IdleSubtitle.Margin = new Thickness(0, 10, 0, 0); IdleSubtitle.TextAlignment = TextAlignment.Center; Placeholder.Children.Add(IdleSubtitle);
            var top = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12) };
            var row = new DockPanel();
            DockPanel.SetDock(Source, Dock.Right); row.Children.Add(Source);
            var number = Text((index + 1).ToString("00"), 11, Mint); number.VerticalAlignment = VerticalAlignment.Center; number.Margin = new Thickness(0, 0, 10, 0); row.Children.Add(number);
            Name = new TextBox { Text = owner.T("Goggles ", "Gafas ") + (index + 1), Foreground = Brushes.White, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, MaxLength = 40 };
            AutomationProperties.SetName(Name, "Channel " + (index + 1) + " name"); AutomationProperties.SetName(Source, "Channel " + (index + 1) + " USB");
            row.Children.Add(Name); top.Children.Add(row); Status.Margin = new Thickness(0, 8, 0, 0); Status.TextTrimming = TextTrimming.CharacterEllipsis; top.Children.Add(Status); Overlay.Children.Add(top);
            var controls = new DockPanel();
            var stats = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) };
            stats.Children.Add(MetricLabels); Metrics.Margin = new Thickness(0, 3, 0, 0); stats.Children.Add(Metrics); DockPanel.SetDock(stats, Dock.Right); controls.Children.Add(stats);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; controls.Children.Add(buttons);
            Connect.FontSize = Record.FontSize = 11; Connect.Padding = Record.Padding = new Thickness(10, 8, 10, 8);
            buttons.Children.Add(Connect); buttons.Children.Add(Record);
            var full = Button("⛶"); full.Padding = new Thickness(10, 8, 10, 8); full.ToolTip = owner.T("Fullscreen", "Pantalla completa"); AutomationProperties.SetName(full, "Fullscreen channel " + (index + 1)); buttons.Children.Add(full);
            var bottom = Rounded(controls, new SolidColorBrush(Color.FromArgb(230, 20, 24, 26)), 16); bottom.Padding = new Thickness(6, 8, 6, 8); bottom.Margin = new Thickness(8); bottom.VerticalAlignment = VerticalAlignment.Bottom; Overlay.Children.Add(bottom);
            Source.SelectionChanged += (_, _) =>
            {
                if (UpdatingChoices || Busy || Session != null) return;
                if (Source.SelectedItem is Choice selected) { owner.assignments.Assign(index, selected.Id); Error = null; owner.RefreshChoices(); owner.Refresh(); }
            };
            Connect.Click += async (_, _) => { if (Session == null) await owner.Connect(this); else await owner.Disconnect(this); };
            Record.Click += async (_, _) => await owner.ToggleRecord(this);
            full.Click += (_, _) => owner.Fullscreen(index);
        }
    }
}
