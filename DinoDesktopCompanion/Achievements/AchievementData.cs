namespace DinoDesktopCompanion.Achievements;

public sealed class AchievementData
{
    public HashSet<string> UnlockedAchievements { get; set; } = new();
}
