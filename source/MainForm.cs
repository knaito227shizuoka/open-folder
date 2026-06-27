using System.Runtime.InteropServices;

namespace OpenFolder;

/// <summary>
/// 画面上に表示しないホットキー受け取り専用ウィンドウ。
/// Ctrl+Alt+Z で LauncherDialog を表示/非表示トグルする。
/// </summary>
public class MainForm : Form
{
    // グローバルホットキー: Ctrl+Alt+Z
    private const int  HotkeyId  = 1;
    private const uint MOD_CTRL  = 0x0002;
    private const uint MOD_ALT   = 0x0001;
    private const uint VK_Z      = 0x5A;
    private const int  WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly AppConfig _config = AppConfig.Load();
    private LauncherDialog? _dialog;

    public MainForm()
    {
        Text              = "open-folder";
        FormBorderStyle   = FormBorderStyle.None;
        ShowInTaskbar     = false;
        StartPosition     = FormStartPosition.Manual;
        Opacity           = 0;
        MinimumSize = MaximumSize = new Size(1, 1);
        ClientSize        = new Size(1, 1);

        FormClosing += (_, _) =>
        {
            _dialog?.RealClose();
            UnregisterHotKey(Handle, HotkeyId);
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, HotkeyId, MOD_CTRL | MOD_ALT, VK_Z);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            ToggleLauncher();
            return;
        }
        base.WndProc(ref m);
    }

    private void ToggleLauncher()
    {
        if (_dialog != null && !_dialog.IsDisposed && _dialog.Visible)
        {
            _dialog.Hide();
            return;
        }
        if (_dialog == null || _dialog.IsDisposed)
            _dialog = new LauncherDialog(_config);
        _dialog.ResetAndShow();
    }
}
