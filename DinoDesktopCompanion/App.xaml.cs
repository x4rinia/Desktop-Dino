using System.Windows;
using DinoDesktopCompanion.Configuration;
using DinoDesktopCompanion.GPU;
using DinoDesktopCompanion.Collections;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Tasks;
using DinoDesktopCompanion.Statistics;
using DinoDesktopCompanion.Achievements;
using DinoDesktopCompanion.Expeditions;
using DinoDesktopCompanion.Core;
using DinoDesktopCompanion.UI.Tutorial;

namespace DinoDesktopCompanion;

public partial class App : System.Windows.Application
{
    public static new App Current => (App)System.Windows.Application.Current;
    public ConfigurationService Configuration { get; private set; } = null!;
    public FileLogger Logger { get; private set; } = null!;
    public OllamaClient Ollama { get; private set; } = null!;
    public ProgressService Progress { get; private set; } = null!;
    public CollectionManager Collections { get; private set; } = null!;
    public DinoTaskService Tasks { get; private set; } = null!;
    public StatisticsService Statistics { get; private set; } = null!;
    public AchievementService Achievements { get; private set; } = null!;
    public ExpeditionService Expeditions { get; private set; } = null!;
    public AreaService Areas { get; private set; } = null!;
    public DinoDesktopCompanion.Profiles.ProfileManager Profiles { get; private set; } = null!;
    public MainWindow DinoWindow { get; private set; } = null!;
    private TrayIconService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TaskbarIntegrationService.Initialize();
        Logger = new FileLogger();
        Configuration = new ConfigurationService(Logger);
        Configuration.Load();
        Ollama = new OllamaClient(Logger);
        Profiles = new DinoDesktopCompanion.Profiles.ProfileManager(Logger);
        if (Profiles.ActiveProfile is null)
        {
            var firstProfile = new DinoDesktopCompanion.UI.Profiles.ProfileWindow(Profiles, true);
            firstProfile.ShowDialog();
            if (Profiles.ActiveProfile is null) { Shutdown(); return; }
        }
        Profiles.ActiveProfileChanged += (_, _) => Dispatcher.BeginInvoke(new Action(SwitchProfileSafely));
        
