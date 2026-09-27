namespace DinoDesktopCompanion.Progress;

public sealed class ProgressData
{
    public int Level { get; set; } = 1;
    public int CurrentXP { get; set; }
    public int XPToNextLevel { get; set; } = 100;
    public long TotalXP { get; set; }
    public int DinoCoins { get; set; }
    
    public int AdventurePoints { get; set; } = 10;
    public int MaxAdventurePoints { get; set; } = 10;
    public DateTimeOffset? SleepStartedAt { get; set; }
    public DateTimeOffset LastAdventurePointRegenAt { get; set; } = DateTimeOffset.MinValue;
    public double AdventurePointRegenProgress { get; set; }
    public double BonusXpRemainder { get; set; }
    
    public int DailyPlayCount { get; set; }
    public int DailyFeedCount { get; set; }
    public DateTimeOffset LastResetDate { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastPassiveRegen { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastAppleTime { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastWashTime { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset LastBallTime { get; set; } = DateTimeOffset.MinValue;
}
