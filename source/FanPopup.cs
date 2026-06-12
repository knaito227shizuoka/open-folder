using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OpenFolder;

internal static class Draw
{
    public static GraphicsPath RoundedRect(float x, float y, float w, float h, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// マウスオーバーでアイコンの下に表示するパス一覧パネル。
/// ダークな1枚パネルの中に行を縦に並べ、各行の左端にテーマ色のアクセントバーを置く。
/// per-pixelアルファの1枚のレイヤードウィンドウに全体を描画する方式で、
/// 透明部分は自動でクリックスルーになる。フォーカスは奪わない。
/// </summary>
public class FanPopup : Form
{
    private const int PanelAnimMs = 130;  // パネルのスライドイン時間
    private const int RowAnimMs = 110;    // 1行のフェードイン時間
    private const int StaggerMs = 22;     // 行ごとの時差
    private const int FadeOutMs = 90;

    // パネル配色(SF調ダーク + シアンのアクセント)
    private static readonly Color PanelFill = Color.FromArgb(13, 18, 28);
    private static readonly Color PanelBorder = Color.FromArgb(110, 190, 255);
    private static readonly Color HoverFill = Color.FromArgb(120, 200, 255);

    private sealed class Row
    {
        public string Text = "";
        public Action Action = () => { };
        public Action? RightAction;
        public Color FillColor;
        public Color TextColor;
        public float TextWidth;
        public RectangleF FinalRect;
    }

    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };
    private readonly List<Row> _rows = new();
    private Bitmap? _frame;
    private float _scale = 1f;
    private float _rowH;
    private RectangleF _panel;     // パネル最終位置(ウィンドウ座標)
    private int _hover = -1;
    private long _t0;
    private bool _fadingOut;

    public bool IsFadingOut => _fadingOut;

    public Rectangle ChipAreaScreenBounds
    {
        get
        {
            if (_rows.Count == 0) return Bounds;
            var bounds = Rectangle.Ceiling(_panel);
            bounds.Offset(Left, Top);
            return bounds;
        }
    }

    public FanPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Font = new Font("Yu Gothic UI", 10.8f);

        _anim.Tick += (_, _) => Animate();
        MouseMove += (_, e) => SetHover(HitTest(e.Location));
        MouseLeave += (_, _) => SetHover(-1);
        MouseUp += OnRowMouseUp;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000; // WS_EX_LAYERED (UpdateLayeredWindowで描画)
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    /// <summary>アンカー(ウィジェット)の下にパネルを表示する。右端はアンカー右端に揃える。</summary>
    public void ShowAt(List<(string Text, string ChipTheme, Action Action, Action? RightAction)> items, Rectangle anchor)
    {
        _anim.Stop();
        _fadingOut = false;
        _hover = -1;
        _rows.Clear();

        using (var g = CreateGraphics())
        {
            _scale = g.DpiX / 96f;
            _rowH = 30 * _scale;
            foreach (var (text, chipTheme, action, rightAction) in items)
            {
                var fillColor = PathEntry.GetChipColor(chipTheme);
                _rows.Add(new Row
                {
                    Text = text,
                    Action = action,
                    RightAction = rightAction,
                    FillColor = fillColor,
                    TextColor = PathEntry.GetFontColorForBackground(fillColor),
                    TextWidth = g.MeasureString(text, Font).Width,
                });
            }
        }

        var n = _rows.Count;
        var pad = 6f * _scale;                    // パネル内側余白
        var rowGap = 2f * _scale;
        var textLeft = 10f * _scale + 3f * _scale + 9f * _scale; // 行左端→バー→テキスト
        var maxTextW = _rows.Max(r => r.TextWidth);
        var rowW = Math.Max(120 * _scale, textLeft + maxTextW + 14 * _scale);

        var panelW = rowW + pad * 2;
        var panelH = n * _rowH + Math.Max(0, n - 1) * rowGap + pad * 2;
        var panelRight = (float)anchor.Right;
        var panelTop = anchor.Bottom + 8 * _scale;
        var panelScreen = new RectangleF(panelRight - panelW, panelTop, panelW, panelH);

        // スライドイン余白ぶんを含めてウィンドウ範囲を決める
        var union = panelScreen;
        union.Inflate(12 * _scale, 12 * _scale);
        var bounds = Rectangle.Ceiling(union);
        MinimumSize = MaximumSize = bounds.Size;
        Bounds = bounds;

        // スクリーン座標 → ウィンドウ座標へ変換
        _panel = new RectangleF(panelScreen.X - bounds.X, panelScreen.Y - bounds.Y, panelW, panelH);
        for (var i = 0; i < n; i++)
        {
            _rows[i].FinalRect = new RectangleF(
                _panel.X + pad, _panel.Y + pad + i * (_rowH + rowGap), rowW, _rowH);
        }

        _frame?.Dispose();
        _frame = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        _t0 = Environment.TickCount64;
        RenderFrame(0);
        Push(255);
        Show();
        _anim.Start();
    }

    public void FadeOut()
    {
        if (!Visible || _fadingOut) return;
        _fadingOut = true;
        _t0 = Environment.TickCount64;
        _anim.Start();
    }

    public void HideNow()
    {
        _anim.Stop();
        _fadingOut = false;
        Cursor = Cursors.Default;
        Hide();
    }

    private void Animate()
    {
        var elapsed = Environment.TickCount64 - _t0;
        if (_fadingOut)
        {
            var t = Math.Min(1f, elapsed / (float)FadeOutMs);
            Push((byte)(255 * (1 - t)));
            if (t >= 1f) HideNow();
        }
        else
        {
            var done = RenderFrame(elapsed);
            Push(255);
            if (done) _anim.Stop();
        }
    }

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>パネルをスライドイン、行を時差付きフェードインで描画する。全アニメ完了で true。</summary>
    private bool RenderFrame(long elapsed)
    {
        if (_frame == null) return true;
        using var g = Graphics.FromImage(_frame);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var panelT = Math.Clamp(elapsed / (float)PanelAnimMs, 0f, 1f);
        var panelEase = EaseOutCubic(panelT);
        var dy = -(1f - panelEase) * 8 * _scale; // アンカー側から滑り出す

        var panel = _panel;
        panel.Offset(0, dy);
        DrawPanel(g, panel, panelEase);

        var done = panelT >= 1f;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var t = Math.Clamp((elapsed - (long)i * StaggerMs) / (float)RowAnimMs, 0f, 1f);
            if (t < 1f) done = false;
            if (t <= 0f) continue;

            var ease = EaseOutCubic(t);
            var rect = row.FinalRect;
            rect.Offset(-(1f - ease) * 8 * _scale, dy);
            DrawRow(g, rect, row, i == _hover, ease * panelEase);
        }
        return done;
    }

