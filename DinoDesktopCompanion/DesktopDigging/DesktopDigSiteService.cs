using System.Windows.Media;
using System.Windows.Threading;
using DinoDesktopCompanion.Collections;
using DinoDesktopCompanion.Core;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Statistics;
using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.DesktopDigging;

public sealed class DesktopDigSiteService : IDisposable
{
    private readonly MainWindow _owner;
    private readonly ProgressService _progress;
    private readonly StatisticsService _statistics;
    private readonly CollectionManager _collections;
    private readonly AreaService _areas;
    private readonly Func<bool> _canSpawn;
    private readonly Action<DigSiteWindow> _travelToSite;
    private readonly Action<bool, string> _finishDinoDigging;
    private readonly Action<string> _showMessage;
    private readonly DesktopDiggingOptions _options;
    private readonly DispatcherTimer _spawnTimer = new();
    private readonly DispatcherTimer _expiryTimer = new();
    private readonly DispatcherTimer _passiveTimer = new();
    private DigSiteWindow? _activeSite;
    private bool _running;
    private bool _completed;
    private bool _diggingStarted;
    private bool _energyPaidForActiveDig;
    private string _activeAreaId = "garten";
    private DesktopDigSize? _activeDigSize;
    private bool _activeSiteIsRare;

    public DesktopDigSiteService(
        MainWindow owner,
        ProgressService progress,
        StatisticsService statistics,
        CollectionManager collections,
        AreaService areas,
        Func<bool> canSpawn,
        Action<DigSiteWindow> travelToSite,
        Action<bool, string> finishDinoDigging,
        Action<string> showMessage,
        DesktopDiggingOptions? options = null)
    {
        _owner = owner;
        _progress = progress;
        _statistics = statistics;
        _collections = collections;
        _areas = areas;
        _canSpawn = canSpawn;
        _travelToSite = travelToSite;
        _finishDinoDigging = finishDinoDigging;
        _showMessage = showMessage;
        _options = options ?? DesktopDiggingOptions.Forest;
        _spawnTimer.Tick += SpawnTimer_Tick;
        _expiryTimer.Tick += (_, _) => DismissActiveSite();
        _passiveTimer.Tick += (_, _) => PassiveTimer_Tick();
        _progress.AdventurePointsChanged += Progress_AdventurePointsChanged;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        ScheduleNext();
    }

    public void CancelActive()
    {
        if (_activeSite is null) return;
        _passiveTimer.Stop();
        _expiryTimer.Stop();
        _activeSite.Close();
    }

    /// <summary>
    /// Interner Testeinstieg: erzeugt ohne Wartezeit eine Grabungsstelle,
    /// sofern alle normalen Spawn-Voraussetzungen erfüllt sind.
    /// </summary>
    public bool TrySpawnNowForTesting()
    {
        if (!_running || _activeSite is not null || !CanSpawnSelectedArea()) return false;
        _spawnTimer.Stop();
        ShowSite();
        return true;
    }

    private void SpawnTimer_Tick(object? sender, EventArgs e)
    {
        _spawnTimer.Stop();
        if (_activeSite is not null || !CanSpawnSelectedArea())
        {
            ScheduleRetry();
            return;
        }

        ShowSite();
    }

