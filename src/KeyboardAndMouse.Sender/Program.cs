using KeyboardAndMouse;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppHost.SetDpiAware();

        if (args.Length < 1 || args[0] is "-h" or "--help" or "/?")
        {
            PrintUsage();
            return args.Length < 1 ? 1 : 0;
        }

        var host = args[0];
        var port = Protocol.DefaultPort;
        if (args.Length > 1 && (!int.TryParse(args[1], out port) || port is <= 0 or > 65535))
        {
            Console.WriteLine("端口无效。");
            return 1;
        }

        Console.Title = "键鼠映射 - 发送端";
        Console.WriteLine("发送端：拦截本机键鼠，经 LiteNetLib 发到接收端。");
        Console.WriteLine($"目标 {host}:{port}");
        Console.WriteLine("Ctrl+Alt+Q  开关拦截（开启后本机键鼠会被阻断）");
        Console.WriteLine("Ctrl+Alt+X  退出并恢复本机输入");
        Console.WriteLine();

        using var client = new MapperClient();
        using var hook = new InputHook();

        client.Connecting += (h, p) => Console.WriteLine($"正在连接 {h}:{p} ...");
        client.Connected += () =>
            Console.WriteLine("已连接。按 Ctrl+Alt+Q 开始拦截。");

        client.Disconnected += reason =>
        {
            hook.Capturing = false;
            Console.WriteLine($"连接断开（{reason}），已恢复本机输入。");
        };

        hook.ToggleRequested += () =>
        {
            if (!client.IsConnected)
            {
                Console.WriteLine("尚未连接，无法开启拦截。");
                return;
            }

            hook.Capturing = !hook.Capturing;
            client.SendReleaseAll();
            Console.WriteLine(hook.Capturing
                ? "拦截已开启 —— 本机键盘鼠标已阻断，输入发往接收端。"
                : "拦截已关闭 —— 本机输入已恢复。");
        };

        hook.ExitRequested += () =>
        {
            hook.Capturing = false;
            client.SendReleaseAll();
            Console.WriteLine("正在退出...");
            AppHost.RequestQuit();
        };

        hook.EventCaptured += client.Enqueue;

        try
        {
            client.Start(host, port);
            hook.Install();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"启动失败：{ex.Message}");
            return 1;
        }

        AppHost.RunMessageLoop();
        hook.Capturing = false;
        client.SendReleaseAll();
        Thread.Sleep(50);
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法: KeyboardAndMouse.Sender <接收端IP> [端口]");
        Console.WriteLine($"默认端口 {Protocol.DefaultPort}");
        Console.WriteLine("示例: KeyboardAndMouse.Sender 192.168.1.8");
        Console.WriteLine("本机自测: KeyboardAndMouse.Sender 127.0.0.1");
    }
}