    private void DrawPanel(Graphics g, RectangleF rect, float alpha)
    {
        var a = (int)(alpha * 255);
        var radius = 10 * _scale;
        using var path = Draw.RoundedRect(rect.X, rect.Y, rect.Width, rect.Height, radius);
        using var brush = new SolidBrush(Color.FromArgb(a * 235 / 255, PanelFill));
        using var pen = new Pen(Color.FromArgb(a * 100 / 255, PanelBorder), 1f);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        // 対角コーナーの短いブラケットだけでSF感を出す(左上・右下)
        var len = 16 * _scale;
        using var accentPen = new Pen(Color.FromArgb(a * 190 / 255, PanelBorder), 1.6f);
        g.DrawLine(accentPen, rect.X + radius, rect.Y, rect.X + radius + len, rect.Y);
        g.DrawLine(accentPen, rect.X, rect.Y + radius, rect.X, rect.Y + radius + len);
        g.DrawLine(accentPen, rect.Right - radius - len, rect.Bottom, rect.Right - radius, rect.Bottom);
        g.DrawLine(accentPen, rect.Right, rect.Bottom - radius - len, rect.Right, rect.Bottom - radius);
    }

    private void DrawRow(Graphics g, RectangleF rect, Row row, bool hover, float alpha)
    {
        var a = (int)(alpha * 255);

        if (hover)
        {
            using var hoverPath = Draw.RoundedRect(rect.X, rect.Y, rect.Width, rect.Height, 6 * _scale);
            using var hoverBrush = new SolidBrush(Color.FromArgb(a * 34 / 255, HoverFill));
            g.FillPath(hoverBrush, hoverPath);
        }

        var rowFill = hover ? AdjustBrightness(row.FillColor, 0.16f) : row.FillColor;
        using (var rowPath = Draw.RoundedRect(rect.X + 3 * _scale, rect.Y + 2 * _scale,
                   rect.Width - 6 * _scale, rect.Height - 4 * _scale, (rect.Height - 4 * _scale) / 2))
        using (var rowBrush = new SolidBrush(Color.FromArgb(a * 245 / 255, rowFill)))
        using (var rowPen = new Pen(Color.FromArgb(a * 72 / 255, row.TextColor), 1f))
        {
            g.FillPath(rowBrush, rowPath);
            g.DrawPath(rowPen, rowPath);
        }

        // 左端のテーマ色アクセントバー
        var barW = 3f * _scale;
        var barH = rect.Height - 12 * _scale;
        var barX = rect.X + 10 * _scale;
        var bar = hover ? AdjustBrightness(row.TextColor, 0.25f) : row.TextColor;
        using (var barPath = Draw.RoundedRect(barX, rect.Y + (rect.Height - barH) / 2, barW, barH, barW / 2))
        using (var barBrush = new SolidBrush(Color.FromArgb(a, bar)))
            g.FillPath(barBrush, barPath);

        using var textBrush = new SolidBrush(Color.FromArgb(a, row.TextColor));
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        var textX = barX + barW + 9 * _scale;
        var textRect = new RectangleF(textX, rect.Y, rect.Right - textX - 6 * _scale, rect.Height);
        g.DrawString(row.Text, Font, textBrush, textRect, format);
    }

