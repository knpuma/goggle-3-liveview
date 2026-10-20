using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Goggles3.Core;

namespace Goggle3.Linux;

public sealed class MainWindow : Window
{
    private static readonly IBrush Mint = Brush.Parse("#95E4D0");
    private readonly Image video = new() { Stretch = Stretch.Uniform };
    private readonly Grid hud = new();
    private readonly StackPanel idle = new() { Spacing = 14, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock headline = new() { FontSize = 29, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock hint = new() { FontSize = 15, Foreground = Brush.Parse("#92AAA9"), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { Foreground = Mint, FontSize = 13 };
    private readonly TextBlock notice = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 900, FontSize = 13 };
    private readonly TextBlock stats = new() { FontFamily = FontFamily.Default, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button connect = new(), tutorial = new(), record = new(), fullscreen = new(), language = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch frameWatch = Stopwatch.StartNew();
    private FfmpegVideoSession? session;
    private WriteableBitmap? bitmap;
    private byte[]? pending;
    private bool spanish, recording, busy, closing, canClose, hasVideo;
    private long frameCount, previousFrames;
    private double rate, lastStats, lastFrame;
    private string? recordingPath;
    private WindowState previousState;
    private readonly string preferencePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "goggle-3-liveview", "language");
    private string T(string english, string es) => spanish ? es : english;

    public MainWindow()
    {
        Title = "Goggle 3 LiveView"; Width = 1180; Height = 720; MinWidth = 880; MinHeight = 520;
        FontFamily = new FontFamily("DejaVu Sans");
        Background = Brush.Parse("#080D10");
        try { spanish = File.ReadAllText(preferencePath).Trim() == "es"; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (Program.Arguments.Contains("--smoke-test")) spanish = false;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://goggle-3-liveview/Assets/icon.png")));
        var root = new Grid(); Content = root;
        root.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Color.Parse("#10262A"), 0), new GradientStop(Color.Parse("#070B0F"), 0.8)] };
        root.Children.Add(video);
        var icon = new Image { Width = 90, Height = 90, Source = new Bitmap(AssetLoader.Open(new Uri("avares://goggle-3-liveview/Assets/icon.png"))) };
        idle.Children.Add(icon); idle.Children.Add(headline); idle.Children.Add(hint); root.Children.Add(idle);
        hud.RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"); hud.Margin = new Thickness(24);
        var top = new DockPanel { LastChildFill = false };
        top.Children.Add(new TextBlock { Text = "Goggle 3 LiveView", FontSize = 17, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var badge = new Border { Background = Brush.Parse("#CC172B2E"), CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 10), Child = status };
        DockPanel.SetDock(badge, Dock.Right); top.Children.Add(badge); hud.Children.Add(top);
        var noteBox = new Border { Background = Brush.Parse("#D9121E22"), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 12), Child = notice };
        Grid.SetRow(noteBox, 2); hud.Children.Add(noteBox);
        notice.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty) noteBox.IsVisible = !string.IsNullOrEmpty(notice.Text); };
        noteBox.IsVisible = false;
        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        foreach (var button in new[] { connect, tutorial, record, fullscreen, language })
        {
            button.Padding = new Thickness(14, 10); button.CornerRadius = new CornerRadius(8);
            buttons.Children.Add(button);
        }
        connect.Background = Mint; connect.Foreground = Brush.Parse("#102421"); connect.FontWeight = FontWeight.SemiBold;
        fullscreen.Content = new Avalonia.Controls.Shapes.Path { Width = 18, Height = 18,
            Data = StreamGeometry.Parse("M7,1 L1,1 L1,7 M13,1 L19,1 L19,7 M1,13 L1,19 L7,19 M13,19 L19,19 L19,13"),
            Stroke = Brushes.White, StrokeThickness = 1.6, Stretch = Stretch.Uniform };
        fullscreen.Padding = new Thickness(12, 10);
        bottom.Children.Add(buttons); Grid.SetColumn(stats, 2); bottom.Children.Add(stats);
        var bar = new Border { Background = Brush.Parse("#ED14272B"), BorderBrush = Brush.Parse("#324348"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Child = bottom };
        Grid.SetRow(bar, 3); hud.Children.Add(bar); root.Children.Add(hud);
        connect.Click += async (_, _) => await ToggleConnectionAsync();
        tutorial.Click += async (_, _) => { var guide = CreateTutorial(); await guide.ShowDialog(this); };
        record.Click += async (_, _) => await ToggleRecordingAsync();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        language.Click += (_, _) =>
        {
            spanish = !spanish; RefreshText();
            try { Directory.CreateDirectory(Path.GetDirectoryName(preferencePath)!); File.WriteAllText(preferencePath, spanish ? "es" : "en"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; }
            else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen) { ToggleFullscreen(); e.Handled = true; }
        };
        PropertyChanged += (_, e) => { if (e.Property == WindowStateProperty) hud.IsVisible = WindowState != WindowState.FullScreen; };
        timer.Tick += (_, _) => DrawFrame(); timer.Start();
        Closing += async (_, e) =>
        {
            if (canClose) return;
            e.Cancel = true;
            if (closing || busy) return;
            closing = true; SetBusy(true);
            try { await DisconnectAsync(); }
            finally { timer.Stop(); bitmap?.Dispose(); canClose = true; Close(); }
        };
        RefreshText();
        Opened += async (_, _) =>
        {
            int i = Array.IndexOf(Program.Arguments, "--smoke-test");
            if (i >= 0 && i + 1 < Program.Arguments.Length) await SmokeAsync(Program.Arguments[i + 1]);
        };
    }

    private void RefreshText()
    {
        headline.Text = T("Your view, on a bigger screen.", "Tu visión, en una pantalla más grande.");
        hint.Text = T("Connect your DJI Goggles 3 with a USB data cable.", "Conecta tus DJI Goggles 3 con un cable USB de datos.");
        connect.Content = session == null ? T("Connect", "Conectar") : T("Disconnect", "Desconectar");
        tutorial.Content = "Tutorial"; record.Content = recording ? T("■ Stop", "■ Detener") : T("● Record", "● Grabar");
        language.Content = spanish ? "EN" : "ES";
        ToolTip.SetTip(language, T("Switch to Spanish", "Cambiar a inglés"));
        ToolTip.SetTip(fullscreen, T("Clean fullscreen · F11 / Esc", "Pantalla completa · F11 / Esc"));
        ToolTip.SetTip(stats, T("Rendered frames · preview size · received video bitrate (not the radio link)", "Fotogramas recibidos · tamaño de vista · bitrate recibido (no el enlace de radio)"));
        status.Text = session == null ? T("●  Ready for USB", "●  Listo para USB") : hasVideo ? T("●  Live · USB", "●  En directo · USB") : T("○  Waiting for video", "○  Esperando vídeo");
        UpdateStats(); record.IsEnabled = hasVideo && !busy; fullscreen.IsEnabled = true;
    }
    private void UpdateStats(double fps = 0) => stats.Text = $"{fps:0} FPS    ·    {(hasVideo ? "1920 × 1080" : "— × —")}    ·    {rate:0.0} Mb/s";
    private void SetBusy(bool value) { busy = value; connect.IsEnabled = !value; record.IsEnabled = !value && hasVideo; }

    private async Task ToggleConnectionAsync()
    {
        if (busy) return; SetBusy(true); notice.Text = "";
        try
        {
            if (session != null) { await DisconnectAsync(); return; }
            if (!File.Exists("/usr/bin/ffmpeg")) throw new IOException(T("Install FFmpeg first: sudo apt install ffmpeg", "Instala FFmpeg primero: sudo apt install ffmpeg"));
            var adapters = UsbNetwork.Find();
            if (adapters.Count == 0) throw new IOException(T("No DJI USB network adapter found. Enable OTG Wired Connection, check the data cable and open Tutorial.", "No se detecta el adaptador USB DJI. Activa OTG Wired Connection, comprueba el cable de datos y abre Tutorial."));
            if (adapters.Count > 1) throw new IOException(T("This viewer uses one pair of goggles. Disconnect the other DJI USB devices.", "Este visor utiliza unas gafas. Desconecta los otros dispositivos USB DJI."));
            notice.Text = T("Preparing the DJI USB adapter. Approve the system authorization dialog if requested.", "Preparando el adaptador USB DJI. Autoriza el diálogo del sistema si aparece.");
            await UsbNetwork.PrepareAsync(adapters[0]);
            notice.Text = T("Waiting for LiveView. If no image arrives, restart LiveView sharing on the goggles, then disconnect and connect again.", "Esperando LiveView. Si no llega imagen, reinicia la compartición LiveView en las gafas y vuelve a conectar.");
            var active = new FfmpegVideoSession("/usr/bin/ffmpeg"); session = active;
            active.Frame += pixels => { Interlocked.Exchange(ref pending, pixels); Interlocked.Increment(ref frameCount); };
            active.Bitrate += value => rate = value;
            active.RecordingEnded += result => Dispatcher.UIThread.Post(() =>
            {
                recording = false;
                notice.Text = result.Error == null ? T("Saved: ", "Guardado: ") + result.Path + (result.Dropped > 0 ? T($" · {result.Dropped} frames dropped", $" · {result.Dropped} fotogramas omitidos") : "") : T("Recording error: ", "Error de grabación: ") + result.Error;
                RefreshText();
            });
            active.Ended += message => Dispatcher.UIThread.Post(async () =>
            {
                if (session != active) return;
                SetBusy(true);
                try { await DisconnectAsync(); notice.Text = T("Connection ended: ", "Conexión finalizada: ") + message; }
                finally { SetBusy(false); }
            });
            active.Start(new UdpVideoReceiver(UsbNetwork.LocalAddress, interfaceIndex: adapters[0].Index));
        }
        catch (Exception error)
        {
            await DisconnectAsync(); notice.Text = error.Message;
        }
        finally { SetBusy(false); RefreshText(); }
    }

    private async Task DisconnectAsync()
    {
        var active = session; session = null;
        if (active != null) await active.DisposeAsync();
        recording = false; hasVideo = false; rate = 0; Interlocked.Exchange(ref pending, null);
        video.Source = null; bitmap?.Dispose(); bitmap = null; idle.IsVisible = true;
        previousFrames = Interlocked.Read(ref frameCount); RefreshText();
    }
    private void DrawFrame()
    {
        var pixels = Interlocked.Exchange(ref pending, null);
        if (pixels != null)
        {
            bitmap ??= new WriteableBitmap(new PixelSize(1920, 1080), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var buffer = bitmap.Lock())
            {
                if (buffer.RowBytes == 1920 * 4) Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
                else for (int y = 0; y < 1080; y++) Marshal.Copy(pixels, y * 1920 * 4, buffer.Address + y * buffer.RowBytes, 1920 * 4);
            }
            video.Source = bitmap; video.InvalidateVisual(); idle.IsVisible = false;
            if (!hasVideo) { hasVideo = true; notice.Text = ""; RefreshText(); }
            lastFrame = frameWatch.Elapsed.TotalSeconds;
        }
        double now = frameWatch.Elapsed.TotalSeconds;
        if (now - lastStats >= 1)
        {
            long count = Interlocked.Read(ref frameCount);
            UpdateStats((count - previousFrames) / (now - lastStats)); previousFrames = count; lastStats = now;
            if (session != null && hasVideo && now - lastFrame > 3)
            {
                status.Text = T("○  Video interrupted", "○  Vídeo interrumpido");
                notice.Text = T("No new frames. Disconnect and restart LiveView sharing before reconnecting.", "No llegan fotogramas. Desconecta y reinicia LiveView antes de volver a conectar.");
            }
            else if (session != null && hasVideo) status.Text = T("●  Live · USB", "●  En directo · USB");
        }
    }
    private async Task ToggleRecordingAsync()
    {
        if (busy || session == null) return; SetBusy(true);
        try
        {
            if (recording) { notice.Text = T("Saving MP4…", "Guardando MP4…"); await session.StopRecordingAsync(); recording = false; }
            else
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Goggle 3 LiveView");
                if (!Path.IsPathRooted(folder)) folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos", "Goggle 3 LiveView");
                Directory.CreateDirectory(folder);
                recordingPath = Path.Combine(folder, $"Goggle3-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.mp4");
                session.StartRecording(recordingPath); recording = true;
                notice.Text = T("Recording locally · ", "Grabando en local · ") + recordingPath;
            }
        }
        catch (Exception error) { notice.Text = T("Recording error: ", "Error de grabación: ") + error.Message; }
        finally { SetBusy(false); RefreshText(); }
    }
    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen) WindowState = previousState;
        else { previousState = WindowState; WindowState = WindowState.FullScreen; }
        hud.IsVisible = WindowState != WindowState.FullScreen;
    }
    private Window CreateTutorial()
    {
        var panel = new StackPanel { Margin = new Thickness(30), Spacing = 19 };
        panel.Children.Add(new TextBlock { Text = T("Connect your goggles", "Conecta tus gafas"), FontSize = 27, FontWeight = FontWeight.SemiBold });
        string[] steps = spanish ? [
            "01   Conecta las Goggles 3 encendidas con un cable USB de datos.",
            "02   En las gafas: Settings → About → OTG Wired Connection.",
            "03   Mueve el botón 5D hacia atrás y activa la compartición LiveView con un dispositivo móvil por Wi-Fi. Esta app recibe la imagen por USB; no conectes el ordenador a la Wi-Fi de las gafas.",
            "04   Pulsa Conectar. Si aparece la autorización del sistema, permite configurar únicamente el adaptador USB DJI.",
            "Sin aeronave enlazada: mantén activado Camera View Recording en Settings → Camera → Advanced Camera Settings, además de OTG y LiveView.",
            "Grabar guarda un MP4 sin audio en Vídeos / Goggle 3 LiveView. Espera a que termine de guardarse. F11 activa la pantalla completa sin controles; Esc vuelve a la ventana.",
            "Si no aparece imagen: cierra otros visores, reinicia la compartición LiveView y vuelve a conectar. No cambies los controladores de otros adaptadores ni desactives el cortafuegos."] : [
            "01   Power on your Goggles 3 and connect a USB data cable.",
            "02   On the goggles: Settings → About → OTG Wired Connection.",
            "03   Push the 5D button backward and enable LiveView sharing to a mobile device via Wi-Fi. This app receives video over USB; do not join the goggles Wi-Fi network on your computer.",
            "04   Click Connect. If system authorization appears, allow configuration of the DJI USB adapter only.",
            "Without a linked aircraft: keep Camera View Recording enabled in Settings → Camera → Advanced Camera Settings, as well as OTG and LiveView.",
            "Record saves a silent MP4 in Videos / Goggle 3 LiveView. Wait for saving to finish. F11 enables clean fullscreen; Esc returns to the window.",
            "No image? Close other viewers, restart LiveView sharing and reconnect. Do not replace other network drivers or disable your firewall."];
        foreach (string step in steps) panel.Children.Add(new TextBlock { Text = step, TextWrapping = TextWrapping.Wrap, FontSize = 15, LineHeight = 23 });
        panel.Children.Add(new TextBlock { Text = T("Independent MIT community project. DJI menu names follow the official Goggles 3 manual; USB viewing on Linux is not a DJI-supported feature.", "Proyecto comunitario MIT independiente. Los nombres de menús siguen el manual oficial de DJI; el visor USB para Linux no es una función respaldada por DJI."), Foreground = Brush.Parse("#9CB1B3"), TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var manual = new Button { Content = T("DJI manual", "Manual DJI") };
        manual.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri("https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf"));
        var help = new Button { Content = T("Linux help", "Ayuda Linux") };
        help.Click += async (_, _) => await Launcher.LaunchUriAsync(new Uri("https://github.com/knpuma/goggle-3-liveview/blob/main/Linux/README.md"));
        links.Children.Add(manual); links.Children.Add(help); panel.Children.Add(links);
        return new Window { Title = T("USB setup · Goggle 3 LiveView", "Configuración USB · Goggle 3 LiveView"), Width = 690, Height = 740, MinWidth = 500, MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush.Parse("#102024"), Content = new ScrollViewer { Content = panel } };
    }

    // CI-only entry point; not exposed as a user feature, no demo media shipped.
    private async Task SmokeAsync(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory); await Task.Delay(600);
            Capture(this, Path.Combine(directory, "linux-en.png"));
            spanish = true; RefreshText(); await Task.Delay(200);
            Capture(this, Path.Combine(directory, "linux-es.png"));
            if (connect.Content?.ToString() != "Conectar") throw new InvalidOperationException("Spanish UI failed");
            spanish = false; RefreshText();
            var guide = CreateTutorial(); guide.Show(this); await Task.Delay(300);
            Capture(guide, Path.Combine(directory, "linux-tutorial.png")); guide.Close();
            ToggleFullscreen(); await Task.Delay(800);
            if (WindowState != WindowState.FullScreen || hud.IsVisible) throw new InvalidOperationException("Fullscreen failed");
            var screen = Screens.ScreenFromWindow(this) ?? throw new InvalidOperationException("No display found");
            if (Math.Abs(Bounds.Width * RenderScaling - screen.Bounds.Width) > 2 || Math.Abs(Bounds.Height * RenderScaling - screen.Bounds.Height) > 2)
                throw new InvalidOperationException("Window did not actually fill the screen");
            ToggleFullscreen(); await Task.Delay(300);
            if (!hud.IsVisible || WindowState == WindowState.FullScreen) throw new InvalidOperationException("Fullscreen restore failed");
            var pixels = new byte[FfmpegVideoSession.FrameSize];
            for (int p = 0; p < pixels.Length; p += 4) { pixels[p] = 30; pixels[p + 1] = 80; pixels[p + 2] = 120; pixels[p + 3] = 255; }
            Interlocked.Exchange(ref pending, pixels); await Task.Delay(300);
            if (video.Source == null || idle.IsVisible) throw new InvalidOperationException("Video surface did not render");
            notice.Text = "Automated rendering check — not USB video.";
            Capture(this, Path.Combine(directory, "rendering-check.png"));
            await DisconnectAsync();
            await ToggleConnectionAsync();
            if (session != null || string.IsNullOrWhiteSpace(notice.Text)) throw new InvalidOperationException("Missing USB error failed");
            File.WriteAllText(Path.Combine(directory, "ui-smoke.txt"), "PASS: English, Spanish, tutorial, actual fullscreen dimensions, restore, BGRA rendering, missing USB handling. No hardware video claim.\n");
            Close();
        }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
    }
    private static void Capture(Window window, string path)
    {
        using var shot = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
        shot.Render(window); shot.Save(path);
    }
}
