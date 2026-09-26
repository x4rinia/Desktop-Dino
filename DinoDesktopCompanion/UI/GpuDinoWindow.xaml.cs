using System.Windows;
using System.Windows.Input;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.GPU;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.UI;

public partial class GpuDinoWindow : Window
{
    private readonly ConfigurationService _configuration;
    private readonly OllamaClient _ollama;

    public GpuDinoWindow(ConfigurationService configuration, OllamaClient ollama)
    {
        InitializeComponent(); _configuration = configuration; _ollama = ollama;
        Icon = AppIconService.LoadImageSource(); HeaderIcon.Source = AppIconService.LoadImageSource();
        ChatBackgroundImage.Source = LoadChatBackground();
    }

    private static System.Windows.Media.Imaging.BitmapSource? LoadChatBackground()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Home", "haus.png");
        if (!File.Exists(path)) return null;

        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        const int x = 1;
        const int y = 35;
        const int width = 806;
        const int height = 365;
        if (x + width > image.PixelWidth || y + height > image.PixelHeight) return image;

        var crop = new System.Windows.Media.Imaging.CroppedBitmap(image, new Int32Rect(x, y, width, height));
        crop.Freeze();
        return crop;
    }

    private async void Ask_Click(object sender, RoutedEventArgs e) => await AskDinoAsync();

    private async Task AskDinoAsync()
    {
        if (App.Current.DinoWindow.IsSleeping) { AnswerText.Text = "Dino schläft gerade. Wecke ihn zuerst auf."; return; }
        if (string.IsNullOrWhiteSpace(PromptBox.Text)) return;
        var c = _configuration.Current;
        if (!c.GpuDinoEnabled) { AnswerText.Text = "Meine lokalen Gedanken sind noch nicht aktiviert. Schau kurz in die Einstellungen."; return; }
        AskButton.IsEnabled = false; AnswerText.Text = "Dino denkt nach …";
        var answer = await _ollama.AskAsync(c.OllamaUrl, c.OllamaModel, PromptBox.Text.Trim());
        AnswerText.Text = answer;
        AskButton.IsEnabled = true;

        if (System.Windows.Application.Current is App app)
        {
            app.Statistics.TrackChat();
            app.Progress.AddXP(5, "Chat");
        }
    }

    private async void PromptBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { e.Handled = true; await AskDinoAsync(); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
