using System.Text.Json;
using System.Text.Json.Serialization;
using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Collections;

public class CollectionManager
{
    private readonly JsonFileStore<CollectionSaveData> _store;
    public CollectionSaveData Current { get; }
    
    public CosmeticCollectionService Cosmetics { get; }
    public ToyCollectionService Toys { get; }
    public HomeCollectionService Home { get; }
    public HomeBonusService HomeBonuses { get; }
    public event EventHandler? CollectionChanged;

    public CollectionManager(FileLogger logger, string? dataDirectory = null, string? definitionDirectory = null)
    {
        _store = new JsonFileStore<CollectionSaveData>("collections.json", logger, dataDirectory);
        Current = _store.Load(() => new CollectionSaveData());
        Current.UnlockedSkinIds ??= new HashSet<string>();
        Current.UnlockedToys ??= new HashSet<string>();
        Current.ToyFirstFoundAt = Current.ToyFirstFoundAt == null
            ? new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, DateTimeOffset>(Current.ToyFirstFoundAt, StringComparer.OrdinalIgnoreCase);
        Current.UnlockedHomeItems ??= new HashSet<string>();
        Current.EquippedHomeItemsBySlot ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Data Migration from old inventory.json if collections.json is fresh
        MigrateOldInventory(logger, dataDirectory);

        var definitions = definitionDirectory ?? Path.Combine(AppContext.BaseDirectory, "GameData");
        
        Cosmetics = new CosmeticCollectionService(this, definitions, logger);
        Toys = new ToyCollectionService(this, definitions, logger);
        Home = new HomeCollectionService(this, definitions, logger);
        HomeBonuses = new HomeBonusService(Home, Current);
        Home.AutoEquipUnlockedItems();
        Save();
    }

    private void MigrateOldInventory(FileLogger logger, string? dataDirectory)
    {
        var oldPath = Path.Combine(dataDirectory ?? AppContext.BaseDirectory, "inventory.json");
        if (File.Exists(oldPath) && Current.UnlockedSkinIds.Count == 0)
        {
            try
            {
                var oldJson = File.ReadAllText(oldPath);
                using var doc = JsonDocument.Parse(oldJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("UnlockedSkinIds", out var unlockedSkins))
                {
                    foreach (var s in unlockedSkins.EnumerateArray()) Current.UnlockedSkinIds.Add(s.GetString()!);
                }
                if (root.TryGetProperty("EquippedSkinId", out var eqSkin))
                {
                    Current.EquippedSkinId = eqSkin.GetString() ?? "standard";
                }
                
                logger.Info("Erfolgreich von altem inventory.json zu collections.json migriert.");
                Save();
            }
            catch (Exception ex)
            {
                logger.Error("Fehler bei Migration von inventory.json", ex);
            }
        }
    }

    public void Save()
    {
        _store.Save(Current);
        Cosmetics?.RefreshState();
        Toys?.RefreshState();
        Home?.RefreshState();

        CollectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
