namespace OpenFolder;

/// <summary>
/// Ctrl+Alt+Z で表示する検索モーダル。ボーダーレスのダークテーマ。
/// キーボード: ↑↓で選択移動、Enter で起動、Esc/フォーカス外で閉じる、Tab でボタンへ移動。
/// </summary>
public class LauncherDialog : Form
{
    // ---- レイアウト定数(論理 px / 96 DPI 基準) ----
    private const int L_Width   = 900;  // 1列445px × 2列 + 外枠
    private const int L_Columns = 2;
    private const int L_TopBar  = 64;   // 検索行の高さ(余白込み)
    private const int L_Sep     = 1;    // セパレータ
    private const int L_RowH    = 32;   // 候補行の高さ
    private const int L_MinRows  = 3;
    private const int L_MaxRows  = 30;
    private const int L_RowPadL  = 12;  // 左余白 = 選択時の青バー幅
    private const int L_RowPadR  = 8;   // 右余白

    // ---- カラーパレット ----
    private static readonly Color C_Bg       = Color.FromArgb(22, 22, 38);
    private static readonly Color C_RowSel   = Color.FromArgb(52, 52, 80);
    private static readonly Color C_TextNorm = Color.FromArgb(195, 195, 218);
    private static readonly Color C_TextSel  = Color.White;
    private static readonly Color C_TextMatch = Color.FromArgb(130, 215, 255);  // シアン(マッチ箇所)
    private static readonly Color C_Border      = Color.FromArgb(130, 130, 185);
    private static readonly Color C_BorderInner = Color.FromArgb(58, 58, 88);
    private static readonly Color C_Sep      = Color.FromArgb(50, 50, 75);
    private static readonly Color C_SearchBg = Color.FromArgb(32, 32, 50);
    private static readonly Color C_BtnBg    = Color.FromArgb(42, 42, 64);
    private static readonly Color C_BtnHov   = Color.FromArgb(60, 60, 90);

    private readonly AppConfig _config;
    private List<PathEntry> _filteredEntries = new();
    private bool _realClose;
    private Rectangle _workArea;

    private readonly Panel   _searchPanel;
    private readonly TextBox _queryBox;
    private readonly Button  _settingsButton;
    private readonly Button  _addButton;
    private readonly ListBox _windowsList;
    private readonly ListBox _wslList;
    private ListBox _list = null!;

    private void SelectIndex(int index)
    {
        if (index < 0 || index >= _filteredEntries.Count) return;
        var entry = _filteredEntries[index];
        _list = entry.IsWsl ? _wslList : _windowsList;
        (entry.IsWsl ? _windowsList : _wslList).ClearSelected();
        _list.SelectedItem = entry;
    }
    private Font _boldFont = null!;

