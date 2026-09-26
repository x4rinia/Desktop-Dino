using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.Dino.Animation;
using DinoDesktopCompanion.Dino.Behaviour;
using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.GPU;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.UI;
using DinoDesktopCompanion.UI.Settings;
using DinoDesktopCompanion.DesktopDigging;

namespace DinoDesktopCompanion;

public partial class MainWindow : Window
{
    private readonly ConfigurationService _configuration;
    private readonly OllamaClient _ollama;
    private readonly DinoStateMachine _states = new();
    private readonly DinoAnimationService _animations;
    private readonly DinoSpriteCatalog _catalog;
    private readonly DinoSpritePlayer _spritePlayer;
    private readonly MessageService _messages;
    private readonly DinoBehaviourService _behaviour;
    private readonly DinoHomeService _home;
    private readonly MouseFollowService _mouseFollow;
    private readonly DispatcherTimer _bubbleTimer = new();
    private readonly DispatcherTimer _returnToIdleTimer = new();
    private readonly DispatcherTimer _sleepClickMenuTimer = new();
    private readonly DispatcherTimer _fullScreenTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _apRegenTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _leavesTimer = new();
    private readonly DispatcherTimer _autoLeafCollectTimer = new() { Interval = TimeSpan.FromSeconds(6) };
    private readonly DispatcherTimer _shootingStarTimer = new();
    private DispatcherTimer? _walkTimer;
    private UI.AreaVignetteWindow? _vignetteWindow;
    private string? _pendingWakeMessage;
    private bool _isEventActive;
    private GpuDinoWindow? _chatWindow;
    private bool _isSmoothMoving;
    private bool _isDesktopDigging;
    private bool _hiddenForFullscreen;
    private bool _allowClose;
    private readonly Progress.ProgressService _progress;
    private readonly Statistics.StatisticsService _statistics;
    private readonly DesktopDigSiteService _digSites;
    private readonly DesktopActivityRewardService _rewardService;
    private bool _waitingForHighFive;
    private bool _waitingForAttention;
    private bool _waitingForDigInteraction;

