namespace DinoDesktopCompanion.DesktopDigging;

/// <summary>Zentrale Werte für die erste aktive Desktop-Grabung im Wald.</summary>
public sealed class DesktopDiggingOptions
{
    public string EnabledAreaId { get; init; } = "wald";
    public TimeSpan MinimumSpawnDelay { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan MaximumSpawnDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan MinimumIgnoredLifetime { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan MaximumIgnoredLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(15);
    public IReadOnlyList<DesktopDigSize> DigSizes { get; init; } =
    [
        new("Klein", 5, 5, 45),
        new("Mittel", 8, 8, 35),
        new("Groß", 10, 10, 20)
    ];
    public TimeSpan PassiveDigInterval { get; init; } = TimeSpan.FromSeconds(10);
    public int PassiveDigAmount { get; init; } = 1;
    public int AdventurePointCost { get; init; } = 1;
    public int CoinReward { get; init; } = 5;
    public double CollectibleChance { get; init; } = 0.25;

    public static DesktopDiggingOptions Forest { get; } = new();
}

public sealed record DesktopDigSize(string Name, int RequiredProgress, int ExperienceReward, int Weight);
