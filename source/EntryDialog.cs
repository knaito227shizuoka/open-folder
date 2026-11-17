namespace OpenFolder;

/// <summary>パスの追加・編集ダイアログ。名前はパスから自動生成される。</summary>
public class EntryDialog : Form
{
    private readonly TextBox _pathBox = new();
    private readonly CheckBox _wslCheck = new();
    private readonly Label _namePreview = new();
    private readonly List<ThemeButton> _themeButtons = new();

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

        var themeGroup = new GroupBox { Text = "背景色", Bounds = new Rectangle(70, 104, 376, 100) };
        string[] darkThemes = { "DarkBlack", "DarkBlue", "DarkRed", "DarkGreen", "DarkYellow" };
        string[] lightThemes = { "LightWhite", "LightBlue", "LightRed", "LightGreen", "LightYellow" };
        string[] darkTexts = { "黒", "青", "赤", "緑", "黄" };
        string[] lightTexts = { "白", "青", "赤", "緑", "黄" };
        for (var i = 0; i < darkThemes.Length; i++)
        {
            AddThemeButton(themeGroup, darkThemes[i], darkTexts[i], 10 + i * 72, 22);
            AddThemeButton(themeGroup, lightThemes[i], lightTexts[i], 10 + i * 72, 60);
        }
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
        var button = new ThemeButton(ChipThemes.GetChipColor(theme))
        {
            Text = text,
            Tag = theme,
            Bounds = new Rectangle(x, y, 66, 32),
        };
        button.Click += (_, _) => SelectTheme(button);
        _themeButtons.Add(button);
        parent.Controls.Add(button);
    }

    private void SelectTheme(ThemeButton selected)
    {
        foreach (var button in _themeButtons)
            button.Checked = button == selected;
    }

    private void ApplyStyleSelection()
    {
        var theme = ChipThemes.NormalizeChipTheme(Entry.ChipTheme);
        foreach (var button in _themeButtons)
            button.Checked = string.Equals((string?)button.Tag, theme, StringComparison.Ordinal);
    }

    /// <summary>色見本+色名を枠で囲んだ選択ボタン。選択中は赤い太枠で示す。</summary>
    private sealed class ThemeButton : Control
    {
        private readonly Color _swatch;
        private bool _checked;
        private bool _hover;

        public ThemeButton(Color swatch)
        {
            _swatch = swatch;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable, true);
        }

        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_hover ? SystemColors.ControlLight : SystemColors.Window);

            var swatch = new Rectangle(8, (Height - 18) / 2, 18, 18);
            using (var b = new SolidBrush(_swatch)) g.FillRectangle(b, swatch);
            g.DrawRectangle(Pens.Gray, swatch);

            var textRect = new Rectangle(swatch.Right + 4, 0, Width - swatch.Right - 8, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

            if (_checked)
            {
                using var pen = new Pen(Color.Red, 3);
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else
            {
                g.DrawRectangle(SystemPens.ControlDark, 0, 0, Width - 1, Height - 1);
            }

            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(4, 4, Width - 8, Height - 8));
        }
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
