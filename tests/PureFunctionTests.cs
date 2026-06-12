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

    private static int Main()
    {
        GenerateName_Cases();
        TryConvertUncWslPath_Cases();
        NormalizeChipTheme_Cases();
        QuoteCmdArgument_Cases();

        Console.WriteLine();
        Console.WriteLine($"結果: {_passed} passed, {_failed} failed");
        return _failed;
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

    // ---- 簡易アサーション ----

    private static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine($"[{name}]");
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
