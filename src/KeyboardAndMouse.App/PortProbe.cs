using System.Net;
using System.Net.Sockets;

namespace KeyboardAndMouse.App;

internal static class PortProbe
{
    public const int Min = 1;
    public const int Max = 65535;

    public static bool IsInRange(int port) => port is >= Min and <= Max;

    public static bool TryParseValid(string? text, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!int.TryParse(text.Trim(), out port))
            return false;
        return IsInRange(port);
    }

    public static bool IsUdpFree(int port)
    {
        if (!IsInRange(port))
            return false;

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static int[] SuggestFree(int count, string? typed)
    {
        var start = Protocol.DefaultPort;
        if (int.TryParse(typed?.Trim(), out var n) && IsInRange(n))
            start = n;

        var found = new int[count];
        var wrote = 0;
        var span = Max - Min + 1;
        for (var i = 0; i < span && wrote < count; i++)
        {
            var port = Min + (start - Min + i) % span;
            if (!IsUdpFree(port))
                continue;
            found[wrote++] = port;
        }

        if (wrote == count)
            return found;
        return found.AsSpan(0, wrote).ToArray();
    }
}
