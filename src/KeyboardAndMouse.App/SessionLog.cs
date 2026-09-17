using System.Collections.Concurrent;
using System.Text;

namespace KeyboardAndMouse.App;

internal sealed class SessionLog : IDisposable
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;
    private readonly string _path;
    private bool _disposed;

    public string Path => _path;

    public SessionLog()
    {
        var day = DateTime.Now.ToString("yyyyMMdd");
        var dir = System.IO.Path.Combine(ExeDirectory(), "log", day);
        Directory.CreateDirectory(dir);

        var id = 1;
        while (true)
        {
            var candidate = System.IO.Path.Combine(dir, id + ".log");
            var legacy = System.IO.Path.Combine(dir, id.ToString());
            if (File.Exists(legacy) || Directory.Exists(candidate) || Directory.Exists(legacy))
            {
                id++;
                continue;
            }

            try
            {
                using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                }

                _path = candidate;
                break;
            }
            catch (IOException)
            {
                id++;
            }
        }

        File.WriteAllText(_path, $"# {DateTime.Now:yyyy-MM-dd HH:mm:ss} 启动{Environment.NewLine}", Encoding.UTF8);

        _thread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "session-log"
        };
        _thread.Start();
    }

    public void Append(string message)
    {
        if (_disposed)
            return;
        _queue.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void WriteLoop()
    {
        try
        {
            using var writer = new StreamWriter(_path, append: true, Encoding.UTF8) { AutoFlush = true };
            while (!_cts.IsCancellationRequested || !_queue.IsEmpty)
            {
                if (_queue.TryDequeue(out var line))
                {
                    writer.WriteLine(line);
                    continue;
                }

                Thread.Sleep(50);
            }
        }
        catch
        {
            // ignore disk errors
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cts.Cancel();
        _thread.Join(1000);
        _cts.Dispose();
    }

    private static string ExeDirectory()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            var dir = System.IO.Path.GetDirectoryName(exe);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;
        }

        return AppContext.BaseDirectory;
    }
}
