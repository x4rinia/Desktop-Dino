namespace DinoDesktopCompanion.Customization;

public enum UtilityItemType { Home, HomeDecoration, Consumable, PassiveUpgrade }

public sealed class UtilityItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string EffectDescription { get; set; } = "";
    public UtilityItemType Type { get; set; }
    public int Cost { get; set; }
    public int UnlockLevel { get; set; }
    public string Rarity { get; set; } = "Gewöhnlich";
    public string? SetId { get; set; }
    
    // Effects
    public int InstantAP { get; set; }
    public int MaxAPBonus { get; set; }
    public int SleepRegenBonusPercent { get; set; }
    public int PassiveRegenMinutes { get; set; }

    public bool IsUnlocked { get; set; }
    public bool IsEquipped { get; set; } // For Home and PassiveUpgrades
}
