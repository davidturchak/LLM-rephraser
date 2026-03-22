using System;
using System.Threading;
using System.Windows.Forms;

namespace LlmRephraser;

static class Program
{
    private const string MutexName = "Global\\LlmRephraser_SingleInstance_Mutex";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "LLM-Rephraser is already running.\nCheck the system tray.",
                "LLM-Rephraser",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
