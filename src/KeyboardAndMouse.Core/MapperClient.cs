using System.Collections.Concurrent;
using System.Diagnostics;
using LiteNetLib;
using LiteNetLib.Utils;

namespace KeyboardAndMouse;

public sealed class MapperClient : IDisposable
{
    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _net;
    private readonly ConcurrentQueue<InputEvent> _queue = new();
    private readonly NetDataWriter _writer = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly long _moveMinTicks;
    private NetPeer? _peer;
    private Thread? _thread;
    private string _host = "127.0.0.1";
    private int _port = Protocol.DefaultPort;
    private long _nextConnectAt;
    private ushort _mouseSeq;
    private bool _timerStarted;
    private bool _disposed;
    private int _sendIntervalMs = 1;
    private int _pendingMovePacked;
    private int _hasPendingMove;
    private long _nextMoveSendAt;

    public MapperClient()
    {
        // 鼠标移动最快约 500Hz，避免挤爆可靠通道导致按键/点击发不出去。
        _moveMinTicks = Stopwatch.Frequency / 500;

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

        _listener.PeerConnectedEvent += peer =>
        {
            _peer = peer;
            SendCaps();
            Connected?.Invoke();
        };

        _listener.PeerDisconnectedEvent += (peer, info) =>
        {
            if (ReferenceEquals(_peer, peer))
                _peer = null;
            Disconnected?.Invoke(info.Reason.ToString());
            _nextConnectAt = Environment.TickCount64 + 1500;
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

            if (ev.Kind == InputEventKind.CaptureControl)
            {
                CaptureControlReceived?.Invoke((CaptureAction)ev.Button);
                return;
            }
        };
    }

    public bool IsConnected => _peer is { ConnectionState: ConnectionState.Connected };
    public ChannelFlags LocalCaps { get; private set; } = ChannelFlags.Both;
    public ChannelFlags RemoteCaps { get; private set; } = ChannelFlags.Both;
    public ChannelFlags EffectiveCaps => LocalCaps & RemoteCaps;
    public int SendIntervalMs => _sendIntervalMs;

    public event Action? Connected;
    public event Action<string>? Disconnected;
    public event Action<string, int>? Connecting;
    public event Action? CapsChanged;
    public event Action<CaptureAction>? CaptureControlReceived;

    public void SetLocalCaps(ChannelFlags flags)
    {
        LocalCaps = flags;
        if (IsConnected)
            SendCaps();
        CapsChanged?.Invoke();
    }

    /// <summary>0 = 最快（忙等），1–8 为轮询间隔毫秒。</summary>
    public void SetSendIntervalMs(int ms)
    {
        _sendIntervalMs = Math.Clamp(ms, 0, 8);
    }

    public void Start(string host, int port)
    {
        _host = host;
        _port = port;
        Native.timeBeginPeriod(1);
        _timerStarted = true;
        if (!_net.Start())
            throw new InvalidOperationException("无法启动 UDP 客户端");

        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "kmm-client",
            Priority = ThreadPriority.Highest
        };
        _thread.Start();
    }

    public void Enqueue(InputEvent ev)
    {
        if (ev.IsKeyboard && !EffectiveCaps.HasFlag(ChannelFlags.Keyboard))
            return;
        if (ev.IsMouse && !EffectiveCaps.HasFlag(ChannelFlags.Mouse))
            return;

        if (ev.Kind == InputEventKind.MouseMove)
        {
            Interlocked.Exchange(ref _pendingMovePacked, PackMove(ev.AbsX, ev.AbsY));
            Volatile.Write(ref _hasPendingMove, 1);
            return;
        }

        _queue.Enqueue(ev);
    }

    public void SendReleaseAll() => _queue.Enqueue(InputEvent.ReleaseAll());

    public void SendCaptureState(bool capturing)
    {
        if (!IsConnected)
            return;
        Send(InputEvent.CaptureState(capturing), Protocol.ChannelReliable, DeliveryMethod.ReliableOrdered);
    }

    private void PollLoop()
    {
        TryConnect();
        var clock = Stopwatch.StartNew();
        while (!_cts.IsCancellationRequested)
        {
            var started = clock.Elapsed;
            _net.PollEvents();
            if (_peer is null && Environment.TickCount64 >= _nextConnectAt)
                TryConnect();
            FlushQueue();
            WaitRemaining(started, clock);
        }
    }

    private void WaitRemaining(TimeSpan started, Stopwatch clock)
    {
        var interval = Volatile.Read(ref _sendIntervalMs);
        if (interval <= 0)
        {
            Thread.SpinWait(40);
            return;
        }

        var target = TimeSpan.FromMilliseconds(interval);
        while (!_cts.IsCancellationRequested)
        {
            var elapsed = clock.Elapsed - started;
            if (elapsed >= target)
                return;
            var left = target - elapsed;
            if (left.TotalMilliseconds > 1.5)
                Thread.Sleep(1);
            else
                Thread.SpinWait(80);
        }
    }

    private void TryConnect()
    {
        _nextConnectAt = Environment.TickCount64 + 2000;
        Connecting?.Invoke(_host, _port);
        _net.Connect(_host, _port, Protocol.ConnectKey);
    }

    private void FlushQueue()
    {
        if (_peer is not { ConnectionState: ConnectionState.Connected })
        {
            Volatile.Write(ref _hasPendingMove, 0);
            while (_queue.Count > 256 && _queue.TryDequeue(out _))
            {
            }
            return;
        }

        // 先发按键/点击等可靠包，再限速发鼠标移动，避免移动包挤死可靠通道。
        while (_queue.TryDequeue(out var ev))
            Send(ev, Protocol.ChannelReliable, DeliveryMethod.ReliableOrdered);

        TryFlushPendingMove();
    }

    private void TryFlushPendingMove()
    {
        if (Volatile.Read(ref _hasPendingMove) == 0)
            return;

        var now = Stopwatch.GetTimestamp();
        var intervalMs = Volatile.Read(ref _sendIntervalMs);
        var minTicks = intervalMs <= 0
            ? _moveMinTicks
            : Math.Max(_moveMinTicks, Stopwatch.Frequency * intervalMs / 1000);
        if (now < _nextMoveSendAt)
            return;
        if (Interlocked.Exchange(ref _hasPendingMove, 0) == 0)
            return;

        _nextMoveSendAt = now + minTicks;
        var packed = Volatile.Read(ref _pendingMovePacked);
        var absX = (ushort)(packed & 0xFFFF);
        var absY = (ushort)((packed >>> 16) & 0xFFFF);
        _mouseSeq++;
        Send(InputEvent.MouseMove(absX, absY, _mouseSeq), Protocol.ChannelMouse, DeliveryMethod.Unreliable);
    }

    private static int PackMove(ushort x, ushort y) => unchecked((int)((uint)x | ((uint)y << 16)));

    private void SendCaps()
    {
        if (_peer is null)
            return;
        Send(InputEvent.Capabilities(LocalCaps), Protocol.ChannelReliable, DeliveryMethod.ReliableOrdered);
    }

    private void Send(in InputEvent ev, byte channel, DeliveryMethod method)
    {
        _writer.Reset();
        PacketCodec.Write(_writer, ev);
        _peer?.Send(_writer, channel, method);
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
