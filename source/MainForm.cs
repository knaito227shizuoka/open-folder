using System.Drawing.Drawing2D;

namespace OpenFolder;

/// <summary>
/// デスクトップに常駐する小型ウィジェット。
/// マウスオーバー: 登録パス一覧 / 右クリック: 設定メニュー / ドラッグ: 移動
/// </summary>
public class MainForm : Form
{
    private const int BaseSize = 56;       // 96dpi基準の論理サイズ
    private const int DragThreshold = 5;

    private readonly AppConfig _config = AppConfig.Load();
    private readonly FanPopup _popup = new();
    private readonly System.Windows.Forms.Timer _hoverWatch = new() { Interval = 100 };
    private readonly int _widgetSize;
    private Point _mouseDownScreen;
    private Point _dragOffset;
    private bool _dragging;

    public MainForm()
    {
        Text = "open-folder";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;

        float dpiScale;
        using (var g = CreateGraphics()) dpiScale = g.DpiX / 96f;
        _widgetSize = (int)(BaseSize * dpiScale);

        // OSの最小ウィンドウサイズ(約136x39)へのクランプを防ぐ
        MinimumSize = new Size(_widgetSize, _widgetSize);
        MaximumSize = new Size(_widgetSize, _widgetSize);
        ClientSize = new Size(_widgetSize, _widgetSize);

        BackColor = Color.FromArgb(40, 44, 60);
        TopMost = _config.AlwaysOnTop;
        DoubleBuffered = true;

        RestorePosition();
        ApplyRoundedRegion();

        MouseEnter += (_, _) => { if (!_dragging) ShowPopup(); };
        _hoverWatch.Tick += (_, _) => CheckHoverAway();

        MouseDown += OnWidgetMouseDown;
        MouseMove += OnWidgetMouseMove;
        MouseUp += OnWidgetMouseUp;
        FormClosing += (_, _) => SavePosition();
    }

    private void RestorePosition()
    {
        // 初期位置はプライマリ画面の作業領域(タスクバーを除く)右下
        var work = Screen.PrimaryScreen!.WorkingArea;
        var x = _config.WindowX ?? work.Right - _widgetSize - 24;
        var y = _config.WindowY ?? work.Bottom - _widgetSize - 24;

        var area = SystemInformation.VirtualScreen;
        Location = new Point(
            Math.Clamp(x, area.Left, area.Right - _widgetSize),
            Math.Clamp(y, area.Top, area.Bottom - _widgetSize));
    }

    private void SavePosition()
    {
        _config.WindowX = Location.X;
        _config.WindowY = Location.Y;
        _config.Save();
    }

    private void ApplyRoundedRegion()
    {
        Region = new Region(Draw.RoundedRect(0, 0, _widgetSize, _widgetSize, _widgetSize * 0.25f));
    }

    /// <summary>SFチックなアイコンをベクター描画する(56x56基準の座標系)。
    /// 六角形のネオンフレーム+発光コア+周回アーク。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var s = _widgetSize / 56f;
        var c = 28f * s; // 中心

        var cyan = Color.FromArgb(0, 229, 255);
        var violet = Color.FromArgb(150, 110, 255);

        // 背景(深い宇宙っぽい紺のグラデーション)
        using (var bg = new LinearGradientBrush(
                   new Rectangle(0, 0, _widgetSize, _widgetSize),
                   Color.FromArgb(20, 26, 48), Color.FromArgb(8, 10, 22), 65f))
            g.FillRectangle(bg, 0, 0, _widgetSize, _widgetSize);

        // 六角形フレーム(頂点が上)
        var hex = new PointF[6];
        for (var i = 0; i < 6; i++)
        {
            var rad = (90f - 60f * i) * MathF.PI / 180f;
            hex[i] = new PointF(c + 19 * s * MathF.Cos(rad), c - 19 * s * MathF.Sin(rad));
        }
        using (var dim = new Pen(Color.FromArgb(70, cyan), 4.5f * s) { LineJoin = LineJoin.Round })
            g.DrawPolygon(dim, hex); // にじみ(外側の淡い線)
        using (var frame = new Pen(Color.FromArgb(220, cyan), 1.6f * s) { LineJoin = LineJoin.Round })
            g.DrawPolygon(frame, hex);

        // コアの発光ハロー(中心が明るく外へ消えるグラデーション)
        using (var halo = new GraphicsPath())
        {
            halo.AddEllipse(c - 13 * s, c - 13 * s, 26 * s, 26 * s);
            using var glow = new PathGradientBrush(halo)
            {
                CenterColor = Color.FromArgb(200, 140, 240, 255),
                SurroundColors = new[] { Color.FromArgb(0, cyan) },
            };
            g.FillPath(glow, halo);
        }

        // コア本体
        using (var core = new SolidBrush(Color.White))
            g.FillEllipse(core, c - 3.2f * s, c - 3.2f * s, 6.4f * s, 6.4f * s);

