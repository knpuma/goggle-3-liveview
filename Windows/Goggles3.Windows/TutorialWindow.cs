using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Goggles3.Windows;
internal sealed class TutorialWindow : Window
{
    public TutorialWindow()
    {
        Title = "Set up your Goggles 3"; Width = 660; Height = 690; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(25,28,30)); Foreground = Brushes.White;
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = "Set up your Goggles 3", FontSize = 26, FontWeight = FontWeights.Bold });
        content.Children.Add(new TextBlock { Text = "Direct USB to your computer · no RC Pro required", Foreground = Brushes.Gray, Margin = new Thickness(0,8,0,20) });
        (string Title,string Text)[] steps = [
            ("Connect the goggles to your computer", "Power on the Goggles 3 and use a USB data cable."),
            ("Enable OTG Wired Connection", "Open Settings → About and enable OTG Wired Connection. DJI documents this setting for connecting the goggles to a computer."),
            ("Enable LiveView sharing", "Push the 5D button backward and enable liveview sharing to a mobile device via Wi-Fi. Goggle 3 LiveView also needs this setting for USB reception."),
            ("Show the goggles screen without an aircraft", "Under Settings → Camera → Advanced Camera Settings, enable Camera View Recording. Keep it enabled to display the goggles screen in this app without an aircraft."),
            ("Click Connect", "On first connection, Windows may ask for permission to set up the goggles RNDIS adapter. Do not use Zadig or replace its driver with WinUSB. If no video arrives, disconnect, restart LiveView sharing on the goggles, then reconnect.")];
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
        content.Children.Add(new TextBlock { Text = "Record saves an MP4 in Videos/Goggle 3 LiveView. Click Stop to finalize the file.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.Gray });
        var footer = new DockPanel { Margin = new Thickness(0,18,0,12) };
        var close = new Button { Content = "Got it", Background = Brushes.Turquoise, Foreground = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_,_) => Close(); DockPanel.SetDock(close,Dock.Right); footer.Children.Add(close);
        var manual = new Button { Content = "Official DJI manual", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left };
        manual.Click += (_,_) => Process.Start(new ProcessStartInfo("https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf") { UseShellExecute = true });
        footer.Children.Add(manual); content.Children.Add(footer);
        content.Children.Add(new TextBlock { Text = "Setting names follow the DJI manual. USB reception in this app and operation without an aircraft are independent project features. See the project guide for connection help.", FontSize = 10, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray });
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
