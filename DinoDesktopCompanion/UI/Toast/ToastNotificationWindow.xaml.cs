using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DinoDesktopCompanion.UI.Toast;

public partial class ToastNotificationWindow : Window
{
    private readonly DispatcherTimer _timer;

    public ToastNotificationWindow(string title, string message, string icon)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        IconText.Text = icon;
        Opacity = 0;
        
        // Position bottom right of primary screen
        var workArea = System.Windows.SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            var fadeOut = (Storyboard)FindResource("FadeOut");
            fadeOut.Completed += (s2, e2) => Close();
            BeginStoryboard(fadeOut);
        };
        
        Loaded += (s, e) =>
        {
            BeginStoryboard((Storyboard)FindResource("FadeIn"));
            _timer.Start();
        };
    }

    public static void ShowToast(string title, string message, string icon = "⭐")
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var toast = new ToastNotificationWindow(title, message, icon);
            toast.Show();
        });
    }
}
