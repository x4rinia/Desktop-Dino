using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DinoDesktopCompanion.Achievements;
using DinoDesktopCompanion.Collections;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Statistics;
using DinoDesktopCompanion.Tasks;
using DinoDesktopCompanion.Core;
using DinoDesktopCompanion.UI.Interaction;
using DinoDesktopCompanion.Games;
using System.Globalization;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace DinoDesktopCompanion.UI;

public partial class InteractionWindow : Window
{
    private readonly ProgressService _progress;
    private readonly CollectionManager _collections;
    private readonly DinoTaskService _tasks;
    private readonly StatisticsService _stats;
    private readonly AchievementService _achievements;
    private readonly AreaService _areas;

    private readonly DinoDesktopCompanion.Dino.Animation.DinoSpriteCatalog _catalog;
    private readonly DinoDesktopCompanion.Dino.Animation.DinoSpritePlayer _spritePlayer;
    private string? _selectedHomeSlotId;
    private int _selectedSlotItemIndex;
    private AlbumEntryDefinition? _selectedAlbumEntry;
    private string _albumAreaFilter = "all";
    private string _albumRarityFilter = "all";

    public InteractionWindow(ProgressService progress, CollectionManager collections, DinoTaskService tasks, StatisticsService stats, AchievementService achievements, AreaService areas)
    {
        InitializeComponent();
        _progress = progress;
        _collections = collections;
        _tasks = tasks;
        _stats = stats;
        _achievements = achievements;
        _areas = areas;

        _catalog = new DinoDesktopCompanion.Dino.Animation.DinoSpriteCatalog(new DinoDesktopCompanion.Services.FileLogger());
        _spritePlayer = new DinoDesktopCompanion.Dino.Animation.DinoSpritePlayer(_catalog, frame =>
        {
            WardrobeDino.SetSprite(frame);
        });
        HomeEquipButton.Click += HomeEquipButton_Click;
        LoadInterfaceArtwork();
        InitializeAlbumFilters();

        UpdateProgressUI();
        UpdateProfileUI();
        UpdatePreviewDino();
        LoadStartDashboard();
        LoadShop();
        LoadGarderobe();
        LoadAlbum();

        LoadHome();
        LoadAchievements();
        LoadAreas();
        LoadAdventureDetails();
        LoadCollections();
        LoadAchievements();



        _progress.XPAdded += (_, _) => Dispatcher.Invoke(() => { UpdateProgressUI(); });
        _progress.LevelUp += (_, _) => Dispatcher.Invoke(UpdateProgressUI);
        _progress.CoinsChanged += (_, _) => Dispatcher.Invoke(UpdateProgressUI);
        _progress.AdventurePointsChanged += (_, _) => Dispatcher.Invoke(UpdateProgressUI);

        _collections.CollectionChanged += Collections_CollectionChanged;
        _achievements.AchievementUnlocked += Achievements_AchievementUnlocked;
        Closed += InteractionWindow_Closed;
    }

    private void UpdatePreviewDino()
    {
        var equippedSkinId = _collections.Current.EquippedSkinId;
        var skinDef = _collections.Cosmetics.Skins.FirstOrDefault(s => s.Id == equippedSkinId);
        if (skinDef != null)
        {
            _catalog.SetSkin(skinDef);
            SkinText.Text = skinDef.Name;
            WardrobeDino.ApplySkin(skinDef);
        }

        var (idleFrames, _, _) = _catalog.Get(DinoDesktopCompanion.Dino.States.DinoState.Idle);
        if (idleFrames.Count > 0)
        {
            WardrobeDino.SetSprite(idleFrames[0]);
        }
    }

    private void HideAllPanels()
    {
        if (PanelStart != null) PanelStart.Visibility = Visibility.Collapsed;
        if (PanelShop != null) PanelShop.Visibility = Visibility.Collapsed;
        if (PanelGarderobe != null) PanelGarderobe.Visibility = Visibility.Collapsed;
        if (PanelToys != null) PanelToys.Visibility = Visibility.Collapsed;

        if (PanelAchievements != null) PanelAchievements.Visibility = Visibility.Collapsed;
        if (PanelAdventure != null) PanelAdventure.Visibility = Visibility.Collapsed;
        if (PanelHome != null) PanelHome.Visibility = Visibility.Collapsed;
    }

    private void Nav_Start_Checked(object sender, RoutedEventArgs e)
    {
        HideAllPanels();
        if (PanelStart != null) PanelStart.Visibility = Visibility.Visible;
        if (ProfileNameText is null) return;
        UpdateProfileUI();
        UpdateProgressUI();

        UpdateSleepUI();
    }
    private void Nav_Adventure_Checked(object sender, RoutedEventArgs e) => ShowAdventure();
    private void Nav_Garderobe_Checked(object sender, RoutedEventArgs e) { HideAllPanels(); if (PanelGarderobe != null) PanelGarderobe.Visibility = Visibility.Visible; _wardrobePreviewSkinId = null; LoadGarderobe(); }
    private void Nav_Toys_Checked(object sender, RoutedEventArgs e) { HideAllPanels(); if (PanelToys != null) PanelToys.Visibility = Visibility.Visible; LoadAlbum(); }

    private void Nav_Achievements_Checked(object sender, RoutedEventArgs e) { HideAllPanels(); if (PanelAchievements != null) PanelAchievements.Visibility = Visibility.Visible; }
    private void Nav_Home_Checked(object sender, RoutedEventArgs e) { HideAllPanels(); if (PanelHome != null) PanelHome.Visibility = Visibility.Visible; UpdateSleepUI(); }

    private void UpdateProgressUI()
    {
        LevelText.Text = _progress.Current.Level.ToString();
        XpText.Text = $"{_progress.Current.CurrentXP} / {_progress.Current.XPToNextLevel}";
        var progressPercent = _progress.Current.XPToNextLevel > 0 ? (double)_progress.Current.CurrentXP / _progress.Current.XPToNextLevel * 100 : 0;
        XpBar.Value = progressPercent;
        CoinsText.Text = _progress.Current.DinoCoins.ToString();
        ApText.Text = $"{_progress.Current.AdventurePoints} / {_progress.Current.MaxAdventurePoints}";
        if (SleepApText != null) SleepApText.Text = ApText.Text;
        if (SleepRegenProgressBar != null) UpdateSleepRegenerationUI();
    }



