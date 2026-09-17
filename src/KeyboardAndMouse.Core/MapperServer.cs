using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace KeyboardAndMouse;

public sealed class MapperServer : IDisposable
{
    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _net;
    private readonly NetDataWriter _writer = new();
    private readonly CancellationTokenSource _cts = new();
    private NetPeer? _peer;
    private Thread? _thread;
    private bool _timerStarted;
    private bool _disposed;

    public MapperServer()
    {
        _net = new NetManager(_listener)
        {
            AutoRecycle = true,
            DisconnectTimeout = 8000,
            UpdateTime = 1,
            IPv6Enabled = false,
            ChannelsCount = Protocol.ChannelCount,
            UnsyncedReceiveEvent = true,
            UnsyncedDeliveryEvent = true
        };

        _listener.ConnectionRequestEvent += request =>
        {
            if (_net.ConnectedPeersCount >= 1)
            {
                request.Reject();
                return;
            }

            request.AcceptIfKey(Protocol.ConnectKey);
        };

        _listener.PeerConnectedEvent += peer =>
        {
            _peer = peer;
            SendCaps();
            ClientConnected?.Invoke(peer.Address.ToString());
        };

        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            if (ReferenceEquals(_peer, peer))
                _peer = null;
            ClientDisconnected?.Invoke($"{peer.Address} ({info.Reason})");
            Received?.Invoke(InputEvent.ReleaseAll());
        };

        _listener.NetworkReceiveEvent += (_, reader, _, _) =>
        {
            if (!PacketCodec.TryRead(reader, out var ev))
                return;
            if (ev.Kind == InputEventKind.Caps)
            {
                RemoteCaps = ev.Caps;
                CapsChanged?.Invoke();
                return;
            }

            if (ev.Kind == InputEventKind.CaptureState)
            {
                CaptureStateReceived?.Invoke(ev.Down);
                return;
            }

            if (ev.IsKeyboard && !EffectiveCaps.HasFlag(ChannelFlags.Keyboard))
                return;
            if (ev.IsMouse && !EffectiveCaps.HasFlag(ChannelFlags.Mouse))
                return;
            Received?.Invoke(ev);
        };
    }

    public bool HasClient => _peer is { ConnectionState: ConnectionState.Connected };
    public ChannelFlags LocalCaps { get; private set; } = ChannelFlags.Both;
    public ChannelFlags RemoteCaps { get; private set; } = ChannelFlags.Both;
    public ChannelFlags EffectiveCaps => LocalCaps & RemoteCaps;

    public event Action<string>? ClientConnected;
    public event Action<string>? ClientDisconnected;
    public event Action<InputEvent>? Received;
    public event Action? CapsChanged;
    public event Action<bool>? CaptureStateReceived;

    public void SetLocalCaps(ChannelFlags flags)
    {
        LocalCaps = flags;
        SendCaps();
        CapsChanged?.Invoke();
    }

    public void Start(int port)
    {
        Native.timeBeginPeriod(1);
        _timerStarted = true;
        if (!_net.Start(port))
            throw new InvalidOperationException($"无法在 UDP {port} 上监听");

        _thread = new Thread(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                _net.PollEvents();
                Thread.Sleep(1);
            }
        })
        {
            IsBackground = true,
            Name = "kmm-server",
            Priority = ThreadPriority.Highest
        };
        _thread.Start();
    }

    public static IEnumerable<string> LocalIPv4Addresses()
    {
        foreach (var address in Dns.GetHostAddresses(Dns.GetHostName()))
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                yield return address.ToString();
        }
    }

    private void SendCaps()
    {
        if (_peer is not { ConnectionState: ConnectionState.Connected })
            return;
        _writer.Reset();
        PacketCodec.Write(_writer, InputEvent.Capabilities(LocalCaps));
        _peer.Send(_writer, Protocol.ChannelReliable, DeliveryMethod.ReliableOrdered);
    }

    public void SendCaptureControl(CaptureAction action)
    {
        if (_peer is not { ConnectionState: ConnectionState.Connected })
            return;
        _writer.Reset();
        PacketCodec.Write(_writer, InputEvent.CaptureControl(action));
        _peer.Send(_writer, Protocol.ChannelReliable, DeliveryMethod.ReliableOrdered);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts.Cancel();
        _thread?.Join(1000);
        _net.Stop();
        _cts.Dispose();
        if (_timerStarted)
            Native.timeEndPeriod(1);
    }
}
