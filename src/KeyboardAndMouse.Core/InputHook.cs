using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyboardAndMouse;

public sealed class InputHook : IDisposable
{
    private readonly Native.HookProc _keyboardProc;
    private readonly Native.HookProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private RawMouseWindow? _rawMouse;
    private volatile bool _capturing;
    private double _nx;
    private double _ny;
    private bool _cursorHidden;
    private bool _cursorClipped;
    private bool _inputBlocked;
    private bool _captureKeyboard = true;
    private bool _captureMouse = true;
    private volatile bool _keepLocalUi;
    private IntPtr _uiWindow;
    private string _keyboardDeviceId = "";
    private IntPtr _keyboardDeviceHandle;
    private bool _ctrl;
    private bool _alt;
    private bool _disposed;
    private long _lastHotkeyTicks;
    private readonly HashSet<ushort> _replayedDown = [];

    public InputHook()
    {
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public bool Capturing
    {
        get => _capturing;
        set
        {
            if (value == _capturing)
                return;
            _capturing = value;
            if (value)
            {
                _keyboardDeviceHandle = KeyboardDevices.HandleForId(_keyboardDeviceId);
                ReleaseStickyKeys();
                PromoteHooks();
                _rawMouse?.SetKeyboardNoLegacy(_captureKeyboard);
                ResetLogicalFromCursor();
                SyncLocalBlock();
                if (_captureMouse)
                {
                    EventCaptured?.Invoke(InputEvent.MouseMove(
                        (ushort)Math.Round(_nx * 65535),
                        (ushort)Math.Round(_ny * 65535),
                        0));
                }
            }
            else
            {
                // 先停逻辑再恢复 legacy，并强制松开 Win/Ctrl/Alt，避免取消拦截后按键乱跳。
                ReleaseStickyKeys();
                _rawMouse?.RestoreLegacyKeyboard();
                UnfreezeLocalCursor();
                ReleaseStickyKeys();
                _ctrl = false;
                _alt = false;
            }
        }
    }

    public void ApplyChannelFlags(ChannelFlags flags)
    {
        _captureKeyboard = flags.HasFlag(ChannelFlags.Keyboard);
        _captureMouse = flags.HasFlag(ChannelFlags.Mouse);
        if (_capturing)
        {
            _rawMouse?.SetKeyboardNoLegacy(_captureKeyboard);
            SyncLocalBlock();
        }
    }

    public void SetKeepLocalUi(bool enabled, IntPtr uiWindow)
    {
        _keepLocalUi = enabled;
        _uiWindow = uiWindow;
        if (_capturing)
            SyncLocalBlock();
    }

    public void RefreshLocalClip()
    {
        if (_capturing && _keepLocalUi)
            ClipToUiWindow();
    }

    public void SetKeyboardDevice(string? deviceId)
    {
        _keyboardDeviceId = deviceId ?? "";
        _keyboardDeviceHandle = KeyboardDevices.HandleForId(_keyboardDeviceId);
    }

    public void NotifyUiVisible(bool visible)
    {
        if (!_capturing)
            return;
        if (visible)
            SyncLocalBlock();
        else if (_keepLocalUi)
            UnfreezeMouseOnly();
    }

    public event Action<InputEvent>? EventCaptured;
    public event Action? ToggleRequested;
    public event Action? ExitRequested;
    public event Action<IntPtr>? KeyboardHeard;

    public void Install()
    {
        var module = Native.GetModuleHandle(null);
        _keyboardHook = Native.SetWindowsHookEx(Native.WhKeyboardLl, _keyboardProc, module, 0);
        if (_keyboardHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "安装键盘钩子失败");

        _mouseHook = Native.SetWindowsHookEx(Native.WhMouseLl, _mouseProc, module, 0);
        if (_mouseHook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "安装鼠标钩子失败");

        _rawMouse = new RawMouseWindow();
        _rawMouse.Moved += OnRawMouseMoved;
        _rawMouse.Key += OnRawKeyboard;
        SetKeyboardDevice(_keyboardDeviceId);
    }

    private void PromoteHooks()
    {
        var module = Native.GetModuleHandle(null);
        var keyboard = Native.SetWindowsHookEx(Native.WhKeyboardLl, _keyboardProc, module, 0);
        if (keyboard != IntPtr.Zero)
        {
            if (_keyboardHook != IntPtr.Zero)
                Native.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = keyboard;
        }

        var mouse = Native.SetWindowsHookEx(Native.WhMouseLl, _mouseProc, module, 0);
        if (mouse != IntPtr.Zero)
        {
            if (_mouseHook != IntPtr.Zero)
                Native.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = mouse;
        }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

        try
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var down = (info.flags & Native.LlkfUp) == 0;
            var vk = (ushort)info.vkCode;
            var injected = (info.flags & Native.LlkfInjected) != 0;

            // 拦截中：任何本机 Win（含 SendInput 注入）一律吃掉，防止开始菜单回放。
            if (_capturing && _captureKeyboard && IsWinKey(vk))
                return (IntPtr)1;

            if (injected)
                return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            UpdateModifiers(vk, down);

            if ((vk == Native.VkQ || vk == Native.VkX) && (_ctrl && _alt || HotkeyModifiersDown()))
            {
                if (down)
                    RaiseHotkey(vk);
                return (IntPtr)1;
            }

            if (!_capturing)
                return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            // 仅鼠标通道：键盘留给本机。
            if (!_captureKeyboard)
                return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            return (IntPtr)1;
        }
        catch
        {
            return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

        try
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((info.flags & Native.LlmhfInjected) != 0)
                return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            if (!_capturing || !_captureMouse)
                return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            var overUi = _keepLocalUi && IsUiWindow(info.pt);
            var msg = (int)wParam;
            switch (msg)
            {
                case Native.WmMouseMove:
                    break;
                case Native.WmLButtonDown:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(0, true));
                    break;
                case Native.WmLButtonUp:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(0, false));
                    break;
                case Native.WmRButtonDown:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(1, true));
                    break;
                case Native.WmRButtonUp:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(1, false));
                    break;
                case Native.WmMButtonDown:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(2, true));
                    break;
                case Native.WmMButtonUp:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(2, false));
                    break;
                case Native.WmXButtonDown:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(XButtonIndex(info.mouseData), true));
                    break;
                case Native.WmXButtonUp:
                    if (!overUi)
                        EventCaptured?.Invoke(InputEvent.MouseButton(XButtonIndex(info.mouseData), false));
                    break;
                case Native.WmMouseWheel:
                    EventCaptured?.Invoke(InputEvent.MouseWheel((short)(info.mouseData >> 16)));
                    // 滚轮绝不能进本机窗口，否则 AutoScroll 会把界面滚塌。
                    return (IntPtr)1;
                case Native.WmMouseHWheel:
                    EventCaptured?.Invoke(InputEvent.MouseWheel((short)(info.mouseData >> 16)));
                    return (IntPtr)1;
                default:
                    return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }

            if (_keepLocalUi)
                return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            return (IntPtr)1;
        }
        catch
        {
            return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }
    }

    private void OnRawMouseMoved(int dx, int dy, bool absolute)
    {
        if (!_capturing || !_captureMouse)
            return;

        if (absolute)
            return;

        if (dx == 0 && dy == 0)
            return;

        Native.GetVirtualScreen(out _, out _, out var vw, out var vh);
        var spanX = Math.Max(vw - 1, 1);
        var spanY = Math.Max(vh - 1, 1);
        _nx = Math.Clamp(_nx + dx / (double)spanX, 0, 1);
        _ny = Math.Clamp(_ny + dy / (double)spanY, 0, 1);

        EventCaptured?.Invoke(InputEvent.MouseMove(
            (ushort)Math.Round(_nx * 65535),
            (ushort)Math.Round(_ny * 65535),
            0));
    }

    private void OnRawKeyboard(IntPtr device, ushort vk, ushort scan, bool down, bool extended)
    {
        if (down && vk is not 0 and not 0xFF)
            KeyboardHeard?.Invoke(device);

        if (!_capturing)
            return;
        if (vk == 0 || vk == 0xFF)
            return;

        if ((vk == Native.VkQ || vk == Native.VkX) && HotkeyModifiersDown())
        {
            if (down)
                RaiseHotkey(vk);
            return;
        }

        if (!_captureKeyboard)
            return;

        // Win：只发给对方。本机绝不 SendInput / 回放（Win 常走另一接口，以前会被误回放）。
        if (IsWinKey(vk))
        {
            _replayedDown.Remove(vk);
            EventCaptured?.Invoke(InputEvent.Key(vk, scan, down, extended));
            return;
        }

        var selected = string.IsNullOrEmpty(_keyboardDeviceId) || IsSelectedKeyboard(device);
        if (!selected)
        {
            TrackAndReplayLocal(vk, scan, down, extended);
            return;
        }

        if (_replayedDown.Remove(vk))
            ForceKeyUp(vk, scan, extended);

        if (!string.IsNullOrEmpty(_keyboardDeviceId))
            _keyboardDeviceHandle = device;

        EventCaptured?.Invoke(InputEvent.Key(vk, scan, down, extended));
    }

    private void TrackAndReplayLocal(ushort vk, ushort scan, bool down, bool extended)
    {
        if (IsWinKey(vk))
        {
            EventCaptured?.Invoke(InputEvent.Key(vk, scan, down, extended));
            return;
        }

        if (down)
            _replayedDown.Add(vk);
        else
            _replayedDown.Remove(vk);
        ReplayKeyLocal(vk, scan, down, extended);
    }

    private void ReleaseStickyKeys()
    {
        ushort[] sticky =
        [
            Native.VkLControl, Native.VkRControl, Native.VkControl,
            Native.VkLMenu, Native.VkRMenu, Native.VkMenu,
            0xA0, 0xA1, 0x10 // LShift, RShift, Shift
        ];
        foreach (var vk in sticky)
            ForceKeyUp(vk, scan: 0, extended: vk is Native.VkRControl or Native.VkRMenu);

        foreach (var vk in _replayedDown.ToArray())
        {
            if (!IsWinKey(vk))
                ForceKeyUp(vk, scan: 0, extended: false);
        }
        _replayedDown.Clear();

        // 关闭拦截后清一次本机 Win 残留（拦截中禁止任何本机 Win 注入）。
        if (!_capturing)
        {
            SendLocalWinUp(Native.VkLWin);
            SendLocalWinUp(Native.VkRWin);
        }
    }

    private static void ForceKeyUp(ushort vk, ushort scan, bool extended)
    {
        if (IsWinKey(vk))
            return;
        if (scan == 0)
            scan = (ushort)Native.MapVirtualKey(vk, 0);
        ReplayKeyLocal(vk, scan, down: false, extended);
    }

    private static void SendLocalWinUp(ushort vk)
    {
        var scan = (ushort)Native.MapVirtualKey(vk, 0);
        var input = new INPUT
        {
            type = Native.InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = Native.KeyeventfKeyup | Native.KeyeventfExtendedkey,
                    time = 0,
                    dwExtraInfo = Native.ExtraInfoTag
                }
            }
        };
        Native.SendInput(1, [input], INPUT.Size);
    }

    private static bool IsWinKey(ushort vk) => vk is Native.VkLWin or Native.VkRWin;

    private void RaiseHotkey(ushort vk)
    {
        var now = Environment.TickCount64;
        if (now - _lastHotkeyTicks < 250)
            return;
        _lastHotkeyTicks = now;
        if (vk == Native.VkQ)
            ToggleRequested?.Invoke();
        else
            ExitRequested?.Invoke();
    }

    private bool IsSelectedKeyboard(IntPtr device)
    {
        if (device == IntPtr.Zero)
            return false;
        if (_keyboardDeviceHandle != IntPtr.Zero && device == _keyboardDeviceHandle)
            return true;
        return KeyboardDevices.Matches(_keyboardDeviceId, device);
    }

    private static bool HotkeyModifiersDown()
    {
        const int keyDown = 0x8000;
        return (Native.GetAsyncKeyState(Native.VkControl) & keyDown) != 0
            && (Native.GetAsyncKeyState(Native.VkMenu) & keyDown) != 0;
    }

    private static void ReplayKeyLocal(ushort vk, ushort scan, bool down, bool extended)
    {
        // 拦截逻辑里禁止任何本机 Win 注入（按下/抬起都不要），否则等于回放本机。
        if (IsWinKey(vk))
            return;

        uint flags = 0;
        if (!down)
            flags |= Native.KeyeventfKeyup;
        if (extended)
            flags |= Native.KeyeventfExtendedkey;
        var input = new INPUT
        {
            type = Native.InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = Native.ExtraInfoTag
                }
            }
        };
        Native.SendInput(1, [input], INPUT.Size);
    }

    private void ResetLogicalFromCursor()
    {
        Native.GetCursorPos(out var pt);
        Native.GetVirtualScreen(out var vx, out var vy, out var vw, out var vh);
        _nx = Math.Clamp((pt.X - vx) / (double)Math.Max(vw - 1, 1), 0, 1);
        _ny = Math.Clamp((pt.Y - vy) / (double)Math.Max(vh - 1, 1), 0, 1);
    }

    private bool IsUiWindow(POINT pt)
    {
        if (_uiWindow == IntPtr.Zero)
            return false;
        var hwnd = Native.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero)
            return false;
        if (hwnd == _uiWindow)
            return true;
        return Native.GetAncestor(hwnd, Native.GaRoot) == _uiWindow;
    }

    private void ShowLocalCursor()
    {
        while (Native.ShowCursor(true) < 0)
        {
        }

        _cursorHidden = false;
    }

    private void FreezeLocalCursor()
    {
        Native.GetCursorPos(out var pt);
        var clip = new RECT
        {
            Left = pt.X,
            Top = pt.Y,
            Right = pt.X + 1,
            Bottom = pt.Y + 1
        };
        _cursorClipped = Native.ClipCursor(ref clip);
        if (!_cursorHidden)
        {
            Native.ShowCursor(false);
            _cursorHidden = true;
        }
    }

    private void ClipToUiWindow()
    {
        if (_uiWindow == IntPtr.Zero || !Native.GetWindowRect(_uiWindow, out var rect))
        {
            FreezeLocalCursor();
            return;
        }

        if (rect.Right <= rect.Left + 2)
            rect.Right = rect.Left + 2;
        if (rect.Bottom <= rect.Top + 2)
            rect.Bottom = rect.Top + 2;

        _cursorClipped = Native.ClipCursor(ref rect);
        ShowLocalCursor();
    }

    private void SyncLocalBlock()
    {
        if (!_capturing)
        {
            UnfreezeLocalCursor();
            return;
        }

        if (_keepLocalUi)
            ClipToUiWindow();
        else if (_captureMouse)
            FreezeLocalCursor();
        else
            UnfreezeMouseOnly();

        // 不再使用 BlockInput：它会吃掉鼠标按键等 LL 钩子事件，导致只剩 Raw 鼠标移动可用。
        if (_inputBlocked)
        {
            Native.BlockInput(false);
            _inputBlocked = false;
        }
    }

    private void UnfreezeMouseOnly()
    {
        if (_cursorClipped)
        {
            Native.ClipCursor(IntPtr.Zero);
            _cursorClipped = false;
        }

        if (_cursorHidden)
        {
            Native.ShowCursor(true);
            _cursorHidden = false;
        }
    }

    private void UnfreezeLocalCursor()
    {
        if (_inputBlocked)
        {
            Native.BlockInput(false);
            _inputBlocked = false;
        }

        UnfreezeMouseOnly();
    }

    private static byte XButtonIndex(uint mouseData)
    {
        var which = mouseData >> 16;
        return which == Native.Xbutton2 ? (byte)4 : (byte)3;
    }

    private void UpdateModifiers(ushort vk, bool down)
    {
        switch (vk)
        {
            case Native.VkControl:
            case Native.VkLControl:
            case Native.VkRControl:
                _ctrl = down;
                break;
            case Native.VkMenu:
            case Native.VkLMenu:
            case Native.VkRMenu:
                _alt = down;
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _capturing = false;
        ReleaseStickyKeys();
        UnfreezeLocalCursor();

        if (_rawMouse is not null)
        {
            try
            {
                _rawMouse.RestoreLegacyKeyboard();
            }
            catch
            {
                // best-effort restore before dispose
            }

            ReleaseStickyKeys();
            _rawMouse.Moved -= OnRawMouseMoved;
            _rawMouse.Key -= OnRawKeyboard;
            _rawMouse.Dispose();
            _rawMouse = null;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }
}
