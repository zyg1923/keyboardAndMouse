using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeyboardAndMouse;

public sealed class KeyboardIdentify : IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevRemove = 0x00000001;
    private ProbeWindow? _window;
    private bool _disposed;

    public event Action<IntPtr>? Pressed;

    public void Start()
    {
        Stop();
        _window = new ProbeWindow();
        _window.Pressed += device => Pressed?.Invoke(device);
    }

    public void Stop()
    {
        if (_window is null)
            return;
        _window.Dispose();
        _window = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }

    private sealed class ProbeWindow : NativeWindow, IDisposable
    {
        public event Action<IntPtr>? Pressed;
        private bool _disposed;

        public ProbeWindow()
        {
            try
            {
                CreateHandle(new CreateParams
                {
                    Caption = "KmmIdentifyKeyboard",
                    Parent = new IntPtr(-3)
                });
            }
            catch
            {
                CreateHandle(new CreateParams
                {
                    Caption = "KmmIdentifyKeyboard",
                    X = 0,
                    Y = 0,
                    Width = 1,
                    Height = 1,
                    Style = unchecked((int)0x80000000)
                });
            }

            Register(sink: true);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Register(sink: false);
            DestroyHandle();
        }

        private void Register(bool sink)
        {
            var rid = new RAWINPUTDEVICE[]
            {
                new()
                {
                    usUsagePage = 1,
                    usUsage = 6,
                    dwFlags = sink ? RidevInputSink : RidevRemove,
                    hwndTarget = sink ? Handle : IntPtr.Zero
                }
            };
            if (!Native.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                if (sink)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法开始识别键盘");
                return;
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
                if (header.dwType != Native.RimTypeKeyboard)
                    return;

                var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(buffer, (int)headerSize));
                var down = (keyboard.Flags & Native.RiKeyBreak) == 0;
                if (!down || keyboard.VKey is 0 or 0xFF)
                    return;
                Pressed?.Invoke(header.hDevice);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
