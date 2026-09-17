using System.Runtime.InteropServices;

namespace KeyboardAndMouse;

public readonly record struct KeyboardDeviceInfo(IntPtr Handle, string Id, string Label);

public static class KeyboardDevices
{
    public static IReadOnlyList<KeyboardDeviceInfo> List()
    {
        try
        {
            return Enumerate();
        }
        catch
        {
            return Array.Empty<KeyboardDeviceInfo>();
        }
    }

    public static IntPtr HandleForId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return IntPtr.Zero;
        foreach (var device in List())
        {
            if (IsSameDeviceId(id, device.Id))
                return device.Handle;
        }

        return IntPtr.Zero;
    }

    public static bool Matches(string? selectedId, IntPtr handle)
    {
        if (string.IsNullOrWhiteSpace(selectedId) || handle == IntPtr.Zero)
            return false;

        string liveId;
        try
        {
            liveId = DeviceName(handle);
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(liveId))
            return false;
        return IsSameDeviceId(selectedId, liveId);
    }

    public static bool IsSameDeviceId(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(PhysicalKey(a), PhysicalKey(b), StringComparison.OrdinalIgnoreCase);
    }

    public static KeyboardDeviceInfo? FindByHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return null;

        string id;
        try
        {
            id = DeviceName(handle);
        }
        catch
        {
            id = "";
        }

        if (string.IsNullOrEmpty(id) || IsIgnored(id))
            return null;

        foreach (var device in List())
        {
            if (device.Handle == handle || IsSameDeviceId(device.Id, id))
                return device;
        }

        return new KeyboardDeviceInfo(handle, id, BuildLabel(id));
    }

    public static string PhysicalKey(string id)
    {
        var vid = Extract(id, "VID_");
        var pid = Extract(id, "PID_");
        // 只按 VID/PID 合并：Win 键常走另一个 HID 接口，带 Col/MI 会误判成两块键盘，导致修饰键卡住。
        if (vid.Length > 0 && pid.Length > 0)
            return $"VID_{vid}&PID_{pid}";

        return NormalizeInterface(id);
    }

    private static IReadOnlyList<KeyboardDeviceInfo> Enumerate()
    {
        var result = new List<KeyboardDeviceInfo>();
        var seenPhysical = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var itemSize = (uint)Marshal.SizeOf<RAWINPUTDEVICELIST>();
        uint count = 0;
        if (Native.GetRawInputDeviceList(IntPtr.Zero, ref count, itemSize) == uint.MaxValue || count == 0)
            return result;

        var buffer = Marshal.AllocHGlobal((int)(itemSize * count));
        try
        {
            var got = Native.GetRawInputDeviceList(buffer, ref count, itemSize);
            if (got == uint.MaxValue)
                return result;

            for (var i = 0; i < got; i++)
            {
                var item = Marshal.PtrToStructure<RAWINPUTDEVICELIST>(IntPtr.Add(buffer, (int)(i * itemSize)));
                if (item.dwType != Native.RimTypeKeyboard)
                    continue;

                var id = DeviceName(item.hDevice);
                if (string.IsNullOrWhiteSpace(id) || IsIgnored(id))
                    continue;
                if (!seenPhysical.Add(PhysicalKey(id)))
                    continue;

                result.Add(new KeyboardDeviceInfo(item.hDevice, id, BuildLabel(id)));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        DeduplicateLabels(result);
        return result;
    }

    private static string DeviceName(IntPtr handle)
    {
        uint chars = 0;
        Native.GetRawInputDeviceInfo(handle, Native.RidIDeviceName, IntPtr.Zero, ref chars);
        if (chars < 2)
            return "";

        var bytes = checked((int)chars * sizeof(char));
        var buffer = Marshal.AllocHGlobal(bytes);
        try
        {
            var size = chars;
            if (Native.GetRawInputDeviceInfo(handle, Native.RidIDeviceName, buffer, ref size) == uint.MaxValue)
                return "";
            return Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsIgnored(string id)
    {
        return Contains(id, "ROOT")
            || Contains(id, "RDP")
            || Contains(id, "TERMINAL")
            || Contains(id, "VIRTUAL")
            || Contains(id, "DUMMY");
    }

    private static bool Contains(string id, string token) =>
        id.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static string BuildLabel(string id)
    {
        var product = DeviceProperty(id, Native.DevpkeyFriendlyName)
            ?? DeviceProperty(id, Native.DevpkeyName);
        if (!string.IsNullOrWhiteSpace(product) &&
            !product.Equals("HID Keyboard Device", StringComparison.OrdinalIgnoreCase) &&
            !product.Equals("HID-compliant keyboard", StringComparison.OrdinalIgnoreCase) &&
            !product.Equals("USB 输入设备", StringComparison.OrdinalIgnoreCase) &&
            !product.Equals("USB Input Device", StringComparison.OrdinalIgnoreCase))
        {
            return product;
        }

        if (Contains(id, "ACPI"))
            return "板载键盘";

        var vid = Extract(id, "VID_");
        var pid = Extract(id, "PID_");
        if (vid.Length > 0 && pid.Length > 0)
            return $"VID_{vid} PID_{pid}";

        return product is { Length: > 0 } ? product : "键盘";
    }

    private static void DeduplicateLabels(List<KeyboardDeviceInfo> devices)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
            counts[device.Label] = counts.GetValueOrDefault(device.Label) + 1;

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (counts[device.Label] < 2)
                continue;
            var n = seen.GetValueOrDefault(device.Label) + 1;
            seen[device.Label] = n;
            devices[i] = device with { Label = $"{device.Label}（{n}）" };
        }
    }

    private static string? DeviceProperty(string id, DEVPROPKEY key)
    {
        try
        {
            var path = NormalizeInterface(id);
            uint size = 0;
            Native.CM_Get_Device_Interface_PropertyW(path, in key, out _, null, ref size, 0);
            if (size < 2)
                return null;

            var buffer = new byte[size];
            if (Native.CM_Get_Device_Interface_PropertyW(path, in key, out var type, buffer, ref size, 0) != Native.CrSuccess)
                return null;
            if (type != Native.DevpropTypeString)
                return null;

            var text = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeInterface(string id)
    {
        if (id.StartsWith(@"\??\", StringComparison.Ordinal))
            return @"\\?\" + id[4..];
        return id;
    }

    private static string Extract(string id, string token)
    {
        var at = id.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return "";
        var start = at + token.Length;
        var length = 0;
        while (start + length < id.Length && Uri.IsHexDigit(id[start + length]))
            length++;
        return length == 0 ? "" : id.Substring(start, length).ToUpperInvariant();
    }
}
