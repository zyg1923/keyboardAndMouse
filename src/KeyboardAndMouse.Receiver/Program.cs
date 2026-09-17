using KeyboardAndMouse;

internal static class Program
{
    private static int Main(string[] args)
    {
        AppHost.SetDpiAware();

        if (args is ["-h" or "--help" or "/?"])
        {
            PrintUsage();
            return 0;
        }

        var port = Protocol.DefaultPort;
        if (args.Length > 0 && (!int.TryParse(args[0], out port) || port is <= 0 or > 65535))
        {
            Console.WriteLine("端口无效。");
            return 1;
        }

        Console.Title = "键鼠映射 - 接收端";
        Console.WriteLine("接收端：等待发送端连接，并把事件注入为本机输入。");
        Console.WriteLine($"监听 UDP {port}");
        Console.WriteLine("本机 IPv4：");
        var anyIp = false;
        foreach (var ip in MapperServer.LocalIPv4Addresses())
        {
            anyIp = true;
            Console.WriteLine($"  {ip}");
        }

        if (!anyIp)
            Console.WriteLine("  (未找到 IPv4 地址)");

        Console.WriteLine();
        Console.WriteLine("发送端请执行: KeyboardAndMouse.Sender <上面的IP>");
        Console.WriteLine("Ctrl+C 退出。若要控制已提权窗口，请以管理员运行本程序。");
        Console.WriteLine();

        var injector = new InputInjector();
        using var server = new MapperServer();
        server.ClientConnected += ip => Console.WriteLine($"发送端已连接：{ip}");
        server.ClientDisconnected += info => Console.WriteLine($"发送端断开：{info}，已松开按键。");
        server.Received += ev => injector.Apply(ev);

        try
        {
            server.Start(port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"启动失败：{ex.Message}");
            return 1;
        }

        Console.WriteLine("等待连接...");
        using var exit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            injector.ReleaseAll();
            exit.Set();
        };
        exit.Wait();
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法: KeyboardAndMouse.Receiver [端口]");
        Console.WriteLine($"默认端口 {Protocol.DefaultPort}");
    }
}
