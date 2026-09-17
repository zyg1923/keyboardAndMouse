using System.Runtime.InteropServices;

namespace KeyboardAndMouse;

internal static class Native
{
    public const int WhKeyboardLl = 13;
    public const int WhMouseLl = 14;
    public const int WmQuit = 0x0012;
    public const int WmKeyDown = 0x0100;
    public const int WmKeyUp = 0x0101;
    public const int WmSysKeyDown = 0x0104;
    public const int WmSysKeyUp = 0x0105;
    public const int WmMouseMove = 0x0200;
    public const int WmLButtonDown = 0x0201;
    public const int WmLButtonUp = 0x0202;
    public const int WmRButtonDown = 0x0204;
    public const int WmRButtonUp = 0x0205;
    public const int WmMButtonDown = 0x0207;
    public const int WmMButtonUp = 0x0208;
    public const int WmMouseWheel = 0x020A;
    public const int WmXButtonDown = 0x020B;
    public const int WmXButtonUp = 0x020C;
    public const int WmMouseHWheel = 0x020E;

    public const uint LlkfInjected = 0x10;
    public const uint LlkfExtended = 0x01;
    public const uint LlkfUp = 0x80;
    public const uint LlmhfInjected = 0x01;

    public const uint InputMouse = 0;
    public const uint InputKeyboard = 1;

    public const uint MouseeventfMove = 0x0001;
    public const uint MouseeventfLeftdown = 0x0002;
    public const uint MouseeventfLeftup = 0x0004;
    public const uint MouseeventfRightdown = 0x0008;
    public const uint MouseeventfRightup = 0x0010;
    public const uint MouseeventfMiddledown = 0x0020;
    public const uint MouseeventfMiddleup = 0x0040;
    public const uint MouseeventfXdown = 0x0080;
    public const uint MouseeventfXup = 0x0100;
    public const uint MouseeventfWheel = 0x0800;
    public const uint MouseeventfHwheel = 0x1000;
    public const uint MouseeventfMoveNoCoalesce = 0x2000;
    public const uint MouseeventfVirtualDesk = 0x4000;
    public const uint MouseeventfAbsolute = 0x8000;

    public const uint KeyeventfExtendedkey = 0x0001;
    public const uint KeyeventfKeyup = 0x0002;
    public const uint KeyeventfScancode = 0x0008;
    public const uint Xbutton1 = 0x0001;
    public const uint Xbutton2 = 0x0002;

    public const ushort VkControl = 0x11;
    public const ushort VkMenu = 0x12;
    public const ushort VkLControl = 0xA2;
    public const ushort VkRControl = 0xA3;
    public const ushort VkLMenu = 0xA4;
    public const ushort VkRMenu = 0xA5;
    public const ushort VkEscape = 0x1B;
    public const ushort VkSnapshot = 0x2C;
    public const ushort VkLWin = 0x5B;
    public const ushort VkRWin = 0x5C;
    public const ushort VkApps = 0x5D;
    public const ushort VkQ = 0x51;
    public const ushort VkX = 0x58;

    public static readonly UIntPtr ExtraInfoTag = new(0x4B4D4D31);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClipCursor(IntPtr lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll")]
    public static extern uint GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

    public const uint RidIDeviceName = 0x20000007;
    public const uint RimTypeKeyboard = 1;
    public const uint RiKeyBreak = 1;
    public const uint RiKeyE0 = 2;
    public const int CrSuccess = 0;
    public const uint DevpropTypeString = 0x00000012;

    public static readonly DEVPROPKEY DevpkeyName = new(
        new Guid(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac), 10);
    public static readonly DEVPROPKEY DevpkeyFriendlyName = new(
        new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0), 14);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_PropertyW(
        string pszDeviceInterface,
        in DEVPROPKEY propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        ref uint propertyBufferSize,
        uint ulFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BlockInput([MarshalAs(UnmanagedType.Bool)] bool fBlockIt);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    public const uint GaRoot = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("winmm.dll")]
    public static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll")]
    public static extern uint timeEndPeriod(uint uMilliseconds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    public static void GetVirtualScreen(out int x, out int y, out int w, out int h)
    {
        x = GetSystemMetrics(76);
        y = GetSystemMetrics(77);
        w = GetSystemMetrics(78);
        h = GetSystemMetrics(79);
        if (w <= 0) w = 1;
        if (h <= 0) h = 1;
    }

    public static ushort NormalizeToAbs(int pos, int origin, int size)
    {
        if (size <= 1)
            return 0;
        var value = (int)Math.Round((pos - origin) * 65535.0 / (size - 1));
        return (ushort)Math.Clamp(value, 0, 65535);
    }

    public static void WithForegroundAttached(Action action)
    {
        var foreground = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);
        var attached = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attached = AttachThreadInput(currentThread, foregroundThread, true);
            action();
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    public static void RunMessageLoop()
    {
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }
}

public static class AppHost
{
    public static void SetDpiAware() => Native.SetProcessDPIAware();

    public static void RunMessageLoop() => Native.RunMessageLoop();

    public static void RequestQuit() => Native.PostQuitMessage(0);

    public static IntPtr GetForegroundWindow() => Native.GetForegroundWindow();

    public static bool SetForegroundWindow(IntPtr hWnd) => Native.SetForegroundWindow(hWnd);

    public static void BeginHighResTimer() => Native.timeBeginPeriod(1);

    public static void EndHighResTimer() => Native.timeEndPeriod(1);
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion U;
    public static int Size => Marshal.SizeOf<INPUT>();
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICELIST
{
    public IntPtr hDevice;
    public uint dwType;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DEVPROPKEY
{
    public Guid fmtid;
    public uint pid;

    public DEVPROPKEY(Guid fmtid, uint pid)
    {
        this.fmtid = fmtid;
        this.pid = pid;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWKEYBOARD
{
    public ushort MakeCode;
    public ushort Flags;
    public ushort Reserved;
    public ushort VKey;
    public uint Message;
    public uint ExtraInformation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTDEVICE
{
    public ushort usUsagePage;
    public ushort usUsage;
    public uint dwFlags;
    public IntPtr hwndTarget;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUTHEADER
{
    public uint dwType;
    public uint dwSize;
    public IntPtr hDevice;
    public IntPtr wParam;
}

[StructLayout(LayoutKind.Explicit)]
internal struct RAWMOUSE
{
    [FieldOffset(0)] public ushort usFlags;
    [FieldOffset(4)] public uint ulButtons;
    [FieldOffset(4)] public ushort usButtonFlags;
    [FieldOffset(6)] public ushort usButtonData;
    [FieldOffset(8)] public uint ulRawButtons;
    [FieldOffset(12)] public int lLastX;
    [FieldOffset(16)] public int lLastY;
    [FieldOffset(20)] public uint ulExtraInformation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RAWINPUT
{
    public RAWINPUTHEADER header;
    public RAWMOUSE mouse;
}
