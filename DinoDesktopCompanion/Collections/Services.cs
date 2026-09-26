using System.Text.Json;
using System.Text.Json.Serialization;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Statistics;
using DinoDesktopCompanion.Core;
using DinoDesktopCompanion.Customization;

namespace DinoDesktopCompanion.Collections;

public static class CollectionLoader
{
    public static List<T> LoadDefinitions<T>(string directory, string fileName, FileLogger logger)
    {
        try
        {
            var path = Path.Combine(directory, fileName);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
            return File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), options) ?? [] : [];
        }
        catch (Exception ex)
        {
            logger.Error($"Definitionen aus {fileName} konnten nicht geladen werden.", ex);
            return [];
        }
    }
}

public class CosmeticCollectionService
{
    private readonly CollectionManager _manager;
    private bool _evaluating;
    private static readonly string[] StandardColorIds = ["standard", "blue", "red", "yellow", "cyan", "purple", "orange", "pink"];
    public IReadOnlyList<SkinDefinition> Skins { get; }

    public CosmeticCollectionService(CollectionManager manager, string defDir, FileLogger logger)
    {
        _manager = manager;
        Skins = CollectionLoader.LoadDefinitions<SkinDefinition>(defDir, "skins.json", logger);
        EnsureDefaultSkin();
    }

    private void EnsureDefaultSkin()
    {
        if (Skins.Count > 0)
        {
            var standard = Skins.FirstOrDefault(s => s.UnlockType == SkinUnlockType.Default) ?? Skins.OrderBy(s => s.UnlockLevel).First();
            var changed = false;
            if (_manager.Current.SkinUnlockMigrationVersion < 1)
            {
                changed |= _manager.Current.UnlockedSkinIds.Remove("blue");
                changed |= _manager.Current.UnlockedSkinIds.Remove("rainbow");
                _manager.Current.SkinUnlockMigrationVersion = 1;
                changed = true;
            }
            changed |= _manager.Current.UnlockedSkinIds.Add(standard.Id);
            if (string.IsNullOrWhiteSpace(_manager.Current.EquippedSkinId) || !_manager.Current.UnlockedSkinIds.Contains(_manager.Current.EquippedSkinId)) 
            {
                _manager.Current.EquippedSkinId = standard.Id;
                changed = true;
            }
            if (changed) _manager.Save();
        }
    }

    public void EvaluateUnlocks(ProgressService progress, StatisticsService statistics, AreaService areas)
    {
        if (_evaluating) return;
        _evaluating = true;
        try
        {
            var changed = false;
            foreach (var skin in Skins.Where(s => s.UnlockType is not (SkinUnlockType.Default or SkinUnlockType.Coins or SkinUnlockType.Event)))
                if (MeetsCondition(skin, progress, statistics, areas)) changed |= _manager.Current.UnlockedSkinIds.Add(skin.Id);
            if (changed) _manager.Save();
            else RefreshState();
        }
        finally { _evaluating = false; }
    }

    public bool TryPurchase(SkinDefinition skin, ProgressService progress)
    {
        if (skin.UnlockType != SkinUnlockType.Coins || skin.Cost <= 0
            || progress.Current.Level < skin.UnlockLevel || _manager.Current.UnlockedSkinIds.Contains(skin.Id)) return false;
        if (!progress.TrySpendCoins(skin.Cost, $"Skin:{skin.Id}")) return false;
        _manager.Current.UnlockedSkinIds.Add(skin.Id);
        _manager.Save();
        return true;
    }

