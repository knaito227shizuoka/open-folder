namespace OpenFolder;

public static class EntrySearch
{
    /// <summary>
    /// 名前・パス・WSLディストリ名の部分一致(大文字小文字無視)でフィルタし、名前昇順で返す。
    /// query が空の場合は全件返す。
    /// </summary>
    public static List<PathEntry> Filter(IEnumerable<PathEntry> entries, string query)
    {
        var sorted = entries.ToList();
        sorted.Sort(PathEntry.ByName);
        if (string.IsNullOrEmpty(query)) return sorted;
        return sorted.Where(e =>
            e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            e.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(e.WslDistro) && e.WslDistro.Contains(query, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }
}
