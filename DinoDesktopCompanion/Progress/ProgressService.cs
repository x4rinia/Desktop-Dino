using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Collections;

namespace DinoDesktopCompanion.Progress;

public sealed class ProgressService
{
    private const int BaseMaxAdventurePoints = 10;
    private const int BaseAdventurePointsPerCompletedRegenCycle = 1;
    private static readonly TimeSpan BaseApRegenDuration = TimeSpan.FromMinutes(5);
    private readonly JsonFileStore<ProgressData> _store;
    private readonly object _gate = new();
    private readonly Func<HomeBonusSummary> _homeBonusProvider;
    public ProgressData Current { get; }
    public bool IsSleeping => Current.SleepStartedAt.HasValue;
    public TimeSpan EffectiveAdventurePointRegenerationInterval => TimeSpan.FromMinutes(GetEffectiveMinutesPerAdventurePoint());
    public int ApPerCompletedRegenCycle => BaseAdventurePointsPerCompletedRegenCycle
        + Math.Max(0, _homeBonusProvider().ApPerRegenCycleBonus);
    public int InitialAdventurePointsGained { get; private set; }
    public event EventHandler<XPAddedEventArgs>? XPAdded;
    public event EventHandler<LevelUpEventArgs>? LevelUp;
    public event EventHandler<CoinsChangedEventArgs>? CoinsChanged;
    public event EventHandler<AdventurePointsChangedEventArgs>? AdventurePointsChanged;

    public ProgressService(FileLogger logger, string? dataDirectory = null, Func<HomeBonusSummary>? homeBonusProvider = null,
        bool allowOfflineSleepRegeneration = true, DateTimeOffset? offlineSleepStartedAt = null)
    {
        _store = new JsonFileStore<ProgressData>("progress.json", logger, dataDirectory);
        _homeBonusProvider = homeBonusProvider ?? (() => HomeBonusSummary.Empty);
        Current = _store.Load(() => new ProgressData());
        var hadPersistedSleepState = Current.SleepStartedAt.HasValue;
        Normalize();
        if (!allowOfflineSleepRegeneration)
        {
            Current.SleepStartedAt = null;
            if (Current.AdventurePoints >= Current.MaxAdventurePoints)
                Current.AdventurePointRegenProgress = 0;
            Current.LastAdventurePointRegenAt = DateTimeOffset.Now;
        }
        else if (offlineSleepStartedAt.HasValue)
        {
            Current.SleepStartedAt = offlineSleepStartedAt;
            if (!hadPersistedSleepState || Current.LastAdventurePointRegenAt < offlineSleepStartedAt.Value)
                Current.LastAdventurePointRegenAt = offlineSleepStartedAt.Value;
        }
        InitialAdventurePointsGained = RefreshAdventurePoints(DateTimeOffset.Now);
    }

