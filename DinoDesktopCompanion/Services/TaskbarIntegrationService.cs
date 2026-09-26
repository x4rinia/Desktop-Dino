using System.Runtime.InteropServices;

namespace DinoDesktopCompanion.Services;

public static class TaskbarIntegrationService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    public static void Initialize()
    {
        try { SetCurrentProcessExplicitAppUserModelID("DinoDesktopCompanion.DesktopPet"); }
        catch { }
    }
}