    private static Color AdjustBrightness(Color color, float amount)
    {
        var light = color.GetBrightness() > 0.6f;
        var r = light ? color.R - (int)(color.R * amount) : color.R + (int)((255 - color.R) * amount);
        var g = light ? color.G - (int)(color.G * amount) : color.G + (int)((255 - color.G) * amount);
        var b = light ? color.B - (int)(color.B * amount) : color.B + (int)((255 - color.B) * amount);
        return Color.FromArgb(color.A, r, g, b);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _rows.Count; i++)
            if (_rows[i].FinalRect.Contains(p)) return i;
        return -1;
    }

    private void SetHover(int index)
    {
        if (_hover == index || _fadingOut) return;
        _hover = index;
        Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
        if (!_anim.Enabled) // 展開アニメ中はTickが描き直すので二重描画しない
        {
            RenderFrame(long.MaxValue);
            Push(255);
        }
    }

    private void OnRowMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
        var i = HitTest(e.Location);
        if (i < 0) return;
        var action = e.Button == MouseButtons.Right ? _rows[i].RightAction : _rows[i].Action;
        if (action == null) return;
        HideNow();
        action();
    }

    // ---- UpdateLayeredWindow による per-pixel アルファ描画 ----

    private void Push(byte globalAlpha)
    {
        if (_frame == null || !IsHandleCreated) return;
        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = _frame.GetHbitmap(Color.FromArgb(0));
        var oldBitmap = SelectObject(memDc, hBitmap);

        var size = new SIZE { cx = _frame.Width, cy = _frame.Height };
        var source = new POINT();
        var topPos = new POINT { x = Left, y = Top };
        var blend = new BLENDFUNCTION { SourceConstantAlpha = globalAlpha, AlphaFormat = 1 /*AC_SRC_ALPHA*/ };
        UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref source, 0, ref blend, 2 /*ULW_ALPHA*/);

        SelectObject(memDc, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, screenDc);
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst,
        ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
}
