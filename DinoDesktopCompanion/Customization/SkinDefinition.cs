namespace DinoDesktopCompanion.Customization;

public sealed class SkinDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public int UnlockLevel { get; set; }
    public int Cost { get; set; }
    public string Rarity { get; set; } = "Gewöhnlich";
    public string? SetId { get; set; }
    public string BaseColor { get; set; } = "#789E5B";
    public string AccentColor { get; set; } = "#506D40";
    public string? SecondaryColor { get; set; }
    public EffectType Effect { get; set; } = EffectType.None;
    public string? GradientType { get; set; }
    public string? PatternType { get; set; }
    public string? Glow { get; set; }
    public string? SparkleType { get; set; }
    public string? RareEffectType { get; set; }
    public string? UnlockCondition { get; set; }
    public SkinUnlockType UnlockType { get; set; } = SkinUnlockType.Coins;
    public int RequiredCount { get; set; }
    public string? RequiredAreaId { get; set; }
    public List<string> RequiredFindIds { get; set; } = [];
    public List<string> RequiredRarities { get; set; } = [];
    public bool IsUnlocked { get; set; }
    public bool IsEquipped { get; set; }
}

public enum SkinUnlockType
{
    Default,
    Coins,
    Level,
    CollectionProgress,
    AreaCollection,
    SpecificFind,
    DigCount,
    AllAreasUnlocked,
    AllStandardSkins,
    FullCollection,
    Event,
    GoldFund,
    CrystalFund
}

public enum EffectType
{
    None,
    Gloss,
    Glitter,
    Rainbow,
    Shimmer
}