    public string GetUnlockText(SkinDefinition skin) => skin.UnlockCondition ?? (skin.UnlockType switch
    {
        SkinUnlockType.Default => "Von Anfang an verfügbar",
        SkinUnlockType.Coins when skin.UnlockLevel > 1 => $"Level {skin.UnlockLevel} + {skin.Cost} Dino Coins",
        SkinUnlockType.Coins => $"{skin.Cost} Dino Coins",
        SkinUnlockType.Level => $"Erreiche Level {skin.UnlockLevel}",
        SkinUnlockType.DigCount => $"Schließe {skin.RequiredCount} Ausgrabungen ab",
        SkinUnlockType.AreaCollection => $"Entdecke {skin.RequiredCount} unterschiedliche Funde in {skin.RequiredAreaId}",
        SkinUnlockType.CollectionProgress => $"Entdecke {skin.RequiredCount} unterschiedliche Sammelobjekte",
        SkinUnlockType.SpecificFind => "Entdecke den benötigten besonderen Fund",
        SkinUnlockType.AllAreasUnlocked => "Schalte alle Gebiete frei",
        SkinUnlockType.AllStandardSkins => "Besitze alle Standard-Farbskins",
        SkinUnlockType.FullCollection => "Vervollständige die Sammlung zu 100 %",
        _ => "Besondere Freischaltung"
    });

    private bool MeetsCondition(SkinDefinition skin, ProgressService progress, StatisticsService statistics, AreaService areas)
    {
        if (progress.Current.Level < skin.UnlockLevel) return false;
        var unlockedFinds = _manager.Current.UnlockedToys;
        return skin.UnlockType switch
        {
            SkinUnlockType.Level => true,
            SkinUnlockType.CollectionProgress when skin.RequiredRarities.Count > 0 =>
                SkinsCountByRarity(skin.RequiredRarities) >= skin.RequiredCount,
            SkinUnlockType.CollectionProgress => unlockedFinds.Count >= skin.RequiredCount,
            SkinUnlockType.AreaCollection => _manager.Toys.Items.Count(item => item.AreaId == skin.RequiredAreaId && unlockedFinds.Contains(item.Id)) >= skin.RequiredCount,
            SkinUnlockType.SpecificFind => skin.RequiredFindIds.Any(unlockedFinds.Contains),
            SkinUnlockType.DigCount => statistics.Current.DigSitesCompleted >= skin.RequiredCount,
            SkinUnlockType.AllAreasUnlocked => areas.Current.Areas.All(area => progress.Current.Level >= area.MinLevel),
            SkinUnlockType.AllStandardSkins => StandardColorIds.All(_manager.Current.UnlockedSkinIds.Contains),
            SkinUnlockType.FullCollection => _manager.Toys.Items.Count > 0 && _manager.Toys.Items.All(item => unlockedFinds.Contains(item.Id)),
            _ => false
        };
    }

    private int SkinsCountByRarity(IReadOnlyCollection<string> rarities) => _manager.Toys.Items.Count(item =>
        _manager.Current.UnlockedToys.Contains(item.Id) && rarities.Contains(item.Rarity, StringComparer.OrdinalIgnoreCase));

    public void RefreshState()
    {
        foreach (var skin in Skins) 
        { 
            skin.IsUnlocked = _manager.Current.UnlockedSkinIds.Contains(skin.Id); 
            skin.IsEquipped = skin.Id == _manager.Current.EquippedSkinId; 
        }
    }

    public bool UnlockSkin(string id) 
    { 
        if (_manager.Current.UnlockedSkinIds.Add(id)) 
        { 
            _manager.Save(); 
            return true; 
        } 
        return false; 
    }

    public bool EquipSkin(string id)
    {
        if (!_manager.Current.UnlockedSkinIds.Contains(id) || Skins.All(s => s.Id != id)) return false;
        _manager.Current.EquippedSkinId = id; 
        _manager.Save(); 
        return true;
    }
}

public class ToyCollectionService
{
    private readonly CollectionManager _manager;
    
    // Using AlbumEntryDefinition temporarily since it has AreaId and Rarity.
    public IReadOnlyList<AlbumEntryDefinition> Items { get; }

    public ToyCollectionService(CollectionManager manager, string defDir, FileLogger logger) 
    { 
        _manager = manager; 
        Items = CollectionLoader.LoadDefinitions<AlbumEntryDefinition>(defDir, "toys.json", logger);
    }
    