        // 周回アーク(シアンと紫の2本、端は丸く)
        var orbit = new RectangleF(c - 14.5f * s, c - 14.5f * s, 29 * s, 29 * s);
        using (var arc1 = new Pen(Color.FromArgb(230, cyan), 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arc1, orbit.X, orbit.Y, orbit.Width, orbit.Height, -35, 95);
        using (var arc2 = new Pen(Color.FromArgb(210, violet), 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(arc2, orbit.X, orbit.Y, orbit.Width, orbit.Height, 140, 70);

        // アーク先端の衛星ドット
        using (var dot = new SolidBrush(cyan))
        {
            var rad = 62f * MathF.PI / 180f; // arc1の終端あたり
            g.FillEllipse(dot,
                c + 14.5f * s * MathF.Cos(rad) - 2f * s,
                c + 14.5f * s * MathF.Sin(rad) - 2f * s, 4f * s, 4f * s);
        }
        using (var dot2 = new SolidBrush(violet))
        {
            var rad = 135f * MathF.PI / 180f;
            g.FillEllipse(dot2,
                c + 14.5f * s * MathF.Cos(rad) - 1.6f * s,
                c + 14.5f * s * MathF.Sin(rad) - 1.6f * s, 3.2f * s, 3.2f * s);
        }
    }

    // ---- ドラッグ移動 と クリック判定 ----

    private void OnWidgetMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _mouseDownScreen = Cursor.Position;
        _dragOffset = e.Location;
        _dragging = false;
        Capture = true;
    }

    private void OnWidgetMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Capture) return;
        var current = Cursor.Position;
        if (!_dragging)
        {
            var dx = Math.Abs(current.X - _mouseDownScreen.X);
            var dy = Math.Abs(current.Y - _mouseDownScreen.Y);
            if (dx < DragThreshold && dy < DragThreshold) return;
            _dragging = true;
            _popup.HideNow(); // ドラッグ中はポップアップを消す
        }
        Location = new Point(current.X - _dragOffset.X, current.Y - _dragOffset.Y);
    }

    private void OnWidgetMouseUp(object? sender, MouseEventArgs e)
    {
        Capture = false;
        if (e.Button == MouseButtons.Right)
        {
            _popup.HideNow();
            BuildSettingsMenu().Show(this, e.Location);
        }
        else if (e.Button == MouseButtons.Left)
        {
            if (_dragging)
                SavePosition();
            else
                ShowPopup(); // クリックでも出せるようにしておく
        }
        _dragging = false;
    }

    // ---- ホバーポップアップ ----

    private void ShowPopup()
    {
        if (_popup.Visible && !_popup.IsFadingOut) return;

        var items = new List<(string Text, string ChipTheme, Action Action, Action? RightAction)>();
        if (_config.Entries.Count == 0)
        {
            items.Add(("パスを追加...", "DarkBlack", ManageEntries, ManageEntries));
        }
        else
        {
            foreach (var entry in _config.Entries)
            {
                var captured = entry;
                items.Add((entry.DisplayName, entry.ChipTheme, () => OpenEntry(captured), () => EditEntry(captured)));
            }
        }

        _popup.TopMost = TopMost;
        _popup.ShowAt(items, Bounds);
        _hoverWatch.Start();
    }

    /// <summary>カーソルがウィジェットとポップアップの外に出たらフェードアウトする。</summary>
    private void CheckHoverAway()
    {
        if (!_popup.Visible)
        {
            _hoverWatch.Stop();
            return;
        }
        var chipSafe = _popup.ChipAreaScreenBounds;
        chipSafe.Inflate(10, 10);
        var iconSafe = Bounds;
        iconSafe.Inflate(10, 10);

        var cursor = Cursor.Position;
        if (!chipSafe.Contains(cursor) && !iconSafe.Contains(cursor))
        {
            _hoverWatch.Stop();
            _popup.FadeOut();
        }
    }

    // ---- メニュー ----

    private ContextMenuStrip BuildSettingsMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("パスの管理...", null, (_, _) => ManageEntries());
        menu.Items.Add(new ToolStripSeparator());

        var editorMenu = new ToolStripMenuItem($"エディタ: {Launcher.EditorLabel(_config.Editor)}");
        foreach (var (command, label) in Launcher.Editors)
        {
            var item = new ToolStripMenuItem(label) { Checked = _config.Editor == command };
            item.Click += (_, _) => { _config.Editor = command; _config.Save(); };
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
        menu.Items.Add("終了", null, (_, _) => Close());
        return menu;
    }

    // ---- 操作 ----

    private void ManageEntries()
    {
        using var dialog = new ManageDialog(_config.Entries);
        dialog.ShowDialog(this);
        _config.Save();
    }

    private void EditEntry(PathEntry entry)
    {
        using var dialog = new EntryDialog(entry);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _config.Entries.Sort(PathEntry.ByName);
        _config.Save();
    }

    private void OpenEntry(PathEntry entry)
    {
        try
        {
            Launcher.Open(entry, _config.Editor);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"起動に失敗しました:\n{ex.Message}", "open-folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
