using System.Text.Json;

namespace OpenFolder;

public class PathEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsWsl { get; set; }

    public override string ToString() => $"{Name}  ({Path})";

    /// <summary>パスから表示名を機械的に生成する。例: "WSL : github - hoge", "C : github - huga"</summary>
    public static string GenerateName(string path, bool isWsl)
    {
        var parts = path.Replace('\\', '/').TrimEnd('/').Split('/')
            .Where(p => p.Length > 0).ToArray();
        var env = isWsl ? "WSL"
            : parts.Length > 0 && parts[0].EndsWith(":") ? parts[0].TrimEnd(':').ToUpperInvariant()
            : "Win";
        var folder = parts.Length > 0 ? parts[^1] : path;
        var parent = parts.Length >= 2 ? parts[^2] : "";
        if (parent.EndsWith(":")) parent = ""; // ドライブ直下は親を省略
        return parent.Length > 0 ? $"{env} : {parent} - {folder}" : $"{env} : {folder}";
    }

    public static readonly Comparison<PathEntry> ByName =
        (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
}

public class AppConfig
{
    public List<PathEntry> Entries { get; set; } = new();
    public string Editor { get; set; } = "code";   // code | cursor | antigravity
    public bool AlwaysOnTop { get; set; } = true;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }

    private static readonly string FilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "open-folder", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
                config.Entries.Sort(PathEntry.ByName);
                return config;
            }
        }
        catch
        {
            // 壊れた設定ファイルは初期化する
        }
        return new AppConfig();
    }

    public void Save()
    {
        Entries.Sort(PathEntry.ByName);
        var dir = System.IO.Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
