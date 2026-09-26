namespace DinoDesktopCompanion.Configuration;

public sealed class AppConfig
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public string Monitor { get; set; } = "";
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool CanRoam { get; set; } = true;
    public string MouseFollow { get; set; } = "Aus";
    public bool SpeechBubbles { get; set; } = true;
    public bool RandomMessages { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool QuietMode { get; set; }
    public bool HideInFullscreen { get; set; } = true;
    public string SleepBehavior { get; set; } = "Ecke";
    public string Activity { get; set; } = "Ruhig";
    public int SleepAfterMinutes { get; set; } = 20;
    public int MessageFrequencyMinutes { get; set; } = 8;
    public bool GpuDinoEnabled { get; set; }
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "";
    public double DinoScale { get; set; } = 1.0;
    public double DinoSize { get; set; } = 230;
    public double AnimationSpeed { get; set; } = 1.0;
}
