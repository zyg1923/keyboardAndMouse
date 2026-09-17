namespace KeyboardAndMouse;

public sealed class InputInjector
{
    private readonly HashSet<ushort> _keysDown = [];
    private readonly HashSet<byte> _buttonsDown = [];
    private ushort _lastMouseSeq;
    private int _lastMouseX;
    private int _lastMouseY;

    public void Apply(in InputEvent ev)
    {
        switch (ev.Kind)
        {
            case InputEventKind.MouseMove:
            {
                Native.GetVirtualScreen(out var vx, out var vy, out var vw, out var vh);
                var spanX = Math.Max(vw - 1, 1);
                var spanY = Math.Max(vh - 1, 1);
                var px = vx + (int)Math.Round(ev.AbsX / 65535.0 * spanX);
                var py = vy + (int)Math.Round(ev.AbsY / 65535.0 * spanY);
                if (ev.Seq != 0 && !IsNewerSeq(ev.Seq))
                    break;
                _lastMouseSeq = ev.Seq;
                _lastMouseX = px;
                _lastMouseY = py;
                // 协议坐标已是虚拟桌面 0–65535，直接喂给 SendInput。
                SendMouse(
                    Native.MouseeventfMove | Native.MouseeventfAbsolute | Native.MouseeventfVirtualDesk | Native.MouseeventfMoveNoCoalesce,
                    ev.AbsX,
                    ev.AbsY,
                    0);
                var overlay = OverlayInput.FindSnipasteOverlay();
                if (overlay != IntPtr.Zero)
                    OverlayInput.SendMouseMove(overlay, px, py);
                break;
            }
            case InputEventKind.MouseButton:
                ApplyMouseButton(ev.Button, ev.Down);
                break;
            case InputEventKind.MouseWheel:
                SendMouse(Native.MouseeventfWheel, 0, 0, unchecked((uint)(int)ev.Wheel));
                break;
            case InputEventKind.Key:
                ApplyKey(ev.Vk, ev.Scan, ev.Down, ev.Extended);
                break;
            case InputEventKind.ReleaseAll:
                ReleaseAll();
                break;
        }
    }

    public void ReleaseAll()
    {
        var inputs = new List<INPUT>(_keysDown.Count + _buttonsDown.Count);
        foreach (var vk in _keysDown)
            inputs.Add(BuildKey(vk, scan: 0, down: false, extended: IsLikelyExtended(vk)));
        foreach (var button in _buttonsDown)
            inputs.Add(BuildMouseButton(button, down: false));

        _keysDown.Clear();
        _buttonsDown.Clear();
        Send(inputs);
    }

    private void ApplyKey(ushort vk, ushort scan, bool down, bool extended)
    {
        if (down)
            _keysDown.Add(vk);
        else
            _keysDown.Remove(vk);

        Send(BuildKey(vk, scan, down, extended));
        var overlay = OverlayInput.FindSnipasteOverlay();
        if (overlay != IntPtr.Zero)
            OverlayInput.SendKey(overlay, vk, scan, down, extended);
    }

    private void ApplyMouseButton(byte button, bool down)
    {
        if (down)
            _buttonsDown.Add(button);
        else
            _buttonsDown.Remove(button);

        Send(BuildMouseButton(button, down));
        var overlay = OverlayInput.FindSnipasteOverlay();
        if (overlay != IntPtr.Zero)
            OverlayInput.SendMouseButton(overlay, _lastMouseX, _lastMouseY, button, down);
    }

    private static INPUT BuildKey(ushort vk, ushort scan, bool down, bool extended)
    {
        if (scan == 0)
            scan = (ushort)Native.MapVirtualKey(vk, IsHardwareKey(vk) ? 4u : 0u);

        extended = extended || IsLikelyExtended(vk);
        uint flags = 0;
        if (!down)
            flags |= Native.KeyeventfKeyup;
        if (extended)
            flags |= Native.KeyeventfExtendedkey;

        // ESC / F 键 / Win / 多媒体键用扫描码注入，截图类软件更认「硬件键」。
        var useScan = IsHardwareKey(vk) && scan != 0;
        if (useScan)
            flags |= Native.KeyeventfScancode;

        return new INPUT
        {
            type = Native.InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = useScan ? (ushort)0 : vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = UIntPtr.Zero
                }
            }
        };
    }

    private static INPUT BuildMouseButton(byte button, bool down)
    {
        uint flags;
        uint data = 0;
        switch (button)
        {
            case 0:
                flags = down ? Native.MouseeventfLeftdown : Native.MouseeventfLeftup;
                break;
            case 1:
                flags = down ? Native.MouseeventfRightdown : Native.MouseeventfRightup;
                break;
            case 2:
                flags = down ? Native.MouseeventfMiddledown : Native.MouseeventfMiddleup;
                break;
            case 3:
                flags = down ? Native.MouseeventfXdown : Native.MouseeventfXup;
                data = Native.Xbutton1;
                break;
            default:
                flags = down ? Native.MouseeventfXdown : Native.MouseeventfXup;
                data = Native.Xbutton2;
                break;
        }

        return new INPUT
        {
            type = Native.InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = data,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = Native.ExtraInfoTag
                }
            }
        };
    }

    private static void SendMouse(uint flags, int dx, int dy, uint data)
    {
        var input = new INPUT
        {
            type = Native.InputMouse,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    mouseData = data,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = Native.ExtraInfoTag
                }
            }
        };
        // 鼠标移动高频路径：不 AttachThreadInput，避免额外延迟。
        Native.SendInput(1, [input], INPUT.Size);
    }

    private static void Send(List<INPUT> inputs)
    {
        if (inputs.Count == 0)
            return;
        Send(inputs.ToArray());
    }

    private static void Send(INPUT input) => Send(new[] { input });

    private static void Send(INPUT[] inputs)
    {
        Native.WithForegroundAttached(() =>
            Native.SendInput((uint)inputs.Length, inputs, INPUT.Size));
    }

    private bool IsNewerSeq(ushort seq)
    {
        if (_lastMouseSeq == 0)
            return true;
        return (ushort)(seq - _lastMouseSeq) < 32768 && seq != _lastMouseSeq;
    }

    private static bool IsHardwareKey(ushort vk) =>
        vk is Native.VkEscape or Native.VkSnapshot
            or Native.VkLWin or Native.VkRWin or Native.VkApps
            or (>= 0x70 and <= 0x87)   // F1-F24
            or (>= 0xA6 and <= 0xB7); // 浏览器/音量/媒体/计算机(Launch App)

    private static bool IsLikelyExtended(ushort vk) => vk is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or
        0x2D or 0x2E or Native.VkRControl or Native.VkRMenu
        or Native.VkLWin or Native.VkRWin or Native.VkApps
        or Native.VkSnapshot
        or (>= 0xA6 and <= 0xB7);
}