    private void UpdateProfileUI()
    {
        var profile = App.Current.Profiles.ActiveProfile;
        if (profile is null) return;
        ProfileNameText.Text = profile.ProfileName;
        DinoNameText.Text = profile.DinoName;
        ProfileNoteText.Text = string.IsNullOrWhiteSpace(profile.Note) ? "Keine Notiz" : profile.Note;
        var state = profile.State switch
        {
            DinoDesktopCompanion.Profiles.DinoState.Sleeping => "Schläft",
            _ => "Wach"
        };
        StateText.Text = state;

        // Apply profile color ring
        if (ProfileColorRing != null)
        {
            var colorStr = DinoDesktopCompanion.Profiles.ProfileManager.NormalizeProfileColor(profile.ProfileColor);
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                ProfileColorRing.Stroke = new System.Windows.Media.SolidColorBrush(color);
            }
            catch { /* keep default */ }
        }
    }

    private void UpdateSleepUI()
    {
        var sleeping = _progress.IsSleeping;
        bool isHome = false;
        if (App.Current.MainWindow is MainWindow mw) isHome = mw.Home.IsHome;
        var atMaximum = _progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints;
        StateText.Text = isHome ? (sleeping ? "Schläft (Zuhause)" : "Ruht (Zuhause)") : (sleeping ? "Schläft (Desktop)" : "Wach (Desktop)");
        SleepApText.Text = $"{_progress.Current.AdventurePoints} / {_progress.Current.MaxAdventurePoints}";
        SleepDinoButton.IsEnabled = !sleeping && !atMaximum;
        SleepDinoButton.ToolTip = atMaximum && !sleeping ? "Dino hat bereits volle Energie!" : null;
        WakeDinoButton.IsEnabled = sleeping;
        
        if (SleepDinoButton != null) SleepDinoButton.Visibility = sleeping ? Visibility.Collapsed : Visibility.Visible;
        if (WakeDinoButton != null) WakeDinoButton.Visibility = sleeping ? Visibility.Visible : Visibility.Collapsed;
        
        if (SendHomeButton != null) SendHomeButton.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
        if (CallDesktopButton != null) CallDesktopButton.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;

        UpdateSleepRegenerationUI();
        
        if (HomeDinoImage != null)
        {
            if (isHome)
            {
                HomeDinoImage.Visibility = Visibility.Visible;
                HomeDinoImage.Source = sleeping ? LoadHomeImage("Assets/Sprites/Sleep/sleep-01.png") : LoadHomeImage("Assets/Sprites/Sit/sit-01.png");
            }
            else
            {
                HomeDinoImage.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateSleepRegenerationUI()
    {
        var atMaximum = _progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints;
        var progress = atMaximum ? 0 : Math.Clamp(_progress.Current.AdventurePointRegenProgress, 0, 0.9999999);
        var percent = (int)Math.Floor(progress * 100);
        SleepRegenProgressBar.Value = percent;
        SleepRegenProgressText.Text = $"{percent} %";

        if (atMaximum)
            return;
    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new DinoDesktopCompanion.UI.Profiles.ProfileWindow(App.Current.Profiles) { Owner = this };
        if (window.ShowDialog() == true)
        {
            UpdateProfileUI();
            ProfileColorRing.InvalidateVisual();
            Dispatcher.BeginInvoke(new Action(UpdateProfileUI), System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    private string? _wardrobePreviewSkinId;

    private void LoadGarderobe()
    {
        if (SkinsList != null) SkinsList.Children.Clear();
        _collections.Cosmetics.EvaluateUnlocks(_progress, _stats, _areas);
        
        var currentSkinId = _collections.Current.EquippedSkinId;
        if (string.IsNullOrEmpty(_wardrobePreviewSkinId)) _wardrobePreviewSkinId = currentSkinId;
        
        var previewSkin = _collections.Cosmetics.Skins.FirstOrDefault(s => s.Id == _wardrobePreviewSkinId);
        if (previewSkin != null && WardrobeDino != null)
        {
            WardrobeDino.ApplySkin(previewSkin);
            WardrobePreviewName.Text = previewSkin.Name;
            var isUnlocked = _collections.Current.UnlockedSkinIds.Contains(previewSkin.Id);
            WardrobePreviewStatus.Text = previewSkin.Id == currentSkinId ? "Aktuell ausgerüstet" : (isUnlocked ? "Vorschau aktiv" : "Gesperrt");
            WardrobeEquipButton.IsEnabled = isUnlocked && previewSkin.Id != currentSkinId;
            WardrobeEquipButton.Visibility = isUnlocked && previewSkin.Id != currentSkinId ? Visibility.Visible : Visibility.Collapsed;
        }

        var orderedSkins = _collections.Cosmetics.Skins
            .OrderBy(s => GetAlbumRarityOrder(s.Rarity))
            .ThenBy(s => s.UnlockLevel)
            .ThenBy(s => s.Cost)
            .ThenBy(s => s.Name)
            .ToList();

        foreach (var skin in orderedSkins)
        {
            if (SkinsList == null) continue;
            var unlocked = _collections.Current.UnlockedSkinIds.Contains(skin.Id);
            var isEquipped = skin.Id == currentSkinId;
            var coinSkin = skin.UnlockType == DinoDesktopCompanion.Customization.SkinUnlockType.Coins;
            var levelReady = _progress.Current.Level >= skin.UnlockLevel;
            var affordable = _progress.Current.DinoCoins >= skin.Cost;
            var status = isEquipped ? "Ausgerüstet" : unlocked ? "Freigeschaltet" : coinSkin && levelReady && affordable ? "Kaufbar" : "Gesperrt";
            var description = unlocked ? (skin.Id == _wardrobePreviewSkinId ? "Wird anprobiert" : "Klicken zum Anprobieren") : $"🔒 So bekommst du diesen Skin:\n{_collections.Cosmetics.GetUnlockText(skin)}";
            var card = CreateCard(skin.Name, description, $"{status} · {skin.Rarity}", skin.Rarity, isEquipped);
            
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += (_, _) => 
            {
                _wardrobePreviewSkinId = skin.Id;
                LoadGarderobe();
            };

            if (!unlocked && coinSkin)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = levelReady ? $"Kaufen ({skin.Cost} Coins)" : $"Ab Level {skin.UnlockLevel}",
                    IsEnabled = levelReady && affordable,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                btn.Click += (s, e) =>
                {
                    e.Handled = true;
                    if (_collections.Cosmetics.TryPurchase(skin, _progress)) { _wardrobePreviewSkinId = skin.Id; LoadGarderobe(); LoadShop(); UpdatePreviewDino(); UpdateProgressUI(); }
                };
                ((StackPanel)card.Child).Children.Add(btn);
            }
            if (!unlocked) card.Opacity = .82;
            SkinsList.Children.Add(card);
        }
    }

    private void WardrobeEquipButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_wardrobePreviewSkinId))
        {
            _collections.Cosmetics.EquipSkin(_wardrobePreviewSkinId);
            LoadGarderobe();
            UpdatePreviewDino();
        }
    }

    private void LoadShop()
    {
        ShopList.Children.Clear();
        var orderedSkins = _collections.Cosmetics.Skins
            .OrderBy(s => GetAlbumRarityOrder(s.Rarity))
            .ThenBy(s => s.UnlockLevel)
            .ThenBy(s => s.Cost)
            .ThenBy(s => s.Name)
            .ToList();

        foreach (var skin in orderedSkins)
        {
            if (_collections.Current.UnlockedSkinIds.Contains(skin.Id)) continue;
            var canBuy = skin.UnlockType == DinoDesktopCompanion.Customization.SkinUnlockType.Coins && _progress.Current.Level >= skin.UnlockLevel;
            var card = CreateCard(skin.Name, canBuy ? $"Kaufbar ({skin.Cost} Coins)" : _collections.Cosmetics.GetUnlockText(skin), $"Skin · {skin.Rarity}", skin.Rarity);
            if (canBuy)
            {
                var btn = new System.Windows.Controls.Button { Content = $"Kaufen ({skin.Cost} Coins)", Margin = new Thickness(0, 5, 0, 0) };
                btn.Click += (_, _) =>
                {
                    if (_collections.Cosmetics.TryPurchase(skin, _progress)) { LoadShop(); LoadGarderobe(); UpdateProgressUI(); }
                    else System.Windows.MessageBox.Show("Nicht genug Coins!");
                };
                ((StackPanel)card.Child).Children.Add(btn);
            }
            else { card.Opacity = 0.6; }
            ShopList.Children.Add(card);
        }
    }

    private Border CreateCard(string title, string description, string category, string rarity = "Gewöhnlich", bool highlight = false)
    {
        var accentBrush = GetRarityAccentBrush(rarity);
        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 252, 251)),
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(highlight ? 2.5 : 1.5),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(10),
            Padding = new Thickness(15),
            Width = 220
        };
        
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Bold, FontSize = 16, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 58, 53)) });
        sp.Children.Add(new TextBlock { Text = category, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = accentBrush, Margin = new Thickness(0, 4, 0, 10) });
        sp.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 58, 53)) });
        if (highlight)
        {
            sp.Children.Add(new TextBlock { Text = "✓ Ausgerüstet", Foreground = accentBrush, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0) });
        }
        border.Child = sp;
        return border;
    }

    private void LoadStartDashboard()
    {
        // Add logic if needed, currently static
    }

    private void LoadInterfaceArtwork()
    {
        WardrobeBackgroundImage.Source = LoadHomeImage("Assets/Backgrounds/garderobe.png");

        var houseScene = LoadCroppedInterfaceImage("Assets/Home/haus.png", 1, 35, 806, 365);
        OverviewBackgroundImage.Source = houseScene;
        PreviewPortraitImage.Source = LoadHomeImage("Assets/Home/dino_portrait.jpg");

        AdventureBackgroundImage.Source = LoadHomeImage("Assets/Backgrounds/adventure_map_clean.png");
        AchievementsBackgroundImage.Source = LoadCroppedInterfaceImage("Assets/Backgrounds/expedtion.png", 12, 90, 1268, 805);
    }

    private void InitializeAlbumFilters()
    {
        AddAlbumFilterOption(AlbumAreaFilter, "Alle", "all");
        AddAlbumFilterOption(AlbumAreaFilter, "Garten", "garten");
        AddAlbumFilterOption(AlbumAreaFilter, "Wald", "wald");
        AddAlbumFilterOption(AlbumAreaFilter, "Strand", "strand");
        AddAlbumFilterOption(AlbumAreaFilter, "Höhle", "hoehle");
        AddAlbumFilterOption(AlbumAreaFilter, "Schnee", "schneeland");
        AddAlbumFilterOption(AlbumAreaFilter, "Events", "events");

        AddAlbumFilterOption(AlbumRarityFilter, "Alle", "all");
        AddAlbumFilterOption(AlbumRarityFilter, "Gewöhnlich", "Gewöhnlich");
        AddAlbumFilterOption(AlbumRarityFilter, "Ungewöhnlich", "Ungewöhnlich");
        AddAlbumFilterOption(AlbumRarityFilter, "Selten", "Selten");
        AddAlbumFilterOption(AlbumRarityFilter, "Episch", "Episch");
        AddAlbumFilterOption(AlbumRarityFilter, "Legendär", "Legendär");
        AddAlbumFilterOption(AlbumRarityFilter, "Geheim", "secret");

        AlbumAreaFilter.SelectedIndex = 0;
        AlbumRarityFilter.SelectedIndex = 0;
        AlbumAreaFilter.SelectionChanged += AlbumFilter_SelectionChanged;
        AlbumRarityFilter.SelectionChanged += AlbumFilter_SelectionChanged;
    }

    private static void AddAlbumFilterOption(System.Windows.Controls.ComboBox target, string label, string value)
        => target.Items.Add(new ComboBoxItem { Content = label, Tag = value });

    private void AlbumFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _albumAreaFilter = (AlbumAreaFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        _albumRarityFilter = (AlbumRarityFilter.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        RefreshAlbumGrid();
    }

    private void LoadAlbum()
    {
        _collections.Toys.RefreshState();
        RefreshAlbumGrid();
    }

    private void RefreshAlbumGrid()
    {
        if (ToysList == null) return;

        var matching = _collections.Toys.Items
            .Where(item => _albumAreaFilter == "all" || string.Equals(item.AreaId, _albumAreaFilter, StringComparison.OrdinalIgnoreCase))
            .Where(item => _albumRarityFilter switch
            {
                "all" => true,
                "secret" => item.IsSecret,
                _ => string.Equals(item.Rarity, _albumRarityFilter, StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(item => GetAlbumAreaOrder(item.AreaId))
            .ThenBy(item => GetAlbumRarityOrder(item.Rarity))
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ToysList.Children.Clear();
        foreach (var entry in matching)
        {
            ToysList.Children.Add(CreateAlbumCard(entry));
        }

        if (matching.Count == 0)
        {
            ToysList.Children.Add(new TextBlock
            {
                Text = "Für diesen Filter gibt es keine Fundstücke.",
                Margin = new Thickness(14),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray
            });
        }

        var discovered = matching.Count(item => item.IsDiscovered);
        var percent = matching.Count == 0 ? 0 : (int)Math.Round(discovered * 100d / matching.Count);
        AlbumProgressText.Text = $"{discovered} / {matching.Count} entdeckt";
        AlbumProgressBar.Value = percent;
        AlbumProgressPercent.Text = $"{percent} %";

        if (_selectedAlbumEntry == null || matching.All(item => item.Id != _selectedAlbumEntry.Id))
        {
            _selectedAlbumEntry = matching.FirstOrDefault(item => item.IsDiscovered) ?? matching.FirstOrDefault();
        }
        ShowAlbumDetails(_selectedAlbumEntry);
    }

    private System.Windows.Controls.Button CreateAlbumCard(AlbumEntryDefinition entry)
    {
        var isSelected = _selectedAlbumEntry?.Id == entry.Id;
        var accent = GetRarityAccentBrush(entry.Rarity);
        var border = new Border
        {
            Width = 122,
            Height = 127,
            Margin = new Thickness(4),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(11),
            Background = entry.IsDiscovered ? Brushes.White : new SolidColorBrush(Color.FromRgb(238, 240, 239)),
            BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(44, 143, 124)) : accent,
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            Tag = accent
        };

        var stack = new StackPanel();
        var preview = new Border
        {
            Height = 65,
            CornerRadius = new CornerRadius(8),
            Background = entry.IsDiscovered ? GetAreaBrush(entry.AreaId) : new SolidColorBrush(Color.FromRgb(210, 214, 212))
        };
        var imgSource = entry.IsDiscovered ? LoadHomeImage(entry.AssetPath) : null;
        if (imgSource != null)
        {
            preview.Child = new System.Windows.Controls.Image
            {
                Source = imgSource,
                Width = 45,
                Height = 45,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            preview.Child = new TextBlock
            {
                Text = entry.IsDiscovered ? GetAlbumGlyph(entry) : (entry.IsSecret ? "?" : "🔒"),
                FontSize = entry.IsDiscovered ? 31 : 27,
                FontWeight = FontWeights.Bold,
                Foreground = entry.IsDiscovered ? Brushes.White : new SolidColorBrush(Color.FromRgb(126, 134, 131)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        stack.Children.Add(preview);
        stack.Children.Add(new TextBlock
        {
            Text = GetAlbumDisplayName(entry),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 5, 0, 2)
        });

        var rarity = new TextBlock
        {
            Text = $"● {entry.Rarity}",
            FontSize = 9,
            Foreground = accent,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        stack.Children.Add(rarity);
        border.Child = stack;

        var button = new System.Windows.Controls.Button
        {
            Content = border,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = entry.IsDiscovered ? entry.Name : "Noch nicht entdeckt",
            Tag = entry.Id
        };
        button.Click += (_, _) =>
        {
            if (_selectedAlbumEntry?.Id == entry.Id) return;
            
            _selectedAlbumEntry = entry;
            foreach (System.Windows.Controls.Button childBtn in ToysList.Children)
            {
                if (childBtn.Content is Border childBorder)
                {
                    bool isThisSelected = (childBtn.Tag as string) == entry.Id;
                    var bAccent = childBorder.Tag as System.Windows.Media.Brush;
                    childBorder.BorderBrush = isThisSelected ? new SolidColorBrush(Color.FromRgb(44, 143, 124)) : bAccent;
                    childBorder.BorderThickness = new Thickness(isThisSelected ? 2 : 1);
                }
            }
            ShowAlbumDetails(_selectedAlbumEntry);
        };
        return button;
    }

    private void ShowAlbumDetails(AlbumEntryDefinition? entry)
    {
        if (entry == null)
        {
            AlbumDetailImage.Source = null;
            AlbumDetailGlyph.Text = "?";
            AlbumDetailName.Text = "Kein Fundstück";
            AlbumDetailRarityText.Text = "–";
            AlbumDetailDescription.Text = "Passe die Filter an, um Fundstücke anzuzeigen.";
            AlbumDetailAreaText.Text = "–";
            AlbumDetailFoundAtText.Text = "–";
            AlbumDetailCountTitle.Visibility = Visibility.Collapsed;
            AlbumDetailCountText.Visibility = Visibility.Collapsed;
            AlbumDetailStatusText.Text = "Nicht verfügbar";
            return;
        }

        var image = entry.IsDiscovered ? LoadHomeImage(entry.AssetPath) : null;
        AlbumDetailImage.Source = image;
        AlbumDetailGlyph.Visibility = image == null ? Visibility.Visible : Visibility.Collapsed;
        AlbumDetailGlyph.Text = entry.IsDiscovered ? GetAlbumGlyph(entry) : (entry.IsSecret ? "?" : "🔒");
        AlbumDetailGlyph.Foreground = entry.IsDiscovered ? Brushes.White : new SolidColorBrush(Color.FromRgb(126, 134, 131));
        AlbumDetailPreviewBorder.Background = entry.IsDiscovered ? GetAreaBrush(entry.AreaId) : new SolidColorBrush(Color.FromRgb(218, 222, 220));

        AlbumDetailName.Text = GetAlbumDisplayName(entry);
        AlbumDetailRarityText.Text = entry.Rarity;
        AlbumDetailRarityText.Foreground = GetRarityAccentBrush(entry.Rarity);
        AlbumDetailRarityBadge.Background = GetRarityBadgeBrush(entry.Rarity);
        AlbumDetailDescription.Text = entry.IsDiscovered
            ? entry.Description
            : entry.IsSecret
                ? "Dieses geheime Fundstück wartet noch darauf, entdeckt zu werden."
                : "Dino hat dieses Fundstück noch nicht entdeckt.";
        AlbumDetailAreaText.Text = entry.IsSecret && !entry.IsDiscovered ? "Unbekannt" : GetAlbumAreaName(entry.AreaId, entry.EventId);
        AlbumDetailFoundAtText.Text = entry.IsDiscovered
            ? entry.FirstFoundAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE")) ?? "Früher entdeckt – Datum nicht erfasst"
            : "Noch nicht gefunden";
            
        var baseId = entry.Id.Replace("_gold", "").Replace("_crystal", "");
        var count = _collections.Current.ToyCounts.GetValueOrDefault(baseId, 0);

        if (entry.Id.EndsWith("_crystal", StringComparison.OrdinalIgnoreCase))
        {
            AlbumDetailCountTitle.Visibility = Visibility.Visible;
            AlbumDetailCountText.Visibility = Visibility.Visible;
            AlbumDetailCountText.Text = "Endsammelstück erreicht!";
            AlbumDetailCountText.Foreground = new SolidColorBrush(Color.FromRgb(44, 143, 124));
        }
        else if (entry.Id.EndsWith("_gold", StringComparison.OrdinalIgnoreCase))
        {
            AlbumDetailCountTitle.Visibility = Visibility.Visible;
            AlbumDetailCountText.Visibility = Visibility.Visible;
            AlbumDetailCountText.Text = $"{count} / 40 (Kristall)";
            AlbumDetailCountText.Foreground = count >= 40 ? new SolidColorBrush(Color.FromRgb(44, 143, 124)) : Brushes.Gray;
        }
        else
        {
            var hasEvolutions = _collections.Toys.Items.Any(i => string.Equals(i.Id, entry.Id + "_gold", StringComparison.OrdinalIgnoreCase));
            if (hasEvolutions)
            {
                AlbumDetailCountTitle.Visibility = Visibility.Visible;
                AlbumDetailCountText.Visibility = Visibility.Visible;
                if (count < 20)
                {
                    AlbumDetailCountText.Text = $"{count} / 20 (Gold)";
                    AlbumDetailCountText.Foreground = Brushes.Gray;
                }
                else if (count < 40)
                {
                    AlbumDetailCountText.Text = $"{count} / 40 (Kristall)";
                    AlbumDetailCountText.Foreground = Brushes.Gray;
                }
                else
                {
                    AlbumDetailCountText.Text = "Alle Meilensteine erreicht!";
                    AlbumDetailCountText.Foreground = new SolidColorBrush(Color.FromRgb(44, 143, 124));
                }
            }
            else
            {
                AlbumDetailCountTitle.Visibility = Visibility.Collapsed;
                AlbumDetailCountText.Visibility = Visibility.Collapsed;
            }
        }

        AlbumDetailStatusText.Text = entry.IsDiscovered ? "Entdeckt" : "Nicht entdeckt";
        AlbumDetailStatusText.Foreground = entry.IsDiscovered
            ? new SolidColorBrush(Color.FromRgb(44, 143, 124))
            : Brushes.Gray;
    }

    private static string GetAlbumDisplayName(AlbumEntryDefinition entry)
        => entry.IsDiscovered ? entry.Name : entry.IsSecret ? "???" : "Unbekannter Fund";

    private static int GetAlbumAreaOrder(string areaId) => areaId.ToLowerInvariant() switch
    {
        "garten" => 0,
        "wald" => 1,
        "strand" => 2,
        "hoehle" => 3,
        "schneeland" => 4,
        "events" => 5,
        _ => 6
    };

    private static int GetAlbumRarityOrder(string rarity) => rarity.ToLowerInvariant() switch
    {
        "gewöhnlich" => 0,
        "ungewöhnlich" => 1,
        "selten" => 2,
        "episch" => 3,
        "legendär" => 4,
        _ => 5
    };

    private static string GetAlbumAreaName(string areaId, string? eventId) => areaId.ToLowerInvariant() switch
    {
        "garten" => "Garten",
        "wald" => "Wald",
        "strand" => "Strand",
        "hoehle" => "Höhle",
        "schneeland" => "Schneegebiet",
        "events" => string.IsNullOrWhiteSpace(eventId) ? "Event" : $"Event: {eventId}",
        _ => areaId
    };

    private static System.Windows.Media.Brush GetAreaBrush(string areaId) => areaId.ToLowerInvariant() switch
    {
        "garten" => new SolidColorBrush(Color.FromRgb(81, 174, 91)),
        "wald" => new SolidColorBrush(Color.FromRgb(52, 123, 80)),
        "strand" => new SolidColorBrush(Color.FromRgb(56, 162, 196)),
        "hoehle" => new SolidColorBrush(Color.FromRgb(121, 91, 178)),
        "schneeland" => new SolidColorBrush(Color.FromRgb(91, 171, 213)),
        "events" => new SolidColorBrush(Color.FromRgb(225, 139, 58)),
        _ => new SolidColorBrush(Color.FromRgb(74, 148, 132))
    };

    private static System.Windows.Media.Brush GetRarityAccentBrush(string rarity) => rarity.ToLowerInvariant() switch
    {
        "gewöhnlich" or "ungewöhnlich" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Grün
        "selten" => new SolidColorBrush(Color.FromRgb(37, 130, 235)),                       // Blau
        "episch" => new SolidColorBrush(Color.FromRgb(168, 85, 247)),                       // Lila
        "legendär" => new SolidColorBrush(Color.FromRgb(249, 115, 22)),                     // Orange
        _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))
    };

    private static System.Windows.Media.Brush GetRarityBadgeBrush(string rarity) => rarity.ToLowerInvariant() switch
    {
        "gewöhnlich" or "ungewöhnlich" => new SolidColorBrush(Color.FromArgb(35, 34, 197, 94)),
        "selten" => new SolidColorBrush(Color.FromArgb(35, 37, 130, 235)),
        "episch" => new SolidColorBrush(Color.FromArgb(35, 168, 85, 247)),
        "legendär" => new SolidColorBrush(Color.FromArgb(40, 249, 115, 22)),
        _ => new SolidColorBrush(Color.FromArgb(30, 200, 200, 200))
    };

    private static string GetAlbumGlyph(AlbumEntryDefinition entry)
    {
        var name = entry.Name.ToLowerInvariant();
        if (name.Contains("kleeblatt")) return "🍀";
        if (name.Contains("schnecken") || name.Contains("muschel")) return "🐚";
        if (name.Contains("feder")) return "🕊";
        if (name.Contains("zapfen") || name.Contains("eichel")) return "🌰";
        if (name.Contains("blume") || name.Contains("blüten")) return "🌸";
        if (name.Contains("kristall") || name.Contains("edelstein") || name.Contains("bernstein")) return "💎";
        if (name.Contains("stern")) return "⭐";
        if (name.Contains("flaschenpost")) return "✉";
        if (name.Contains("münze") || name.Contains("medaille")) return "🪙";
        if (name.Contains("knochen") || name.Contains("zahn")) return "🦴";
        if (name.Contains("fossil")) return "🌀";
        if (name.Contains("kürbis")) return "🎃";
        if (name.Contains("ei")) return "🥚";
        if (name.Contains("geschenk")) return "🎁";
        if (name.Contains("abzeichen") || name.Contains("anhänger")) return "🏅";
        if (name.Contains("karte") || name.Contains("rune")) return "🗺";
        if (name.Contains("stein") || name.Contains("kiesel")) return "●";
        return entry.AreaId.ToLowerInvariant() switch
        {
            "garten" => "🌿",
            "wald" => "🌲",
            "strand" => "🌊",
            "hoehle" => "◆",
            "schneeland" => "❄",
            "events" => "★",
            _ => "✦"
        };
    }

    private void Collections_CollectionChanged(object? sender, EventArgs e)
        => Dispatcher.Invoke(() => { LoadAlbum(); if (PanelGarderobe.IsVisible) LoadGarderobe(); });

    private void Achievements_AchievementUnlocked(object? sender, Achievement e)
        => Dispatcher.Invoke(LoadAchievements);

    private void InteractionWindow_Closed(object? sender, EventArgs e)
    {
        _collections.CollectionChanged -= Collections_CollectionChanged;
        _achievements.AchievementUnlocked -= Achievements_AchievementUnlocked;
    }


    private void LoadHome()
    {
        if (HomeCanvas == null || HomeBackgroundImage == null) return;

        var house = _collections.Home.Houses.FirstOrDefault(h => h.Id == _collections.Current.ActiveHouseId);
        if (house == null) return;

        var background = LoadHomeImage(house.BackgroundAssetPath);
        if (background != null)
        {
            System.Windows.Media.Imaging.BitmapSource source = background;
            if (house.BackgroundCropWidth > 0 && house.BackgroundCropHeight > 0
                && house.BackgroundCropX >= 0 && house.BackgroundCropY >= 0
                && house.BackgroundCropX + house.BackgroundCropWidth <= background.PixelWidth
                && house.BackgroundCropY + house.BackgroundCropHeight <= background.PixelHeight)
            {
                var crop = new System.Windows.Media.Imaging.CroppedBitmap(
                    background,
                    new Int32Rect(house.BackgroundCropX, house.BackgroundCropY, house.BackgroundCropWidth, house.BackgroundCropHeight));
                crop.Freeze();
                source = crop;
            }
            HomeBackgroundImage.Source = source;
        }

        HomeCanvas.Children.Clear();
        var configuredSlots = 0;
        foreach (var slot in house.Slots ?? [])
        {
            var slotItem = _collections.Home.Items.FirstOrDefault(item => item.SlotId == slot.Id);
            HomeItemDefinition? displayedItem = null;
            if (_collections.Current.EquippedHomeItemsBySlot.TryGetValue(slot.Id, out var equippedId))
            {
                displayedItem = _collections.Home.Items.FirstOrDefault(i => i.Id == equippedId);
                if (displayedItem != null) configuredSlots++;
            }

            displayedItem ??= slotItem?.IsUnlocked == true ? slotItem : null;
            var itemSource = displayedItem == null ? null : LoadHomeImage(displayedItem.AssetPath);
            if (displayedItem != null && itemSource != null)
            {
                var item = displayedItem;
                var image = new System.Windows.Controls.Image
                {
                    Source = itemSource,
                    Width = displayedItem.VisualWidth > 0 ? displayedItem.VisualWidth : Math.Max(72, slot.Width),
                    Height = displayedItem.VisualHeight > 0 ? displayedItem.VisualHeight : Math.Max(72, slot.Height),
                    Stretch = Stretch.Uniform,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = item.Name
                };
                System.Windows.Controls.Canvas.SetLeft(image, slot.X + (slot.Width - image.Width) / 2);
                System.Windows.Controls.Canvas.SetTop(image, Math.Max(0, slot.Y + (slot.Height - image.Height) / 2));
                System.Windows.Controls.Panel.SetZIndex(image, slot.ZIndex);
                image.MouseLeftButtonUp += (_, _) => ShowHomeSelection(slot, item);
                HomeCanvas.Children.Add(image);
                continue;
            }

            if (displayedItem != null)
            {
                var item = displayedItem;
                var active = _collections.Current.EquippedHomeItemsBySlot.TryGetValue(slot.Id, out var activeId) && activeId == item.Id;
                var hotspot = new System.Windows.Controls.Button
                {
                    Content = active ? "✓" : "•",
                    Width = Math.Min(slot.Width, 76),
                    Height = Math.Max(48, slot.Height),
                    Opacity = active ? 0.7 : 0.5,
                    ToolTip = active ? $"{item.Name}: aktiv" : $"{item.Name}: gekauft",
                    Style = (Style)FindResource("HomeSlotButton")
                };
                System.Windows.Controls.Canvas.SetLeft(hotspot, slot.X + (slot.Width - hotspot.Width) / 2);
                System.Windows.Controls.Canvas.SetTop(hotspot, Math.Max(0, slot.Y - (hotspot.Height - slot.Height) / 2));
                System.Windows.Controls.Panel.SetZIndex(hotspot, slot.ZIndex);
                hotspot.Click += (_, _) => ShowHomeSelection(slot, item);
                HomeCanvas.Children.Add(hotspot);
                continue;
            }

            var lockHeight = Math.Max(54, slot.Height);
            var lockWidth = Math.Min(slot.Width, 92);
            var lockButton = new System.Windows.Controls.Button
            {
                Content = "🔒",
                Width = lockWidth,
                Height = lockHeight,
                Opacity = 0.72,
                ToolTip = $"{slot.Name}: noch nicht eingerichtet",
                Style = (Style)FindResource("HomeSlotButton")
            };
            System.Windows.Controls.Canvas.SetLeft(lockButton, slot.X + (slot.Width - lockWidth) / 2);
            System.Windows.Controls.Canvas.SetTop(lockButton, Math.Max(0, slot.Y - (lockHeight - slot.Height) / 2));
            System.Windows.Controls.Panel.SetZIndex(lockButton, slot.ZIndex);
            lockButton.Click += (_, _) => ShowHomeSelection(slot, slotItem);
            HomeCanvas.Children.Add(lockButton);
        }

        var totalSlots = house.Slots?.Count ?? 0;
        var percentage = totalSlots == 0 ? 0 : (int)Math.Round(configuredSlots * 100d / totalSlots);
        HomeProgressText.Text = $"{configuredSlots} / {totalSlots} eingerichtet";
        HomeProgressBar.Value = percentage;
        HomeProgressPercent.Text = $"{percentage}%";
        var activeBonuses = _collections.HomeBonuses.Current.ActiveDescriptions;
        HomeActiveBonusesText.Text = activeBonuses.Count == 0
            ? "Haus-Boni aktiv: keine"
            : $"Haus-Boni aktiv: {string.Join("  •  ", activeBonuses)}";
        HomeActiveBonusesText.ToolTip = activeBonuses.Count == 0 ? null : string.Join(Environment.NewLine, activeBonuses);

        var selectedSlot = house.Slots?.FirstOrDefault(s => s.Id == _selectedHomeSlotId)
                           ?? house.Slots?.FirstOrDefault();
        if (selectedSlot != null)
        {
            var selectedItem = _collections.Current.EquippedHomeItemsBySlot.TryGetValue(selectedSlot.Id, out var selectedId)
                ? _collections.Home.Items.FirstOrDefault(i => i.Id == selectedId)
                : _collections.Home.Items.FirstOrDefault(i => i.SlotId == selectedSlot.Id);
            ShowHomeSelection(selectedSlot, selectedItem);
        }
    }

    private static System.Windows.Media.Imaging.BitmapImage? LoadHomeImage(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return null;
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path)) return null;

        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static System.Windows.Media.Imaging.BitmapSource? LoadCroppedInterfaceImage(
        string assetPath,
        int x,
        int y,
        int width,
        int height)
    {
        var source = LoadHomeImage(assetPath);
        if (source == null) return null;
        if (x < 0 || y < 0 || width <= 0 || height <= 0
            || x + width > source.PixelWidth || y + height > source.PixelHeight)
        {
            return source;
        }

        var crop = new System.Windows.Media.Imaging.CroppedBitmap(source, new Int32Rect(x, y, width, height));
        crop.Freeze();
        return crop;
    }

    private void HomePrevItemButton_Click(object sender, RoutedEventArgs e)
    {
        var house = _collections.Home.Houses.FirstOrDefault(h => h.Id == _collections.Current.ActiveHouseId);
        var slot = house?.Slots?.FirstOrDefault(s => s.Id == _selectedHomeSlotId);
        if (slot == null) return;
        var itemsForSlot = _collections.Home.Items.Where(i => i.SlotId == slot.Id).OrderBy(i => i.RequiredLevel).ThenBy(i => i.Cost).ToList();
        if (itemsForSlot.Count <= 1) return;
        _selectedSlotItemIndex = (_selectedSlotItemIndex - 1 + itemsForSlot.Count) % itemsForSlot.Count;
        ShowHomeSelection(slot, itemsForSlot[_selectedSlotItemIndex]);
    }

    private void HomeNextItemButton_Click(object sender, RoutedEventArgs e)
    {
        var house = _collections.Home.Houses.FirstOrDefault(h => h.Id == _collections.Current.ActiveHouseId);
        var slot = house?.Slots?.FirstOrDefault(s => s.Id == _selectedHomeSlotId);
        if (slot == null) return;
        var itemsForSlot = _collections.Home.Items.Where(i => i.SlotId == slot.Id).OrderBy(i => i.RequiredLevel).ThenBy(i => i.Cost).ToList();
        if (itemsForSlot.Count <= 1) return;
        _selectedSlotItemIndex = (_selectedSlotItemIndex + 1) % itemsForSlot.Count;
        ShowHomeSelection(slot, itemsForSlot[_selectedSlotItemIndex]);
    }

    private void ShowHomeSelection(HouseSlotDefinition slot, HomeItemDefinition? item)
    {
        _selectedHomeSlotId = slot.Id;
        var itemsForSlot = _collections.Home.Items.Where(i => i.SlotId == slot.Id).OrderBy(i => i.RequiredLevel).ThenBy(i => i.Cost).ToList();
        if (item != null)
        {
            var idx = itemsForSlot.FindIndex(i => i.Id == item.Id);
            if (idx >= 0) _selectedSlotItemIndex = idx;
        }
        else if (itemsForSlot.Count > 0)
        {
            _selectedSlotItemIndex = Math.Clamp(_selectedSlotItemIndex, 0, itemsForSlot.Count - 1);
            item = itemsForSlot[_selectedSlotItemIndex];
        }

        if (itemsForSlot.Count > 1 && item != null)
        {
            HomeSelectionSlotItemCount.Text = $"({_selectedSlotItemIndex + 1}/{itemsForSlot.Count})";
            HomeSelectionSlotItemCount.Visibility = Visibility.Visible;
            HomePrevItemButton.Visibility = Visibility.Visible;
            HomeNextItemButton.Visibility = Visibility.Visible;
        }
        else
        {
            HomeSelectionSlotItemCount.Visibility = Visibility.Collapsed;
            HomePrevItemButton.Visibility = Visibility.Collapsed;
            HomeNextItemButton.Visibility = Visibility.Collapsed;
        }

        HomeSelectionName.Text = item?.Name ?? slot.Name;
        HomeSelectionPreview.Source = item == null ? null : LoadHomeImage(item.AssetPath);
        HomeSelectionPlaceholder.Text = item == null ? "🔒" : "🏡";
        HomeSelectionPlaceholder.Visibility = HomeSelectionPreview.Source == null ? Visibility.Visible : Visibility.Collapsed;

        if (item == null)
        {
            HomeSelectionDescription.Text = "Dieser Einrichtungsplatz ist noch gesperrt.";
            HomeSelectionBonus.Text = "Für diesen Platz ist noch kein Standardobjekt verfügbar.";
            HomeEquipButton.Tag = null;
            HomeEquipButton.Visibility = Visibility.Collapsed;
            return;
        }

        var purchased = _collections.Current.UnlockedHomeItems.Contains(item.Id);
        var equipped = _collections.Current.EquippedHomeItemsBySlot.TryGetValue(slot.Id, out var equippedId) && equippedId == item.Id;
        var previousTierActive = _collections.Home.IsPreviousTierActive(item);
        var levelReady = _progress.Current.Level >= item.RequiredLevel;
        var hasEnoughCoins = _progress.Current.DinoCoins >= item.Cost;

        string status;
        if (equipped)
        {
            var nextTier = _collections.Home.Items.FirstOrDefault(candidate => candidate.RequiredPreviousItemId == item.Id);
            var nextTierInfo = nextTier != null ? $"  ·  Nächste Stufe: {nextTier.Name} (Stufe {nextTier.UpgradeLevel})" : "";
            status = $"Aktiv eingerichtet (Stufe {item.UpgradeLevel}){nextTierInfo}";
        }
        else if (purchased)
        {
            status = $"Im Besitz (Stufe {item.UpgradeLevel})";
        }
        else if (!previousTierActive)
        {
            status = $"Gesperrt: Benötigt aktive Vorgängerstufe (Stufe {item.UpgradeLevel - 1})";
        }
        else if (!levelReady)
        {
            status = $"Gesperrt: Benötigt Level {item.RequiredLevel}";
        }
        else if (!hasEnoughCoins)
        {
            status = $"Gesperrt: Benötigt {item.Cost} Dino Coins";
        }
        else
        {
            status = "Freigeschaltet zum Kauf";
        }

        var tierPrefix = item.UpgradeLevel > 1 ? $"[Stufe {item.UpgradeLevel}] " : "";
        HomeSelectionDescription.Text = $"{tierPrefix}Benötigt: Level {item.RequiredLevel}  ·  Preis: {item.Cost} Dino Coins  ·  Status: {status}";
        var descriptions = (item.Bonuses ?? []).Where(bonus => !string.IsNullOrWhiteSpace(bonus.Description)).Select(bonus => bonus.Description).ToArray();
        HomeSelectionBonus.Text = descriptions.Length == 0 ? "Bonus: Keiner" : $"Bonus: {string.Join("  •  ", descriptions)}";
        HomeEquipButton.Tag = item.Id;
        HomeEquipButton.Content = equipped
            ? "✓ Aktiv"
            : purchased
                ? "Einrichten"
                : !previousTierActive
                    ? "Vorgänger nötig"
                    : levelReady ? $"Kaufen ({item.Cost})" : $"Ab Level {item.RequiredLevel}";
        HomeEquipButton.IsEnabled = !equipped && (purchased || (levelReady && previousTierActive && hasEnoughCoins));
        HomeEquipButton.Visibility = Visibility.Visible;
    }

    private void HomeEquipButton_Click(object sender, RoutedEventArgs e)
    {
        if (HomeEquipButton.Tag is not string itemId) return;
        var item = _collections.Home.Items.FirstOrDefault(candidate => candidate.Id == itemId);
        if (item == null) return;

        if (!_collections.Current.UnlockedHomeItems.Contains(itemId))
        {
            var result = _collections.Home.PurchaseItem(itemId, _progress);
            switch (result)
            {
                case HomePurchaseResult.Success:
                    DinoDesktopCompanion.UI.Toast.ToastNotificationWindow.ShowToast("Neu!", $"Du hast {item.Name} gekauft!", "🪙");
                    UpdateProgressUI();
                    LoadHome();
                    return;
                case HomePurchaseResult.RequiresPreviousTier:
                    System.Windows.MessageBox.Show($"Für Stufe {item.UpgradeLevel} muss die Vorgängerstufe aktuell aktiv im Raum eingerichtet sein.", "Vorgängerstufe benötigt", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                case HomePurchaseResult.LevelTooLow:
                    System.Windows.MessageBox.Show($"Dino benötigt Level {item.RequiredLevel}, bevor du dieses Hausobjekt kaufen kannst.", "Noch gesperrt", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                case HomePurchaseResult.NotEnoughCoins:
                    System.Windows.MessageBox.Show($"Für {item.Name} fehlen noch Dino Coins. Benötigt werden {item.Cost} Coins.", "Nicht genug Dino Coins", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                case HomePurchaseResult.AlreadyPurchased:
                    LoadHome();
                    return;
                default:
                    System.Windows.MessageBox.Show("Dieses Hausobjekt konnte nicht gekauft werden.", "Dinohaus", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
            }
        }

        _progress.RefreshAdventurePoints(DateTimeOffset.Now);
        if (_collections.Home.EquipItem(itemId))
        {
            UpdateProgressUI();
            LoadHome();
        }
    }

    private void LoadAreas()
    {
        if (AreasList is null) return;
        AreasList.Children.Clear();
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
        {
            ["garten"] = (45, 65), ["wald"] = (270, 50), ["strand"] = (90, 238),
            ["hoehle"] = (520, 228), ["schneeland"] = (655, 50)
        };
        foreach (var area in _areas.Current.Areas)
        {
            var unlocked = _progress.Current.Level >= area.MinLevel;
            var selected = area.Id == _areas.Current.SelectedAreaId;
            var label = new StackPanel();
            label.Children.Add(new TextBlock
            {
                Text = unlocked ? area.Name : $"🔒 {area.Name}",
                FontSize = 11, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
            label.Children.Add(new TextBlock
            {
                Text = unlocked ? $"Level {area.MinLevel}" : $"Freischaltung ab Level {area.MinLevel}",
                FontSize = unlocked ? 10 : 9, FontWeight = unlocked ? FontWeights.Normal : FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
            var button = new System.Windows.Controls.Button
            {
                Content = label, Width = 140, Height = 58, MinWidth = 0, Padding = new Thickness(6, 4, 6, 4),
                IsEnabled = true, Opacity = .98,
                Background = selected ? new SolidColorBrush(Color.FromRgb(57, 150, 128)) : new SolidColorBrush(Color.FromArgb(248, 255, 255, 255)),
                Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(20, 39, 35)),
                BorderThickness = new Thickness(selected ? 3 : 1),
                BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(35, 112, 96)) : new SolidColorBrush(Color.FromRgb(190, 211, 203)),
                ToolTip = unlocked ? $"{area.Name} auswählen" : $"Freischaltung ab Level {area.MinLevel}",
                Cursor = unlocked ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow
            };
            ToolTipService.SetShowOnDisabled(button, true);
            if (unlocked)
            {
                button.Click += (_, _) =>
                {
                    if (!EnsureDinoAvailable()) return;
                    if (!_areas.SelectArea(area.Id, _progress.Current.Level)) return;
                    App.Current.DinoWindow.RefreshActiveArea();
                    LoadAreas();
                    LoadAdventureDetails();
                };
            }
            var position = positions.GetValueOrDefault(area.Id, (20d, 20d));
            Canvas.SetLeft(button, position.Item1);
            Canvas.SetTop(button, position.Item2);
            AreasList.Children.Add(button);
        }
    }

    private void LoadAdventureDetails()
    {
        var area = _areas.SelectedArea;
        AdventureAreaDetails.Visibility = Visibility.Visible;
        if (area is null)
        {
            SelectedAreaTitle.Text = "Kein Gebiet aktiv";
            SelectedAreaLevel.Text = "Desktop-Grabungen sind ausgeschaltet";
            SelectedAreaDescription.Text = "Wähle oben ein Gebiet aus, damit wieder zufällige Grabungen erscheinen.";
            DiggingInfoText.Text = "Desktop-Grabungen: Aus";
            DisableAreaButton.Visibility = Visibility.Collapsed;
            DiggingPanel.Visibility = Visibility.Visible;
            return;
        }

        SelectedAreaTitle.Text = $"Aktives Gebiet: {area.Name}";
        SelectedAreaLevel.Text = $"Level {area.MinLevel}";
        SelectedAreaDescription.Text = area.Description;
        DiggingInfoText.Text = $"Desktop-Grabungen: Zufällig aktiv · {GetDigSiteVisualName(area.DigSiteVisual)}";
        DisableAreaButton.Visibility = Visibility.Visible;
        DiggingPanel.Visibility = Visibility.Visible;
    }

    private void DisableArea_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureDinoAvailable()) return;
        _areas.DisableArea();
        App.Current.DinoWindow.RefreshActiveArea();
        LoadAreas();
        LoadAdventureDetails();
    }

    private static string GetDigSiteVisualName(DigSiteVisualType visual) => visual switch
    {
        DigSiteVisualType.MossAndRoots => "Moos und Wurzeln",
        DigSiteVisualType.Sand => "Sand",
        DigSiteVisualType.StonesAndCrystals => "Steine und Kristalle",
        DigSiteVisualType.Snow => "Schnee",
        _ => "Erde und Blätter"
    };

    private void ShowAdventure()
    {
        HideAllPanels();
        PanelAdventure.Visibility = Visibility.Visible;
        LoadAreas();
        LoadAdventureDetails();
    }

    private void ShowPlay()
    {
    }

    private void Adventure_Click(object sender, RoutedEventArgs e) => ShowAdventure();
    private void ShowDigging_Click(object sender, RoutedEventArgs e) => ShowDiggingPanel();
    private void ShowDiggingPanel()
    {
        DiggingPanel.Visibility = Visibility.Visible;
    }

    private void LoadCollections()
    {
    }

    private void LoadAchievements()
    {
        if (AchievementsList == null) return;
        AchievementsList.Children.Clear();

        var unlockedIds = _achievements.Current.UnlockedAchievements;
        var validAchievements = _achievements.AllAchievements;
        var unlockedCount = validAchievements.Count(achievement => unlockedIds.Contains(achievement.Id));
        
        if (AchievementsTotalText != null)
        {
            AchievementsTotalText.Text = $"Erfolge: {unlockedCount} / {validAchievements.Count} abgeschlossen";
            AchievementsTotalBar.Value = validAchievements.Count > 0 ? (double)unlockedCount / validAchievements.Count * 100 : 0;
        }

        string filter = "Alle";
        if (FilterAdventure?.IsChecked == true) filter = "Abenteuer";
        else if (FilterCollect?.IsChecked == true) filter = "Sammeln";
        else if (FilterHome?.IsChecked == true) filter = "Zuhause";

        if (FilterAdventure != null)
        {
            var advCount = validAchievements.Count(a => a.Category == "Abenteuer");
            var advUnl = validAchievements.Count(a => a.Category == "Abenteuer" && unlockedIds.Contains(a.Id));
            FilterAdventure.Content = $"Abenteuer {advUnl}/{advCount}";
            
            var colCount = validAchievements.Count(a => a.Category == "Sammeln");
            var colUnl = validAchievements.Count(a => a.Category == "Sammeln" && unlockedIds.Contains(a.Id));
            FilterCollect.Content = $"Sammeln {colUnl}/{colCount}";
            
            var homCount = validAchievements.Count(a => a.Category == "Zuhause");
            var homUnl = validAchievements.Count(a => a.Category == "Zuhause" && unlockedIds.Contains(a.Id));
            FilterHome.Content = $"Zuhause {homUnl}/{homCount}";
        }

        foreach (var achievement in validAchievements.Where(a => filter == "Alle" || a.Category == filter))
        {
            var unlocked = unlockedIds.Contains(achievement.Id);
            var secret = achievement.IsSecret && !unlocked;
            var card = new Border
            {
                Background = unlocked
                    ? new SolidColorBrush(Color.FromRgb(232, 247, 239))
                    : new SolidColorBrush(Color.FromRgb(242, 244, 243)),
                BorderBrush = unlocked
                    ? new SolidColorBrush(Color.FromRgb(72, 163, 126))
                    : new SolidColorBrush(Color.FromRgb(208, 217, 213)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(13, 10, 13, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = unlocked ? "✔️" : secret ? "?" : "☆",
                FontSize = unlocked ? 20 : 26,
                FontWeight = FontWeights.Bold,
                Foreground = unlocked ? new SolidColorBrush(Color.FromRgb(72, 163, 126)) : Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });

            var copy = new StackPanel { Margin = new Thickness(8, 0, 12, 0) };
            copy.Children.Add(new TextBlock
            {
                Text = secret ? "???" : achievement.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });
            copy.Children.Add(new TextBlock
            {
                Text = secret ? "Dieser Erfolg muss erst entdeckt werden." : achievement.Description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(91, 112, 107)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            
            if (!secret)
            {
                var progressValue = unlocked ? achievement.Target : _achievements.GetProgress(achievement);
                var progressText = new TextBlock
                {
                    Text = unlocked ? "100 %" : $"{progressValue} / {achievement.Target}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(72, 163, 126)),
                    Margin = new Thickness(0, 4, 0, 2)
                };
                copy.Children.Add(progressText);
                
                if (!unlocked && achievement.Target > 1)
                {
                    var progressBar = new System.Windows.Controls.ProgressBar
                    {
                        Height = 4,
                        Minimum = 0,
                        Maximum = achievement.Target,
                        Value = progressValue,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    copy.Children.Add(progressBar);
                }
            }

            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);

            var reward = new TextBlock
            {
                Text = unlocked ? "Abgeschlossen" : $"+{achievement.XPReward} XP\n+{achievement.CoinReward} Coins",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = unlocked ? new SolidColorBrush(Color.FromRgb(44, 143, 124)) : Brushes.Gray,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(reward, 2);
            row.Children.Add(reward);
            card.Child = row;
            AchievementsList.Children.Add(card);
        }
    }

    private void AchievementFilter_Checked(object sender, RoutedEventArgs e)
    {
        LoadAchievements();
    }



    private static bool EnsureDinoAwake()
    {
        if (!App.Current.DinoWindow.IsSleeping) return true;
        System.Windows.MessageBox.Show("Dino schläft gerade. Wecke ihn zuerst auf.", "Dino schläft", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private static bool EnsureDinoAvailable()
    {
        if (App.Current.DinoWindow.EnsureDinoAvailableForAction()) return true;
        System.Windows.MessageBox.Show("Dino gräbt gerade. Brich die Grabung zuerst ab.", "Dino ist beschäftigt", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    private static bool EnsureDinoReadyForAction() => EnsureDinoAwake() && EnsureDinoAvailable();



    private void BuyStandardSkin_Click(object sender, RoutedEventArgs e)
    {
        if (_progress.TrySpendCoins(10, "Shop"))
        {
            DinoDesktopCompanion.UI.Toast.ToastNotificationWindow.ShowToast("Erfolgreich gekauft!", "Skin freigeschaltet", "🪙");
            _stats.TrackPurchase();
            _achievements.CheckCustomCondition("secret_first_skin");
        }
        else
        {
            System.Windows.MessageBox.Show("Nicht genug Dino Coins!", "Shop");
        }
    }

    private void SleepPage_Click(object sender, RoutedEventArgs e)
    {
        Nav_Start_Checked(this, e);
    }

    private void SendHome_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current.MainWindow is MainWindow mw && mw.EnsureDinoAvailableForAction())
        {
            mw.SendHome();
            UpdateSleepUI();
        }
    }

    private void CallDesktop_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current.MainWindow is MainWindow mw && mw.EnsureDinoAvailableForAction())
        {
            mw.CallToCursor();
            UpdateSleepUI();
        }
    }

    private void SleepDino_Click(object sender, RoutedEventArgs e)
    {
        _progress.RefreshAdventurePoints(DateTimeOffset.Now);
        if (_progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints)
        {
            App.Current.DinoWindow.ShowSpeech("Meine Energie ist schon voll!", 3000);
            UpdateSleepUI();
            return;
        }
        if (!_progress.IsSleeping && EnsureDinoAvailable()) App.Current.DinoWindow.SetSleepState();
        UpdateProfileUI();
        UpdateProgressUI();
        UpdateSleepUI();
    }

    private void WakeDino_Click(object sender, RoutedEventArgs e)
    {
        if (_progress.IsSleeping) App.Current.DinoWindow.WakeUp();
        UpdateProfileUI();
        UpdateProgressUI();
        UpdateSleepUI();
    }

    private void OpenInfo_Click(object sender, RoutedEventArgs e)
    {
        ((App)System.Windows.Application.Current).OpenTutorialWindow();
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        ((App)System.Windows.Application.Current).OpenProfileWindow();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (App.Current.MainWindow is MainWindow mw)
        {
            mw.OpenSettings();
        }
    }

    private void QuitApp_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}
