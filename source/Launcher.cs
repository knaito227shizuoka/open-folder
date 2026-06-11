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
            // 例: wsl -e bash -lc "code '/home/user/project'"
            psi = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"-e bash -lc \"{editor} '{entry.Path}'\"",
            };
        }
        else
        {
            // code/cursor 等は .cmd シムなので cmd 経由で起動する
            psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c {editor} \"{entry.Path}\"",
            };
        }
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        Process.Start(psi);
    }

    /// <summary>\\wsl.localhost\Ubuntu\home\... 形式のパスを WSL 内パスに変換する。</summary>
    public static bool TryConvertUncWslPath(string path, out string wslPath)
    {
        wslPath = "";
        foreach (var prefix in new[] { @"\\wsl.localhost\", @"\\wsl$\" })
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = path.Substring(prefix.Length);
            var slash = rest.IndexOf('\\');
            if (slash < 0) return false;
            wslPath = rest.Substring(slash).Replace('\\', '/');
            return wslPath.Length > 0;
        }
        return false;
    }
}
