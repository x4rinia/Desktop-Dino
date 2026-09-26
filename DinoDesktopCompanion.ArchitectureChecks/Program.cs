using DinoDesktopCompanion.Dino.States;
using DinoDesktopCompanion.Profiles;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Statistics;
using DinoDesktopCompanion.Core;
using DinoDesktopCompanion.Collections;
using DinoDesktopCompanion.Games;
using DinoDesktopCompanion.Tasks;
using DinoState = DinoDesktopCompanion.Dino.States.DinoState;

var testParent = Path.Combine(Path.GetTempPath(), "DinoDesktopCompanionArchitectureChecks");
var testRoot = Path.Combine(testParent, Guid.NewGuid().ToString("N"));
var dataDirectory = Path.Combine(testRoot, "Data");
var definitions = Path.Combine(AppContext.BaseDirectory, "GameData");

try
{
    Directory.CreateDirectory(testRoot);
    var logger = new FileLogger(testRoot);
    var profiles = new ProfileManager(logger, dataDirectory);
    Check(profiles.Profiles.Count == 0 && profiles.ActiveProfile is null, "Erster Start ohne Phantomprofil");

    foreach (var sleepState in new[] { DinoState.Sleep, DinoState.SleepLeft, DinoState.SleepRight })
    {
        var states = new DinoStateMachine();
        states.Set(sleepState);
        states.Set(DinoState.Idle);
        states.Set(DinoState.Walk);
        states.Set(DinoState.Happy);
        states.Set(DinoState.Wake);
        Check(states.Current == sleepState,
            $"Schlafsperre blockiert Idle-, Bewegungs-, Animations- und generische Wake-Übergänge ({sleepState})");
        states.WakeUp();
        Check(states.Current == DinoState.Wake, $"Nur explizites WakeUp beendet den Schlaf ({sleepState})");
    }

    var luna = profiles.CreateProfile("Lunas Welt", "Luna", 12, 9);
    Check(luna.Id.Length == 32 && profiles.SetActiveProfile(luna.Id), "Profil mit eindeutiger ID, Dino-Name und Geburtstag");
    var lunaDirectory = profiles.GetActiveProfileDirectory();
    var lunaProgress = new ProgressService(logger, lunaDirectory);
    var lunaCollections = new CollectionManager(logger, lunaDirectory, definitions);
    var lunaStatistics = new StatisticsService(logger, lunaDirectory);
    var lunaAreas = new AreaService(logger, lunaDirectory);
    var house = lunaCollections.Home.Houses.Single(layout => layout.Id == "default");
    Check(File.Exists(Path.Combine(AppContext.BaseDirectory, house.BackgroundAssetPath)), "Dinohaus-Hintergrund wird ausgeliefert");
    Check(house.Slots.Count >= 10 && house.Slots.All(slot => slot.Width > 0 && slot.Height > 0), "Bestehende Dinohaus-Slots mit festen Darstellungsgrößen");
    var levelUps = 0;
    lunaProgress.LevelUp += (_, _) => levelUps++;
    lunaProgress.AddXP(260, "ArchitectureCheck");
    lunaProgress.AddCoins(8, "ArchitectureCheck");
    lunaCollections.Cosmetics.UnlockSkin("blue");
    lunaCollections.Cosmetics.EquipSkin("blue");
    Check(lunaProgress.Current.Level == 3 && lunaProgress.Current.CurrentXP == 10 && levelUps == 2, "XP-Überlauf und Level-Up Events");
    Check(RockPaperScissorsGame.DetermineResult(RockPaperScissorsChoice.Rock, RockPaperScissorsChoice.Scissors) == RockPaperScissorsResult.Win
          && RockPaperScissorsGame.DetermineResult(RockPaperScissorsChoice.Scissors, RockPaperScissorsChoice.Rock) == RockPaperScissorsResult.Loss
          && RockPaperScissorsGame.DetermineResult(RockPaperScissorsChoice.Paper, RockPaperScissorsChoice.Paper) == RockPaperScissorsResult.Draw,
        "Stein-Schere-Papier-Gewinnerlogik");
    lunaStatistics.TrackRockPaperScissors(RockPaperScissorsResult.Win);
    lunaStatistics.TrackRockPaperScissors(RockPaperScissorsResult.Draw);
    lunaStatistics.TrackDigSiteCompleted();
    Check(lunaStatistics.Current.RockPaperScissors.GamesPlayed == 2 && lunaStatistics.Current.RockPaperScissors.Wins == 1 && lunaStatistics.Current.RockPaperScissors.Draws == 1, "Stein-Schere-Papier-Statistik");
    Check(lunaStatistics.Current.DigSitesCompleted == 1, "Grabungsstatistik");
    Check(lunaCollections.Toys.Items.Count > 0 && lunaCollections.Toys.Unlock(lunaCollections.Toys.Items[0].Id) && !lunaCollections.Toys.Unlock(lunaCollections.Toys.Items[0].Id), "Sammelfunde ohne Duplikate");
    Check(lunaAreas.SelectArea("garten", lunaProgress.Current.Level) && !lunaAreas.SelectArea("wald", lunaProgress.Current.Level), "Gebietsauswahl mit Level-Freischaltung");

    var homeDirectory = Path.Combine(testRoot, "HomeUpgradeData");
    var homeCollections = new CollectionManager(logger, homeDirectory, definitions);
    var homeProgress = new ProgressService(logger, homeDirectory, () => homeCollections.HomeBonuses.Current);
    Check(homeCollections.Home.Items.Count == 8, "Datengetriebene Dinohaus-Objekte geladen");
    Check(homeCollections.Home.PurchaseItem("home-bed-basic", homeProgress) == HomePurchaseResult.LevelTooLow, "Hauskauf prüft Levelvoraussetzung");
    homeProgress.AddXP(6750, "HomeUpgradeCheck");
    homeProgress.AddCoins(3000, "HomeUpgradeCheck");
    Check(homeProgress.Current.Level == 16, "Dinohaus-Testlevel erreicht");

    foreach (var item in homeCollections.Home.Items)
        Check(homeCollections.Home.PurchaseItem(item.Id, homeProgress) == HomePurchaseResult.Success, $"Hausobjekt gekauft: {item.Id}");
    Check(homeProgress.Current.DinoCoins == 200, "Hauskäufe ziehen datengetriebene Coin-Preise genau einmal ab");
    Check(homeCollections.Home.PurchaseItem("home-bed-basic", homeProgress) == HomePurchaseResult.AlreadyPurchased
          && homeProgress.Current.DinoCoins == 200, "Gekauftes Hausobjekt kann nicht doppelt bezahlt werden");

    foreach (var item in homeCollections.Home.Items) Check(homeCollections.Home.EquipItem(item.Id), $"Hausobjekt eingerichtet: {item.Id}");
    var homeBonuses = homeCollections.HomeBonuses.Current;
    Check(Math.Abs(homeBonuses.SleepApRegenMultiplier - 1.20) < 0.0001
          && Math.Abs(homeBonuses.DiggingXpMultiplier - 1.05) < 0.0001
          && Math.Abs(homeBonuses.CollectibleChanceBonus - 0.05) < 0.0001
          && Math.Abs(homeBonuses.RarityChanceBonus - 0.03) < 0.0001
          && Math.Abs(homeBonuses.DigSiteLifetimeMultiplier - 1.20) < 0.0001
          && Math.Abs(homeBonuses.GameXpMultiplier - 1.05) < 0.0001
          && homeBonuses.MaxApBonus == 1,
        "Aktive Hausboni werden zentral und additiv berechnet");

    homeProgress.RefreshAdventurePointCapacity();
    Check(homeProgress.Current.MaxAdventurePoints == 13, "Level-MaxAP und Haus-MaxAP werden kombiniert");
    var awakeOfflineStart = DateTimeOffset.Now.AddMinutes(-60.1);
    homeProgress.Current.AdventurePoints = 5;
    homeProgress.Current.SleepStartedAt = null;
    homeProgress.Current.LastAdventurePointRegenAt = awakeOfflineStart;
    homeProgress.Current.AdventurePointRegenProgress = 0;
    homeProgress.RefreshAdventurePointCapacity();
    var awakeReload = new ProgressService(logger, homeDirectory, () => homeCollections.HomeBonuses.Current);
    Check(awakeReload.Current.AdventurePoints == 7, "Wache Offline-Regeneration: 1 AP je 30 Minuten");
    var noDoubleReload = new ProgressService(logger, homeDirectory, () => homeCollections.HomeBonuses.Current);
    Check(noDoubleReload.Current.AdventurePoints == 7, "Neustart erzeugt keine doppelte AP-Regeneration");

    var sleepOfflineStart = DateTimeOffset.Now.AddMinutes(-34.1);
    noDoubleReload.Current.AdventurePoints = 5;
    noDoubleReload.Current.SleepStartedAt = sleepOfflineStart;
    noDoubleReload.Current.LastAdventurePointRegenAt = sleepOfflineStart;
    noDoubleReload.Current.AdventurePointRegenProgress = 0;
    noDoubleReload.RefreshAdventurePointCapacity();
    var sleepReload = new ProgressService(logger, homeDirectory, () => homeCollections.HomeBonuses.Current);
    Check(sleepReload.Current.AdventurePoints == 7 && sleepReload.Current.SleepStartedAt.HasValue,
        "Schlaf-Offline-Regeneration nutzt 20 Minuten und aktive +20 Prozent, ohne Aufwecken");

    sleepReload.Current.AdventurePoints = 12;
    sleepReload.Current.SleepStartedAt = null;
    sleepReload.Current.LastAdventurePointRegenAt = DateTimeOffset.Now.AddHours(-3);
    sleepReload.Current.AdventurePointRegenProgress = 0;
    sleepReload.RefreshAdventurePointCapacity();
    var cappedReload = new ProgressService(logger, homeDirectory, () => homeCollections.HomeBonuses.Current);
    Check(cappedReload.Current.AdventurePoints == 13, "Offline-Regeneration überschreitet MaxAP nicht");

    var persistedHome = new CollectionManager(logger, homeDirectory, definitions);
    Check(persistedHome.Current.UnlockedHomeItems.Count == 8
          && persistedHome.Current.EquippedHomeItemsBySlot.Count == 8,
        "Gekaufte und eingerichtete Hausobjekte bleiben gespeichert");
    Check(Math.Abs(persistedHome.HomeBonuses.ApplyCollectibleChance(0.45) - 0.50) < 0.0001,
        "Sammelfundbonus wird auf die reale Fundchance angewendet");
    var gameXpAwarded = Enumerable.Range(0, 20)
        .Sum(_ => cappedReload.AddXP(3, "HomeGameXpCheck", persistedHome.HomeBonuses.Current.GameXpMultiplier));
    Check(gameXpAwarded == 63, "Kleine Spiel-XP-Boni werden ohne Rundungsverlust angesammelt");

    var poorHomeDirectory = Path.Combine(testRoot, "PoorHomeUpgradeData");
    var poorCollections = new CollectionManager(logger, poorHomeDirectory, definitions);
    var poorProgress = new ProgressService(logger, poorHomeDirectory, () => poorCollections.HomeBonuses.Current);
    poorProgress.AddXP(6750, "HomePurchaseFundsCheck");
    Check(poorCollections.Home.PurchaseItem("home-bed-basic", poorProgress) == HomePurchaseResult.NotEnoughCoins,
        "Hauskauf prüft vorhandene Dino Coins");
    var baseSleepStart = DateTimeOffset.Now.AddMinutes(-40.1);
    poorProgress.Current.AdventurePoints = 5;
    poorProgress.Current.SleepStartedAt = baseSleepStart;
    poorProgress.Current.LastAdventurePointRegenAt = baseSleepStart;
    poorProgress.Current.AdventurePointRegenProgress = 0;
    poorProgress.RefreshAdventurePointCapacity();
    var baseSleepReload = new ProgressService(logger, poorHomeDirectory, () => poorCollections.HomeBonuses.Current);
    Check(baseSleepReload.Current.AdventurePoints == 7,
        "Schlaf-Offline-Regeneration ohne Hausbonus: 1 AP je 20 Minuten");

    var taskService = new DinoTaskService(lunaProgress, logger, lunaDirectory, definitions);
    var expeditionTask = taskService.Tasks.Single(t => t.Id == "data-cave-expedition");
    Check(taskService.Start(expeditionTask.Id, DateTimeOffset.Now - expeditionTask.Duration - TimeSpan.FromSeconds(1)), "Aufgabe starten");
    Check(taskService.RefreshDueTasks() == 1 && taskService.Claim(expeditionTask.Id), "Aufgabe abschließen und zentral belohnen");

    var milo = profiles.CreateProfile("Milos Welt", "Milo");
    profiles.SetActiveProfile(milo.Id);
    var miloProgress = new ProgressService(logger, profiles.GetActiveProfileDirectory());
    var miloCollections = new CollectionManager(logger, profiles.GetActiveProfileDirectory(), definitions);
    Check(miloProgress.Current.Level == 1 && miloProgress.Current.TotalXP == 0 && miloProgress.Current.DinoCoins == 0, "Fortschritt bleibt zwischen Profilen getrennt");
    Check(miloCollections.Current.EquippedSkinId == "standard", "Skin bleibt zwischen Profilen getrennt");

    profiles.SetActiveProfile(luna.Id);
    Check(new ProgressService(logger, profiles.GetActiveProfileDirectory()).Current.TotalXP > 260, "Erstes Profil nach Wechsel vollständig geladen");
    var reloadedStatistics = new StatisticsService(logger, profiles.GetActiveProfileDirectory());
    Check(reloadedStatistics.Current.RockPaperScissors.GamesPlayed == 2 && reloadedStatistics.Current.DigSitesCompleted == 1, "Neue Gameplay-Statistiken persistieren");
    Check(new CollectionManager(logger, profiles.GetActiveProfileDirectory(), definitions).Current.EquippedSkinId == "blue", "Ausgerüsteter Skin persistiert");
    profiles.UpdateProfile(luna.Id, "Luna umbenannt", "Lunchen", 13, 9);
    Check(profiles.ActiveProfile?.DinoName == "Lunchen" && profiles.ActiveProfile.BirthdayDay == 13, "Profil umbenennen und Stammdaten ändern");

    var exportPath = Path.Combine(testRoot, "Luna.dino");
    Check(profiles.ExportProfile(luna.Id, exportPath) && File.Exists(exportPath), "Vollständiger .dino-Export");
    var imported = profiles.ImportProfile(exportPath);
    Check(imported is not null && imported.Id != luna.Id && imported.ProfileName != luna.ProfileName, "Validierter Import mit neuer ID und konfliktfreiem Namen");
    Check(profiles.SetActiveProfile(imported!.Id), "Importiertes Profil laden");

    var reloadedManager = new ProfileManager(logger, dataDirectory);
    Check(reloadedManager.ActiveProfile?.Id == imported.Id, "Zuletzt aktives Profil wird beim Neustart geladen");
    Check(File.Exists(Path.Combine(reloadedManager.GetProfileDirectory(imported.Id), "profile.json")), "SaveVersion-Metadaten liegen im Profil");
    Check(Directory.EnumerateFiles(Path.Combine(dataDirectory, "Backups"), "*.dino", SearchOption.AllDirectories).Any(), "Automatisches Profilbackup");

    Check(reloadedManager.DeleteProfile(milo.Id), "Profil löschen");
    var invalidArchive = Path.Combine(testRoot, "kaputt.dino");
    File.WriteAllText(invalidArchive, "kein zip");
    Check(reloadedManager.ImportProfile(invalidArchive) is null, "Beschädigten Import ablehnen");

    Console.WriteLine("Architecture checks passed: profiles, isolation, progress, tasks, skins, export/import, backups and save version.");
    return 0;
}
finally
{
    var expectedPrefix = testParent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (testRoot.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
}

static void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Architecture check failed: {name}");
    Console.WriteLine($"PASS: {name}");
}
