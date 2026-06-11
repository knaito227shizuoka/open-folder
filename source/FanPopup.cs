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
/// マウスオーバーでアイコンの左上に扇型に展開するパス一覧。
/// per-pixelアルファの1枚のレイヤードウィンドウに全チップを描画する方式で、
/// 透明部分は自動でクリックスルーになる。フォーカスは奪わない。
/// </summary>
public class FanPopup : Form
{
    private const int ChipAnimMs = 180;   // 1チップの展開時間
    private const int StaggerMs = 25;     // チップごとの時差
    private const int FadeOutMs = 90;
    private const float ArcCenterDeg = 180f; // 扇の中心方向(90=真上, 180=真左)
    private const float MaxArcDeg = 80f;
    private const float StepDeg = 16f;

    private sealed class Chip
    {
        public string Text = "";
        public Action Action = () => { };
        public float Width;
        public PointF Dir;          // 展開方向の単位ベクトル
        public float FinalRight;
        public RectangleF FinalRect;
    }

    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };
    private readonly List<Chip> _chips = new();
    private Bitmap? _frame;
    private float _scale = 1f;
    private float _chipH;
    private float _radius;
    private PointF _origin;        // アイコン中心(ウィンドウ座標)
    private int _hover = -1;
    private long _t0;
    private bool _fadingOut;

    public bool IsFadingOut => _fadingOut;

    public FanPopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Font = new Font("Yu Gothic UI", 10f);

        _anim.Tick += (_, _) => Animate();
        MouseMove += (_, e) => SetHover(HitTest(e.Location));
        MouseLeave += (_, _) => SetHover(-1);
        MouseUp += OnChipMouseUp;
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

    /// <summary>アンカー(ウィジェット)の左上に扇型に展開して表示する。</summary>
    public void ShowAt(List<(string Text, Action Action)> items, Rectangle anchor)
    {
        _anim.Stop();
        _fadingOut = false;
        _hover = -1;
        _chips.Clear();

        using (var g = CreateGraphics())
        {
            _scale = g.DpiX / 96f;
            _chipH = 32 * _scale;
            foreach (var (text, action) in items)
            {
                var w = g.MeasureString(text, Font).Width + 26 * _scale;
                _chips.Add(new Chip { Text = text, Action = action, Width = w });
            }
        }

        // 弧の角度割りと、チップ同士が重ならない半径を決める
        var n = _chips.Count;
        var stepDeg = n > 1 ? Math.Min(StepDeg, MaxArcDeg / (n - 1)) : 0f;
        var stepRad = stepDeg * MathF.PI / 180f;
        var minRadius = n > 1 ? (_chipH + 2 * _scale) / stepRad : 0f;
        _radius = Math.Max(120 * _scale, minRadius);

        var anchorCenter = new PointF(anchor.Left + anchor.Width / 2f, anchor.Top + anchor.Height / 2f);
        var startDeg = ArcCenterDeg - stepDeg * (n - 1) / 2f;

        var tips = new PointF[n];
        var alignedRight = anchor.Left - 8 * _scale;
        for (var i = 0; i < n; i++)
        {
            var rad = (startDeg + stepDeg * i) * MathF.PI / 180f;
            var chip = _chips[i];
            chip.Dir = new PointF(MathF.Cos(rad), -MathF.Sin(rad));
            tips[i] = new PointF(anchorCenter.X + chip.Dir.X * _radius, anchorCenter.Y + chip.Dir.Y * _radius);
        }

        // 各チップの最終位置(スクリーン座標)から必要なウィンドウ範囲を求める。
        // 左に開く一覧なので、幅が違ってもアイコン近くの右端ラインに揃える。
        var union = new RectangleF(anchorCenter.X - 4, anchorCenter.Y - 4, 8, 8);
        for (var i = 0; i < n; i++)
        {
            var chip = _chips[i];
            chip.FinalRight = alignedRight;
            chip.FinalRect = new RectangleF(alignedRight - chip.Width, tips[i].Y - _chipH / 2, chip.Width, _chipH);
            union = RectangleF.Union(union, chip.FinalRect);
        }
        union.Inflate(12 * _scale, 12 * _scale);

        var bounds = Rectangle.Ceiling(union);
        MinimumSize = MaximumSize = bounds.Size;
        Bounds = bounds;

        // スクリーン座標 → ウィンドウ座標へ変換
        _origin = new PointF(anchorCenter.X - bounds.X, anchorCenter.Y - bounds.Y);
        foreach (var chip in _chips)
        {
            chip.FinalRight -= bounds.X;
            chip.FinalRect = new RectangleF(
                chip.FinalRect.X - bounds.X, chip.FinalRect.Y - bounds.Y,
                chip.FinalRect.Width, chip.FinalRect.Height);
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

    /// <summary>各チップを時差付きイージングで描画する。全チップ完了で true。</summary>
    private bool RenderFrame(long elapsed)
    {
        if (_frame == null) return true;
        var done = true;
        using var g = Graphics.FromImage(_frame);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        for (var i = 0; i < _chips.Count; i++)
        {
            var chip = _chips[i];
            var t = Math.Clamp((elapsed - (long)i * StaggerMs) / (float)ChipAnimMs, 0f, 1f);
            if (t < 1f) done = false;
            if (t <= 0f) continue;

            // ease-out back(軽いオーバーシュートで弾む感じに)
            const float c1 = 0.8f, c3 = c1 + 1f;
            var ease = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
            var alpha = Math.Clamp(t * 1.6f, 0f, 1f); // 透明度は先行して上げる

            var right = _origin.X + (chip.FinalRight - _origin.X) * ease;
            var centerY = _origin.Y + chip.Dir.Y * _radius * ease;
            var rect = new RectangleF(right - chip.Width, centerY - _chipH / 2, chip.Width, _chipH);

            DrawChip(g, rect, chip.Text, i == _hover, alpha);
        }
        return done;
    }

    private void DrawChip(Graphics g, RectangleF rect, string text, bool hover, float alpha)
    {
        var fill = hover ? Color.FromArgb(72, 104, 176) : Color.FromArgb(40, 44, 60);
        var a = (int)(alpha * 255);
        using var path = Draw.RoundedRect(rect.X, rect.Y, rect.Width, rect.Height, rect.Height / 2);
        using var brush = new SolidBrush(Color.FromArgb(a * 242 / 255, fill));
        using var pen = new Pen(Color.FromArgb(a * 70 / 255, 255, 255, 255), 1f);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        var textColor = hover ? Color.White : Color.Gainsboro;
        using var textBrush = new SolidBrush(Color.FromArgb(a, textColor));
        using var format = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        var textRect = new RectangleF(rect.X + 13 * _scale, rect.Y, rect.Width - 16 * _scale, rect.Height);
        g.DrawString(text, Font, textBrush, textRect, format);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _chips.Count; i++)
            if (_chips[i].FinalRect.Contains(p)) return i;
        return -1;
    }

    private void SetHover(int index)
    {
        if (_hover == index || _fadingOut) return;
        _hover = index;
        if (!_anim.Enabled) // 展開アニメ中はTickが描き直すので二重描画しない
        {
            RenderFrame(long.MaxValue);
            Push(255);
        }
    }

    private void OnChipMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        var i = HitTest(e.Location);
        if (i < 0) return;
        var action = _chips[i].Action;
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
