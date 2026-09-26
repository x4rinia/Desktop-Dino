using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace DinoDesktopCompanion.Services;

public static class FullScreenDetector
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();

    public static bool IsForegroundFullScreen(Window? referenceWindow = null)
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero || handle == GetShellWindow() || !GetWindowRect(handle, out var rect)) return false;
        var screen = Forms.Screen.FromHandle(handle).Bounds;

        if (referenceWindow is not null)
        {
            var referenceHandle = new WindowInteropHelper(referenceWindow).Handle;
            if (referenceHandle != IntPtr.Zero
                && !string.Equals(Forms.Screen.FromHandle(handle).DeviceName, Forms.Screen.FromHandle(referenceHandle).DeviceName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return rect.Left <= screen.Left && rect.Top <= screen.Top && rect.Right >= screen.Right && rect.Bottom >= screen.Bottom;
    }
}
