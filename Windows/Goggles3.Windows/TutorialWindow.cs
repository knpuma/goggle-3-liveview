using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Goggles3.Windows;
internal sealed class TutorialWindow : Window
{
    public TutorialWindow()
    {
        Title = "Prepara tus Goggles 3"; Width = 660; Height = 690; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(25,28,30)); Foreground = Brushes.White;
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = "Prepara tus Goggles 3", FontSize = 26, FontWeight = FontWeights.Bold });
        content.Children.Add(new TextBlock { Text = "USB directo al ordenador · sin RC Pro", Foreground = Brushes.Gray, Margin = new Thickness(0,8,0,20) });
        (string Title,string Text)[] steps = [
            ("Conecta las gafas al ordenador", "Enciende las Goggles 3 y utiliza un cable USB de datos."),
            ("Activa Conexión por cable OTG", "En Configuración → Acerca de, activa el modo OTG. DJI describe este ajuste para conectar las gafas al ordenador."),
            ("Activa la vista en directo compartida", "Empuja el botón 5D hacia atrás y selecciona «Compartir la vista en directo con el dispositivo móvil por Wi-Fi». Goggle 3 LiveView utiliza también este ajuste para recibir por USB."),
            ("Para ver la pantalla sin aeronave", "En Configuración → Cámara → Configuración avanzada de la cámara, habilita «Grabación de la vista de cámara». DJI indica que incluye la información en pantalla; mantenla activada para el modo sin dron de Goggle 3 LiveView."),
            ("Pulsa Conectar", "La primera vez, Windows puede pedir permiso para preparar solo el adaptador RNDIS de las gafas. No uses Zadig ni sustituyas el controlador USB por WinUSB. Si espera imagen, desconecta, desactiva y activa la vista compartida y conecta de nuevo.")];
        for (int i = 0; i < steps.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0,0,0,16) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = (i+1).ToString(), FontSize = 17, Foreground = Brushes.Turquoise });
            var labels = new StackPanel(); Grid.SetColumn(labels,1);
            labels.Children.Add(new TextBlock { Text = steps[i].Title, FontWeight = FontWeights.SemiBold, FontSize = 14 });
            labels.Children.Add(new TextBlock { Text = steps[i].Text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, FontSize = 13, Margin = new Thickness(0,5,0,0) });
            row.Children.Add(labels); content.Children.Add(row);
        }
        content.Children.Add(new TextBlock { Text = "Grabar crea un MP4 en Vídeos/Goggle 3 LiveView. Usa Detener para finalizar el archivo.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.Gray });
        var footer = new DockPanel { Margin = new Thickness(0,18,0,12) };
        var close = new Button { Content = "Entendido", Background = Brushes.Turquoise, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_,_) => Close(); DockPanel.SetDock(close,Dock.Right); footer.Children.Add(close);
        var manual = new Button { Content = "Manual oficial DJI · págs. 24, 28–31", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left };
        manual.Click += (_,_) => Process.Start(new ProcessStartInfo("https://dl.djicdn.com/downloads/DJI_Goggles_3/20240528/DJI_Goggles_3_User_Manual_ES.pdf") { UseShellExecute = true });
        footer.Children.Add(manual); content.Children.Add(footer);
        content.Children.Add(new TextBlock { Text = "Los nombres y rutas proceden del manual DJI. La recepción en el ordenador y la pantalla sin aeronave son funciones del proyecto; consulta la guía del proyecto para conectar el ordenador.", FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray });
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
