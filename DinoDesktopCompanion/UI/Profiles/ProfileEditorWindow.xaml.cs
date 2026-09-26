using System.Windows;
using System.Windows.Controls;
using DinoDesktopCompanion.Profiles;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.UI.Profiles;

public partial class ProfileEditorWindow : Window
{
    public string ProfileName => ProfileNameBox.Text.Trim();
    public string DinoName => string.IsNullOrWhiteSpace(DinoNameBox.Text) ? "Dino" : DinoNameBox.Text.Trim();
    public int? BirthdayDay { get; private set; }
    public int? BirthdayMonth { get; private set; }
    public string ProfileColor => (ProfileColorBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
        ?? ProfileInfo.DefaultProfileColor;

    public ProfileEditorWindow(ProfileInfo? profile = null)
    {
        InitializeComponent();
        Icon = AppIconService.LoadImageSource();
        ProfileNameBox.Text = profile?.ProfileName ?? "";
        DinoNameBox.Text = profile?.DinoName ?? "";
        BirthdayDayBox.Text = profile?.BirthdayDay?.ToString() ?? "";
        BirthdayMonthBox.Text = profile?.BirthdayMonth?.ToString() ?? "";
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
        var hasDay = int.TryParse(BirthdayDayBox.Text, out var day);
        var hasMonth = int.TryParse(BirthdayMonthBox.Text, out var month);
        if (string.IsNullOrWhiteSpace(BirthdayDayBox.Text) && string.IsNullOrWhiteSpace(BirthdayMonthBox.Text))
        {
            BirthdayDay = null; BirthdayMonth = null;
        }
        else if (!hasDay || !hasMonth || day < 1 || month < 1 || month > 12 || day > DateTime.DaysInMonth(2000, month))
        {
            ErrorText.Text = "Bitte einen gültigen Tag und Monat eingeben oder beide Felder leer lassen.";
            return;
        }
        else { BirthdayDay = day; BirthdayMonth = month; }
        DialogResult = true;
    }
}
