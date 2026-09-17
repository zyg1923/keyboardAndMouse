using System.Text.Json;

namespace KeyboardAndMouse.App;

internal sealed class AppSettings
{
    public bool IsSender { get; set; } = true;
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9050;
    public string Channel { get; set; } = "both";
    public bool KeepLocalUi { get; set; }
    public string KeyboardDevice { get; set; } = "";
    /// <summary>UDP 轮询间隔毫秒：0=最快，1–8。</summary>
    public int SendIntervalMs { get; set; } = 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            var path = FilePath;
            if (!File.Exists(path))
                return new AppSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // ignore persistence errors
        }
    }
}
