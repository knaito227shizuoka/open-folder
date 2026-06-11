namespace OpenFolder;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 多重起動防止(常駐ウィジェットが重なって表示されるのを防ぐ)
        using var mutex = new Mutex(true, @"Local\open-folder-widget", out var isFirstInstance);
        if (!isFirstInstance) return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
