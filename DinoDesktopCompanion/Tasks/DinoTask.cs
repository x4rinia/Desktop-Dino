namespace DinoDesktopCompanion.Tasks;

public enum DinoTaskStatus { Available, Running, Completed, Claimed }

public sealed class DinoTask
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public int XPReward { get; set; }
    public int CoinReward { get; set; }
    public DinoTaskStatus Status { get; set; } = DinoTaskStatus.Available;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class TaskProgressData
{
    public List<DinoTask> Tasks { get; set; } = [];
}
