namespace KeyboardAndMouse.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ShowCrash(ex);
        };

        try
        {
            if (!SingleInstance.TryOwn())
                return;

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ShowCrash(ex);
        }
    }

    private static void ShowCrash(Exception ex)
    {
        try
        {
            MessageBox.Show(ex.ToString(), "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // ignore UI errors while crashing
        }
    }
}