    public LauncherDialog(AppConfig config)
    {
        _config = config;
        // 座標はすべて96 DPI基準から再計算する。自動拡縮との二重適用を防ぐ。
        AutoScaleMode = AutoScaleMode.None;
        _workArea = Screen.FromPoint(Cursor.Position).WorkingArea;

        Font      = new Font("Yu Gothic UI", 10f);
        _boldFont = new Font(Font, FontStyle.Bold);

        // ---- フォームスタイル ----
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        StartPosition   = FormStartPosition.Manual;
        KeyPreview      = true;
        BackColor       = C_Bg;
        ForeColor       = C_TextNorm;

        // ---- コントロール配置 ----
        const int sp    = 12;   // 左右余白
        const int qh    = 32;   // TextBox 高さ(上下に余裕)
        const int panH  = qh + 4;  // 検索パネル高さ(上下 2px 余白)
        const int btnH  = 32;   // ボタン高さ
        const int btn1W = 66;
        const int btn2W = 84;
        const int gap   = 6;
        const int panY  = (L_TopBar - panH) / 2;
        const int btnY  = (L_TopBar - btnH) / 2;
        const int qw    = L_Width - sp - gap - btn1W - gap - btn2W - sp;

        // 検索パネル: TextBox を包んでフォーカス枠を描く
        _searchPanel = new Panel
        {
            Location  = new Point(sp, panY),
            Size      = new Size(qw, panH),
            BackColor = C_SearchBg,
        };
        _searchPanel.Paint += (_, pe) =>
        {
            var col = (_queryBox?.Focused == true) ? Color.White : C_Border;
            using var pen = new Pen(col);
            pe.Graphics.DrawRectangle(pen, 0, 0, _searchPanel.Width - 1, _searchPanel.Height - 1);
        };

        _queryBox = new TextBox
        {
            Location        = new Point(4, 0),   // y は後で PreferredHeight から計算
            Size            = new Size(qw - 8, qh),
            PlaceholderText = "検索...",
            BorderStyle     = BorderStyle.None,
            BackColor       = C_SearchBg,
            ForeColor       = C_TextNorm,
            TabIndex        = 0,
            Font            = new Font("Yu Gothic UI", 10.5f),
        };
        // BorderStyle.None の TextBox は高さ自動決定のため、実際の高さでセンタリング
        _queryBox.Location = new Point(4, Math.Max(0, (panH - _queryBox.PreferredHeight) / 2));
        _queryBox.GotFocus  += (_, _) => _searchPanel.Invalidate();
        _queryBox.LostFocus += (_, _) => _searchPanel.Invalidate();
        _searchPanel.Controls.Add(_queryBox);

        _settingsButton = MakeButton("設定", sp + qw + gap, btnY, btn1W, btnH, tabIndex: 1);
        _addButton      = MakeButton("新規追加", sp + qw + gap + btn1W + gap, btnY, btn2W, btnH, tabIndex: 2);

        // ボタンフォーカス時に白枠
        foreach (var btn in new[] { _settingsButton, _addButton })
        {
            btn.GotFocus  += (_, _) => btn.FlatAppearance.BorderColor = Color.White;
            btn.LostFocus += (_, _) => btn.FlatAppearance.BorderColor = C_Border;
        }

        _windowsList = MakeList();
        _wslList = MakeList();
        _list = _windowsList;
        // ---- イベント ----
        _queryBox.TextChanged += OnQueryChanged;
        _queryBox.KeyDown     += OnQueryKeyDown;
        _settingsButton.Click += OnSettingsClick;
        _addButton.Click      += OnAddClick;




        KeyDown    += (_, e) => { if (e.KeyCode == Keys.Escape) { e.Handled = true; Hide(); } };
        Deactivate += (_, _) => Hide();
        FormClosing += (_, e) => { if (!_realClose) { e.Cancel = true; Hide(); } };

        Controls.AddRange(new Control[] { _searchPanel, _settingsButton, _addButton, _windowsList, _wslList });
        ApplyFilter();
        ResizeToFit();
    }

    private ListBox MakeList()
    {
        var list = new ListBox
        {
            DrawMode            = DrawMode.OwnerDrawFixed,
            ItemHeight          = L_RowH,
            SelectionMode       = SelectionMode.One,
            BorderStyle         = BorderStyle.None,
            BackColor           = C_Bg,
            ForeColor           = C_TextNorm,
            TabStop             = false,
            IntegralHeight      = false,
            HorizontalScrollbar = false,
            MultiColumn         = false,
            ColumnWidth         = (L_Width - 10) / L_Columns,
            Location            = new Point(5, L_TopBar + L_Sep),
        };

        list.DrawItem += OnDrawItem;
        list.MouseClick += OnListMouseClick;
        list.MouseMove += OnListMouseMove;
        list.KeyDown += OnQueryKeyDown;
        return list;
    }

