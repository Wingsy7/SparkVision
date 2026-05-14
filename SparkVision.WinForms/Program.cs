using SparkVision.WinForms.Forms;

namespace SparkVision.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FormDashboard());
    }
}
