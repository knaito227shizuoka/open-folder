using System.Text.Json;

namespace OpenFolder;

public class PathEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsWsl { get; set; }
    public string ChipTheme { get; set; } = "DarkBlack";

    public string DisplayName => Name;

    public override string ToString() => $"{DisplayName}  ({Path})";

    public static string NormalizeChipTheme(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "DARK" or "DARK1" or "DARKBLACK" => "DarkBlack",
            "DARK2" => "DarkBlue",
            "DARK3" => "DarkRed",
            "DARK4" => "DarkGreen",
            "DARKBLUE" => "DarkBlue",
            "DARKRED" => "DarkRed",
            "DARKGREEN" => "DarkGreen",
            "LIGHT" or "LIGHT1" or "LIGHTWHITE" => "LightWhite",
            "LIGHT2" => "LightBlue",
            "LIGHT3" => "LightRed",
            "LIGHT4" => "LightGreen",
            "LIGHTBLUE" => "LightBlue",
            "LIGHTRED" => "LightRed",
            "LIGHTGREEN" => "LightGreen",
            _ => "DarkBlack",
        };
    }

    public static Color GetChipColor(string theme)
    {
        return NormalizeChipTheme(theme) switch
        {
            "DarkBlack" => Color.FromArgb(40, 44, 60),
            "DarkBlue" => Color.FromArgb(34, 59, 96),
            "DarkRed" => Color.FromArgb(93, 42, 50),
            "DarkGreen" => Color.FromArgb(43, 78, 58),
            "LightWhite" => Color.FromArgb(244, 246, 250),
            "LightBlue" => Color.FromArgb(218, 232, 252),
            "LightRed" => Color.FromArgb(252, 224, 226),
            "LightGreen" => Color.FromArgb(221, 241, 226),
            _ => Color.FromArgb(40, 44, 60),
        };
    }

    public static Color GetFontColorForBackground(Color background)
    {
        return background.GetBrightness() > 0.62f ? Color.Black : Color.White;
    }

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