    public int AddXP(int amount, string source, double multiplier = 1)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Eine XP-Quelle ist erforderlich.", nameof(source));
        var levelUps = new List<LevelUpEventArgs>();
        var awardedAmount = amount;
        lock (_gate)
        {
            if (double.IsFinite(multiplier) && multiplier > 1)
            {
                var exactBonus = amount * (Math.Clamp(multiplier, 1, 3) - 1) + Current.BonusXpRemainder;
                var wholeBonus = (int)Math.Floor(exactBonus + 0.0000001);
                Current.BonusXpRemainder = Math.Clamp(exactBonus - wholeBonus, 0, 0.9999999);
                awardedAmount += wholeBonus;
            }

            Current.CurrentXP += awardedAmount;
            Current.TotalXP += awardedAmount;
            while (Current.CurrentXP >= Current.XPToNextLevel)
            {
                var previous = Current.Level;
                Current.CurrentXP -= Current.XPToNextLevel;
                Current.Level++;
                Current.XPToNextLevel = CalculateXPToNextLevel(Current.Level);
                Current.MaxAdventurePoints = GetMaxAdventurePoints();
                levelUps.Add(new LevelUpEventArgs(previous, Current.Level));
            }
            _store.Save(Current);
        }
        XPAdded?.Invoke(this, new XPAddedEventArgs(awardedAmount, source, Current.TotalXP));
        foreach (var levelUp in levelUps) LevelUp?.Invoke(this, levelUp);
        return awardedAmount;
    }

    public void AddCoins(int amount, string source)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Eine Coin-Quelle ist erforderlich.", nameof(source));
        lock (_gate) { Current.DinoCoins += amount; _store.Save(Current); }
        CoinsChanged?.Invoke(this, new CoinsChangedEventArgs(amount, source, Current.DinoCoins));
    }

    public bool TrySpendCoins(int amount, string source)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Eine Coin-Quelle ist erforderlich.", nameof(source));
        lock (_gate)
        {
            if (Current.DinoCoins < amount) return false;
            Current.DinoCoins -= amount; _store.Save(Current);
        }
        CoinsChanged?.Invoke(this, new CoinsChangedEventArgs(-amount, source, Current.DinoCoins));
        return true;
    }

    private void Normalize()
    {
        Current.Level = Math.Max(1, Current.Level);
        Current.CurrentXP = Math.Max(0, Current.CurrentXP);
        Current.XPToNextLevel = Current.XPToNextLevel > 0 ? Current.XPToNextLevel : CalculateXPToNextLevel(Current.Level);
        Current.TotalXP = Math.Max(0, Current.TotalXP);
        Current.DinoCoins = Math.Max(0, Current.DinoCoins);
        Current.MaxAdventurePoints = GetMaxAdventurePoints();
        Current.AdventurePoints = Math.Clamp(Current.AdventurePoints, 0, Current.MaxAdventurePoints);
        Current.AdventurePointRegenProgress = double.IsFinite(Current.AdventurePointRegenProgress)
            ? Math.Clamp(Current.AdventurePointRegenProgress, 0, 0.9999999)
            : 0;
        Current.BonusXpRemainder = double.IsFinite(Current.BonusXpRemainder)
            ? Math.Clamp(Current.BonusXpRemainder, 0, 0.9999999)
            : 0;

        var now = DateTimeOffset.Now;
        if (Current.LastAdventurePointRegenAt == DateTimeOffset.MinValue)
        {
            Current.LastAdventurePointRegenAt = Current.SleepStartedAt
                ?? (Current.LastPassiveRegen != DateTimeOffset.MinValue ? Current.LastPassiveRegen : now);
        }
        if (Current.LastAdventurePointRegenAt > now) Current.LastAdventurePointRegenAt = now;
    }

    private static int CalculateXPToNextLevel(int level) => 100 + (Math.Max(1, level) - 1) * 50;

    private static int CalculateLevelAdventurePointBonus(int level)
    {
        if (level < 10) return 0;
        if (level < 20) return 2;
        return 4;
    }

    public bool SpendAdventurePoints(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        RefreshAdventurePoints(DateTimeOffset.Now);
        var changed = false;
        lock (_gate)
        {
            if (Current.AdventurePoints < amount) return false;
            Current.AdventurePoints -= amount;
            _store.Save(Current);
            changed = true;
        }
        if (changed) AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(-amount, Current.AdventurePoints, Current.MaxAdventurePoints));
        return true;
    }

    public void StartSleeping(DateTimeOffset startedAt)
    {
        lock (_gate)
        {
            if (Current.SleepStartedAt.HasValue) return;
            Current.SleepStartedAt = startedAt;
            Current.LastAdventurePointRegenAt = startedAt;
            if (Current.AdventurePoints >= Current.MaxAdventurePoints)
                Current.AdventurePointRegenProgress = 0;
            _store.Save(Current);
        }
        AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(0, Current.AdventurePoints, Current.MaxAdventurePoints));
    }

    public int RegenerateAdventurePoints(DateTimeOffset wakeTime)
    {
        if (Current.SleepStartedAt == null) return 0;
        var gained = RefreshAdventurePoints(wakeTime);
        lock (_gate)
        {
            Current.SleepStartedAt = null;
            Current.LastAdventurePointRegenAt = wakeTime;
            if (Current.AdventurePoints >= Current.MaxAdventurePoints)
                Current.AdventurePointRegenProgress = 0;
            _store.Save(Current);
        }
        AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(0, Current.AdventurePoints, Current.MaxAdventurePoints));
        return gained;
    }

    public int RefreshAdventurePoints(DateTimeOffset now)
    {
        int gained;
        int oldMaximum;
        double oldProgress;
        lock (_gate)
        {
            oldMaximum = Current.MaxAdventurePoints;
            oldProgress = Current.AdventurePointRegenProgress;
            Current.MaxAdventurePoints = GetMaxAdventurePoints();
            Current.AdventurePoints = Math.Clamp(Current.AdventurePoints, 0, Current.MaxAdventurePoints);

            if (now < Current.LastAdventurePointRegenAt) Current.LastAdventurePointRegenAt = now;
            var elapsed = now - Current.LastAdventurePointRegenAt;
            var oldPoints = Current.AdventurePoints;
            if (!Current.SleepStartedAt.HasValue)
            {
                if (Current.AdventurePoints >= Current.MaxAdventurePoints)
                    Current.AdventurePointRegenProgress = 0;
            }
            else if (Current.AdventurePoints >= Current.MaxAdventurePoints)
            {
                Current.AdventurePointRegenProgress = 0;
            }
            else if (elapsed > TimeSpan.Zero)
            {
                var minutesPerPoint = GetEffectiveMinutesPerAdventurePoint();
                Current.AdventurePointRegenProgress += elapsed.TotalMinutes / minutesPerPoint;
                var completedCycles = Math.Max(0, (int)Math.Floor(Current.AdventurePointRegenProgress));
                var availableCapacity = Current.MaxAdventurePoints - Current.AdventurePoints;
                var appliedPoints = Math.Min(availableCapacity, completedCycles * ApPerCompletedRegenCycle);
                Current.AdventurePoints += appliedPoints;
                Current.AdventurePointRegenProgress -= completedCycles;
                if (Current.AdventurePoints >= Current.MaxAdventurePoints) Current.AdventurePointRegenProgress = 0;
            }

            Current.LastAdventurePointRegenAt = now;
            Current.LastPassiveRegen = now;
            gained = Current.AdventurePoints - oldPoints;
            _store.Save(Current);
        }

        if (gained != 0 || oldMaximum != Current.MaxAdventurePoints || Math.Abs(oldProgress - Current.AdventurePointRegenProgress) > 0.000001)
            AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(gained, Current.AdventurePoints, Current.MaxAdventurePoints));
        return gained;
    }

    public void RefreshAdventurePointCapacity()
    {
        int oldMaximum;
        int oldPoints;
        double oldProgress;
        lock (_gate)
        {
            oldMaximum = Current.MaxAdventurePoints;
            oldPoints = Current.AdventurePoints;
            oldProgress = Current.AdventurePointRegenProgress;
            Current.MaxAdventurePoints = GetMaxAdventurePoints();
            Current.AdventurePoints = Math.Clamp(Current.AdventurePoints, 0, Current.MaxAdventurePoints);
            if (Current.AdventurePoints >= Current.MaxAdventurePoints)
                Current.AdventurePointRegenProgress = 0;
            if (oldPoints >= oldMaximum && Current.AdventurePoints < Current.MaxAdventurePoints)
                Current.LastAdventurePointRegenAt = DateTimeOffset.Now;
            _store.Save(Current);
        }
        if (oldMaximum != Current.MaxAdventurePoints || oldPoints != Current.AdventurePoints
            || Math.Abs(oldProgress - Current.AdventurePointRegenProgress) > 0.000001)
            AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(Current.AdventurePoints - oldPoints, Current.AdventurePoints, Current.MaxAdventurePoints));
    }

    public void TickPassiveRegen() => RefreshAdventurePoints(DateTimeOffset.Now);

    public void AddInstantAP(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        RefreshAdventurePoints(DateTimeOffset.Now);
        var added = 0;
        lock (_gate)
        {
            var oldPoints = Current.AdventurePoints;
            Current.AdventurePoints = Math.Min(Current.MaxAdventurePoints, Current.AdventurePoints + amount);
            added = Current.AdventurePoints - oldPoints;
            if (Current.AdventurePoints >= Current.MaxAdventurePoints)
            {
                Current.AdventurePointRegenProgress = 0;
                Current.LastAdventurePointRegenAt = DateTimeOffset.Now;
            }
            _store.Save(Current);
        }
        if (added > 0) AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(added, Current.AdventurePoints, Current.MaxAdventurePoints));
    }

    private int GetMaxAdventurePoints() => BaseMaxAdventurePoints
        + CalculateLevelAdventurePointBonus(Current.Level)
        + Math.Max(0, _homeBonusProvider().MaxApBonus);

    private double GetEffectiveMinutesPerAdventurePoint()
    {
        var bonuses = _homeBonusProvider();
        var multiplier = bonuses.ApRegenMultiplier * bonuses.SleepApRegenMultiplier;
        return Math.Clamp(BaseApRegenDuration.TotalMinutes / Math.Clamp(multiplier, 0.1, 3), 1, 24 * 60);
    }

    private void CheckResetDaily()
    {
        if (Current.LastResetDate.Date < DateTimeOffset.Now.Date)
        {
            Current.DailyPlayCount = 0;
            Current.DailyFeedCount = 0;
            Current.LastResetDate = DateTimeOffset.Now;
        }
    }

    public bool TrackPlayAndGetRewardEligibility()
    {
        RefreshAdventurePoints(DateTimeOffset.Now);
        var spent = false;
        lock (_gate)
        {
            CheckResetDaily();
            if (Current.AdventurePoints >= 1 && Current.DailyPlayCount < 5)
            {
                Current.AdventurePoints -= 1;
                Current.DailyPlayCount++;
                _store.Save(Current);
                spent = true;
            }
        }
        if (spent) AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(-1, Current.AdventurePoints, Current.MaxAdventurePoints));
        return spent;
    }

    public bool TrackFeedAndGetRewardEligibility()
    {
        RefreshAdventurePoints(DateTimeOffset.Now);
        var spent = false;
        lock (_gate)
        {
            CheckResetDaily();
            if (Current.AdventurePoints >= 1 && Current.DailyFeedCount < 5)
            {
                Current.AdventurePoints -= 1;
                Current.DailyFeedCount++;
                _store.Save(Current);
                spent = true;
            }
        }
        if (spent) AdventurePointsChanged?.Invoke(this, new AdventurePointsChangedEventArgs(-1, Current.AdventurePoints, Current.MaxAdventurePoints));
        return spent;
    }
}

public sealed class XPAddedEventArgs : EventArgs
{
    public int Amount { get; } public string Source { get; } public long TotalXP { get; }
    public XPAddedEventArgs(int amount, string source, long totalXP) { Amount = amount; Source = source; TotalXP = totalXP; }
}
public sealed class LevelUpEventArgs : EventArgs
{
    public int PreviousLevel { get; } public int NewLevel { get; }
    public LevelUpEventArgs(int previousLevel, int newLevel) { PreviousLevel = previousLevel; NewLevel = newLevel; }
}
public sealed class CoinsChangedEventArgs : EventArgs
{
    public int Delta { get; } public string Source { get; } public int Balance { get; }
    public CoinsChangedEventArgs(int delta, string source, int balance) { Delta = delta; Source = source; Balance = balance; }
}
public sealed class AdventurePointsChangedEventArgs : EventArgs
{
    public int Delta { get; }
    public int Current { get; }
    public int Maximum { get; }
    public AdventurePointsChangedEventArgs(int delta, int current, int maximum)
    {
        Delta = delta;
        Current = current;
        Maximum = maximum;
    }
}
