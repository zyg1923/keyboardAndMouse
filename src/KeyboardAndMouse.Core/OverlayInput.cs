using System.Diagnostics;

namespace KeyboardAndMouse;

internal static class OverlayInput
{
    private static IntPtr _cached;
    private static long _cachedAt;

    public static IntPtr FindSnipasteOverlay()
    {
        if (Environment.TickCount64 - _cachedAt < 150)
            return _cached;

        IntPtr best = IntPtr.Zero;
        var bestArea = 0;
        Native.EnumWindows((hWnd, _) =>
        {
            if (!Native.IsWindowVisible(hWnd))
                return true;
            Native.GetWindowThreadProcessId(hWnd, out var pid);
            if (pid == 0)
                return true;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (!process.ProcessName.StartsWith("Snipaste", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                return true;
            }

            Native.GetWindowRect(hWnd, out var rect);
            var area = Math.Max(0, rect.Right - rect.Left) * Math.Max(0, rect.Bottom - rect.Top);
            if (area > bestArea)
            {
                bestArea = area;
                best = hWnd;
            }

            return true;
        }, IntPtr.Zero);

        Native.GetVirtualScreen(out _, out _, out var vw, out var vh);
        _cached = bestArea >= vw * vh / 8 ? best : IntPtr.Zero;
        _cachedAt = Environment.TickCount64;
        return _cached;
    }

    public static void SendKey(IntPtr hwnd, ushort vk, ushort scan, bool down, bool extended)
    {
        uint lParam = 1u | ((uint)scan << 16);
        if (extended)
            lParam |= 1u << 24;
        if (!down)
            lParam |= 1u << 30 | 1u << 31;
        var msg = down
            ? (vk is Native.VkMenu or Native.VkLMenu or Native.VkRMenu ? Native.WmSysKeyDown : Native.WmKeyDown)
            : (vk is Native.VkMenu or Native.VkLMenu or Native.VkRMenu ? Native.WmSysKeyUp : Native.WmKeyUp);
        Native.PostMessage(hwnd, (uint)msg, new IntPtr(vk), new IntPtr(unchecked((int)lParam)));
    }

    public static void SendMouseMove(IntPtr hwnd, int screenX, int screenY)
    {
        var pt = new POINT { X = screenX, Y = screenY };
        Native.ScreenToClient(hwnd, ref pt);
        Native.PostMessage(hwnd, (uint)Native.WmMouseMove, IntPtr.Zero, MakeLParam(pt.X, pt.Y));
    }

    public static void SendMouseButton(IntPtr hwnd, int screenX, int screenY, byte button, bool down)
    {
        var pt = new POINT { X = screenX, Y = screenY };
        Native.ScreenToClient(hwnd, ref pt);
        uint msg = button switch
        {
            1 => (uint)(down ? Native.WmRButtonDown : Native.WmRButtonUp),
            2 => (uint)(down ? Native.WmMButtonDown : Native.WmMButtonUp),
            _ => (uint)(down ? Native.WmLButtonDown : Native.WmLButtonUp)
        };
        Native.PostMessage(hwnd, msg, IntPtr.Zero, MakeLParam(pt.X, pt.Y));
    }

    private static IntPtr MakeLParam(int x, int y) => new((y << 16) | (x & 0xFFFF));
}
