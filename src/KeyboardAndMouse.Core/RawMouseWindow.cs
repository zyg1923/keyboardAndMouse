using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyboardAndMouse;

internal sealed class RawMouseWindow : NativeWindow, IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevNoLegacy = 0x00000030;
    private const uint RidevNoHotkeys = 0x00000200;
    private const uint RidevRemove = 0x00000001;
    private const uint RimTypeMouse = 0;
    private const uint RimTypeKeyboard = 1;
    private const ushort MouseMoveAbsolute = 0x01;
    private bool _keyboardNoLegacy;
    private bool _disposed;

    public event Action<int, int, bool>? Moved;
    public event Action<IntPtr, ushort, ushort, bool, bool>? Key;

    public RawMouseWindow()
    {
        try
        {
            CreateHandle(new CreateParams
            {
                Caption = "KmmRawInput",
                Parent = new IntPtr(-3)
            });
        }
        catch
        {
            CreateHandle(new CreateParams
            {
                Caption = "KmmRawInput",
                X = 0,
                Y = 0,
                Width = 1,
                Height = 1,
                Style = unchecked((int)0x80000000)
            });
        }
        Register(sink: true, keyboardNoLegacy: false);
    }

    public void SetKeyboardNoLegacy(bool enabled)
    {
        if (_disposed || _keyboardNoLegacy == enabled)
            return;
        // 必须先 REMOVE 再重新注册，否则 NOLEGACY 关不干净，本机键盘会一直失灵。
        UnregisterAll();
        _keyboardNoLegacy = enabled;
        Register(sink: true, keyboardNoLegacy: enabled);
    }

    public void RestoreLegacyKeyboard()
    {
        if (_disposed)
            return;
        UnregisterAll();
        _keyboardNoLegacy = false;
        Register(sink: true, keyboardNoLegacy: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        UnregisterAll();
        DestroyHandle();
    }

    private void UnregisterAll()
    {
        RAWINPUTDEVICE[] rid =
        [
            new() { usUsagePage = 1, usUsage = 2, dwFlags = RidevRemove, hwndTarget = IntPtr.Zero },
            new() { usUsagePage = 1, usUsage = 6, dwFlags = RidevRemove, hwndTarget = IntPtr.Zero }
        ];
        Native.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    private void Register(bool sink, bool keyboardNoLegacy)
    {
        var flags = sink ? RidevInputSink : RidevRemove;
        var hwnd = sink ? Handle : IntPtr.Zero;
        var keyboardFlags = flags;
        if (sink && keyboardNoLegacy)
            keyboardFlags |= RidevNoLegacy | RidevNoHotkeys;

        RAWINPUTDEVICE[] rid =
        [
            new() { usUsagePage = 1, usUsage = 2, dwFlags = flags, hwndTarget = hwnd },
            new() { usUsagePage = 1, usUsage = 6, dwFlags = keyboardFlags, hwndTarget = hwnd }
        ];
        if (!Native.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            if (sink)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "注册 Raw Input 失败");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmInput)
            ReadInput(m.LParam);
        base.WndProc(ref m);
    }

    private void ReadInput(IntPtr rawHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        uint size = 0;
        Native.GetRawInputData(rawHandle, RidInput, IntPtr.Zero, ref size, headerSize);
        if (size == 0)
            return;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var written = Native.GetRawInputData(rawHandle, RidInput, buffer, ref size, headerSize);
            if (written == uint.MaxValue || written == 0)
                return;

            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            var data = IntPtr.Add(buffer, (int)headerSize);
            if (header.dwType == RimTypeMouse)
            {
                var mouse = Marshal.PtrToStructure<RAWMOUSE>(data);
                var absolute = (mouse.usFlags & MouseMoveAbsolute) != 0;
                if (mouse.lLastX == 0 && mouse.lLastY == 0 && !absolute)
                    return;
                Moved?.Invoke(mouse.lLastX, mouse.lLastY, absolute);
                return;
            }

            if (header.dwType != RimTypeKeyboard)
                return;

            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(data);
            var down = (keyboard.Flags & Native.RiKeyBreak) == 0;
            var extended = (keyboard.Flags & Native.RiKeyE0) != 0;
            Key?.Invoke(header.hDevice, keyboard.VKey, keyboard.MakeCode, down, extended);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
