using DinoDesktopCompanion.Collections;
using DinoDesktopCompanion.Customization;
using DinoDesktopCompanion.Progress;
using DinoDesktopCompanion.Services;
using DinoDesktopCompanion.Statistics;

namespace DinoDesktopCompanion.Achievements;

public sealed class Achievement
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public int XPReward { get; init; }
    public int CoinReward { get; init; }
    public bool IsSecret { get; init; }
    public string Category { get; init; } = "Abenteuer";
    public int Target { get; init; } = 1;
}

public sealed class AchievementService
{
    private readonly JsonFileStore<AchievementData> _store;
    private readonly ProgressService _progress;
    private readonly StatisticsService _statistics;
    private readonly CollectionManager _collections;
    public AchievementData Current { get; }
    public IReadOnlyList<Achievement> AllAchievements { get; }

    public event EventHandler<Achievement>? AchievementUnlocked;

    public AchievementService(FileLogger logger, ProgressService progress, StatisticsService statistics, CollectionManager collections, string? dataDirectory = null)
    {
        _progress = progress;
        _statistics = statistics;
        _collections = collections;
        _store = new JsonFileStore<AchievementData>("achievements.json", logger, dataDirectory);
        Current = _store.Load(() => new AchievementData());
        _store.Save(Current);
        
        AllAchievements = new List<Achievement>
        {
            new() { Id = "dig_1", Title = "Erste Grabung", Description = "Schließe deine erste Ausgrabung ab.", XPReward = 25, CoinReward = 10, Category = "Abenteuer", Target = 1 },
            new() { Id = "dig_10", Title = "10 Grabungen", Description = "Schließe 10 Ausgrabungen ab.", XPReward = 100, CoinReward = 30, Category = "Abenteuer", Target = 10 },
            new() { Id = "dig_50", Title = "50 Grabungen", Description = "Schließe 50 Ausgrabungen ab.", XPReward = 300, CoinReward = 100, Category = "Abenteuer", Target = 50 },
            new() { Id = "dig_100", Title = "100 Grabungen", Description = "Schließe 100 Ausgrabungen ab.", XPReward = 600, CoinReward = 200, Category = "Abenteuer", Target = 100 },

            new() { Id = "collection_first", Title = "Erster Fund", Description = "Entdecke dein erstes Fundstück.", XPReward = 25, CoinReward = 10, Category = "Sammeln", Target = 1 },
            new() { Id = "collection_10", Title = "10 unterschiedliche Fundstücke", Description = "Entdecke 10 unterschiedliche Fundstücke.", XPReward = 100, CoinReward = 30, Category = "Sammeln", Target = 10 },
            new() { Id = "collection_25", Title = "25 Fundstücke", Description = "Entdecke 25 unterschiedliche Fundstücke.", XPReward = 250, CoinReward = 75, Category = "Sammeln", Target = 25 },
            new() { Id = "area_garten_complete", Title = "Garten komplett", Description = "Entdecke alle Fundstücke im Garten.", XPReward = 200, CoinReward = 60, Category = "Sammeln", Target = 1 },
            new() { Id = "area_wald_complete", Title = "Wald komplett", Description = "Entdecke alle Fundstücke im Wald.", XPReward = 250, CoinReward = 75, Category = "Sammeln", Target = 1 },
            new() { Id = "area_strand_complete", Title = "Strand komplett", Description = "Entdecke alle Fundstücke am Strand.", XPReward = 300, CoinReward = 90, Category = "Sammeln", Target = 1 },
            new() { Id = "area_hoehle_complete", Title = "Höhle komplett", Description = "Entdecke alle Fundstücke in der Höhle.", XPReward = 350, CoinReward = 110, Category = "Sammeln", Target = 1 },
            new() { Id = "area_schnee_complete", Title = "Schnee komplett", Description = "Entdecke alle Fundstücke im Schneegebiet.", XPReward = 400, CoinReward = 130, Category = "Sammeln", Target = 1 },
            new() { Id = "collection_rare", Title = "Erster seltener Fund", Description = "Entdecke dein erstes seltenes Fundstück.", XPReward = 100, CoinReward = 30, Category = "Sammeln", Target = 1 },
            new() { Id = "collection_epic", Title = "Erster epischer Fund", Description = "Entdecke dein erstes episches Fundstück.", XPReward = 200, CoinReward = 60, Category = "Sammeln", Target = 1 },
            new() { Id = "collection_legendary", Title = "Erster legendärer Fund", Description = "Entdecke dein erstes legendäres Fundstück.", XPReward = 350, CoinReward = 100, Category = "Sammeln", Target = 1 },
            new() { Id = "collection_complete", Title = "Komplette Sammlung", Description = "Vervollständige das gesamte Sammelalbum.", XPReward = 1000, CoinReward = 300, Category = "Sammeln", Target = 1 },

            new() { Id = "secret_first_skin", Title = "Erster zusätzlicher Skin", Description = "Schalte deinen ersten zusätzlichen Skin frei.", XPReward = 100, CoinReward = 20, Category = "Sammeln", Target = 1 },
            new() { Id = "skins_5", Title = "5 Skins", Description = "Schalte insgesamt 5 Skins frei.", XPReward = 250, CoinReward = 50, Category = "Sammeln", Target = 5 },
            new() { Id = "all_standard_colors", Title = "Alle Standardfarben", Description = "Sammle alle Standard-Farbskins.", XPReward = 500, CoinReward = 150, Category = "Sammeln", Target = 8 },
            new() { Id = "epic_skin", Title = "Erster epischer Skin", Description = "Schalte deinen ersten epischen Skin frei.", XPReward = 500, CoinReward = 150, Category = "Sammeln", Target = 1 },

            new() { Id = "home_first", Title = "Erstes Möbelstück", Description = "Kaufe dein erstes Möbelstück.", XPReward = 50, CoinReward = 15, Category = "Zuhause", Target = 1 },
            new() { Id = "home_5", Title = "5 Hausobjekte", Description = "Besitze 5 Objekte für das Dinohaus.", XPReward = 200, CoinReward = 60, Category = "Zuhause", Target = 5 },
            new() { Id = "home_half", Title = "Haus zu 50 %", Description = "Besitze mindestens die Hälfte aller Hausobjekte.", XPReward = 300, CoinReward = 90, Category = "Zuhause", Target = 1 },
            new() { Id = "home_complete", Title = "Haus komplett", Description = "Besitze alle Hausobjekte.", XPReward = 600, CoinReward = 180, Category = "Zuhause", Target = 1 },

            new() { Id = "level_5", Title = "Level 5", Description = "Erreiche Level 5.", XPReward = 100, CoinReward = 30, Category = "Abenteuer", Target = 5 },
            new() { Id = "level_10", Title = "Level 10", Description = "Erreiche Level 10.", XPReward = 250, CoinReward = 75, Category = "Abenteuer", Target = 10 },
            new() { Id = "level_20", Title = "Level 20", Description = "Erreiche Level 20.", XPReward = 500, CoinReward = 150, Category = "Abenteuer", Target = 20 },
        };

        _statistics.StatisticsChanged += CheckConditions;
        _progress.LevelUp += (_, _) => CheckConditions(this, EventArgs.Empty);
        _collections.CollectionChanged += CheckConditions;
        CheckConditions(this, EventArgs.Empty);
    }