    private Button MakeButton(string text, int x, int y, int w, int h, int tabIndex)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = new Point(x, y),
            Size      = new Size(w, h),
            FlatStyle = FlatStyle.Flat,
            BackColor = C_BtnBg,
            ForeColor = C_TextNorm,
            TabIndex  = tabIndex,
        };
        btn.FlatAppearance.BorderColor         = C_Border;
        btn.FlatAppearance.MouseOverBackColor  = C_BtnHov;
        btn.FlatAppearance.MouseDownBackColor  = Color.FromArgb(70, 70, 105);
        return btn;
    }

    // ---- OnPaint: フォームボーダー + セパレータ ----

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // 検索欄とリストの区切り線
        using (var sepBrush = new SolidBrush(C_Sep))
            g.FillRectangle(sepBrush, 0, Px(L_TopBar), ClientSize.Width, Px(L_Sep));

        // フォームの外枠(2重線)
        using var penOuter = new Pen(C_Border, Px(2));
        g.DrawRectangle(penOuter, Px(1), Px(1), ClientSize.Width - Px(2), ClientSize.Height - Px(2));
        using var penInner = new Pen(C_BorderInner);
        g.DrawRectangle(penInner, Px(4), Px(4), ClientSize.Width - Px(8), ClientSize.Height - Px(8));
    }

    // ---- 公開 API ----

    public void ResetAndShow()
    {
        _workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        // 先に対象画面へ移し、その画面のDPIが確定してからレイアウトする。
        Location = new Point(_workArea.Left + (_workArea.Width - Width) / 2,
            _workArea.Top + (_workArea.Height - Height) / 2);
        _queryBox.Text = "";
        ApplyFilter();
        SelectIndex(0);
        TopMost = _config.AlwaysOnTop;

        if (!Visible) Show();

        // BeginInvoke で WM_DPICHANGED / オートスケール完了後に実行する。
        // Show() の前に Width を使って計算すると DPI スケール前の論理ピクセル値と
        // WorkingArea の物理ピクセル値が混在してずれるため、確定後に再計算する。
        BeginInvoke(() =>
        {
            ResizeToFit();
            CenterOnActiveScreen();
            _queryBox.Location = new Point(
                _queryBox.Location.X,
                Math.Max(0, (_searchPanel.Height - _queryBox.PreferredHeight) / 2));
            Activate();
            _queryBox.Focus();
        });
    }

    public void RealClose()
    {
        _realClose = true;
        Close();
    }

    // ---- レイアウト ----

    private int Px(int value) => (int)Math.Round(value * DeviceDpi / 96.0);

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        // OSが提案する外枠サイズの適用後に、中身も同じDPIで確定する。
        BeginInvoke(() =>
        {
            if (IsDisposed || _realClose) return;
            ResizeToFit();
            CenterOnActiveScreen();
        });
    }

    private void ResizeToFit()
    {
        var work = _workArea;
        var width = Math.Min(Px(L_Width), work.Width - Px(24));
        var inset = Px(5);
        var top = Px(L_TopBar);
        var sep = Px(L_Sep);

        _windowsList.ItemHeight = _wslList.ItemHeight = Px(L_RowH);
        var maxRows = Math.Max(1, Math.Min(L_MaxRows,
            (work.Height - top - sep - inset - Px(24)) / _list.ItemHeight));
        var rows = Math.Min(maxRows, Math.Max(L_MinRows,
            Math.Max(_config.Entries.Count(e => !e.IsWsl), _config.Entries.Count(e => e.IsWsl))));
        // 環境ごとに列を固定し、各列で縦スクロールする。
        var listH = rows * _list.ItemHeight;
        SuspendLayout();
        ClientSize = new Size(width, top + sep + listH + inset);
        var columnWidth = (width - inset * 2) / L_Columns;
        _windowsList.SetBounds(inset, top + sep, columnWidth, listH);
        _wslList.SetBounds(inset + columnWidth, top + sep, width - inset * 2 - columnWidth, listH);
        var sp = Px(12);
        var gap = Px(6);
        _addButton.SetBounds(width - sp - Px(84), Px(16), Px(84), Px(32));
        _settingsButton.SetBounds(_addButton.Left - gap - Px(66), Px(16), Px(66), Px(32));
        _searchPanel.SetBounds(sp, Px(14), _settingsButton.Left - gap - sp, Px(36));
        _queryBox.Width = _searchPanel.Width - Px(8);
        _queryBox.Location = new Point(Px(4), Math.Max(0, (_searchPanel.Height - _queryBox.PreferredHeight) / 2));
        ResumeLayout(false);
        Invalidate(true);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    private void CenterOnActiveScreen()
    {
        var work = _workArea;
        Location = new Point(
            work.Left + (work.Width  - Width)  / 2,
            work.Top  + (work.Height - Height) / 2);
    }

    // ---- フィルタ ----

    private void ApplyFilter()
    {
        _filteredEntries = EntrySearch.Filter(_config.Entries, _queryBox.Text.Trim());
        _filteredEntries = _filteredEntries.Where(e => !e.IsWsl)
            .Concat(_filteredEntries.Where(e => e.IsWsl)).ToList();
        foreach (var list in new[] { _windowsList, _wslList })
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in _filteredEntries.Where(e => e.IsWsl == (list == _wslList)))
                list.Items.Add(entry);
            list.EndUpdate();
        }
        SelectIndex(0);
    }
    // ---- キーボード ----

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down:  e.SuppressKeyPress = true; MoveSelection(+1); break;
            case Keys.Up:    e.SuppressKeyPress = true; MoveSelection(-1); break;
            case Keys.Enter: e.SuppressKeyPress = true; OpenSelected();    break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_filteredEntries.Count == 0) return;
        var n    = _filteredEntries.Count;
        var cur = _list.SelectedItem is PathEntry selected ? _filteredEntries.IndexOf(selected) : -1;
        var next = cur < 0 ? (delta > 0 ? 0 : n - 1) : (cur + delta + n) % n;
        SelectIndex(next);
    }

    private void OpenSelected()
    {
        if (_list.SelectedItem is PathEntry entry) OpenEntry(entry);
    }
    private void OpenEntry(PathEntry entry)
    {
        Hide();
        try { Launcher.Open(entry, _config.Editor); }
        catch (Exception ex)
        {
            MessageBox.Show(null, $"起動に失敗しました:\n{ex.Message}", "open-folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnQueryChanged(object? sender, EventArgs e)
    {
        ApplyFilter();
        SelectIndex(0);
    }

    // ---- 描画 ----

    protected override void Dispose(bool disposing)
    {
        if (disposing) _boldFont.Dispose();
        base.Dispose(disposing);
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0 || e.Index >= list.Items.Count) return;
        var entry = (PathEntry)list.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) != 0;
        var g        = e.Graphics;

        var chipColor = ChipThemes.GetChipColor(entry.ChipTheme);

        // フォーム背景色で行全体を塗る(左右余白 + 行間 1px がここになる)
        using (var bgBrush = new SolidBrush(C_Bg))
            g.FillRectangle(bgBrush, e.Bounds);

        // チップカラーを左右余白の内側・行間 1px の下に塗る
        var chipX    = e.Bounds.X + Px(L_RowPadL);
        var chipW    = e.Bounds.Width - Px(L_RowPadL) - Px(L_RowPadR);
        var chipRect = new Rectangle(chipX, e.Bounds.Y + 1, chipW, e.Bounds.Height - 1);
        using (var chipBrush = new SolidBrush(chipColor))
            g.FillRectangle(chipBrush, chipRect);

        // 選択時: 余白幅ぴったりの左右バー + 上下 2px ライン(隙間なし)
        if (selected)
        {
            var bTB = Px(4);
            var cw = e.Bounds.Width - Px(L_RowPadL) - Px(L_RowPadR);
            using var b = new SolidBrush(Color.FromArgb(80, 165, 255));
            g.FillRectangle(b, e.Bounds.X,                  e.Bounds.Y,             Px(L_RowPadL), e.Bounds.Height); // 左(余白全体)
            g.FillRectangle(b, e.Bounds.Right - Px(L_RowPadR),  e.Bounds.Y,             Px(L_RowPadR), e.Bounds.Height); // 右(余白全体)
            g.FillRectangle(b, e.Bounds.X + Px(L_RowPadL),      e.Bounds.Y,             cw,        bTB);             // 上
            g.FillRectangle(b, e.Bounds.X + Px(L_RowPadL),      e.Bounds.Bottom - bTB,  cw,        bTB);             // 下
        }

        // テキスト色はチップ背景の輝度で自動判定
        var baseTextColor = ChipThemes.GetFontColorForBackground(chipColor);
        var isDark        = chipColor.GetBrightness() <= 0.6f;
        var matchColor    = isDark ? C_TextMatch : Color.FromArgb(0, 90, 210);

        var textX    = chipX + Px(8);
        var textRect = new Rectangle(textX, e.Bounds.Y, chipRect.Right - textX - Px(4), e.Bounds.Height);
        var state = g.Save();
        g.SetClip(textRect, System.Drawing.Drawing2D.CombineMode.Intersect);
        DrawHighlightedText(g, entry.Name, _queryBox.Text.Trim(), Font, _boldFont, baseTextColor, matchColor, textRect);
        g.Restore(state);
    }

    private static void DrawHighlightedText(
        Graphics g, string text, string query,
        Font normalFont, Font boldFont,
        Color normalColor, Color matchColor, Rectangle rect)
    {
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;

        var matchIndex = string.IsNullOrEmpty(query)
            ? -1
            : text.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        if (matchIndex < 0)
        {
            TextRenderer.DrawText(g, text, normalFont, rect, normalColor, flags | TextFormatFlags.EndEllipsis);
            return;
        }

        var before = text[..matchIndex];
        var match  = text[matchIndex..(matchIndex + query.Length)];
        var after  = text[(matchIndex + query.Length)..];
        var x = rect.X;

        if (before.Length > 0)
        {
            var w = TextRenderer.MeasureText(g, before, normalFont, rect.Size, flags).Width;
            TextRenderer.DrawText(g, before, normalFont, new Rectangle(x, rect.Y, w, rect.Height), normalColor, flags);
            x += w;
        }
        if (match.Length > 0 && x < rect.Right)
        {
            var w = TextRenderer.MeasureText(g, match, boldFont, new Size(rect.Right - x, rect.Height), flags).Width;
            TextRenderer.DrawText(g, match, boldFont, new Rectangle(x, rect.Y, w, rect.Height), matchColor, flags);
            x += w;
        }
        if (after.Length > 0 && x < rect.Right)
        {
            TextRenderer.DrawText(g, after, normalFont,
                new Rectangle(x, rect.Y, rect.Right - x, rect.Height), normalColor,
                flags | TextFormatFlags.EndEllipsis);
        }
    }

    // ---- マウス操作 ----

    private void OnListMouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not ListBox list) return;
        var i = list.IndexFromPoint(e.Location);
        if (i >= 0) SelectIndex(_filteredEntries.IndexOf((PathEntry)list.Items[i]));
    }

    private void OnListMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || sender is not ListBox list) return;
        var i = list.IndexFromPoint(e.Location);
        if (i >= 0) OpenEntry((PathEntry)list.Items[i]);
    }
    // ---- 設定ボタン ----

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        BuildSettingsMenu().Show(_settingsButton, new Point(0, _settingsButton.Height));
    }

    private ContextMenuStrip BuildSettingsMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("パスの管理...", null, (_, _) => ManageEntries());
        menu.Items.Add(new ToolStripSeparator());

        var editorMenu = new ToolStripMenuItem($"エディタ: {Launcher.EditorLabel(_config.Editor)}");
        foreach (var (command, label) in Launcher.Editors)
        {
            var item = new ToolStripMenuItem(label) { Checked = _config.Editor == command };
            var cmd  = command;
            item.Click += (_, _) => { _config.Editor = cmd; _config.Save(); };
            editorMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(editorMenu);

        var topMostItem = new ToolStripMenuItem("常に手前に表示") { Checked = _config.AlwaysOnTop };
        topMostItem.Click += (_, _) =>
        {
            _config.AlwaysOnTop = !_config.AlwaysOnTop;
            TopMost = _config.AlwaysOnTop;
            _config.Save();
        };
        menu.Items.Add(topMostItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => Application.Exit());
        return menu;
    }

    private void ManageEntries()
    {
        using var dialog = new ManageDialog(_config.Entries);
        dialog.ShowDialog(this);
        _config.Save();
        ResizeToFit();
        ApplyFilter();
        SelectIndex(0);
        Show(); Activate(); _queryBox.Focus();
    }

    // ---- 新規追加ボタン ----

    private void OnAddClick(object? sender, EventArgs e)
    {
        using var dialog = new EntryDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            Show(); Activate(); _queryBox.Focus();
            return;
        }
        _config.Entries.Add(dialog.Entry);
        _config.Entries.Sort(PathEntry.ByName);
        _config.Save();
        ResizeToFit();
        ApplyFilter();

        var idx = _filteredEntries.IndexOf(dialog.Entry);
        if (idx >= 0)
        {
            SelectIndex(idx);
        }
        else
        {
            _queryBox.Text = "";
            ApplyFilter();
            idx = _filteredEntries.IndexOf(dialog.Entry);
            if (idx >= 0) SelectIndex(idx);
        }
        Show(); Activate(); _queryBox.Focus();
    }
}