    private bool CanSpawnSelectedArea()
    {
        if (!_canSpawn() || _progress.Current.AdventurePoints <= 0) return false;
        var area = _areas.Current.Areas.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, _areas.Current.SelectedAreaId, StringComparison.OrdinalIgnoreCase));
        return area is not null && _progress.Current.Level >= area.MinLevel;
    }

    private void ShowSite()
    {
        if (_activeSite is not null) return;

        _completed = false;
        _diggingStarted = false;
        _energyPaidForActiveDig = false;
        var area = _areas.SelectedArea;
        if (area is null) return;
        _activeAreaId = area.Id;
        _activeDigSize = ChooseDigSize();
        var variantIndex = Random.Shared.Next(3);
        _activeSiteIsRare = Random.Shared.Next(10) == 0;
        var site = new DigSiteWindow(_activeDigSize.RequiredProgress, _activeDigSize.Name, area.Id, area.DigSiteVisual,
            variantIndex, _activeSiteIsRare, 0, CanAdvanceDigging, ShowNoEnergyMessage) { Owner = _owner, ShowInTaskbar = false };
        _activeSite = site;
        site.TravelRequested += (_, _) => StartDigging(site);
        site.DigCompleted += (_, _) => CompleteDig(site);
        site.CancelRequested += (_, _) => CancelDigging(site);
        site.Closed += (_, _) => Site_Closed(site);

        PositionInsideWorkingArea(site);
        site.Show();
        _expiryTimer.Interval = ScaleLifetime(RandomBetween(_options.MinimumIgnoredLifetime, _options.MaximumIgnoredLifetime));
        _expiryTimer.Start();
        
        _owner.TriggerDigSiteSpawnReaction();
    }

    private void StartDigging(DigSiteWindow site)
    {
        if (!ReferenceEquals(_activeSite, site) || _diggingStarted) return;
        if (!CanSpawnSelectedArea() || !string.Equals(_areas.Current.SelectedAreaId, _activeAreaId, StringComparison.OrdinalIgnoreCase))
        {
            ShowTemporaryMessage(site, "Diese Grabungsstelle ist gerade nicht mehr verfügbar.");
            return;
        }

        _diggingStarted = true;
        if (_progress.Current.AdventurePoints <= 0 || !_progress.SpendAdventurePoints(_options.AdventurePointCost))
        {
            _diggingStarted = false;
            const string message = "Dino braucht erst wieder etwas Energie.";
            ShowNoEnergyMessage();
            ShowTemporaryMessage(site, message);
            return;
        }

        _energyPaidForActiveDig = true;
        _expiryTimer.Stop();
        _travelToSite(site);
        StartPassiveTimer();
    }

    private void StartPassiveTimer()
    {
        var speed = Math.Max(0.1, _collections.HomeBonuses.Current.DigSpeedMultiplier);
        _passiveTimer.Interval = TimeSpan.FromTicks((long)(_options.PassiveDigInterval.Ticks / speed));
        _passiveTimer.Start();
    }

    private void PassiveTimer_Tick()
    {
        if (_activeSite is null || !_diggingStarted || !CanAdvanceDigging()) return;
        _activeSite.AdvancePassive(_options.PassiveDigAmount);
    }

    private bool CanAdvanceDigging() => _energyPaidForActiveDig && _owner.IsVisible && !_owner.IsSleeping;

    private void ShowNoEnergyMessage() => _showMessage("Dino braucht erst wieder etwas Energie.");

    private void Progress_AdventurePointsChanged(object? sender, AdventurePointsChangedEventArgs e)
    {
        if (e.Current > 0 || _diggingStarted || _activeSite is null) return;
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
        {
            if (_activeSite is not null && !_diggingStarted) DismissActiveSite();
        }));
    }

    private void CancelDigging(DigSiteWindow site)
    {
        if (!ReferenceEquals(site, _activeSite)) return;
        _passiveTimer.Stop();
        _energyPaidForActiveDig = false;
        site.Close();
    }

    private static void ShowTemporaryMessage(DigSiteWindow site, string message)
    {
        site.ShowUnavailable(message);
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            if (site.IsVisible) site.Close();
        };
        closeTimer.Start();
    }

    private void Site_Closed(DigSiteWindow site)
    {
        if (!ReferenceEquals(_activeSite, site)) return;
        if (_diggingStarted)
        {
            _diggingStarted = false;
            AwardPartialDigXp(site);
            _finishDinoDigging(false, "");
        }

        _activeSite = null;
        _activeAreaId = "garten";
        _activeDigSize = null;
        _energyPaidForActiveDig = false;
        _passiveTimer.Stop();
        _expiryTimer.Stop();
        if (_running) ScheduleNext();
    }

    private void PositionInsideWorkingArea(DigSiteWindow site)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(_owner).Handle;
        var workArea = Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(_owner);
        
        double dinoWidth = 240;
        double dinoHeight = 200;

        var minLeft = (workArea.Left / dpi.DpiScaleX) + dinoWidth;
        var minTop = (workArea.Top / dpi.DpiScaleY) + dinoHeight;
        var maxRight = (workArea.Right / dpi.DpiScaleX) - dinoWidth;
        var maxBottom = (workArea.Bottom / dpi.DpiScaleY) - 50; // Dino can stand above it, so bottom margin isn't as critical

        var safeMaxX = Math.Max(minLeft, maxRight - site.Width);
        var safeMaxY = Math.Max(minTop, maxBottom - site.Height);

        site.Left = minLeft + Random.Shared.NextDouble() * Math.Max(0, safeMaxX - minLeft);
        site.Top = minTop + Random.Shared.NextDouble() * Math.Max(0, safeMaxY - minTop);
    }

    private void CompleteDig(DigSiteWindow site)
    {
        if (_completed || !_diggingStarted || !ReferenceEquals(site, _activeSite)) return;
        _completed = true;
        _diggingStarted = false;
        _energyPaidForActiveDig = false;
        _passiveTimer.Stop();

        var bonuses = _collections.HomeBonuses.Current;
        var awardedXp = _progress.AddXP(
            _activeDigSize?.ExperienceReward ?? 10,
            $"Grabung:{_activeAreaId}",
            bonuses.XpMultiplier * bonuses.DiggingXpMultiplier);
        _progress.AddCoins(_options.CoinReward, $"Grabung:{_activeAreaId}");
        _statistics.TrackDigSiteCompleted();

        var message = $"Grabung fertig: +{awardedXp} XP, +{_options.CoinReward} Dino Coins.";
        var areaCandidates = _collections.Toys.Items
            .Where(item => string.Equals(item.AreaId, _activeAreaId, StringComparison.OrdinalIgnoreCase))
            .Where(item => !item.Id.EndsWith("_gold", StringComparison.OrdinalIgnoreCase) && !item.Id.EndsWith("_crystal", StringComparison.OrdinalIgnoreCase))
            .Where(item => _collections.Current.ToyCounts.GetValueOrDefault(item.Id, 0) < 40)
            .ToList();
        var collectBonus = _activeSiteIsRare ? 0.2 : 0;
        var hasCollectRoll = Random.Shared.NextDouble() < _collections.HomeBonuses.ApplyCollectibleChance(0.75 + collectBonus);
        
        AlbumEntryDefinition? found = null;
        AlbumEntryDefinition? found2 = null;
        if (hasCollectRoll)
        {
            var rarityBonus = bonuses.RarityChanceBonus + (_activeSiteIsRare ? 0.15 : 0);
            found = ChooseByRarity(areaCandidates, rarityBonus);
            
            if (bonuses.RarityChanceBonus > 0 && Random.Shared.NextDouble() < (bonuses.RarityChanceBonus))
            {
                found2 = ChooseByRarity(areaCandidates, rarityBonus);
            }
        }
        
        var HandleFound = new Action<AlbumEntryDefinition>(f => { var isNew = _collections.Toys.Unlock(f.Id, out var superFound); var count = _collections.Current.ToyCounts.GetValueOrDefault(f.Id, 0); message += isNew ? $" Neuer Fund: {f.Name}!" : $" Fund: {f.Name} ({count}x)."; if (superFound != null) message += $"\nWahnsinn! Du erhältst: {superFound.Name}!"; }); if (found is not null) { HandleFound(found); if (found2 is not null) HandleFound(found2); } else message += " Diesmal leider kein Fundstück.";
        
        _finishDinoDigging(true, found?.Rarity ?? "");
        site.ShowCompletion(message, found);
        _showMessage(message);
        var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            if (site.IsVisible) site.Close();
        };
        closeTimer.Start();
    }

    private void AwardPartialDigXp(DigSiteWindow site)
    {
        if (_completed || _activeDigSize is null || site.ProgressValue <= 0) return;
        var completion = Math.Clamp((double)site.ProgressValue / site.RequiredProgress, 0, 1);
        var baseXp = Math.Max(1, (int)Math.Floor(_activeDigSize.ExperienceReward * completion));
        var bonuses = _collections.HomeBonuses.Current;
        var awardedXp = _progress.AddXP(
            baseXp,
            $"GrabungAbgebrochen:{_activeAreaId}",
            bonuses.XpMultiplier * bonuses.DiggingXpMultiplier);
        var percent = (int)Math.Round(completion * 100);
        _showMessage($"Grabung bei {percent} % abgebrochen: +{awardedXp} XP.");
    }

    private DesktopDigSize ChooseDigSize()
    {
        var sizes = _options.DigSizes.Where(size => size.RequiredProgress > 0 && size.Weight > 0).ToList();
        if (sizes.Count == 0) return new DesktopDigSize("Mittel", 8, 8, 1);
        var roll = Random.Shared.Next(sizes.Sum(size => size.Weight));
        foreach (var size in sizes)
        {
            roll -= size.Weight;
            if (roll < 0) return size;
        }
        return sizes[^1];
    }

    private static AlbumEntryDefinition? ChooseByRarity(
        IReadOnlyList<AlbumEntryDefinition> candidates,
        double rarityChanceBonus)
    {
        if (candidates.Count == 0) return null;
        var weights = candidates.Select(item =>
        {
            var (baseWeight, tier) = item.Rarity.ToLowerInvariant() switch
            {
                "ungewöhnlich" => (15d, 1),
                "selten" => (5d, 2),
                "episch" => (2d, 3),
                "legendär" => (0.5d, 4),
                _ => (100d, 0)
            };
            return baseWeight * (1 + tier * Math.Clamp(rarityChanceBonus, 0, 1));
        }).ToArray();

        var roll = Random.Shared.NextDouble() * weights.Sum();
        for (var index = 0; index < candidates.Count; index++)
        {
            roll -= weights[index];
            if (roll <= 0) return candidates[index];
        }
        return candidates[^1];
    }

    private TimeSpan ScaleLifetime(TimeSpan lifetime)
    {
        var multiplier = Math.Clamp(_collections.HomeBonuses.Current.DigSiteLifetimeMultiplier, 0.1, 3);
        return TimeSpan.FromTicks((long)(lifetime.Ticks * multiplier));
    }

    private void DismissActiveSite()
    {
        _expiryTimer.Stop();
        _passiveTimer.Stop();
        _activeSite?.Close();
    }

    private void ScheduleNext()
    {
        if (!_running) return;
        _spawnTimer.Stop();
        _spawnTimer.Interval = RandomBetween(_options.MinimumSpawnDelay, _options.MaximumSpawnDelay);
        _spawnTimer.Start();
    }

    private void ScheduleRetry()
    {
        if (!_running) return;
        _spawnTimer.Stop();
        _spawnTimer.Interval = _options.RetryDelay;
        _spawnTimer.Start();
    }

    private static TimeSpan RandomBetween(TimeSpan minimum, TimeSpan maximum)
    {
        if (maximum <= minimum) return minimum;
        return minimum + TimeSpan.FromTicks((long)((maximum - minimum).Ticks * Random.Shared.NextDouble()));
    }

    public void Dispose()
    {
        _running = false;
        _spawnTimer.Stop();
        _expiryTimer.Stop();
        _progress.AdventurePointsChanged -= Progress_AdventurePointsChanged;
        CancelActive();
    }
}



