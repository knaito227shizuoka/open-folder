using System.Diagnostics;

namespace OpenFolder;

public static class Launcher
{
    public static readonly (string Command, string Label)[] Editors =
    {
        ("code", "VS Code"),
        ("cursor", "Cursor"),
        ("antigravity", "Antigravity"),
    };

    public static string EditorLabel(string command)
    {
        foreach (var (cmd, label) in Editors)
            if (cmd == command) return label;
        return command;
    }

    public static void Open(PathEntry entry, string editor)
    {
        ProcessStartInfo psi;
        if (entry.IsWsl)
        {
            // Windows側のエディタCLIへWSLリモートとして渡す。
            // WSL内の code/cursor シム有無やPATH差分に左右されにくい。
            var distro = string.IsNullOrWhiteSpace(entry.WslDistro) ? GetDefaultWslDistro() : entry.WslDistro;
            psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {editor} --remote wsl+{QuoteCmdArgument(distro)} {QuoteCmdArgument(entry.Path)}",
            };
        }
        else
        {
            // code/cursor 等は .cmd シムなので cmd 経由で起動する
            psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {editor} {QuoteCmdArgument(entry.Path)}",
            };
        }
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process.Start(psi);
    }

    /// <summary>\\wsl.localhost\Ubuntu\home\... 形式のパスを WSL 内パスに変換する。</summary>
    public static bool TryConvertUncWslPath(string path, out string wslPath)
    {
        return TryConvertUncWslPath(path, out wslPath, out _);
    }

    /// <summary>\\wsl.localhost\Ubuntu\home\... 形式のパスを WSL 内パスとディストリ名に変換する。</summary>
    public static bool TryConvertUncWslPath(string path, out string wslPath, out string distro)
    {
        wslPath = "";
        distro = "";
        foreach (var prefix in new[] { @"\\wsl.localhost\", @"\\wsl$\" })
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = path.Substring(prefix.Length);
            var slash = rest.IndexOf('\\');
            if (slash < 0) return false;
            distro = rest.Substring(0, slash);
            wslPath = rest.Substring(slash).Replace('\\', '/');
            return distro.Length > 0 && wslPath.Length > 0;
        }
        return false;
    }

    private static string? _cachedDefaultDistro;

    private static string GetDefaultWslDistro()
    {
        // 既定ディストリ名はセッション中変わらない前提でプロセス内にキャッシュする。
        if (_cachedDefaultDistro != null) return _cachedDefaultDistro;

        // wsl --list --quiet はデフォルトディストリを先頭に列挙する。
        // 出力は UTF-16LE なので StandardOutputEncoding を明示する。
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = "--list --quiet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.Unicode,
            CreateNoWindow = true,
        };
        process.Start();

        if (!process.WaitForExit(5000))
        {
            try { process.Kill(); } catch { /* 既に終了している場合は無視 */ }
            throw new InvalidOperationException("WSL の既定ディストリビューション名の取得がタイムアウトしました。");
        }

        var output = process.StandardOutput.ReadToEnd();
        var distro = output
            .Split(new[] { '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length > 0);

        if (process.ExitCode != 0 || distro == null)
            throw new InvalidOperationException("WSL の既定ディストリビューション名を取得できませんでした。");

        _cachedDefaultDistro = distro;
        return distro;
    }

    internal static string QuoteCmdArgument(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