    private void CheckConditions(object? sender, EventArgs e)
    {
        var stats = _statistics.Current;
        var unlockedFinds = _collections.Current.UnlockedToys;
        var unlockedSkins = _collections.Current.UnlockedSkinIds;
        var unlockedHomeItems = _collections.Current.UnlockedHomeItems;

        TryUnlock("dig_1", stats.DigSitesCompleted >= 1);
        TryUnlock("dig_10", stats.DigSitesCompleted >= 10);
        TryUnlock("dig_50", stats.DigSitesCompleted >= 50);
        TryUnlock("dig_100", stats.DigSitesCompleted >= 100);

        TryUnlock("collection_first", unlockedFinds.Count >= 1);
        TryUnlock("collection_10", unlockedFinds.Count >= 10);
        TryUnlock("collection_25", unlockedFinds.Count >= 25);
        TryUnlock("area_garten_complete", IsAreaComplete("garten"));
        TryUnlock("area_wald_complete", IsAreaComplete("wald"));
        TryUnlock("area_strand_complete", IsAreaComplete("strand"));
        TryUnlock("area_hoehle_complete", IsAreaComplete("hoehle"));
        TryUnlock("area_schnee_complete", IsAreaComplete("schneeland"));
        TryUnlock("collection_rare", HasFindOfRarity("Selten"));
        TryUnlock("collection_epic", HasFindOfRarity("Episch"));
        TryUnlock("collection_legendary", HasFindOfRarity("Legendär"));
        TryUnlock("collection_complete", _collections.Toys.Items.Count > 0 && _collections.Toys.Items.All(item => unlockedFinds.Contains(item.Id)));



        var defaultSkinIds = _collections.Cosmetics.Skins.Where(skin => skin.UnlockType == SkinUnlockType.Default).Select(skin => skin.Id).ToHashSet();
        TryUnlock("secret_first_skin", unlockedSkins.Any(id => !defaultSkinIds.Contains(id)));
        TryUnlock("skins_5", unlockedSkins.Count >= 5);
        TryUnlock("all_standard_colors", StandardColorIds.All(unlockedSkins.Contains));
        TryUnlock("epic_skin", _collections.Cosmetics.Skins.Any(skin => unlockedSkins.Contains(skin.Id) && string.Equals(skin.Rarity, "Episch", StringComparison.OrdinalIgnoreCase)));

        var homeItemCount = _collections.Home.Items.Count;
        TryUnlock("home_first", unlockedHomeItems.Count >= 1);
        TryUnlock("home_5", unlockedHomeItems.Count >= 5);
        TryUnlock("home_half", homeItemCount > 0 && unlockedHomeItems.Count * 2 >= homeItemCount);
        TryUnlock("home_complete", homeItemCount > 0 && _collections.Home.Items.All(item => unlockedHomeItems.Contains(item.Id)));

        TryUnlock("level_5", _progress.Current.Level >= 5);
        TryUnlock("level_10", _progress.Current.Level >= 10);
        TryUnlock("level_20", _progress.Current.Level >= 20);
    }

