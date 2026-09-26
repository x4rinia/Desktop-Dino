namespace DinoDesktopCompanion.Expeditions;

public sealed class ExpeditionData
{
    public List<Expedition> Expeditions { get; set; } = new();
}

public sealed class Expedition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string AreaId { get; set; } = "garten";
    public int AdventurePointCost { get; set; } = 1;
    public int MinLevel { get; set; } = 1;
    public List<string> PossibleRarities { get; set; } = new();
    
    public TimeSpan Duration { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public bool IsRunning { get; set; }
    public bool IsCompleted { get; set; }
    public int XPReward { get; set; }
    public int CoinReward { get; set; }
    public string ConsumableReward { get; set; } = "";
    public List<string> PossibleRewards { get; set; } = new();
}
