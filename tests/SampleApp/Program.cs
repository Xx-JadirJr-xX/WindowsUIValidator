namespace SampleApp;

/// <summary>
/// Sample WinForms application with deliberately planted layout bugs.
/// It is the target used to verify that every validation rule fires.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
