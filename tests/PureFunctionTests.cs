using OpenFolder;

namespace OpenFolder.Tests;

// 現在の挙動を固定する characterization test。
// 期待値は「現実装が返す値」であり、仕様の理想形ではない。
// リファクタでこれらが変わったら退行を疑うこと(意図的な仕様変更なら別途合意)。
//
// オフライン制約でテスト SDK が使えないため、依存ゼロの簡易ランナーで回す。
// 実行: dotnet run --project tests  (終了コード = 失敗数)

internal static class Program
{
    private static int _passed;
    private static int _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--display"))
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            DisplayLayout_Cases();
            return _failed;
        }
        GenerateName_Cases();
        TryConvertUncWslPath_Cases();
        NormalizeChipTheme_Cases();
        QuoteCmdArgument_Cases();
        EntrySearch_Cases();
        DirectoryForFile_Cases();

        Console.WriteLine();
        Console.WriteLine($"結果: {_passed} passed, {_failed} failed");
        return _failed;
    }

    private static void DirectoryForFile_Cases()
    {
        Eq("/home/hoge/huga", EntryDialog.DirectoryForFile("/home/hoge/huga/index.html"));
        Eq("/home/hoge/huga", EntryDialog.DirectoryForFile("/home/hoge/huga"));
        Eq("/home/hoge/huga/", EntryDialog.DirectoryForFile("/home/hoge/huga/"));
        Eq("/", EntryDialog.DirectoryForFile("/index.html"));
        Eq(@"C:\work\proj", EntryDialog.DirectoryForFile(@"C:\work\proj\README.md"));
        Eq(@"C:\work\proj", EntryDialog.DirectoryForFile(@"C:\work\proj"));
        Eq(@"C:\", EntryDialog.DirectoryForFile(@"C:\a.txt"));
        Eq(@"\\wsl.localhost\Ubuntu\home\hoge", EntryDialog.DirectoryForFile(@"\\wsl.localhost\Ubuntu\home\hoge\a.js"));
        Eq("LICENSE", EntryDialog.DirectoryForFile("LICENSE"));
    }

    private static void DisplayLayout_Cases()
    {
        // 設定ファイルやエディタには触れず、透明なフォームを各画面へ往復させる。
        var config = new AppConfig();
        for (var i = 0; i < 32; i++)
            config.Entries.Add(new PathEntry { Name = $"Entry {i:00}", Path = @"C:\test", IsWsl = i >= 12 });
        using var dialog = new LauncherDialog(config) { Opacity = 0 };
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var workField = typeof(LauncherDialog).GetField("_workArea", flags)!;
        var resize = typeof(LauncherDialog).GetMethod("ResizeToFit", flags)!;
        dialog.Show();
        foreach (var screen in Screen.AllScreens.Concat(Screen.AllScreens.Reverse()))
        {
            workField.SetValue(dialog, screen.WorkingArea);
            dialog.Location = screen.WorkingArea.Location;
            Application.DoEvents();
            resize.Invoke(dialog, null);
            Application.DoEvents();
            var lists = dialog.Controls.OfType<ListBox>().OrderBy(l => l.Left).ToArray();
            var list = lists[0];
            var right = lists[1];
            var dpi = dialog.DeviceDpi;
            Console.WriteLine($"Display {screen.DeviceName}: DPI={dpi}, form={dialog.ClientSize}, list={list.Size}");
            EqInt((int)Math.Round(32 * dpi / 96.0), list.ItemHeight);
            EqInt(12, list.Items.Count);
            EqInt(20, right.Items.Count);
            EqInt(1, list.Items.Cast<PathEntry>().All(e => !e.IsWsl) ? 1 : 0);
            EqInt(1, right.Items.Cast<PathEntry>().All(e => e.IsWsl) ? 1 : 0);
            EqInt(list.Right, right.Left);
            EqInt(dialog.ClientSize.Width, right.Right + list.Left);
            EqInt(dialog.ClientSize.Height, list.Bottom + (int)Math.Round(18 * dpi / 96.0));
            EqInt(1, screen.WorkingArea.Contains(dialog.Bounds) ? 1 : 0);
        }
        var query = dialog.Controls.OfType<Panel>().Single().Controls.OfType<TextBox>().Single();
        query.Text = "Entry 31";
        var columns = dialog.Controls.OfType<ListBox>().OrderBy(l => l.Left).ToArray();
        EqInt(0, columns[0].Items.Count);
        EqInt(1, columns[1].Items.Count);
        Eq("Entry 31", ((PathEntry)columns[1].SelectedItem!).Name);
        query.Text = "no match";
        EqInt(0, columns.Sum(l => l.Items.Count));
        query.Text = "Entry 00";
        EqInt(1, columns[0].Items.Count);
        EqInt(0, columns[1].Items.Count);
        dialog.RealClose();
    }

    private static void GenerateName_Cases()
    {
        Section(nameof(PathEntry.GenerateName));
        // path, isWsl => expected
        Eq("C : github - huga", PathEntry.GenerateName(@"C:\github\huga", false));
        Eq("D : b - c", PathEntry.GenerateName(@"D:\a\b\c", false));
        Eq("C : proj", PathEntry.GenerateName(@"C:\proj", false));            // ドライブ直下は親省略
        Eq("Win : relative", PathEntry.GenerateName("relative", false));       // ドライブ文字なし → Win
        Eq("WSL : user - project", PathEntry.GenerateName("/home/user/project", true));
        Eq("WSL : u - p", PathEntry.GenerateName("/home/u/p", true));
        Eq("WSL : single", PathEntry.GenerateName("/single", true));
    }

    private static void TryConvertUncWslPath_Cases()
    {
        Section(nameof(Launcher.TryConvertUncWslPath));
        ConvertOk(@"\\wsl.localhost\Ubuntu\home\u\p", "Ubuntu", "/home/u/p");
        ConvertOk(@"\\wsl$\Debian\root", "Debian", "/root");
        ConvertOk(@"\\WSL.LOCALHOST\Ubuntu\x", "Ubuntu", "/x");                // 大文字小文字無視
        ConvertFalse(@"C:\normal\path");
        ConvertFalse("/home/user");
        ConvertFalse(@"\\wsl.localhost\Ubuntu");                               // ディストリ名の後にパスがない
    }

    private static void NormalizeChipTheme_Cases()
    {
        Section(nameof(ChipThemes.NormalizeChipTheme));
        Eq("DarkBlack", ChipThemes.NormalizeChipTheme("DARK1"));
        Eq("DarkBlack", ChipThemes.NormalizeChipTheme("DARK"));
        Eq("DarkBlue", ChipThemes.NormalizeChipTheme("DARK2"));
        Eq("DarkRed", ChipThemes.NormalizeChipTheme("DARK3"));
        Eq("DarkGreen", ChipThemes.NormalizeChipTheme("DARK4"));
        Eq("LightWhite", ChipThemes.NormalizeChipTheme("LIGHT"));
        Eq("LightBlue", ChipThemes.NormalizeChipTheme("LIGHT2"));
        Eq("DarkBlue", ChipThemes.NormalizeChipTheme("darkblue"));              // 小文字も通る
        Eq("DarkBlack", ChipThemes.NormalizeChipTheme("unknown-value"));        // 不明値はデフォルト
    }

    private static void QuoteCmdArgument_Cases()
    {
        Section(nameof(Launcher.QuoteCmdArgument));
        Eq("\"simple\"", Launcher.QuoteCmdArgument("simple"));
        Eq("\"/home/u/p\"", Launcher.QuoteCmdArgument("/home/u/p"));
        Eq("\"a\"\"b\"", Launcher.QuoteCmdArgument("a\"b"));                   // " は "" にエスケープ
    }

    private static void EntrySearch_Cases()
    {
        Section(nameof(EntrySearch.Filter));
        var entries = new List<PathEntry>
        {
            new() { Name = "C : github - foo", Path = @"C:\github\foo" },
            new() { Name = "C : work - bar",   Path = @"C:\work\bar" },
            new() { Name = "WSL : home - baz", Path = "/home/user/baz", IsWsl = true, WslDistro = "Ubuntu" },
        };

        // 空クエリ → 全件(名前昇順)
        var all = EntrySearch.Filter(entries, "");
        EqInt(3, all.Count);
        Eq("C : github - foo", all[0].Name);
        Eq("C : work - bar",   all[1].Name);
        Eq("WSL : home - baz", all[2].Name);

        // 名前の部分一致
        var byName = EntrySearch.Filter(entries, "github");
        EqInt(1, byName.Count);
        Eq("C : github - foo", byName[0].Name);

        // パスの部分一致
        var byPath = EntrySearch.Filter(entries, "work");
        EqInt(1, byPath.Count);
        Eq("C : work - bar", byPath[0].Name);

        // 大文字小文字無視
        var ci = EntrySearch.Filter(entries, "GITHUB");
        EqInt(1, ci.Count);
        Eq("C : github - foo", ci[0].Name);

        // 0件
        var none = EntrySearch.Filter(entries, "zzz");
        EqInt(0, none.Count);
    }

    // ---- 簡易アサーション ----

    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"[{name}]");
    }

    private static void EqInt(int expected, int actual)
    {
        if (expected == actual)
        {
            _passed++;
            Console.WriteLine($"  PASS  {actual}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL  expected {expected} but got {actual}");
        }
    }

    private static void Eq(string expected, string actual)
    {
        if (expected == actual)
        {
            _passed++;
            Console.WriteLine($"  PASS  \"{actual}\"");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL  expected \"{expected}\" but got \"{actual}\"");
        }
    }

    private static void ConvertOk(string input, string expectedDistro, string expectedPath)
    {
        var ok = Launcher.TryConvertUncWslPath(input, out var wslPath, out var distro);
        if (ok && distro == expectedDistro && wslPath == expectedPath)
        {
            _passed++;
            Console.WriteLine($"  PASS  {input} -> {distro} : {wslPath}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL  {input} -> ok={ok} distro=\"{distro}\" path=\"{wslPath}\" (expected {expectedDistro} : {expectedPath})");
        }
    }

    private static void ConvertFalse(string input)
    {
        var ok = Launcher.TryConvertUncWslPath(input, out _, out _);
        if (!ok)
        {
            _passed++;
            Console.WriteLine($"  PASS  {input} -> false");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL  {input} -> expected false but returned true");
        }
    }
}


