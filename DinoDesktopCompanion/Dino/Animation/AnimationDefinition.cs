namespace DinoDesktopCompanion.Dino.Animation;

public sealed class AnimationDefinition
{
    public int DurationMs { get; set; } = 1800;
    public double Bob { get; set; } = 2;
    public double Rotate { get; set; }
    public int RepeatCount { get; set; } = 1;
}
