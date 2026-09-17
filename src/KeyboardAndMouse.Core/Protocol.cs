using LiteNetLib.Utils;

namespace KeyboardAndMouse;

[Flags]
public enum ChannelFlags : byte
{
    None = 0,
    Keyboard = 1,
    Mouse = 2,
    Both = Keyboard | Mouse
}

public static class ChannelFlagsText
{
    public static string Describe(ChannelFlags flags) => flags switch
    {
        ChannelFlags.Both => "键盘和鼠标",
        ChannelFlags.Keyboard => "仅键盘",
        ChannelFlags.Mouse => "仅鼠标",
        _ => "无（双方选择没有交集）"
    };
}

public static class Protocol
{
    public const int DefaultPort = 9050;
    public const string ConnectKey = "KmmLiteDemo";
    public const byte ChannelReliable = 0;
    public const byte ChannelMouse = 1;
    public const byte ChannelCount = 4;
}

public enum CaptureAction : byte
{
    Off = 0,
    On = 1,
    Restart = 2
}

public enum InputEventKind : byte
{
    MouseMove = 1,
    MouseButton = 2,
    MouseWheel = 3,
    Key = 4,
    ReleaseAll = 5,
    Caps = 6,
    /// <summary>接收端 → 发送端：请求开关/重启拦截。</summary>
    CaptureControl = 7,
    /// <summary>发送端 → 接收端：同步当前拦截状态。</summary>
    CaptureState = 8
}

public readonly struct InputEvent
{
    public InputEventKind Kind { get; init; }
    public ushort AbsX { get; init; }
    public ushort AbsY { get; init; }
    public ushort Seq { get; init; }
    public byte Button { get; init; }
    public bool Down { get; init; }
    public short Wheel { get; init; }
    public ushort Vk { get; init; }
    public ushort Scan { get; init; }
    public bool Extended { get; init; }
    public ChannelFlags Caps { get; init; }

    public static InputEvent MouseMove(ushort absX, ushort absY, ushort seq) => new()
    {
        Kind = InputEventKind.MouseMove,
        AbsX = absX,
        AbsY = absY,
        Seq = seq
    };

    public static InputEvent MouseButton(byte button, bool down) => new()
    {
        Kind = InputEventKind.MouseButton,
        Button = button,
        Down = down
    };

    public static InputEvent MouseWheel(int delta) => new()
    {
        Kind = InputEventKind.MouseWheel,
        Wheel = (short)Math.Clamp(delta, short.MinValue, short.MaxValue)
    };

    public static InputEvent Key(ushort vk, ushort scan, bool down, bool extended) => new()
    {
        Kind = InputEventKind.Key,
        Vk = vk,
        Scan = scan,
        Down = down,
        Extended = extended
    };

    public static InputEvent ReleaseAll() => new() { Kind = InputEventKind.ReleaseAll };

    public static InputEvent Capabilities(ChannelFlags flags) => new()
    {
        Kind = InputEventKind.Caps,
        Caps = flags
    };

    public static InputEvent CaptureControl(CaptureAction action) => new()
    {
        Kind = InputEventKind.CaptureControl,
        Button = (byte)action
    };

    public static InputEvent CaptureState(bool capturing) => new()
    {
        Kind = InputEventKind.CaptureState,
        Down = capturing
    };

    public bool IsMouse => Kind is InputEventKind.MouseMove or InputEventKind.MouseButton or InputEventKind.MouseWheel;
    public bool IsKeyboard => Kind is InputEventKind.Key;
}

public static class PacketCodec
{
    public static void Write(NetDataWriter writer, in InputEvent ev)
    {
        writer.Put((byte)ev.Kind);
        switch (ev.Kind)
        {
            case InputEventKind.MouseMove:
                writer.Put(ev.AbsX);
                writer.Put(ev.AbsY);
                writer.Put(ev.Seq);
                break;
            case InputEventKind.MouseButton:
                writer.Put(ev.Button);
                writer.Put((byte)(ev.Down ? 1 : 0));
                break;
            case InputEventKind.MouseWheel:
                writer.Put(ev.Wheel);
                break;
            case InputEventKind.Key:
                writer.Put(ev.Vk);
                writer.Put(ev.Scan);
                writer.Put((byte)(ev.Down ? 1 : 0));
                writer.Put((byte)(ev.Extended ? 1 : 0));
                break;
            case InputEventKind.Caps:
                writer.Put((byte)ev.Caps);
                break;
            case InputEventKind.CaptureControl:
                writer.Put(ev.Button);
                break;
            case InputEventKind.CaptureState:
                writer.Put((byte)(ev.Down ? 1 : 0));
                break;
            case InputEventKind.ReleaseAll:
                break;
        }
    }

    public static bool TryRead(NetDataReader reader, out InputEvent ev)
    {
        ev = default;
        if (reader.AvailableBytes < 1)
            return false;

        var kind = (InputEventKind)reader.GetByte();
        switch (kind)
        {
            case InputEventKind.MouseMove:
                if (reader.AvailableBytes < 6) return false;
                ev = InputEvent.MouseMove(reader.GetUShort(), reader.GetUShort(), reader.GetUShort());
                return true;
            case InputEventKind.MouseButton:
                if (reader.AvailableBytes < 2) return false;
                ev = InputEvent.MouseButton(reader.GetByte(), reader.GetByte() != 0);
                return true;
            case InputEventKind.MouseWheel:
                if (reader.AvailableBytes < 2) return false;
                ev = InputEvent.MouseWheel(reader.GetShort());
                return true;
            case InputEventKind.Key:
                if (reader.AvailableBytes < 6) return false;
                ev = InputEvent.Key(reader.GetUShort(), reader.GetUShort(), reader.GetByte() != 0, reader.GetByte() != 0);
                return true;
            case InputEventKind.Caps:
                if (reader.AvailableBytes < 1) return false;
                ev = InputEvent.Capabilities((ChannelFlags)reader.GetByte());
                return true;
            case InputEventKind.CaptureControl:
                if (reader.AvailableBytes < 1) return false;
                ev = InputEvent.CaptureControl((CaptureAction)reader.GetByte());
                return true;
            case InputEventKind.CaptureState:
                if (reader.AvailableBytes < 1) return false;
                ev = InputEvent.CaptureState(reader.GetByte() != 0);
                return true;
            case InputEventKind.ReleaseAll:
                ev = InputEvent.ReleaseAll();
                return true;
            default:
                return false;
        }
    }
}
