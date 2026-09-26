namespace DinoDesktopCompanion.Statistics;

public sealed class StatisticsData
{
    public int TotalDinoClicks { get; set; }
    public TimeSpan ActiveTime { get; set; }
    public int UsageDays { get; set; }
    public int FedSnacks { get; set; }
    public int GamesPlayed { get; set; }
    public int ChatMessages { get; set; }
    public int ExpeditionsCompleted { get; set; }
    public int TasksCompleted { get; set; }
    public int ItemsBought { get; set; }
    public RockPaperScissorsStatistics RockPaperScissors { get; set; } = new();
    public int DigSitesCompleted { get; set; }
    public Dictionary<string, int> FoodPreferences { get; set; } = new();
    public Dictionary<string, int> ToyPreferences { get; set; } = new();
}

public sealed class RockPaperScissorsStatistics
{
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
}
