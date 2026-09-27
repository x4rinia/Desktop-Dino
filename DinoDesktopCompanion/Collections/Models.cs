using System.Text.Json.Serialization;

namespace DinoDesktopCompanion.Collections;

public class HomeItemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public double VisualWidth { get; set; }
    public double VisualHeight { get; set; }
    public string Rarity { get; set; } = "Gewöhnlich";
    public string Source { get; set; } = "Shop";
    public string? UnlockCondition { get; set; }
    public string? GameplayEffect { get; set; }
    public string? InteractionId { get; set; }
    public int RequiredLevel { get; set; } = 1;
    public int Cost { get; set; }
    public string? BaseDecorationId { get; set; }
    public int UpgradeLevel { get; set; } = 1;
    public string? RequiredPreviousItemId { get; set; }
    public List<HomeBonusDefinition> Bonuses { get; set; } = new();
    
    [JsonIgnore]
    public bool IsUnlocked { get; set; }
    [JsonIgnore]
    public bool IsEquipped { get; set; }
}

public sealed class HomeBonusDefinition
{
    public HomeBonusType Type { get; set; }
    public double Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum HomeBonusType
{
    None,
    ApRegeneration,
    SleepApRegeneration,
    AdventurePointsPerRegenCycle,
    MaxAdventurePoints,
    Experience,
    DiggingExperience,
    CollectibleChance,
    RarityChance,
    DigSiteLifetime,
    GameExperience,
    Event,
    DigSpeed,
    AutoLeafCollect,
    LeafRewardBonus,
    BugRewardBonus,
    CoinBonus
}

public class HouseLayoutDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BackgroundAssetPath { get; set; } = string.Empty;
    public int BackgroundCropX { get; set; }
    public int BackgroundCropY { get; set; }
    public int BackgroundCropWidth { get; set; }
    public int BackgroundCropHeight { get; set; }
    public List<HouseSlotDefinition> Slots { get; set; } = new();
}

public class HouseSlotDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 128;
    public double Height { get; set; } = 42;
    public int ZIndex { get; set; }
    public double Scale { get; set; } = 1.0;
}

public class AlbumEntryDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AreaId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AssetPath { get; set; } = string.Empty;
    public string Rarity { get; set; } = "Gewöhnlich";
    public bool IsSecret { get; set; }
    public string? EventId { get; set; }
    
    [JsonIgnore]
    public bool IsDiscovered { get; set; }
    [JsonIgnore]
    public DateTimeOffset? FirstFoundAt { get; set; }
}

public class AlbumAreaDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
}

