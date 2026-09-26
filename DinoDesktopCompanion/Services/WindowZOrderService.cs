using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DinoDesktopCompanion.Services;

public static class WindowZOrderService
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const uint NoMove = 0x0002, NoSize = 0x0001, NoActivate = 0x0010;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    public static void Apply(Window window, bool alwaysOnTop)
    {
        window.Topmost = alwaysOnTop;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero) SetWindowPos(handle, alwaysOnTop ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0, NoMove | NoSize | NoActivate);
    }

    public static void BringToFront(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero) SetWindowPos(handle, HwndTop, 0, 0, 0, 0, NoMove | NoSize | NoActivate);
    }
}
