namespace DinoDesktopCompanion.Collections;

public sealed class HomeBonusService
{
    private readonly HomeCollectionService _home;
    private readonly CollectionSaveData _save;

    public HomeBonusService(HomeCollectionService home, CollectionSaveData save)
    {
        _home = home;
        _save = save;
    }

    public HomeBonusSummary Current => Calculate();

    public HomeBonusSummary Calculate()
    {
        var activeItems = _save.EquippedHomeItemsBySlot.Values
            .Select(id => _home.Items.FirstOrDefault(item => item.Id == id))
            .Where(item => item is not null && _save.UnlockedHomeItems.Contains(item.Id))
            .Cast<HomeItemDefinition>()
            .ToList();

        var totals = new Dictionary<HomeBonusType, double>();
        foreach (var bonus in activeItems.SelectMany(item => item.Bonuses ?? []))
            totals[bonus.Type] = totals.GetValueOrDefault(bonus.Type) + bonus.Value;

        return new HomeBonusSummary
        {
            ApRegenMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.ApRegeneration)),
            SleepApRegenMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.SleepApRegeneration)),
            ApPerRegenCycleBonus = Math.Max(0, (int)Math.Round(totals.GetValueOrDefault(HomeBonusType.AdventurePointsPerRegenCycle))),
            MaxApBonus = Math.Max(0, (int)Math.Round(totals.GetValueOrDefault(HomeBonusType.MaxAdventurePoints))),
            XpMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.Experience)),
            DiggingXpMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.DiggingExperience)),
            CollectibleChanceBonus = PercentFraction(totals.GetValueOrDefault(HomeBonusType.CollectibleChance)),
            RarityChanceBonus = PercentFraction(totals.GetValueOrDefault(HomeBonusType.RarityChance)),
            DigSiteLifetimeMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.DigSiteLifetime)),
            DigSpeedMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.DigSpeed)),
            GameXpMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.GameExperience)),
            AutoLeafCollect = totals.GetValueOrDefault(HomeBonusType.AutoLeafCollect) > 0,
            LeafRewardBonus = totals.GetValueOrDefault(HomeBonusType.LeafRewardBonus),
            BugRewardBonus = totals.GetValueOrDefault(HomeBonusType.BugRewardBonus),
            CoinMultiplier = PercentMultiplier(totals.GetValueOrDefault(HomeBonusType.CoinBonus)),
            ActiveDescriptions = activeItems
                .SelectMany(item => item.Bonuses ?? [])
                .Where(bonus => bonus.Type is not (HomeBonusType.None or HomeBonusType.Event) && !string.IsNullOrWhiteSpace(bonus.Description))
                .Select(bonus => bonus.Description)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public double ApplyCollectibleChance(double baseChance) =>
        Math.Clamp(baseChance + Current.CollectibleChanceBonus, 0, 1);

    public AlbumEntryDefinition? ChooseCollectible(IReadOnlyList<AlbumEntryDefinition> candidates)
    {
        if (candidates.Count == 0) return null;
        var rarityBonus = Current.RarityChanceBonus;
        var weights = candidates.Select(item => 1d + RarityTier(item.Rarity) * rarityBonus).ToArray();
        var roll = Random.Shared.NextDouble() * weights.Sum();
        for (var index = 0; index < candidates.Count; index++)
        {
            roll -= weights[index];
            if (roll <= 0) return candidates[index];
        }
        return candidates[^1];
    }

    private static double PercentMultiplier(double percent) => Math.Clamp(1 + percent / 100d, 0.1, 3);
    private static double PercentFraction(double percent) => Math.Clamp(percent / 100d, 0, 1);

    private static int RarityTier(string rarity) => rarity.ToLowerInvariant() switch
    {
        "ungewöhnlich" => 1,
        "selten" => 2,
        "episch" => 3,
        "legendär" => 4,
        _ => 0
    };
}

public sealed class HomeBonusSummary
{
    public static HomeBonusSummary Empty { get; } = new();
    public double ApRegenMultiplier { get; init; } = 1;
    public double SleepApRegenMultiplier { get; init; } = 1;
    public int ApPerRegenCycleBonus { get; init; }
    public int MaxApBonus { get; init; }
    public double XpMultiplier { get; init; } = 1;
    public double DiggingXpMultiplier { get; init; } = 1;
    public double CollectibleChanceBonus { get; init; }
    public double RarityChanceBonus { get; init; }
    public double DigSiteLifetimeMultiplier { get; init; } = 1;
    public double DigSpeedMultiplier { get; init; } = 1;
    public double GameXpMultiplier { get; init; } = 1;
    public bool AutoLeafCollect { get; init; }
    public double LeafRewardBonus { get; init; }
    public double BugRewardBonus { get; init; }
    public double CoinMultiplier { get; init; } = 1;
    public IReadOnlyList<string> ActiveDescriptions { get; init; } = Array.Empty<string>();
}
