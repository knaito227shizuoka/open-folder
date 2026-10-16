namespace OpenFolder;

/// <summary>パスの追加・編集ダイアログ。名前はパスから自動生成される。</summary>
public class EntryDialog : Form
{
    private readonly TextBox _pathBox = new();
    private readonly CheckBox _wslCheck = new();
    private readonly Label _namePreview = new();
    private readonly List<RadioButton> _themeButtons = new();

    public PathEntry Entry { get; }

    public EntryDialog(PathEntry? entry = null)
    {
        Entry = entry ?? new PathEntry();

        Text = entry == null ? "パスを追加" : "パスを編集";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 260);
        Font = new Font("Yu Gothic UI", 9f);

        var pathLabel = new Label { Text = "パス", Location = new Point(12, 15), AutoSize = true };
        _pathBox.SetBounds(70, 12, 336, 23);
        _pathBox.Text = Entry.Path;
        _pathBox.TextChanged += (_, _) => UpdatePreview();

        var browseButton = new Button { Text = "...", Bounds = new Rectangle(412, 11, 34, 25) };
        browseButton.Click += (_, _) => Browse();

        _wslCheck.Text = "WSL内のパス (例: /home/user/project)";
        _wslCheck.Location = new Point(70, 43);
        _wslCheck.AutoSize = true;
        _wslCheck.Checked = Entry.IsWsl;
        _wslCheck.CheckedChanged += (_, _) => UpdatePreview();

        var nameLabel = new Label { Text = "名前", Location = new Point(12, 75), AutoSize = true };
        _namePreview.SetBounds(70, 75, 376, 23);
        _namePreview.ForeColor = SystemColors.GrayText;

        var themeGroup = new GroupBox { Text = "背景色", Bounds = new Rectangle(70, 104, 376, 92) };
        AddThemeButton(themeGroup, "DarkBlack", "黒", 12, 24);
        AddThemeButton(themeGroup, "DarkBlue", "青", 102, 24);
        AddThemeButton(themeGroup, "DarkRed", "赤", 192, 24);
        AddThemeButton(themeGroup, "DarkGreen", "緑", 282, 24);
        AddThemeButton(themeGroup, "LightWhite", "白", 12, 58);
        AddThemeButton(themeGroup, "LightBlue", "青", 102, 58);
        AddThemeButton(themeGroup, "LightRed", "赤", 192, 58);
        AddThemeButton(themeGroup, "LightGreen", "緑", 282, 58);
        ApplyStyleSelection();

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Bounds = new Rectangle(280, 220, 80, 28) };
        var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(366, 220, 80, 28) };
        okButton.Click += (_, _) =>
        {
            if (!Commit()) DialogResult = DialogResult.None;
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.AddRange(new Control[]
        {
            pathLabel, _pathBox, browseButton, _wslCheck, nameLabel, _namePreview,
            themeGroup, okButton, cancelButton,
        });
        UpdatePreview();
    }

    private void AddThemeButton(Control parent, string theme, string text, int x, int y)
    {
        var color = ChipThemes.GetChipColor(theme);
        var swatch = new Panel
        {
            BackColor = color,
            BorderStyle = BorderStyle.FixedSingle,
            Bounds = new Rectangle(x, y + 3, 16, 16),
        };
        var button = new RadioButton
        {
            Text = text,
            Tag = theme,
            Bounds = new Rectangle(x + 20, y, 56, 22),
        };
        _themeButtons.Add(button);
        parent.Controls.AddRange(new Control[] { swatch, button });
    }

    private void ApplyStyleSelection()
    {
        var theme = ChipThemes.NormalizeChipTheme(Entry.ChipTheme);
        foreach (var button in _themeButtons)
            button.Checked = string.Equals((string?)button.Tag, theme, StringComparison.Ordinal);
    }

    /// <summary>WSL自動判定と名前プレビューの更新。</summary>
    private void UpdatePreview()
    {
        var path = DirectoryForFile(_pathBox.Text.Trim().Trim('"'));
        if (path.StartsWith("/"))
            _wslCheck.Checked = true;
        else if (path.Length >= 2 && path[1] == ':')
            _wslCheck.Checked = false;

        if (Launcher.TryConvertUncWslPath(path, out var wslPath))
        {
            path = wslPath;
            _wslCheck.Checked = true;
        }

        _namePreview.Text = path.Length == 0 ? "(パスから自動生成されます)"
            : PathEntry.GenerateName(path, _wslCheck.Checked);
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _pathBox.Text = dialog.SelectedPath;
    }

    private bool Commit()
    {
        var path = _pathBox.Text.Trim().Trim('"');
        if (path.Length == 0)
        {
            MessageBox.Show(this, "パスを入力してください。", "open-folder");
            return false;
        }

        // 最後の要素がファイル(ドットを含む)なら親フォルダへ補正。
        path = DirectoryForFile(path);
        string? wslDistro = null;
        if (Launcher.TryConvertUncWslPath(path, out var wslPath, out var detectedDistro))
        {
            path = wslPath;
            wslDistro = detectedDistro;
            _wslCheck.Checked = true;
        }

        Entry.Path = path;
        Entry.IsWsl = _wslCheck.Checked;
        Entry.WslDistro = Entry.IsWsl ? wslDistro ?? Entry.WslDistro : null;
        Entry.Name = PathEntry.GenerateName(path, Entry.IsWsl);
        Entry.ChipTheme = _themeButtons.FirstOrDefault(b => b.Checked)?.Tag as string ?? "DarkBlack";
        return true;
    }

    internal static string DirectoryForFile(string path)
    {
        // 最後の要素にドットがあればファイルとみなす(ドット付きフォルダは考慮外)。
        // WSL表記(/)とWindows表記(\)の両方を扱うため文字列だけで判定する。
        var sep = path.LastIndexOfAny(new[] { '/', '\\' });
        if (sep < 0 || sep == path.Length - 1 || !path[(sep + 1)..].Contains('.'))
            return path;
        var parent = path[..sep];
        // ルート直下(/index.html, C:\index.html)はルートを残す。
        if (parent.Length == 0 || (parent.Length == 2 && parent[1] == ':'))
            parent += path[sep];
        return parent;
    }
}
