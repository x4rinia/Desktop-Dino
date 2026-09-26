namespace DinoDesktopCompanion.Dino.Animation;

public sealed class SpriteSequenceDefinition
{
    public string[] Frames { get; set; } = [];
    public int FrameDurationMs { get; set; } = 220;
    public bool Loop { get; set; }
}
