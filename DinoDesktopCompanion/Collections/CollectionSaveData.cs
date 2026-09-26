using System.Text.Json.Serialization;

namespace DinoDesktopCompanion.Collections;

public class CollectionSaveData
{
    // Garderobe
    public HashSet<string> UnlockedSkinIds { get; set; } = [];
    public string? EquippedSkinId { get; set; }
    public int SkinUnlockMigrationVersion { get; set; }

    // Spielzeug
    public HashSet<string> UnlockedToys { get; set; } = [];
    public Dictionary<string, DateTimeOffset> ToyFirstFoundAt { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> ToyCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Haus
    public HashSet<string> UnlockedHomeItems { get; set; } = [];
    public Dictionary<string, string> EquippedHomeItemsBySlot { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ActiveHouseId { get; set; } = "default";

}