    public void RefreshState() 
    {
        foreach (var item in Items)
        {
            item.IsDiscovered = _manager.Current.UnlockedToys.Contains(item.Id);
            item.FirstFoundAt = _manager.Current.ToyFirstFoundAt.TryGetValue(item.Id, out var foundAt)
                ? foundAt
                : null;
        }
    }

    public bool Unlock(string id)
    {
        if (_manager.Current.UnlockedToys.Add(id))
        {
            _manager.Current.ToyFirstFoundAt[id] = DateTimeOffset.Now;
            _manager.Save();
            return true;
        }
        return false;
    }
}

public class HomeCollectionService
{
    private readonly CollectionManager _manager;
    public IReadOnlyList<HouseLayoutDefinition> Houses { get; }
    public IReadOnlyList<HomeItemDefinition> Items { get; }

    public HomeCollectionService(CollectionManager manager, string defDir, FileLogger logger)
    {
        _manager = manager;
        Houses = CollectionLoader.LoadDefinitions<HouseLayoutDefinition>(defDir, "houses.json", logger);
        Items = CollectionLoader.LoadDefinitions<HomeItemDefinition>(defDir, "home_items.json", logger);
    }

    public void RefreshState()
    {
        foreach(var item in Items)
        {
            item.IsUnlocked = _manager.Current.UnlockedHomeItems.Contains(item.Id);
            item.IsEquipped = _manager.Current.EquippedHomeItemsBySlot.TryGetValue(item.SlotId, out var equipped) && equipped == item.Id;
        }
    }

    public bool UnlockItem(string id)
    {
        var item = Items.FirstOrDefault(candidate => candidate.Id == id);
        if (item == null || !_manager.Current.UnlockedHomeItems.Add(id)) return false;
        _manager.Current.EquippedHomeItemsBySlot[item.SlotId] = id;
        _manager.Save();
        return true;
    }

    internal bool AutoEquipUnlockedItems()
    {
        var changed = false;
        foreach (var group in Items.GroupBy(item => item.SlotId, StringComparer.OrdinalIgnoreCase))
        {
            if (_manager.Current.EquippedHomeItemsBySlot.TryGetValue(group.Key, out var equippedId)
                && _manager.Current.UnlockedHomeItems.Contains(equippedId)
                && group.Any(item => item.Id == equippedId)) continue;

            var unlocked = group.Where(item => _manager.Current.UnlockedHomeItems.Contains(item.Id))
                .OrderByDescending(item => item.RequiredLevel)
                .ThenByDescending(item => item.Cost)
                .FirstOrDefault();
            if (unlocked == null) continue;
            _manager.Current.EquippedHomeItemsBySlot[group.Key] = unlocked.Id;
            changed = true;
        }
        return changed;
    }

    public bool EquipItem(string id)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item == null || !_manager.Current.UnlockedHomeItems.Contains(id)) return false;
        _manager.Current.EquippedHomeItemsBySlot[item.SlotId] = id;
        _manager.Save();
        return true;
    }

    public HomePurchaseResult PurchaseItem(string id, ProgressService progress)
    {
        var item = Items.FirstOrDefault(candidate => candidate.Id == id);
        if (item == null) return HomePurchaseResult.NotFound;
        if (_manager.Current.UnlockedHomeItems.Contains(id)) return HomePurchaseResult.AlreadyPurchased;
        if (progress.Current.Level < item.RequiredLevel) return HomePurchaseResult.LevelTooLow;
        if (item.Cost > 0 && !progress.TrySpendCoins(item.Cost, $"Dinohaus:{id}")) return HomePurchaseResult.NotEnoughCoins;

        if (UnlockItem(id)) return HomePurchaseResult.Success;
        if (item.Cost > 0) progress.AddCoins(item.Cost, $"Dinohaus-Rückerstattung:{id}");
        return HomePurchaseResult.AlreadyPurchased;
    }
    
    public int GetUnlockedCount() => _manager.Current.UnlockedHomeItems.Count;
}

public enum HomePurchaseResult
{
    Success,
    NotFound,
    LevelTooLow,
    NotEnoughCoins,
    AlreadyPurchased
}
