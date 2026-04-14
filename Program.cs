using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LlmRephraser;

static class Program
{
    private const string MutexName = "Global\\LlmRephraser_SingleInstance_Mutex";
    public const string RephraseEventName = "LlmRephraser_RephraseClipboard_Event";

    [STAThread]
    static void Main(string[] args)
    {
        // Register Syncfusion license
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXtfdHRQRWRYUEZ2WkpWYEo=");

        using var mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            if (args.Contains("--rephrase-clipboard"))
            {
                // Signal the running instance to start rephrase from clipboard
                try
                {
                    using var evt = EventWaitHandle.OpenExisting(RephraseEventName);
                    evt.Set();
                }
                catch { /* event not found — running instance may not be ready */ }
            }
            else
            {
                MessageBox.Show(
                    "LLM-Rephraser is already running.\nCheck the system tray.",
                    "LLM-Rephraser",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