    public MainWindow(ConfigurationService configuration, FileLogger logger, OllamaClient ollama, Progress.ProgressService progress, Statistics.StatisticsService statistics)
    {
        InitializeComponent();
        _configuration = configuration; _ollama = ollama;
        _progress = progress; _statistics = statistics;
        _animations = new DinoAnimationService(logger);
        _catalog = new DinoSpriteCatalog(logger);
        _spritePlayer = new DinoSpritePlayer(_catalog, Dino.SetSprite);
        _messages = new MessageService(logger);
        _behaviour = new DinoBehaviourService(configuration.Current, _states, progress);
        _home = new DinoHomeService(_states);
        _rewardService = new DesktopActivityRewardService(progress);
        _mouseFollow = new MouseFollowService(configuration.Current, GetCompanionCenter, CanFollowMouse);
        var app = (App)System.Windows.Application.Current;
        _digSites = new DesktopDigSiteService(this, progress, statistics, app.Collections, app.Areas,
            CanSpawnDigSite, TravelToDigSite, FinishDesktopDigging, message => ShowSpeech(message));
        _states.StateChanged += ApplyState;
        
        _behaviour.WalkRequested += WalkBriefly;
        _behaviour.RandomMessageRequested += () => ShowSpeech(_messages.GetForTimeOfDay());
        _behaviour.TransientStateRequested += PlayTransientState;
        _behaviour.TurnAroundRequested += () => { if (_states.Current == DinoState.Idle) { Dino.SetFacingLeft(!Dino.IsFacingLeft); ReturnToIdleAfter(1500); } };
        _behaviour.HeartEventRequested += () => { if (_states.Current == DinoState.Idle) ShowSpeech("💖", 3000); };
        _behaviour.HuntMouseRequested += () => { if (_states.Current == DinoState.Idle) HuntMouseBriefly(); };
        _behaviour.HighFiveRequested += StartHighFive;
        _behaviour.AreaEventRequested += StartRandomAreaEvent;
        
        ((App)System.Windows.Application.Current).Collections.CollectionChanged += (_, _) => UpdateSkin();
        UpdateSkin();

        _vignetteWindow = new UI.AreaVignetteWindow();
        UpdateVignette();

        _mouseFollow.StepRequested += (_, args) => FollowMouseStep(args);
        _bubbleTimer.Tick += (_, _) => { SpeechBubble.Visibility = Visibility.Collapsed; _bubbleTimer.Stop(); };
        _returnToIdleTimer.Tick += (_, _) =>
        {
            _returnToIdleTimer.Stop();
            if (!_isDesktopDigging && !IsSleepState(_states.Current) && _states.Current != DinoState.Home)
                _states.Set(DinoState.Idle);
        };
        _sleepClickMenuTimer.Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime);
        _sleepClickMenuTimer.Tick += (_, _) =>
        {
            _sleepClickMenuTimer.Stop();

        };
        _fullScreenTimer.Tick += (_, _) => CheckFullscreen();
        _apRegenTimer.Tick += (_, _) => 
        { 
            if (_progress.IsSleeping)
            {
                _progress.RefreshAdventurePoints(DateTimeOffset.Now); 
                if (_progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints)
                {
                    WakeUpFromFullEnergy();
                }
            }
            else if (IdleTimeService.GetIdleTime().TotalMinutes > 1.5 && !_isDesktopDigging)
            {
                _rewardService.TryRewardAP("IdleRest");
            }
        };
        _autoLeafCollectTimer.Tick += (_, _) => TryDinoAutoLeafCollect();
        _leavesTimer.Tick += LeavesTimer_Tick;
        _shootingStarTimer.Tick += ShootingStarTimer_Tick;
        _progress.AdventurePointsChanged += Progress_AdventurePointsChanged;
        _configuration.Changed += (_, _) => ApplySettings();
        Loaded += OnLoaded;
        SourceInitialized += (_, _) => { ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WindowProc); ReapplyTopmost(); };
        Activated += (_, _) => ReapplyTopmost();
        Deactivated += (_, _) => { if (_configuration.Current.AlwaysOnTop && IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ReapplyTopmost)); };
        IsVisibleChanged += (_, _) => { if (IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ReapplyTopmost)); };
        Icon = AppIconService.LoadImageSource();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var restoreSleep = _progress.IsSleeping
            || ((App)System.Windows.Application.Current).Profiles.ActiveProfile?.State == Profiles.DinoState.Sleeping;
        if (restoreSleep && _progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints)
        {
            restoreSleep = false;
            _progress.StopSleeping();
            if (((App)System.Windows.Application.Current).Profiles.ActiveProfile != null)
                ((App)System.Windows.Application.Current).Profiles.ActiveProfile.State = Profiles.DinoState.Idle;
        }
        MonitorService.RestoreOrCenter(this, _configuration.Current);
        if (restoreSleep)
            RestoreSleepState();
        else
            ApplyState(DinoState.Idle);
        ApplySettings();
        _behaviour.Start();
        _fullScreenTimer.Start();
        _apRegenTimer.Start();
        _autoLeafCollectTimer.Start();
        _digSites.Start();
        ScheduleNextLeavesTimer();
        ScheduleNextShootingStarTimer();
        if (IsSleeping) return;
        if (_progress.InitialAdventurePointsGained > 0)
            ShowSpeech($"⚡ +{_progress.InitialAdventurePointsGained} AP", 2800);
        else
            ShowSpeech(_messages.Get("greeting"));
    }

    private void UpdateSkin()
    {
        var collections = ((App)System.Windows.Application.Current).Collections;
        var equippedSkinId = collections.Current.EquippedSkinId;
        var skinDef = collections.Cosmetics.Skins.FirstOrDefault(s => s.Id == equippedSkinId);
        if (skinDef != null)
        {
            _catalog.SetSkin(skinDef);
            Dino.ApplySkin(skinDef);
            ApplyState(_states.Current);
        }
    }

    public void ApplySettings()
    {
        var cfg = _configuration.Current;
        WindowZOrderService.Apply(this, cfg.AlwaysOnTop);
        if (cfg.QuietMode && _states.Current != DinoState.Home && !IsSleepState(_states.Current)) _states.Set(DinoState.Idle);
        var menuItems = Dino.ContextMenu?.Items.OfType<System.Windows.Controls.MenuItem>();
        var quiet = menuItems?.FirstOrDefault(item => item.Header?.ToString() == "Dino ruhig");
        var roam = menuItems?.FirstOrDefault(item => item.Header?.ToString() == "Frei bewegen");
        if (quiet is not null) quiet.IsChecked = cfg.QuietMode;
        if (roam is not null) roam.IsChecked = cfg.CanRoam;
        Dino.Width = cfg.DinoSize; Dino.Height = cfg.DinoSize * 185d / 230d;
        Dino.LayoutTransform = new ScaleTransform(cfg.DinoScale, cfg.DinoScale);
        Width = Math.Max(300, cfg.DinoSize * cfg.DinoScale + 54);
        Height = Math.Max(285, cfg.DinoSize * 185d / 230d * cfg.DinoScale + 105);
        MonitorService.KeepVisible(this);
        _mouseFollow.ApplyMode();
        ApplyState(_states.Current);
    }

    private void ApplyState(DinoState state)
    {
        Dispatcher.Invoke(() =>
        {
            var sleeping = IsSleepState(state);
            var savedState = sleeping ? Profiles.DinoState.Sleeping : Profiles.DinoState.Idle;
            ((App)System.Windows.Application.Current).Profiles.UpdateDinoState(savedState, sleeping ? DateTime.Now : null);
            if (sleeping)
            {
                if (!_progress.Current.SleepStartedAt.HasValue) _progress.StartSleeping(DateTimeOffset.Now);
                _digSites.CancelActive();
                if (_configuration.Current.SleepBehavior == "Versteckt")
                {
                    Hide();
                }
                else
                {
                    // Do nothing - Dino sleeps wherever he currently is.
                }
            }
            else if (state == DinoState.Wake)
            {
                Show();
            }

            Dispatcher.Invoke(() => UpdateSleepToolTip());

            Dino.ShowState(state);
            _spritePlayer.Play(state, _configuration.Current.Animations && !_configuration.Current.QuietMode, _configuration.Current.AnimationSpeed);
            _animations.Apply(Dino, state, _configuration.Current.Animations && !_configuration.Current.QuietMode, _configuration.Current.AnimationSpeed);
            if (state == DinoState.Home) { SpeechBubble.Visibility = Visibility.Collapsed; return; }
            if (sleeping) ShowSpeech(_messages.Get("sleep"), 2500);
            else if (state == DinoState.Wake)
            {
                if (!string.IsNullOrEmpty(_pendingWakeMessage))
                {
                    ShowSpeech(_pendingWakeMessage, 3000);
                    _pendingWakeMessage = null;
                }
                else
                {
                    ShowSpeech(_messages.Get("wake"));
                }
                ReturnToIdleAfter(900);
            }
        });
    }

    public void SetSleepState()
    {
        if ((_progress.IsSleeping && _home.IsHome) || IsSleeping || !EnsureDinoAvailableForAction()) return;
        _progress.RefreshAdventurePoints(DateTimeOffset.Now);
        if (_progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints)
        {
            ShowSpeech("Meine Energie ist schon voll!", 3000);
            return;
        }
        _digSites.CancelActive();
        StopAnimatedWindowMovement();
        _walkTimer?.Stop();
        _walkTimer = null;
        _returnToIdleTimer.Stop();
        _progress.StartSleeping(DateTimeOffset.Now);
        
        if (!_home.IsHome)
        {
            _states.Set(Dino.IsFacingLeft ? DinoState.SleepLeft : DinoState.SleepRight);
        }
        RefreshActiveArea();
    }

    public void WakeUpFromFullEnergy()
    {
        if (!IsSleeping && !_progress.IsSleeping) return;
        _progress.RegenerateAdventurePoints(DateTimeOffset.Now);
        
        if (_home.IsHome) 
        {
            _progress.StopSleeping();
            return;
        }

        _progress.StopSleeping();
        _pendingWakeMessage = "Ich bin wieder fit!";
        _states.Set(DinoState.Wake);
    }

    private void RestoreSleepState()
    {
        _states.Set(Dino.IsFacingLeft ? DinoState.SleepLeft : DinoState.SleepRight);
    }

    private static bool IsSleepState(DinoState state) => DinoStateMachine.IsSleepState(state);

    public bool IsSleeping => IsSleepState(_states.Current);
    public bool IsPerformingAction => _isDesktopDigging;
    public bool EnsureDinoAvailableForAction()
    {
        if (!_isDesktopDigging) return true;
        ShowSpeech("Ich grabe gerade! Brich die Grabung zuerst ab.");
        return false;
    }

    public void RefreshActiveArea()
    {
        if (!_isDesktopDigging) 
        {
            _digSites.CancelActive();
            foreach (var leaf in _activeLeaves.ToList()) { leaf.Close(); }
            _activeLeaves.Clear();
        }
        UpdateVignette();
    }

    public void UpdateVignette()
    {
        if (_vignetteWindow == null) return;
        var app = (App)System.Windows.Application.Current;
        var area = app.Areas?.Current?.SelectedAreaId;
        if (_home.IsHome || string.IsNullOrEmpty(area) || area == "none" || _hiddenForFullscreen || !IsVisible)
        {
            _vignetteWindow.Hide();
        }
        else
        {
            _vignetteWindow.UpdateArea(area);
        }
    }

    public void WakeUp()
    {
        if (!IsSleeping && !_progress.IsSleeping) return;
        _progress.RegenerateAdventurePoints(DateTimeOffset.Now);
        _progress.StopSleeping();
        
        if (_home.IsHome)
        {
            RefreshActiveArea();
            return;
        }
        
        _states.WakeUp();
    }

    public void WakeDino() => WakeUp();

    public void ToggleSleepState()
    {
        if (IsSleeping) WakeUp();
        else SetSleepState();
    }

    public void OpenDinoMenu() => OpenInteractionMenu();

    public void PetFromMenu()
    {
        _statistics.TrackClick();
        if (IsSleeping) return;
        if (DateTimeOffset.Now - _lastClickXP > TimeSpan.FromSeconds(5))
        {
            _progress.AddXP(1, "Pet");
            _lastClickXP = DateTimeOffset.Now;
        }

        var hasHeart = SpeechBubble.IsVisible && SpeechText.Text == "💖";
        if (hasHeart)
        {
            _states.Set(DinoState.BigHappy);
            ShowSpeech("Das liebe ich!");
            ReturnToIdleAfter(1500);
            return;
        }

        var rand = new Random().Next(4);
        var msg = rand switch { 0 => "Das gefällt mir!", 1 => "Hihi!", 2 => "💖", _ => "" };
        if (msg != "") ShowSpeech(msg, 1200);

        _states.Set(rand % 2 == 0 ? DinoState.Happy : DinoState.SmallHappy);
        ReturnToIdleAfter(1200);
    }

    private void HuntMouseBriefly()
    {
        var cursor = System.Windows.Forms.Control.MousePosition;
        var center = GetCompanionCenter();
        var dx = cursor.X - center.X; var dy = cursor.Y - center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 600 || distance < 100)
        {
            _states.Set(DinoState.Curious);
            ReturnToIdleAfter(1500);
            return;
        }
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var targetLeft = Left + (dx * 0.3) / dpi.DpiScaleX;
        var targetTop = Top + (dy * 0.3) / dpi.DpiScaleY;
        _isSmoothMoving = true;
        _states.Set(DinoState.Walk);
        Dino.SetFacingLeft(dx < 0);
        var duration = TimeSpan.FromMilliseconds(1000);
        var leftAnimation = new System.Windows.Media.Animation.DoubleAnimation(Left, targetLeft, duration) { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
        var topAnimation = new System.Windows.Media.Animation.DoubleAnimation(Top, targetTop, duration) { FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop };
        leftAnimation.Completed += (_, _) =>
        {
            Left = targetLeft; Top = targetTop; BeginAnimation(LeftProperty, null); BeginAnimation(TopProperty, null);
            _isSmoothMoving = false;
            _states.Set(DinoState.Sniff);
            ReturnToIdleAfter(1500);
        };
        BeginAnimation(LeftProperty, leftAnimation); BeginAnimation(TopProperty, topAnimation);
    }

    public void ShowSpeech(string text, int milliseconds = 4500)
    {
        if (!_configuration.Current.SpeechBubbles) return;
        SpeechText.Text = text; SpeechBubble.Visibility = Visibility.Visible;
        _bubbleTimer.Stop(); _bubbleTimer.Interval = TimeSpan.FromMilliseconds(milliseconds); _bubbleTimer.Start();
    }

    private DateTimeOffset _lastClickXP = DateTimeOffset.MinValue;
    private InteractionWindow? _interactionWindow;
    private System.Windows.Controls.ContextMenu? _dinoContextMenu;
    private System.Windows.Point? _dragStartPos;
    private System.Windows.Point? _lastMousePos;
    private double _petDistance;
    private bool _isPetting;
    private DateTime _lastPetTime = DateTime.MinValue;

    private void Dino_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDesktopDigging)
        {
            ShowSpeech("Ich grabe gerade!");
            return;
        }
        if (e.ClickCount >= 2 && !IsSleeping)
        {
            OpenInteractionMenu();
            return;
        }
        _dragStartPos = e.GetPosition(this);
        _lastMousePos = _dragStartPos;
        _petDistance = 0;
        _isPetting = false;
        Dino.CaptureMouse();
    }

    private void Dino_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _dragStartPos.HasValue)
        {
            var pos = e.GetPosition(this);
            var dist = Math.Sqrt(Math.Pow(pos.X - _lastMousePos.Value.X, 2) + Math.Pow(pos.Y - _lastMousePos.Value.Y, 2));
            _petDistance += dist;
            _lastMousePos = pos;
            
            var distFromStart = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.Value.X, 2) + Math.Pow(pos.Y - _dragStartPos.Value.Y, 2));
            
            if (!_isPetting && distFromStart >= 15)
            {
                Dino.ReleaseMouseCapture();
                _dragStartPos = null;
                StopAnimatedWindowMovement();
                if (!IsSleeping) { _states.Set(DinoState.Happy); ReturnToIdleAfter(850); }
                try { DragMove(); } catch (InvalidOperationException) { }
                MonitorService.KeepVisible(this);
                MonitorService.SavePosition(this, _configuration.Current);
                _configuration.Save();
            }
            else if (!_isPetting && _petDistance > 60 && distFromStart < 15)
            {
                _isPetting = true;
                if (!IsSleeping && (DateTime.Now - _lastPetTime).TotalSeconds > 2)
                {
                    _lastPetTime = DateTime.Now;
                    var state = Random.Shared.Next(2) == 0 ? DinoState.Happy : DinoState.SmallHappy;
                    _states.Set(state);
                    if (!_rewardService.TryRewardAP("Pet"))
                    {
                        var speeches = new[] { "♥", "Hehe!", "Das ist schön!", "Schön!" };
                        ShowSpeech(speeches[Random.Shared.Next(speeches.Length)]);
                    }
                    ReturnToIdleAfter(1500);
                }
            }
        }
    }

    private void Dino_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStartPos.HasValue)
        {
            Dino.ReleaseMouseCapture();
            var pos = e.GetPosition(this);
            var distFromStart = Math.Sqrt(Math.Pow(pos.X - _dragStartPos.Value.X, 2) + Math.Pow(pos.Y - _dragStartPos.Value.Y, 2));
            
            if (!_isPetting && distFromStart < 10 && !IsSleeping && _petDistance < 30)
            {
                if (_waitingForHighFive)
                {
                    _waitingForHighFive = false;
                    SpeechBubble.RenderTransform = Transform.Identity;
                    _states.Set(DinoState.Happy);
                    _rewardService.TryRewardAP("HighFive");
                    ReturnToIdleAfter(1500);
                    return;
                }
                if (_waitingForAttention)
                {
                    _waitingForAttention = false;
                    SpeechBubble.RenderTransform = Transform.Identity;
                    _states.Set(DinoState.SmallHappy);
                    _rewardService.TryRewardAP("Attention");
                    ReturnToIdleAfter(1500);
                    return;
                }
                if (_waitingForDigInteraction)
                {
                    _waitingForDigInteraction = false;
                    _states.Set(DinoState.Happy);
                    _rewardService.TryRewardAP("DigFind");
                    ReturnToIdleAfter(1500);
                    return;
                }

                // Normaler Klick
                OpenInteractionMenu();
            }
            else if (IsSleeping && !_isPetting && distFromStart < 10)
            {
                OpenInteractionMenu();
            }
            _dragStartPos = null;
        }
    }

    private void Dino_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Rechtsklick-Menü deaktiviert wie gewünscht.
    }

    private System.Windows.Controls.ToolTip? _sleepToolTip;
    private System.Windows.Controls.TextBlock? _sleepToolTipApText;
    private System.Windows.Controls.TextBlock? _sleepToolTipRegenText;
    private System.Windows.Controls.ProgressBar? _sleepToolTipRegenBar;

    private void EnsureSleepToolTip()
    {
        if (_sleepToolTip != null) return;
        var stackPanel = new System.Windows.Controls.StackPanel { Width = 160 };
        stackPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "AP", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7A939E")) });
        
        _sleepToolTipApText = new System.Windows.Controls.TextBlock { FontSize = 18, FontWeight = System.Windows.FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A82D6")), Margin = new System.Windows.Thickness(0, 2, 0, 12) };
        stackPanel.Children.Add(_sleepToolTipApText);
        
        var grid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(0, 0, 0, 6) };
        grid.Children.Add(new System.Windows.Controls.TextBlock { Text = "Nächster AP", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A6572")), FontWeight = System.Windows.FontWeights.SemiBold });
        _sleepToolTipRegenText = new System.Windows.Controls.TextBlock { FontSize = 11, FontWeight = System.Windows.FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A6572")), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        grid.Children.Add(_sleepToolTipRegenText);
        stackPanel.Children.Add(grid);
        
        _sleepToolTipRegenBar = new System.Windows.Controls.ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#329A5B")), Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2ECE9")), BorderThickness = new System.Windows.Thickness(0) };
        stackPanel.Children.Add(_sleepToolTipRegenBar);
        
        _sleepToolTip = new System.Windows.Controls.ToolTip
        {
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF")),
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BFD5E5DF")),
            BorderThickness = new System.Windows.Thickness(1),
            Padding = new System.Windows.Thickness(12),
            Content = stackPanel
        };
    }

    private void UpdateSleepToolTip()
    {
        if (IsSleeping && _states.Current != DinoState.Home)
        {
            EnsureSleepToolTip();
            Dino.ToolTip = _sleepToolTip;
            
            if (_sleepToolTipApText != null) _sleepToolTipApText.Text = $"{_progress.Current.AdventurePoints} / {_progress.Current.MaxAdventurePoints}";
            var atMaximum = _progress.Current.AdventurePoints >= _progress.Current.MaxAdventurePoints;
            var progress = atMaximum ? 0 : Math.Clamp(_progress.Current.AdventurePointRegenProgress, 0, 0.9999999);
            var percent = (int)Math.Floor(progress * 100);
            if (_sleepToolTipRegenBar != null) _sleepToolTipRegenBar.Value = percent;
            if (_sleepToolTipRegenText != null) _sleepToolTipRegenText.Text = $"{percent} %";
        }
        else
        {
            Dino.ToolTip = null;
        }
    }

    private void Progress_AdventurePointsChanged(object? sender, Progress.AdventurePointsChangedEventArgs e)
    {
        if (IsSleeping && e.Current >= e.Maximum)
        {
            Dispatcher.BeginInvoke(new Action(() => WakeUpFromFullEnergy()));
            return;
        }

        if (e.Delta > 0 && !IsSleeping)
            Dispatcher.BeginInvoke(new Action(() => ShowSpeech($"⚡ +{e.Delta} AP", 2800)));
            
        Dispatcher.BeginInvoke(new Action(UpdateSleepToolTip));
    }

    private void OpenInteractionMenu()
    {
        if (_interactionWindow is not null) 
        { 
            _interactionWindow.Close(); 
            _interactionWindow = null;
            return; 
        }
        _interactionWindow = new InteractionWindow(((App)System.Windows.Application.Current).Progress, ((App)System.Windows.Application.Current).Collections, ((App)System.Windows.Application.Current).Tasks, _statistics, ((App)System.Windows.Application.Current).Achievements, ((App)System.Windows.Application.Current).Areas);
        _interactionWindow.Closed += (_, _) => _interactionWindow = null;
        _interactionWindow.Show();
    }

    private void WalkBriefly()
    {
        if (_configuration.Current.QuietMode || !_configuration.Current.CanRoam || _isSmoothMoving || _isDesktopDigging || IsSleeping) return;
        _states.Set(DinoState.Walk);
        var direction = Random.Shared.Next(2) == 0 ? -1 : 1;
        Dino.SetFacingLeft(direction < 0);
        _walkTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _walkTimer = timer;
        var steps = 0;
        timer.Tick += (_, _) =>
        {
            Left += direction * 3;
            MonitorService.KeepVisible(this);
            if (++steps < 20) return;
            timer.Stop(); _walkTimer = null; _states.Set(DinoState.Idle);
            MonitorService.SavePosition(this, _configuration.Current); _configuration.Save();
        };
        timer.Start();
    }

    private void WalkAlongBottom(bool isLongWalk)
    {
        if (_configuration.Current.QuietMode || !_configuration.Current.CanRoam || _isSmoothMoving || _isDesktopDigging || IsSleeping) return;
        _states.Set(DinoState.Walk);
        var direction = Random.Shared.Next(2) == 0 ? -1 : 1;
        Dino.SetFacingLeft(direction < 0);
        
        var center = new System.Drawing.Point((int)(Left + Width / 2), (int)(Top + Height / 2));
        var area = System.Windows.Forms.Screen.FromPoint(center).WorkingArea;
        Top = area.Bottom - Height - 10;
        
        _walkTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        _walkTimer = timer;
        var steps = 0;
        var maxSteps = isLongWalk ? 40 : 15;
        timer.Tick += (_, _) =>
        {
            Left += direction * 3;
            MonitorService.KeepVisible(this);
            if (++steps < maxSteps) return;
            timer.Stop(); _walkTimer = null; _states.Set(DinoState.Idle);
            MonitorService.SavePosition(this, _configuration.Current); _configuration.Save();
        };
        timer.Start();
    }

    private void PlayDigAnimation()
    {
        if (_configuration.Current.QuietMode || _isSmoothMoving || IsSleeping) return;
        _states.Set(DinoState.Dig);

        var anim = new DoubleAnimation
        {
            From = -5, To = 5, Duration = TimeSpan.FromMilliseconds(100), AutoReverse = true, RepeatBehavior = new RepeatBehavior(10)
        };
        var transform = new RotateTransform();
        Dino.RenderTransform = transform;
        Dino.RenderTransformOrigin = new System.Windows.Point(0.5, 0.8);
        transform.BeginAnimation(RotateTransform.AngleProperty, anim);

        ReturnToIdleAfter(2000);
    }

    private void PlayTransientState(DinoState state, int duration)
    {
        if (_home.IsHome || _configuration.Current.QuietMode || _isDesktopDigging || IsSleeping) return;
        _states.Set(state); ReturnToIdleAfter(duration);
    }
    
    public void TriggerDigSiteSpawnReaction()
    {
        if (_home.IsHome || _configuration.Current.QuietMode || _isDesktopDigging || IsSleeping || _isSmoothMoving) return;
        
        var reaction = Random.Shared.Next(2) == 0 ? DinoState.Curious : DinoState.LookAround;
        _states.Set(reaction);
        ShowSpeech("!");
        ReturnToIdleAfter(1500);
    }

    private void ReturnToIdleAfter(int milliseconds)
    {
        _returnToIdleTimer.Stop(); _returnToIdleTimer.Interval = TimeSpan.FromMilliseconds(milliseconds); _returnToIdleTimer.Start();
    }

    private void CheckFullscreen()
    {
        if (_home.IsHome) { _digSites.CancelActive(); _hiddenForFullscreen = false; if (IsVisible) Hide(); UpdateVignette(); return; }
        // A fullscreen app on another monitor must not hide the companion.
        var full = FullScreenDetector.IsForegroundFullScreen(this);
        if (full) _digSites.CancelActive();
        if (!_configuration.Current.HideInFullscreen) { if (_hiddenForFullscreen) { Show(); _hiddenForFullscreen = false; ReapplyTopmost(); UpdateVignette(); } return; }
        if (full && IsVisible && IsActive == false) { _digSites.CancelActive(); StopAnimatedWindowMovement(); _hiddenForFullscreen = true; Hide(); UpdateVignette(); }
        else if (!full && _hiddenForFullscreen) { _hiddenForFullscreen = false; Show(); ReapplyTopmost(); UpdateVignette(); }
    }

    public void SendHome()
    {
        if (!EnsureDinoAvailableForAction()) return;
        _home.SendHome(
            () =>
            {
                _digSites.CancelActive();
                _walkTimer?.Stop(); _walkTimer = null;
                StopAnimatedWindowMovement();
                _returnToIdleTimer.Stop(); _bubbleTimer.Stop();
                MonitorService.SavePosition(this, _configuration.Current); _configuration.Save();
            },
            () => { _hiddenForFullscreen = false; Hide(); UpdateVignette(); });
    }

    public void CallToCursor()
    {
        if (!EnsureDinoAvailableForAction()) return;
        _home.CallDino(() =>
        {
            _digSites.CancelActive();
            StopAnimatedWindowMovement();
            _hiddenForFullscreen = false;
            MonitorService.CallToCursor(this);
            ReapplyTopmost();
            
            if (_progress.IsSleeping) 
            {
                _states.Set(Dino.IsFacingLeft ? DinoState.SleepLeft : DinoState.SleepRight);
            }
            else 
            {
                ShowSpeech("Hier bin ich! 🦕");
                ReturnToIdleAfter(900);
            }
            
            UpdateVignette();
            RefreshActiveArea();
        });
    }

    public DinoHomeService Home => _home;
    public void CelebrateLevelUp(int level)
    {
        if (_home.IsHome || IsSleeping) return;
        _states.Set(DinoState.Happy);
        ShowSpeech($"Level {level}! 🦕");
        ReturnToIdleAfter(1800);
    }
    public void OpenSettings()
    {
        var window = new SettingsWindow(_configuration, _ollama);
        if (IsVisible) window.Owner = this;
        window.ShowDialog(); ReapplyTopmost();
    }
    public void OpenGpuDino()
    {
        if (IsSleeping) { ShowSpeech("Dino schläft gerade. Wecke ihn zuerst auf."); return; }
        if (!EnsureDinoAvailableForAction()) return;
        if (_chatWindow is not null) { _chatWindow.Activate(); return; }
        _chatWindow = new GpuDinoWindow(_configuration, _ollama);
        if (IsVisible) _chatWindow.Owner = this;
        _chatWindow.Closed += (_, _) => { _chatWindow = null; ReapplyTopmost(); };
        _chatWindow.Show();
    }
    public void Restart() { System.Diagnostics.Process.Start(Environment.ProcessPath!); _allowClose = true; App.Current.ExitApplication(); }
    public void PrepareForExit() => _allowClose = true;
    public void CloseDinoMenuForProfileSwitch()
    {
        _sleepClickMenuTimer.Stop();
        if (_dinoContextMenu is { } contextMenu) contextMenu.IsOpen = false;
        if (_interactionWindow is not { } interactionWindow) return;
        _interactionWindow = null;
        interactionWindow.Close();
    }

    private void ReapplyTopmost()
    {
        if (!_home.IsHome && IsVisible) WindowZOrderService.Apply(this, _configuration.Current.AlwaysOnTop);
    }

    private bool CanFollowMouse() => IsVisible && !_home.IsHome && !_hiddenForFullscreen && !_configuration.Current.QuietMode && !_isDesktopDigging
                                     && !IsSleepState(_states.Current) && _states.Current != DinoState.Doze
                                     && IdleTimeService.GetIdleTime() < TimeSpan.FromMinutes(2) && _walkTimer is null && !_isSmoothMoving;

    private bool CanSpawnDigSite() => IsVisible && !_home.IsHome && !_hiddenForFullscreen && !_isDesktopDigging
                                      && !IsSleepState(_states.Current) && _states.Current is not (DinoState.Doze or DinoState.Home)
                                      && !FullScreenDetector.IsForegroundFullScreen(this);

    private void TravelToDigSite(DigSiteWindow site)
    {
        if (!site.IsVisible) return;
        _isDesktopDigging = true;
        site.Topmost = false;
        WindowZOrderService.Apply(this, _configuration.Current.AlwaysOnTop);
        WindowZOrderService.BringToFront(this);
        _returnToIdleTimer.Stop();
        StopAnimatedWindowMovement();
        _walkTimer?.Stop();
        _walkTimer = null;
        _states.Set(DinoState.Walk);

        var dpi = VisualTreeHelper.GetDpi(this);
        var siteCenterPixels = new System.Drawing.Point(
            (int)((site.Left + site.Width / 2) * dpi.DpiScaleX),
            (int)((site.Top + site.Height / 2) * dpi.DpiScaleY));
        var workArea = System.Windows.Forms.Screen.FromPoint(siteCenterPixels).WorkingArea;
        var workLeft = workArea.Left / dpi.DpiScaleX;
        var workTop = workArea.Top / dpi.DpiScaleY;
        var workRight = workArea.Right / dpi.DpiScaleX;
        var workBottom = workArea.Bottom / dpi.DpiScaleY;
        var targetLeft = site.Left + (site.Width - Width) / 2;
        targetLeft = Math.Clamp(targetLeft, workLeft, Math.Max(workLeft, workRight - Width));
        var targetTop = Math.Clamp(site.Top - Height + 45, workTop, Math.Max(workTop, workBottom - Height));
        Dino.SetFacingLeft(targetLeft < Left);

        _isSmoothMoving = true;
        var distance = Math.Sqrt(Math.Pow(targetLeft - Left, 2) + Math.Pow(targetTop - Top, 2));
        var durationMs = Math.Max(500, (int)(distance / 200 * 1000));
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var leftAnimation = new DoubleAnimation(Left, targetLeft, duration) { FillBehavior = FillBehavior.Stop, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        var topAnimation = new DoubleAnimation(Top, targetTop, duration) { FillBehavior = FillBehavior.Stop, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        leftAnimation.Completed += async (_, _) =>
        {
            Left = targetLeft;
            Top = targetTop;
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            _isSmoothMoving = false;
            MonitorService.KeepVisible(this);
            if (!site.IsVisible) { _states.Set(DinoState.Idle); return; }
            WindowZOrderService.BringToFront(this);
            
            _states.Set(DinoState.Sniff);
            await Task.Delay(1500);
            if (!site.IsVisible || !_isDesktopDigging) return;

            site.BeginDigging();
            StartDesktopDiggingAnimation();
        };
        BeginAnimation(LeftProperty, leftAnimation);
        BeginAnimation(TopProperty, topAnimation);
    }

    private void StartDesktopDiggingAnimation()
    {
        _states.Set(DinoState.Dig);
        if (_configuration.Current.QuietMode) return;

        var animation = new DoubleAnimation
        {
            From = -4,
            To = 4,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        var transform = new RotateTransform();
        Dino.RenderTransform = transform;
        Dino.RenderTransformOrigin = new System.Windows.Point(0.5, 0.8);
        transform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private async void FinishDesktopDigging(bool completed, string rarity)
    {
        _isDesktopDigging = false;
        StopAnimatedWindowMovement();
        if (Dino.RenderTransform is RotateTransform transform)
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
        Dino.RenderTransform = Transform.Identity;

        if (_home.IsHome || IsSleepState(_states.Current)) return;
        
        if (completed)
        {
            _states.Set(DinoState.Inspect);
            await Task.Delay(1500);
            if (_home.IsHome || IsSleepState(_states.Current)) return;
            
            rarity = rarity.ToLowerInvariant();
            if (rarity == "episch" || rarity == "legendär")
            {
                _states.Set(DinoState.BigHappy);
                ShowSpeech("Wow! Schau mal!");
            }
            else if (rarity == "ungewöhnlich" || rarity == "selten")
            {
                _states.Set(DinoState.Happy);
                ShowSpeech("Oh! Das ist selten!");
            }
            else if (rarity != "")
            {
                _states.Set(DinoState.SmallHappy);
                ShowSpeech("Ich hab was gefunden!");
            }
            else
            {
                _states.Set(DinoState.SmallHappy);
            }
            
            if (Random.Shared.Next(100) < 30)
            {
                _waitingForDigInteraction = true;
                DispatcherTimer t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                t.Tick += delegate { _waitingForDigInteraction = false; t.Stop(); };
                t.Start();
            }

            ReturnToIdleAfter(1800);
        }
        else
        {
            _states.Set(DinoState.Idle);
        }
    }

    private System.Drawing.Point GetCompanionCenter()
    {
        var screenPoint = PointToScreen(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2));
        return new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y);
    }

    private void FollowMouseStep(MouseFollowStepEventArgs args)
    {
        if (!CanFollowMouse()) return;
        if (Math.Abs(args.DeltaX) > 1) Dino.SetFacingLeft(args.DeltaX < 0);
        var dpi = VisualTreeHelper.GetDpi(this);
        var targetLeft = Left + args.DeltaX / dpi.DpiScaleX;
        var targetTop = Top + args.DeltaY / dpi.DpiScaleY;
        _isSmoothMoving = true;
        _states.Set(DinoState.Walk);
        var duration = TimeSpan.FromMilliseconds(480 / Math.Clamp(_configuration.Current.AnimationSpeed, .5, 2));
        var leftAnimation = new DoubleAnimation(Left, targetLeft, duration) { FillBehavior = FillBehavior.Stop, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        var topAnimation = new DoubleAnimation(Top, targetTop, duration) { FillBehavior = FillBehavior.Stop, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
        leftAnimation.Completed += (_, _) =>
        {
            Left = targetLeft; Top = targetTop; BeginAnimation(LeftProperty, null); BeginAnimation(TopProperty, null);
            _isSmoothMoving = false;
            MonitorService.KeepVisible(this); MonitorService.SavePosition(this, _configuration.Current); _configuration.Save();
        };
        BeginAnimation(LeftProperty, leftAnimation); BeginAnimation(TopProperty, topAnimation);
        ReturnToIdleAfter(680);
    }

    private void StopAnimatedWindowMovement()
    {
        if (!_isSmoothMoving) return;
        var currentLeft = Left; var currentTop = Top;
        BeginAnimation(LeftProperty, null); BeginAnimation(TopProperty, null);
        Left = currentLeft; Top = currentTop; _isSmoothMoving = false;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmNcHitTest = 0x0084;
        const int HtTransparent = -1;
        if (msg != WmNcHitTest) return IntPtr.Zero;
        var packed = lParam.ToInt64();
        var point = PointFromScreen(new System.Windows.Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
        var dinoHit = point.X >= Dino.TranslatePoint(new System.Windows.Point(0, 0), this).X && point.X <= Dino.TranslatePoint(new System.Windows.Point(Dino.ActualWidth, 0), this).X
                      && point.Y >= Dino.TranslatePoint(new System.Windows.Point(0, 0), this).Y && point.Y <= Dino.TranslatePoint(new System.Windows.Point(0, Dino.ActualHeight), this).Y;
        var bubbleOrigin = SpeechBubble.TranslatePoint(new System.Windows.Point(0, 0), this);
        var bubbleHit = SpeechBubble.IsVisible && point.X >= bubbleOrigin.X && point.X <= bubbleOrigin.X + SpeechBubble.ActualWidth
                        && point.Y >= bubbleOrigin.Y && point.Y <= bubbleOrigin.Y + SpeechBubble.ActualHeight;
        if (!dinoHit && !bubbleHit) { handled = true; return new IntPtr(HtTransparent); }
        return IntPtr.Zero;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; SendHome(); return; }
        _behaviour.Stop(); _fullScreenTimer.Stop(); _apRegenTimer.Stop(); _spritePlayer.Stop(); _mouseFollow.Dispose(); _digSites.Dispose(); base.OnClosing(e);
    }

    private void CleanupEvent()
    {
        _waitingForHighFive = false;
        _waitingForAttention = false;
        _waitingForDigInteraction = false;
        SpeechBubble.RenderTransform = Transform.Identity;
        if (!_isDesktopDigging && !IsSleepState(_states.Current) && _states.Current != DinoState.Home)
        {
            _states.Set(DinoState.Idle);
        }
    }

    private void StartHighFive()
    {
        if (_states.Current != DinoState.Idle || IsPerformingAction || IsSleeping) return;
        _states.Set(DinoState.Sit);
        ShowSpeech("Pfote!", 3500);
        _waitingForHighFive = true;

        var anim = new DoubleAnimation(0.9, 1.1, TimeSpan.FromMilliseconds(400)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        var st = new ScaleTransform(1, 1);
        SpeechBubble.RenderTransform = st;
        SpeechBubble.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        t.Tick += delegate { 
            t.Stop(); 
            if (_waitingForHighFive) CleanupEvent(); 
        };
        t.Start();
    }

    private void StartAttention()
    {
        if (_states.Current != DinoState.Idle || IsPerformingAction || IsSleeping) return;
        _states.Set(DinoState.LookAround);
        ShowSpeech("Hey!", 3500);
        _waitingForAttention = true;

        var anim = new DoubleAnimation(-5, 5, TimeSpan.FromMilliseconds(100)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
        var rt = new RotateTransform(0);
        SpeechBubble.RenderTransform = rt;
        SpeechBubble.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        rt.BeginAnimation(RotateTransform.AngleProperty, anim);

        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        t.Tick += delegate { 
            t.Stop(); 
            if (_waitingForAttention) CleanupEvent(); 
        };
        t.Start();
    }

    private void StartMouseGame()
    {
        if (_states.Current != DinoState.Idle || IsPerformingAction || IsSleeping) return;
        _states.Set(DinoState.Curious);
        ShowSpeech("Fang mich!", 2000);
        int markersToClick = 3;
        bool completed = false;
        SpawnMarkers("🐁", 3, () => 
        {
            if (completed) return;
            markersToClick--;
            if (markersToClick <= 0)
            {
                completed = true;
                _states.Set(DinoState.Happy);
                _rewardService.TryRewardAP("MouseGame");
                ReturnToIdleAfter(1500);
            }
        });
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.5) };
        t.Tick += delegate { t.Stop(); if (!completed && _states.Current == DinoState.Curious) CleanupEvent(); };
        t.Start();
    }

    private void StartPawTrail()
    {
        if (_states.Current != DinoState.Idle || IsPerformingAction || IsSleeping) return;
        _states.Set(DinoState.Sniff);
        ShowSpeech("Komm mit!", 2000);
        int markersToClick = 3;
        bool completed = false;
        SpawnMarkers("🐾", 3, () => 
        {
            if (completed) return;
            markersToClick--;
            if (markersToClick <= 0)
            {
                completed = true;
                _states.Set(DinoState.BigHappy);
                _rewardService.TryRewardAP("PawTrail");
                ReturnToIdleAfter(1500);
            }
        });
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5.5) };
        t.Tick += delegate { t.Stop(); if (!completed && _states.Current == DinoState.Sniff) CleanupEvent(); };
        t.Start();
    }

    private Action? _lastRandomEvent;

    private void StartRandomAreaEvent()
    {
        var availableEvents = new List<Action>
        {
            StartAttention,
            StartMouseGame,
            StartPawTrail,
            SpawnSingleLeaf,
            StartShootingStarEvent
        };

        if (_lastRandomEvent != null && availableEvents.Count > 1)
        {
            availableEvents.Remove(_lastRandomEvent);
        }

        if (availableEvents.Count > 0)
        {
            var nextEvent = availableEvents[Random.Shared.Next(availableEvents.Count)];
            _lastRandomEvent = nextEvent;
            nextEvent();
        }
    }

    private void SpawnMarkers(string text, int count, Action onClick)
    {
        var center = GetCompanionCenter();
        var dpi = VisualTreeHelper.GetDpi(this);
        for (int i = 0; i < count; i++)
        {
            var marker = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Topmost = true,
                Width = 40,
                Height = 40,
                ShowInTaskbar = false
            };
            
            var content = new System.Windows.Controls.TextBlock
            {
                Text = text,
                FontSize = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            marker.Content = content;

            var r = Random.Shared;
            var offsetX = r.Next(-200, 200);
            var offsetY = r.Next(-150, 150);
            
            marker.Left = (center.X + offsetX) / dpi.DpiScaleX;
            marker.Top = (center.Y + offsetY) / dpi.DpiScaleY;

            marker.MouseLeftButtonDown += (s, e) =>
            {
                marker.Close();
                onClick();
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) => { marker.Close(); timer.Stop(); };
            timer.Start();

            marker.Show();
        }
    }

    private readonly List<Window> _activeLeaves = new();

    private void LeavesTimer_Tick(object? sender, EventArgs e)
    {
        _leavesTimer.Stop();
        var app = (App)System.Windows.Application.Current;
        bool hasActiveArea = !string.IsNullOrEmpty(app.Areas.Current.SelectedAreaId) && app.Areas.Current.SelectedAreaId != "none";

        if (!hasActiveArea || _home.IsHome || IsSleeping)
        {
            foreach (var leaf in _activeLeaves.ToList()) { leaf.Close(); }
            _activeLeaves.Clear();
        }
        else if (_activeLeaves.Count < 5) 
        {
            SpawnSingleLeaf();
        }
        ScheduleNextLeavesTimer();
    }

    private void ScheduleNextLeavesTimer()
    {
        _leavesTimer.Stop();
        // Erstes Gebietsobjekt schon nach 5s, dann alle 8–20s ein neues
        _leavesTimer.Interval = _activeLeaves.Count == 0
            ? TimeSpan.FromSeconds(5)
            : TimeSpan.FromSeconds(Random.Shared.Next(8, 20));
        _leavesTimer.Start();
    }

    private void ShootingStarTimer_Tick(object? sender, EventArgs e)
    {
        _shootingStarTimer.Stop();
        var app = (App)System.Windows.Application.Current;
        bool hasActiveArea = !string.IsNullOrEmpty(app.Areas.Current.SelectedAreaId) && app.Areas.Current.SelectedAreaId != "none";

        if (hasActiveArea && !IsSleeping && !_home.IsHome) 
        {
            StartShootingStarEvent();
        }
        ScheduleNextShootingStarTimer();
    }

    private void ScheduleNextShootingStarTimer()
    {
        _shootingStarTimer.Stop();
        _shootingStarTimer.Interval = TimeSpan.FromSeconds(Random.Shared.Next(60, 180));
        _shootingStarTimer.Start();
    }

    private sealed record AreaCollectibleVisual(System.Windows.Media.ImageSource Image, string RewardSource, string Emoji);

    private static AreaCollectibleVisual MakeAreaCollectibleVisual(string? areaId)
    {
        var dg = new System.Windows.Media.DrawingGroup();
        using (var dc = dg.Open())
        {
            switch (areaId?.ToLowerInvariant())
            {
                case "garten":
                {
                    var petal = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 113, 176));
                    var petalOutline = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(176, 58, 123)), 1.2);
                    var center = new System.Windows.Point(24, 23);
                    dc.DrawLine(new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 145, 70)), 2.5), center, new System.Windows.Point(17, 45));
                    foreach (var offset in new[] { new Vector(0, -11), new Vector(10, -3), new Vector(6, 9), new Vector(-6, 9), new Vector(-10, -3) })
                        dc.DrawEllipse(petal, petalOutline, center + offset, 7, 9);
                    dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 205, 66)), null, center, 6, 6);
                    break;
                }
                case "strand":
                {
                    var shell = System.Windows.Media.Geometry.Parse("M 7,39 Q 24,4 41,39 Q 24,47 7,39 Z");
                    var outline = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(164, 99, 63)), 1.5);
                    dc.DrawGeometry(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 202, 151)), outline, shell);
                    foreach (var x in new[] { 14d, 19d, 24d, 29d, 34d })
                        dc.DrawLine(new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(222, 139, 100)), 1.2), new System.Windows.Point(24, 10), new System.Windows.Point(x, 40));
                    break;
                }
                case "schneeland":
                {
                    var icePen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(77, 190, 235)), 2.4);
                    var branchPen = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(155, 231, 255)), 1.6);
                    var center = new System.Windows.Point(24, 24);
                    foreach (var angle in new[] { 0d, 60d, 120d })
                    {
                        var radians = angle * Math.PI / 180d;
                        var dx = Math.Cos(radians) * 19;
                        var dy = Math.Sin(radians) * 19;
                        dc.DrawLine(icePen, new System.Windows.Point(center.X - dx, center.Y - dy), new System.Windows.Point(center.X + dx, center.Y + dy));
                    }
                    dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 250, 255)), branchPen, center, 5, 5);
                    dc.DrawLine(branchPen, new System.Windows.Point(8, 15), new System.Windows.Point(15, 15));
                    dc.DrawLine(branchPen, new System.Windows.Point(33, 33), new System.Windows.Point(40, 33));
                    break;
                }
                case "hoehle":
                {
                    var stem = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 218, 177));
                    var outline = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(91, 55, 88)), 1.5);
                    dc.DrawRoundedRectangle(stem, outline, new Rect(19, 23, 10, 23), 4, 4);
                    dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(159, 78, 177)), outline, new System.Windows.Point(24, 21), 18, 12);
                    dc.DrawEllipse(System.Windows.Media.Brushes.LavenderBlush, null, new System.Windows.Point(17, 18), 3, 2);
                    dc.DrawEllipse(System.Windows.Media.Brushes.LavenderBlush, null, new System.Windows.Point(29, 15), 2.5, 2.5);
                    dc.DrawEllipse(System.Windows.Media.Brushes.LavenderBlush, null, new System.Windows.Point(34, 23), 2, 2);
                    break;
                }
                default:
                {
                    var fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 161, 69));
                    var outline = new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 103, 42)), 1.5);
                    dc.DrawEllipse(fill, outline, new System.Windows.Point(24, 24), 18, 11);
                    dc.DrawLine(outline, new System.Windows.Point(8, 30), new System.Windows.Point(40, 18));
                    dc.DrawLine(new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 91, 35)), 2), new System.Windows.Point(40, 18), new System.Windows.Point(46, 12));
                    break;
                }
            }
        }
        dg.Freeze();
        var image = new System.Windows.Media.DrawingImage(dg);
        image.Freeze();
        return areaId?.ToLowerInvariant() switch
        {
            "garten" => new AreaCollectibleVisual(image, "Blüte", "🌸"),
            "strand" => new AreaCollectibleVisual(image, "Muschel", "🐚"),
            "schneeland" => new AreaCollectibleVisual(image, "Eiskristall", "❄️"),
            "hoehle" => new AreaCollectibleVisual(image, "Pilz", "🍄"),
            _ => new AreaCollectibleVisual(image, "Blatt", "🍃")
        };
    }

    private static System.Windows.Media.ImageSource MakeShootingStarImage()
    {
        var dg = new System.Windows.Media.DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 225, 102)), null, new System.Windows.Point(5, 43), 1.5, 1.5);
            dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(130, 255, 225, 102)), null, new System.Windows.Point(11, 38), 2, 2);
            dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(170, 255, 225, 102)), null, new System.Windows.Point(17, 33), 2.5, 2.5);
            dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 225, 102)), null, new System.Windows.Point(23, 27), 3, 3);

            var geometry = new System.Windows.Media.StreamGeometry();
            using (var context = geometry.Open())
            {
                var points = Enumerable.Range(0, 10).Select(index =>
                {
                    var radius = index % 2 == 0 ? 15d : 6.5d;
                    var angle = -Math.PI / 2 + index * Math.PI / 5;
                    return new System.Windows.Point(34 + Math.Cos(angle) * radius, 16 + Math.Sin(angle) * radius);
                }).ToArray();
                context.BeginFigure(points[0], true, true);
                context.PolyLineTo(points.Skip(1).ToArray(), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 221, 74)), new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(213, 135, 22)), 1.5), geometry);
            dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new System.Windows.Point(31, 11), 2.2, 2.2);
        }
        dg.Freeze();
        var image = new System.Windows.Media.DrawingImage(dg);
        image.Freeze();
        return image;
    }

    private void SpawnSingleLeaf()
    {
        if (IsSleeping) return;
        var workArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var r = Random.Shared;
        var areaId = ((App)System.Windows.Application.Current).Areas.Current.SelectedAreaId;
        var collectible = MakeAreaCollectibleVisual(areaId);

        var marker = new Window
        {
            AllowsTransparency = true,
            WindowStyle = WindowStyle.None, Background = System.Windows.Media.Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
            Width = 56, Height = 56,
            Tag = collectible
        };
        var img = new System.Windows.Controls.Image
        {
            Source = collectible.Image, Width = 50, Height = 50,
            Cursor = System.Windows.Input.Cursors.Hand,
            RenderTransform = new RotateTransform(r.Next(-40, 40), 25, 25)
        };
        marker.Content = img;
        marker.Left = r.Next(workArea.Left + 60, workArea.Right - 60) / dpi.DpiScaleX;
        marker.Top = r.Next(workArea.Top  + 60, workArea.Bottom - 60) / dpi.DpiScaleY;
        _activeLeaves.Add(marker);

        marker.MouseLeftButtonDown += (s, e) =>
        {
            if (!_activeLeaves.Contains(marker)) return;

            // AP-Prüfung: kein AP -> Dino muss schlafen
            if (_progress.Current.AdventurePoints <= 0)
            {
                ShowSpeech("Zu müde... 💤");
                SetSleepState();
                return;
            }

            _activeLeaves.Remove(marker);
            marker.Close();

            // Zufällig: 50% Chance kostet 1 AP
            if (r.Next(2) == 0)
            {
                _progress.SpendAdventurePoints(1);
                ShowSpeech("-1 AP");
            }

            ApplyAreaCollectibleReward(collectible, isAuto: false);
        };

        // Gebietsobjekt verschwindet nach 25–40s von selbst
        var lifetime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(r.Next(25, 40)) };
        lifetime.Tick += (_, _) =>
        {
            lifetime.Stop();
            if (!_activeLeaves.Contains(marker)) return;
            _activeLeaves.Remove(marker);
            try { marker.Close(); } catch { }
        };
        lifetime.Start();
        marker.Show();
        // KEIN State-Set hier – damit Grabung weiterhin erscheinen kann
    }

    private void TryDinoAutoLeafCollect()
    {
        if (IsSleeping || _isDesktopDigging || _isSmoothMoving || _states.Current != DinoState.Idle) return;
        if (_activeLeaves.Count == 0) return;

        var bonuses = ((App)System.Windows.Application.Current).Collections.HomeBonuses.Current;
        if (!bonuses.AutoLeafCollect) return;

        if (Random.Shared.NextDouble() > 0.35) return;

        var marker = _activeLeaves.FirstOrDefault();
        if (marker == null) return;

        if (_progress.Current.AdventurePoints <= 0) return;

        _isSmoothMoving = true;
        var tLeft = marker.Left - Width / 2;
        var tTop = marker.Top - Height / 2;
        var sLeft = Left;
        var sTop = Top;

        Dino.SetFacingLeft(tLeft < sLeft);
        _states.Set(DinoState.Walk);

        var steps = 0;
        const int totalSteps = 40;
        var walk = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        walk.Tick += (_, _) =>
        {
            steps++;
            if (!_activeLeaves.Contains(marker) || IsSleeping || _isDesktopDigging)
            {
                walk.Stop();
                _isSmoothMoving = false;
                if (!IsSleeping && !_isDesktopDigging) _states.Set(DinoState.Idle);
                return;
            }

            var p = Math.Min(1.0, (double)steps / totalSteps);
            Left = sLeft + (tLeft - sLeft) * p;
            Top = sTop + (tTop - sTop) * p;

            if (steps >= totalSteps)
            {
                walk.Stop();
                _isSmoothMoving = false;
                if (!_activeLeaves.Contains(marker))
                {
                    _states.Set(DinoState.Idle);
                    return;
                }

                _activeLeaves.Remove(marker);
                try { marker.Close(); } catch { }

                if (Random.Shared.Next(2) == 0 && _progress.Current.AdventurePoints > 0)
                {
                    _progress.SpendAdventurePoints(1);
                }

                _states.Set(DinoState.Sniff);
                var collectible = marker.Tag as AreaCollectibleVisual
                    ?? MakeAreaCollectibleVisual(((App)System.Windows.Application.Current).Areas.Current.SelectedAreaId);
                ApplyAreaCollectibleReward(collectible, isAuto: true);
                ReturnToIdleAfter(900);
            }
        };
        walk.Start();
    }

    private void ApplyAreaCollectibleReward(AreaCollectibleVisual collectible, bool isAuto)
    {
        var r = Random.Shared;
        var bonuses = ((App)System.Windows.Application.Current).Collections.HomeBonuses.Current;
        var leafBonus = bonuses.LeafRewardBonus;

        var giveCoin = r.Next(2) == 0 || (leafBonus > 0 && r.NextDouble() < (leafBonus / 100.0));
        if (giveCoin)
        {
            var coinAmount = 1;
            if (leafBonus >= 15 && r.Next(3) == 0) coinAmount++;
            _progress.AddCoins(coinAmount, collectible.RewardSource);
            ShowSpeech(coinAmount > 1 ? $"+{coinAmount} Coins! {collectible.Emoji}" : $"+1 Coin {collectible.Emoji}");
        }
        else
        {
            var xpAmount = 5;
            if (leafBonus > 0) xpAmount += (int)Math.Max(1, Math.Round(leafBonus / 10.0));
            _progress.AddXP(xpAmount, collectible.RewardSource);
            ShowSpeech($"+{xpAmount} XP {collectible.Emoji}");
        }
    }

    private void StartShootingStarEvent()
    {
        if (IsSleeping) return;
        var workArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var r = Random.Shared;
        var requiredClicks = new[] { 3, 5, 8, 12 }[r.Next(4)];
        int clicks = 0;
        bool done = false;

        var shootingStar = new Window
        {
            AllowsTransparency = true,
            WindowStyle = WindowStyle.None, Background = System.Windows.Media.Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
            Width = 72, Height = 72,
        };
        var img = new System.Windows.Controls.Image
        {
            Source = MakeShootingStarImage(), Width = 56, Height = 56,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var counter = new System.Windows.Controls.TextBlock
        {
            Text = $"0/{requiredClicks}",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        var counterBadge = new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 45, 55, 78)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 222, 92)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            MinWidth = 34,
            Height = 20,
            Padding = new Thickness(5, 0, 5, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
            Child = counter,
            IsHitTestVisible = false
        };
        var starContent = new System.Windows.Controls.Grid();
        starContent.Children.Add(img);
        starContent.Children.Add(counterBadge);
        shootingStar.Content = starContent;
        shootingStar.Left = r.Next(workArea.Left + 80, workArea.Right - 80) / dpi.DpiScaleX;
        shootingStar.Top  = r.Next(workArea.Top  + 80, workArea.Bottom - 80) / dpi.DpiScaleY;

        double targetLeft = shootingStar.Left, targetTop = shootingStar.Top;
        void PickTarget()
        {
            targetLeft = r.Next(workArea.Left + 80, workArea.Right - 80) / dpi.DpiScaleX;
            targetTop  = r.Next(workArea.Top  + 80, workArea.Bottom - 80) / dpi.DpiScaleY;
        }
        PickTarget();

        var crawl = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        crawl.Tick += (_, _) =>
        {
            var dx = targetLeft - shootingStar.Left;
            var dy = targetTop  - shootingStar.Top;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 6) { PickTarget(); return; }
            shootingStar.Left += dx / d * 7.2;
            shootingStar.Top  += dy / d * 7.2;
            img.RenderTransform = dx < 0
                ? new ScaleTransform(-1, 1, 25, 25)
                : Transform.Identity;
        };

        var retarget = new DispatcherTimer { Interval = TimeSpan.FromSeconds(r.Next(2, 5)) };
        retarget.Tick += (_, _) => { retarget.Interval = TimeSpan.FromSeconds(r.Next(2, 5)); PickTarget(); };

        DispatcherTimer? timeout = null;
        void CloseShootingStar()
        {
            crawl.Stop(); retarget.Stop(); timeout?.Stop();
            if (!done) { done = true; try { shootingStar.Close(); } catch { } if (!_isDesktopDigging) ReturnToIdleAfter(600); }
        }

        img.MouseLeftButtonDown += (s, e) =>
        {
            if (done) return;
            clicks++;
            counter.Text = $"{clicks}/{requiredClicks}";
            if (!_isDesktopDigging) _states.Set(DinoState.Curious);
            if (clicks < requiredClicks)
            {
                PickTarget();
                if (!_isDesktopDigging) ReturnToIdleAfter(600);
                return;
            }

            var shootingStarBonus = ((App)System.Windows.Application.Current).Collections.HomeBonuses.Current.BugRewardBonus;
            var baseCoins = requiredClicks switch { 3 => 1, 5 => 2, 8 => 3, _ => 5 };
            var baseXp = requiredClicks switch { 3 => 5, 5 => 10, 8 => 18, _ => 30 };
            var rewardMultiplier = 1d + shootingStarBonus / 100d;
            var coinReward = Math.Max(1, (int)Math.Round(baseCoins * rewardMultiplier));
            var xpReward = Math.Max(1, (int)Math.Round(baseXp * rewardMultiplier));
            _progress.AddCoins(coinReward, "Sternschnuppe");
            _progress.AddXP(xpReward, "Sternschnuppe");

            var guaranteedAp = shootingStarBonus switch { >= 35 => 3, >= 25 => 2, >= 15 => 1, _ => 0 };
            var randomAp = guaranteedAp == 0 && r.NextDouble() < requiredClicks switch { 3 => 0.05, 5 => 0.10, 8 => 0.20, _ => 0.35 } ? 1 : 0;
            var requestedAp = guaranteedAp + randomAp;
            var previousAp = _progress.Current.AdventurePoints;
            if (requestedAp > 0) _progress.AddInstantAP(requestedAp);
            var apReward = _progress.Current.AdventurePoints - previousAp;
            ShowSpeech($"+{coinReward} Coins · +{xpReward} XP{(apReward > 0 ? $" · +{apReward} AP" : "")} 🌠");

            if (!_isDesktopDigging) { _states.Set(DinoState.Happy); ReturnToIdleAfter(1500); }
            CloseShootingStar();
        };

        timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(18 + requiredClicks * 3) };
        timeout.Tick += delegate { timeout.Stop(); CloseShootingStar(); };
        timeout.Start();

        crawl.Start();
        retarget.Start();
        shootingStar.Show();
        _states.Set(DinoState.Curious);
        ReturnToIdleAfter(1200);
    }
}
