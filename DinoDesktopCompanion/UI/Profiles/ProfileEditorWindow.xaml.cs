using System.Windows;
using System.Windows.Controls;
using DinoDesktopCompanion.Profiles;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.UI.Profiles;

public partial class ProfileEditorWindow : Window
{
    public string ProfileName => ProfileNameBox.Text.Trim();
    public string DinoName => string.IsNullOrWhiteSpace(DinoNameBox.Text) ? "Dino" : DinoNameBox.Text.Trim();
    public string Note => NoteBox.Text.Trim();
    public string ProfileColor => (ProfileColorBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
        ?? ProfileInfo.DefaultProfileColor;

    public ProfileEditorWindow(ProfileInfo? profile = null)
    {
        InitializeComponent();
        Icon = AppIconService.LoadImageSource();
        ProfileNameBox.Text = profile?.ProfileName ?? "";
        DinoNameBox.Text = profile?.DinoName ?? "";
        NoteBox.Text = profile?.Note ?? "";
        ProfileColorBox.SelectedValue = ProfileManager.NormalizeProfileColor(profile?.ProfileColor);
        if (ProfileColorBox.SelectedIndex < 0) ProfileColorBox.SelectedIndex = 0;
        Loaded += (_, _) => { ProfileNameBox.Focus(); ProfileNameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ErrorText.Text = "Bitte gib einen Profilnamen ein.";
            return;
        }
        DialogResult = true;
    }
}
