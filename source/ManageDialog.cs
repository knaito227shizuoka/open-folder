namespace OpenFolder;

/// <summary>登録パスの一覧管理(追加・編集・削除)ダイアログ。一覧は名前の昇順。</summary>
public class ManageDialog : Form
{
    private readonly ListBox _list = new();
    private readonly List<PathEntry> _entries;

    public ManageDialog(List<PathEntry> entries)
    {
        _entries = entries;

        Text = "パスの管理";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 280);
        MinimumSize = new Size(420, 220);
        Font = new Font("Yu Gothic UI", 9f);

        _list.SetBounds(12, 12, 400, 256);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _list.IntegralHeight = false;
        _list.DoubleClick += (_, _) => Edit();

        var buttons = new (string Text, Action Action)[]
        {
            ("追加", Add), ("編集", Edit), ("削除", Remove),
        };
        var y = 12;
        foreach (var (text, action) in buttons)
        {
            var button = new Button { Text = text, Bounds = new Rectangle(424, y, 84, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            button.Click += (_, _) => action();
            Controls.Add(button);
            y += 34;
        }

        var closeButton = new Button
        {
            Text = "閉じる",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(424, 240, 84, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        CancelButton = closeButton;
        Controls.Add(closeButton);
        Controls.Add(_list);

        Refill(0);
    }

    private void Refill(int selectIndex)
    {
        _list.Items.Clear();
        foreach (var entry in _entries) _list.Items.Add(entry);
        if (_entries.Count > 0)
            _list.SelectedIndex = Math.Clamp(selectIndex, 0, _entries.Count - 1);
    }

    private void Add()
    {
        using var dialog = new EntryDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _entries.Add(dialog.Entry);
        _entries.Sort(PathEntry.ByName);
        Refill(_entries.IndexOf(dialog.Entry));
    }

    private void Edit()
    {
        if (_list.SelectedIndex < 0) return;
        var entry = _entries[_list.SelectedIndex];
        using var dialog = new EntryDialog(entry);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _entries.Sort(PathEntry.ByName);
        Refill(_entries.IndexOf(entry));
    }

    private void Remove()
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        if (MessageBox.Show(this, $"「{_entries[i].Name}」を削除しますか?", "open-folder",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _entries.RemoveAt(i);
        Refill(i);
    }

}
