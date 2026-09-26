using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.Services;

public static class MonitorService
{
    public static void RestoreOrCenter(System.Windows.Window window, Configuration.AppConfig config)
    {
        var saved = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == config.Monitor);
        if (saved is not null && double.IsFinite(config.Left) && double.IsFinite(config.Top))
        {
            window.Left = config.Left; window.Top = config.Top;
        }
        else
        {
            var area = Forms.Screen.PrimaryScreen!.WorkingArea;
            window.Left = area.Right - window.Width - 40; window.Top = area.Bottom - window.Height - 30;
        }
        KeepVisible(window);
    }

    public static void KeepVisible(System.Windows.Window window)
    {
        var center = new System.Drawing.Point((int)(window.Left + window.Width / 2), (int)(window.Top + window.Height / 2));
        var area = Forms.Screen.FromPoint(center).WorkingArea;
        window.Left = Math.Clamp(window.Left, area.Left, Math.Max(area.Left, area.Right - window.Width));
        window.Top = Math.Clamp(window.Top, area.Top, Math.Max(area.Top, area.Bottom - window.Height));
    }

    public static void SavePosition(System.Windows.Window window, Configuration.AppConfig config)
    {
        var center = new System.Drawing.Point((int)(window.Left + window.Width / 2), (int)(window.Top + window.Height / 2));
        config.Left = window.Left; config.Top = window.Top; config.Monitor = Forms.Screen.FromPoint(center).DeviceName;
    }

    public static void CallToCursor(System.Windows.Window window)
    {
        var cursor = Forms.Control.MousePosition;
        var area = Forms.Screen.FromPoint(cursor).WorkingArea;
        window.Left = Math.Clamp(cursor.X - window.Width / 2, area.Left, area.Right - window.Width);
        window.Top = Math.Clamp(cursor.Y - window.Height + 20, area.Top, area.Bottom - window.Height);
        window.Show(); window.Activate();
    }

    public static void MoveToMonitor(System.Windows.Window window, string deviceName)
    {
        var screen = Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName);
        if (screen is null) return;
        var area = screen.WorkingArea;
        window.Left = area.Left + (area.Width - window.Width) / 2;
        window.Top = area.Bottom - window.Height - 30;
        KeepVisible(window);
    }
}
