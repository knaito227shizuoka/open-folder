namespace OpenFolder;

/// <summary>パスの追加・編集ダイアログ。名前はパスから自動生成される。</summary>
public class EntryDialog : Form
{
    private readonly TextBox _pathBox = new();
    private readonly CheckBox _wslCheck = new();
    private readonly Label _namePreview = new();

    public PathEntry Entry { get; }

    public EntryDialog(PathEntry? entry = null)
    {
        Entry = entry ?? new PathEntry();

        Text = entry == null ? "パスを追加" : "パスを編集";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 150);
        Font = new Font("Yu Gothic UI", 9f);

        var pathLabel = new Label { Text = "パス", Location = new Point(12, 15), AutoSize = true };
        _pathBox.SetBounds(70, 12, 296, 23);
        _pathBox.Text = Entry.Path;
        _pathBox.TextChanged += (_, _) => UpdatePreview();

        var browseButton = new Button { Text = "...", Bounds = new Rectangle(372, 11, 34, 25) };
        browseButton.Click += (_, _) => Browse();

        _wslCheck.Text = "WSL内のパス (例: /home/user/project)";
        _wslCheck.Location = new Point(70, 43);
        _wslCheck.AutoSize = true;
        _wslCheck.Checked = Entry.IsWsl;
        _wslCheck.CheckedChanged += (_, _) => UpdatePreview();

        var nameLabel = new Label { Text = "名前", Location = new Point(12, 75), AutoSize = true };
        _namePreview.SetBounds(70, 75, 336, 23);
        _namePreview.ForeColor = SystemColors.GrayText;

        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Bounds = new Rectangle(240, 110, 80, 28) };
        var cancelButton = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(326, 110, 80, 28) };
        okButton.Click += (_, e) => { if (!Commit()) DialogResult = DialogResult.None; };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.AddRange(new Control[] { pathLabel, _pathBox, browseButton, _wslCheck, nameLabel, _namePreview, okButton, cancelButton });
        UpdatePreview();
    }

    /// <summary>WSL自動判定と名前プレビューの更新。</summary>
    private void UpdatePreview()
    {
        var path = _pathBox.Text.Trim();
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
        var path = _pathBox.Text.Trim();
        if (path.Length == 0)
        {
            MessageBox.Show(this, "パスを入力してください。", "open-folder");
            return false;
        }

        // \\wsl.localhost\... 形式は WSL パスに変換する
        if (Launcher.TryConvertUncWslPath(path, out var wslPath))
        {
            path = wslPath;
            _wslCheck.Checked = true;
        }

        Entry.Path = path;
        Entry.IsWsl = _wslCheck.Checked;
        Entry.Name = PathEntry.GenerateName(path, Entry.IsWsl);
        return true;
    }
}
