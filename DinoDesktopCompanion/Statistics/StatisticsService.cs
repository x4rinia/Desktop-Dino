using DinoDesktopCompanion.Services;

namespace DinoDesktopCompanion.Statistics;

public sealed class StatisticsService
{
    private readonly JsonFileStore<StatisticsData> _store;
    public StatisticsData Current { get; }
    public event EventHandler? StatisticsChanged;

    public StatisticsService(FileLogger logger, string? dataDirectory = null)
    {
        _store = new JsonFileStore<StatisticsData>("stats.json", logger, dataDirectory);
        Current = _store.Load(() => new StatisticsData());
        Normalize();
        _store.Save(Current);
    }

    public void TrackClick() { Current.TotalDinoClicks++; Changed(); }
    public void TrackSnack(string foodId) { Current.FedSnacks++; IncrementPref(Current.FoodPreferences, foodId); Changed(); }
    public void TrackGame(string toyId) { Current.GamesPlayed++; IncrementPref(Current.ToyPreferences, toyId); Changed(); }
    public void TrackChat() { Current.ChatMessages++; Changed(); }
    public void TrackExpedition() { Current.ExpeditionsCompleted++; Changed(); }
    public void TrackTask() { Current.TasksCompleted++; Changed(); }
    public void TrackPurchase() { Current.ItemsBought++; Changed(); }
    public void TrackRockPaperScissors(RockPaperScissorsResult result)
    {
        Current.GamesPlayed++;
        Current.RockPaperScissors.GamesPlayed++;
        IncrementPref(Current.ToyPreferences, "Stein, Schere, Papier");
        switch (result)
        {
            case RockPaperScissorsResult.Win: Current.RockPaperScissors.Wins++; break;
            case RockPaperScissorsResult.Loss: Current.RockPaperScissors.Losses++; break;
            default: Current.RockPaperScissors.Draws++; break;
        }
        Changed();
    }
    public void TrackDigSiteCompleted() { Current.DigSitesCompleted++; Changed(); }
    public void AddActiveTime(TimeSpan delta) { Current.ActiveTime += delta; Changed(); }
    
    public string GetFavoriteFood() => Current.FoodPreferences.Count > 0 ? Current.FoodPreferences.OrderByDescending(x => x.Value).First().Key : "Noch unbekannt";
    public string GetFavoriteToy() => Current.ToyPreferences.Count > 0 ? Current.ToyPreferences.OrderByDescending(x => x.Value).First().Key : "Noch unbekannt";

    private void IncrementPref(Dictionary<string, int> dict, string key)
    {
        if (dict.ContainsKey(key)) dict[key]++; else dict[key] = 1;
    }

    private void Changed()
    {
        _store.Save(Current);
        StatisticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Normalize()
    {
        Current.RockPaperScissors ??= new RockPaperScissorsStatistics();
        Current.FoodPreferences ??= new Dictionary<string, int>();
        Current.ToyPreferences ??= new Dictionary<string, int>();
    }
}

public enum RockPaperScissorsResult
{
    Win,
    Loss,
    Draw
}
