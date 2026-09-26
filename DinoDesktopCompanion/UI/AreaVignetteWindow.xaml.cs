using System.Windows;
using System.Windows.Controls;

namespace DinoDesktopCompanion.UI;

public partial class AreaVignetteWindow : Window
{
    public AreaVignetteWindow()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Right - Width - 10;
        Top = SystemParameters.WorkArea.Bottom - Height - 10;
    }

    public void UpdateArea(string areaId)
    {
        switch (areaId?.ToLowerInvariant())
        {
            case "garten":
                VignetteIcon.Text = "🌿";
                VignetteText.Text = "Garten";
                VignetteBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 44, 143, 124));
                break;
            case "wald":
                VignetteIcon.Text = "🌲";
                VignetteText.Text = "Wald";
                VignetteBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 34, 90, 44));
                break;
            case "strand":
                VignetteIcon.Text = "🐚";
                VignetteText.Text = "Strand";
                VignetteBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 50, 150, 200));
                break;
            case "hoehle":
                VignetteIcon.Text = "🦇";
                VignetteText.Text = "Höhle";
                VignetteBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 80, 70, 90));
                break;
            case "schneeland":
                VignetteIcon.Text = "❄️";
                VignetteText.Text = "Schneeland";
                VignetteBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 150, 200, 220));
                VignetteText.Foreground = System.Windows.Media.Brushes.DarkSlateGray;
                break;
            default:
                Hide();
                return;
        }
        VignetteText.Foreground = (areaId?.ToLowerInvariant() == "schneeland") ? System.Windows.Media.Brushes.DarkSlateGray : System.Windows.Media.Brushes.White;
        Show();
    }
}
