using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.GPU;
using DinoDesktopCompanion.Services;
using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly ConfigurationService _configuration;
    private readonly OllamaClient _ollama;
    private sealed record MonitorItem(string Device, string Label);

    public SettingsWindow(ConfigurationService configuration, OllamaClient ollama)
    {
        InitializeComponent(); _configuration = configuration; _ollama = ollama;
        Icon = AppIconService.LoadImageSource(); HeaderIcon.Source = AppIconService.LoadImageSource();
        MonitorBox.ItemsSource = Forms.Screen.AllScreens.Select((s, i) => new MonitorItem(s.DeviceName, $"Monitor {i + 1} – {s.Bounds.Width}×{s.Bounds.Height}"));
        LoadValues();
    }

    private void LoadValues()
    {
        var c = _configuration.Current;
        StartupBox.IsChecked = c.StartWithWindows;
        TopmostBox.IsChecked = c.AlwaysOnTop;
        BubblesBox.IsChecked = c.SpeechBubbles;
        RoamBox.IsChecked = c.CanRoam;
        MessagesBox.IsChecked = c.RandomMessages;
        AnimationsBox.IsChecked = c.Animations;
        SizeSlider.Value = c.DinoSize;
        SpeedSlider.Value = c.AnimationSpeed;
        ActivityBox.SelectedIndex = c.Activity switch { "Normal" => 1, "Aktiver" or "Lebhaft" => 2, _ => 0 };
        MouseFollowBox.SelectedIndex = c.MouseFollow switch { "Schwach" => 1, "Normal" => 2, "Neugierig" => 3, _ => 0 };
        MonitorBox.SelectedValue = c.Monitor;
        if (MonitorBox.SelectedIndex < 0) MonitorBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var c = _configuration.Current;
        c.StartWithWindows = StartupBox.IsChecked == true;
        c.AlwaysOnTop = TopmostBox.IsChecked == true;
        c.SpeechBubbles = BubblesBox.IsChecked == true;
        c.CanRoam = RoamBox.IsChecked == true;
        c.RandomMessages = MessagesBox.IsChecked == true;
        c.Animations = AnimationsBox.IsChecked == true;
        c.DinoSize = SizeSlider.Value;
        c.AnimationSpeed = SpeedSlider.Value;
        c.Activity = ((ComboBoxItem)ActivityBox.SelectedItem).Content.ToString()!;
        c.MouseFollow = ((ComboBoxItem)MouseFollowBox.SelectedItem).Content.ToString()!;
        var oldMonitor = c.Monitor;
        c.Monitor = (MonitorBox.SelectedItem as MonitorItem)?.Device ?? c.Monitor;
        try { StartupService.SetEnabled(c.StartWithWindows); } catch { }
        _configuration.Save(true);
        if (c.Monitor != oldMonitor)
        {
            MonitorService.MoveToMonitor(App.Current.DinoWindow, c.Monitor);
            MonitorService.SavePosition(App.Current.DinoWindow, c);
            _configuration.Save();
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
