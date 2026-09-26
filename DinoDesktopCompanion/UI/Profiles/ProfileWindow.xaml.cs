using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DinoDesktopCompanion.Profiles;
using DinoDesktopCompanion.Services;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DinoDesktopCompanion.UI.Profiles;

public partial class ProfileWindow : Window
{
    private readonly ProfileManager _manager;
    private bool _requireProfile;

    public ProfileWindow(ProfileManager manager, bool requireProfile = false)
    {
        InitializeComponent();
        _manager = manager;
        _requireProfile = requireProfile;
        Icon = AppIconService.LoadImageSource();
        LoadProfiles();
        if (_requireProfile && _manager.Profiles.Count == 0) Loaded += (_, _) => CreateProfile();
    }

    private void LoadProfiles()
    {
        ProfilesPanel.Children.Clear();
        EmptyHint.Visibility = _manager.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var profile in _manager.Profiles)
        {
            var isCurrent = profile.Id == _manager.ActiveProfile?.Id;
            var summary = _manager.GetSummary(profile);
            var card = new Border
            {
                Width = 350, Background = Brushes.White,
                BorderBrush = new SolidColorBrush(isCurrent ? Color.FromRgb(57, 139, 123) : Color.FromRgb(213, 229, 223)),
                BorderThickness = new Thickness(isCurrent ? 2 : 1), CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 15, 15), Padding = new Thickness(16)
            };
            var body = new StackPanel();
            var summaryRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            var previewBrush = new BrushConverter().ConvertFromString(ProfileManager.NormalizeProfileColor(profile.ProfileColor)) as System.Windows.Media.Brush ?? new SolidColorBrush(Color.FromRgb(120, 158, 91));
            summaryRow.Children.Add(new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(22), Background = previewBrush, BorderBrush = new SolidColorBrush(Color.FromRgb(70, 105, 97)), BorderThickness = new Thickness(2), Margin = new Thickness(0, 0, 12, 0), ToolTip = "Profilfarbe" });
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = profile.ProfileName + (isCurrent ? "  • Aktiv" : ""), FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(32, 58, 53)) });
            texts.Children.Add(new TextBlock { Text = $"{profile.DinoName}  ·  Level {summary.Level}  ·  {summary.SkinName}", Margin = new Thickness(0, 4, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(70, 105, 97)) });
            summaryRow.Children.Add(texts);
            body.Children.Add(summaryRow);
            body.Children.Add(new TextBlock { Text = $"Zuletzt: {profile.LastPlayed:g}", Margin = new Thickness(0, 8, 0, 8), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(99, 124, 118)) });
            if (!string.IsNullOrWhiteSpace(profile.Note))
                body.Children.Add(new TextBlock { Text = profile.Note, TextWrapping = TextWrapping.Wrap, MaxHeight = 72, Margin = new Thickness(0, 0, 0, 12), Foreground = new SolidColorBrush(Color.FromRgb(70, 105, 97)), ToolTip = profile.Note });
            if (!isCurrent)
            {
                var activateButton = MakeButton("Als aktiv setzen", () => ActivateProfile(profile));
                activateButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                activateButton.Margin = new Thickness(0, 0, 0, 8);
                body.Children.Add(activateButton);
            }
            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            var editButton = MakeButton("Bearbeiten", () => EditProfile(profile));
            var exportButton = MakeButton("Export", () => ExportProfile(profile));
            var deleteButton = MakeButton("Löschen", () => DeleteProfile(profile), true);
            Grid.SetColumn(exportButton, 1);
            Grid.SetColumn(deleteButton, 2);
            actions.Children.Add(editButton);
            actions.Children.Add(exportButton);
            actions.Children.Add(deleteButton);
            body.Children.Add(actions);
            card.Child = body;
            ProfilesPanel.Children.Add(card);
        }
    }

    private static System.Windows.Controls.Button MakeButton(string text, Action action, bool danger = false)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            MinWidth = 72,
            MinHeight = 34,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 7, 4),
            Foreground = danger ? Brushes.Firebrick : null
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e) => CreateProfile();

    private void CreateProfile()
    {
        var editor = new ProfileEditorWindow { Owner = this };
        if (editor.ShowDialog() != true) return;
        try
        {
            var profile = _manager.CreateProfile(editor.ProfileName, editor.DinoName, profileColor: editor.ProfileColor, note: editor.Note);
            if (!_manager.SetActiveProfile(profile.Id))
                MessageBox.Show("Das Profil wurde gespeichert, konnte aber nicht aktiviert werden. Bitte versuche es erneut.", "Profilaktivierung", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoadProfiles();
            if (_requireProfile) DialogResult = true;
        }
        catch (Exception ex)
        {
            LogError("Profil konnte nicht erstellt werden.", ex);
            MessageBox.Show("Das Profil konnte nicht gespeichert werden. Bitte versuche es erneut.", "Profil konnte nicht erstellt werden", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void EditProfile(ProfileInfo profile)
    {
        var editor = new ProfileEditorWindow(profile) { Owner = this };
        if (editor.ShowDialog() != true) return;
        try
        {
            _manager.UpdateProfile(profile.Id, editor.ProfileName, editor.DinoName, profile.BirthdayDay, profile.BirthdayMonth, editor.ProfileColor, editor.Note);
            LoadProfiles();
        }
        catch (Exception ex)
        {
            LogError("Profil konnte nicht bearbeitet werden.", ex);
            MessageBox.Show("Die Änderungen konnten nicht gespeichert werden. Bitte versuche es erneut.", "Profil konnte nicht gespeichert werden", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_manager.ActiveProfile is { } profile && !_manager.SaveProfile(profile.Id))
            {
                MessageBox.Show("Das Profil konnte nicht gespeichert werden.", "Profil speichern", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            LogError("Profil konnte nicht gespeichert werden.", ex);
            MessageBox.Show("Das Profil konnte nicht gespeichert werden. Bitte versuche es erneut.", "Profil speichern", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ActivateProfile(ProfileInfo profile)
    {
        try
        {
            if (!_manager.SetActiveProfile(profile.Id))
            {
                MessageBox.Show("Das Profil konnte nicht aktiviert werden. Bitte versuche es erneut.", "Profilaktivierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LoadProfiles();
        }
        catch (Exception ex)
        {
            LogError($"Profil {profile.Id} konnte nicht aktiviert werden.", ex);
            MessageBox.Show("Das Profil konnte nicht aktiviert werden. Die App bleibt geöffnet.", "Profilaktivierung", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void LogError(string message, Exception ex)
    {
        if (System.Windows.Application.Current is App app) app.Logger.Error(message, ex);
    }

    private void DeleteProfile(ProfileInfo profile)
    {
        if (MessageBox.Show($"Möchtest du den Spielstand „{profile.ProfileName}“ wirklich löschen?", "Profil löschen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (MessageBox.Show("Letzte Bestätigung: Dieser Spielstand wird endgültig gelöscht. Fortfahren?", "Endgültig löschen", MessageBoxButton.YesNo, MessageBoxImage.Error) != MessageBoxResult.Yes) return;
        _manager.DeleteProfile(profile.Id);
        LoadProfiles();
        if (_manager.Profiles.Count == 0) { _requireProfile = true; CreateProfile(); }
    }

    private void ExportProfile(ProfileInfo profile)
    {
        var dialog = new SaveFileDialog { Filter = "Dino-Spielstand (*.dino)|*.dino", FileName = _manager.GetSuggestedExportName(profile), AddExtension = true, DefaultExt = ".dino" };
        if (dialog.ShowDialog() != true) return;
        MessageBox.Show(_manager.ExportProfile(profile.Id, dialog.FileName) ? "Spielstand erfolgreich exportiert." : $"Export fehlgeschlagen: {_manager.LastError}", "Dino-Export", MessageBoxButton.OK, _manager.LastError is null ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog { Filter = "Dino-Spielstand (*.dino;*.zip)|*.dino;*.zip" };
        if (open.ShowDialog() != true) return;
        var profile = _manager.ImportProfile(open.FileName);
        if (profile is null) { MessageBox.Show($"Import fehlgeschlagen: {_manager.LastError}", "Ungültiger Spielstand", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        if (!_manager.SetActiveProfile(profile.Id))
        {
            MessageBox.Show("Das Profil wurde importiert, konnte aber nicht aktiviert werden.", "Profilaktivierung", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoadProfiles();
            return;
        }
        LoadProfiles();
        MessageBox.Show($"„{profile.ProfileName}“ wurde importiert.", "Dino-Import", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_requireProfile && _manager.Profiles.Count == 0)
        {
            e.Cancel = true;
            MessageBox.Show("Bitte erstelle oder importiere mindestens ein Dino-Profil.", "Profil erforderlich", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        base.OnClosing(e);
    }
}