    private static readonly string[] StandardColorIds = ["standard", "blue", "red", "yellow", "cyan", "purple", "orange", "pink"];

    private bool IsAreaComplete(string areaId)
    {
        var items = _collections.Toys.Items.Where(item => string.Equals(item.AreaId, areaId, StringComparison.OrdinalIgnoreCase)).ToList();
        return items.Count > 0 && items.All(item => _collections.Current.UnlockedToys.Contains(item.Id));
    }

    private bool HasFindOfRarity(string rarity) => _collections.Toys.Items.Any(item =>
        _collections.Current.UnlockedToys.Contains(item.Id) && string.Equals(item.Rarity, rarity, StringComparison.OrdinalIgnoreCase));

    public void CheckCustomCondition(string id) => TryUnlock(id, true);

    public int GetProgress(Achievement ach)
    {
        if (Current.UnlockedAchievements.Contains(ach.Id)) return ach.Target;
        
        var stats = _statistics.Current;
        var unlockedFinds = _collections.Current.UnlockedToys;
        var unlockedSkins = _collections.Current.UnlockedSkinIds;
        var unlockedHomeItems = _collections.Current.UnlockedHomeItems;

        if (ach.Id.StartsWith("dig_")) return Math.Min(stats.DigSitesCompleted, ach.Target);
        if (ach.Id == "collection_10" || ach.Id == "collection_25" || ach.Id == "collection_first") return Math.Min(unlockedFinds.Count, ach.Target);
        if (ach.Id.StartsWith("area_") && ach.Id.EndsWith("_complete"))
        {
            var areaId = ach.Id.Replace("area_", "").Replace("_complete", "");
            if (areaId == "schnee") areaId = "schneeland";
            var items = _collections.Toys.Items.Where(item => string.Equals(item.AreaId, areaId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (items.Count == 0) return 0;
            var unlocked = items.Count(item => unlockedFinds.Contains(item.Id));
            // Just return 0 or 1 since target is 1, but we can do ratio. Wait, Target is 1 for area complete.
            return unlocked == items.Count ? 1 : 0;
        }
        if (ach.Id.StartsWith("collection_")) return HasFindOfRarity(ach.Id.Replace("collection_", "")) ? 1 : 0;
        if (ach.Id == "skins_5") return Math.Min(unlockedSkins.Count, ach.Target);
        if (ach.Id == "all_standard_colors") return Math.Min(StandardColorIds.Count(unlockedSkins.Contains), ach.Target);
        if (ach.Id == "home_first" || ach.Id == "home_5") return Math.Min(unlockedHomeItems.Count, ach.Target);
        if (ach.Id.StartsWith("level_")) return Math.Min(_progress.Current.Level, ach.Target);

        return 0; // default for others
    }

    private void TryUnlock(string id, bool condition)
    {
        if (!condition || Current.UnlockedAchievements.Contains(id)) return;
        var ach = AllAchievements.FirstOrDefault(a => a.Id == id);
        if (ach is null) return;

        Current.UnlockedAchievements.Add(id);
        _store.Save(Current);
        
        if (ach.XPReward > 0) _progress.AddXP(ach.XPReward, $"Achievement:{id}");
        if (ach.CoinReward > 0) _progress.AddCoins(ach.CoinReward, $"Achievement:{id}");
        
        AchievementUnlocked?.Invoke(this, ach);
    }
}
