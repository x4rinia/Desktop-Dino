namespace DinoDesktopCompanion.Profiles;

public sealed class ProfileInfo
{
    public const string DefaultProfileColor = "#789E5B";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileName { get; set; } = "Mein Dino";
    public string DinoName { get; set; } = "Dino";
    public int? BirthdayDay { get; set; }
    public int? BirthdayMonth { get; set; }
    public string ProfileColor { get; set; } = DefaultProfileColor;
    public DateTime LastPlayed { get; set; }
    public int SaveVersion { get; set; } = 1;
    public DinoState State { get; set; } = DinoState.Idle;
    public DateTime? SleepStartTime { get; set; }
    public Dictionary<string, int> EventProgress { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> UnlockedTitles { get; set; } = [];
    public string? EquippedTitle { get; set; }
}

public enum DinoState
{
    Idle,
    Sleeping,
    OnExpedition
}
