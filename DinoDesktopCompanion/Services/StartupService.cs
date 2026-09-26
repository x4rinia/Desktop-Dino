using Microsoft.Win32;

namespace DinoDesktopCompanion.Services;

public static class StartupService
{
    private const string Name = "Dino Desktop Companion";
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (enabled) key?.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else key?.DeleteValue(Name, false);
    }
}
