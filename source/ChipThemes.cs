namespace OpenFolder;

/// <summary>
/// パス行の配色(テーマ)を扱う。設定モデル(<see cref="PathEntry"/>)とは分離し、
/// 描画側だけが参照する。`PathEntry.ChipTheme` に保存される文字列を色へ解決する。
/// </summary>
public static class ChipThemes
{
    /// <summary>保存値(旧エイリアス含む)を正規のテーマ名へ正規化する。
    /// 旧形式 `DARK1` 等の受理は後方互換のため維持すること。</summary>
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
}