        SwitchProfile(true);
        _tray = new TrayIconService(DinoWindow, Configuration, Ollama, ExitApplication, OpenProfileWindow);
        DinoWindow.Show();
        Logger.Info("Dino Desktop Companion gestartet.");
        if (!Configuration.Current.HasSeenTutorial)
        {
            Configuration.Current.HasSeenTutorial = true;
            Configuration.Save();
            OpenTutorialWindow();
        }
    }
    
    public void OpenProfileWindow()
    {
        var pw = new DinoDesktopCompanion.UI.Profiles.ProfileWindow(Profiles);
        if (DinoWindow?.IsVisible == true) pw.Owner = DinoWindow;
        pw.ShowDialog();
    }

    public void OpenTutorialWindow()
    {
        var tw = new TutorialWindow();
        if (DinoWindow?.IsVisible == true) tw.Owner = DinoWindow;
        tw.Show();
    }
    
    private void SwitchProfile(bool isStartup = false)
    {
        var dataDir = Profiles.GetActiveProfileDirectory();
        
        Areas = new AreaService(Logger, dataDir);
        Collections = new CollectionManager(Logger, dataDir);
        var activeProfile = Profiles.ActiveProfile;
        var wasSleeping = activeProfile?.State == DinoDesktopCompanion.Profiles.DinoState.Sleeping;
        DateTimeOffset? offlineSleepStartedAt = activeProfile?.SleepStartTime is DateTime sleepStart
            ? new DateTimeOffset(sleepStart) : null;
        Progress = new ProgressService(Logger, dataDir, () => Collections.HomeBonuses.Current,
            wasSleeping, offlineSleepStartedAt);
        Collections.CollectionChanged += (_, _) => Progress.RefreshAdventurePointCapacity();
        Tasks = new DinoTaskService(Progress, Logger, dataDir);
        Statistics = new StatisticsService(Logger, dataDir);
        Achievements = new AchievementService(Logger, Progress, Statistics, Collections, dataDir);
        Expeditions = new ExpeditionService(Logger, Progress, Statistics, dataDir);
        void RefreshSkinUnlocks() => Collections.Cosmetics.EvaluateUnlocks(Progress, Statistics, Areas);
        Progress.LevelUp += (_, _) => RefreshSkinUnlocks();
        Statistics.StatisticsChanged += (_, _) => RefreshSkinUnlocks();
        Collections.CollectionChanged += (_, _) => RefreshSkinUnlocks();
        RefreshSkinUnlocks();
        Achievements.AchievementUnlocked += (_, ach) => Dispatcher.Invoke(() =>
        {
            DinoDesktopCompanion.UI.Toast.ToastNotificationWindow.ShowToast("Erfolg freigeschaltet!", ach.Title, "🌟");
        });
        
        Progress.LevelUp += (_, args) => Dispatcher.Invoke(() =>
        {
            DinoDesktopCompanion.UI.Toast.ToastNotificationWindow.ShowToast("Level Up!", $"Du hast Level {args.NewLevel} erreicht!", "🎉");
            DinoWindow?.CelebrateLevelUp(args.NewLevel);
        });

        if (!isStartup && DinoWindow != null)
        {
            var oldWindow = DinoWindow;
            DinoWindow = new MainWindow(Configuration, Logger, Ollama, Progress, Statistics);
            MainWindow = DinoWindow;
            DinoWindow.Show();
            if (_tray != null) _tray.UpdateWindow(DinoWindow);
            oldWindow.PrepareForExit();
            oldWindow.Close();
        }
        else
        {
            DinoWindow = new MainWindow(Configuration, Logger, Ollama, Progress, Statistics);
            MainWindow = DinoWindow;
        }
    }

    private void SwitchProfileSafely()
    {
        var previousDinoWindow = DinoWindow;
        var openProfileWindows = Windows.OfType<DinoDesktopCompanion.UI.Profiles.ProfileWindow>()
            .Where(window => window.IsVisible)
            .ToArray();
        var previousAreas = Areas;
        var previousCollections = Collections;
        var previousProgress = Progress;
        var previousTasks = Tasks;
        var previousStatistics = Statistics;
        var previousAchievements = Achievements;
        var previousExpeditions = Expeditions;
        try
        {
            // Profilfenster vom alten Dino/Menu lösen, damit es beim Neuladen offen bleibt.
            foreach (var profileWindow in openProfileWindows) profileWindow.Owner = null;
            previousDinoWindow.CloseDinoMenuForProfileSwitch();
            SwitchProfile();
        }
        catch (Exception ex)
        {
            Areas = previousAreas;
            Collections = previousCollections;
            Progress = previousProgress;
            Tasks = previousTasks;
            Statistics = previousStatistics;
            Achievements = previousAchievements;
            Expeditions = previousExpeditions;
            foreach (var profileWindow in openProfileWindows.Where(window => window.IsVisible))
                profileWindow.Owner = previousDinoWindow;
            Logger.Error("Profilwechsel fehlgeschlagen.", ex);
            System.Windows.MessageBox.Show("Das Profil konnte nicht vollständig geladen werden. Die App bleibt geöffnet.", "Profilwechsel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var profileWindow in openProfileWindows.Where(window => window.IsVisible))
        {
            try { profileWindow.Owner = DinoWindow; }
            catch (InvalidOperationException ex) { Logger.Error("Profilfenster konnte nicht an den neuen Dino gebunden werden.", ex); }
        }
    }

    public void ExitApplication()
    {
        Configuration.Save();
        _tray?.Dispose();
        Logger.Info("Dino Desktop Companion beendet.");
        DinoWindow?.PrepareForExit();
        Shutdown();
    }
}
