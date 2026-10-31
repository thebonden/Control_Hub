namespace ControlHub.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--self-test")
            return PackageSelfTest.RunAsync(args[1]).GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        using var singleton = new Mutex(true, @"Local\ControlHub.Desktop.0.0.001", out var first);
        if (!first)
        {
            MessageBox.Show("Control Hub er allerede åbent. / Control Hub is already open.", "Control Hub");
            return 0;
        }
        try { Application.Run(new MainWindow()); return 0; }
        catch
        {
            MessageBox.Show("Control Hub kunne ikke åbnes. Indstillingerne er ikke ændret. Se installationsvejledningen.\nControl Hub could not open. Settings have not been changed. See the installation guide.", "Control Hub");
            return 1;
        }
    }
}
