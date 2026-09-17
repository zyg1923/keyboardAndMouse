using System.Runtime.InteropServices;

namespace KeyboardAndMouse.App;

internal static class SingleInstance
{
    private const string MutexName = @"Local\KeyboardAndMouse.App.SingleInstance";
    private const string ShowMessageName = "KeyboardAndMouse.App.ShowMe";
    private static Mutex? _mutex;

    public static readonly uint ShowMessage = RegisterWindowMessage(ShowMessageName);

    public static bool TryOwn()
    {
        _mutex = new Mutex(true, MutexName, out var created);
        if (created)
            return true;

        _mutex.Dispose();
        _mutex = null;
        PostMessage((IntPtr)0xFFFF, ShowMessage, IntPtr.Zero, IntPtr.Zero);
        return false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
