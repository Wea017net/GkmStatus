using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Runtime.Versioning;

namespace GkmStatus
{
    [SupportedOSPlatform("windows")]
    internal static partial class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (GkmStatus.src.UpdaterService.TryRunUpdaterMode(args))
                return;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();

            using var mutex = new System.Threading.Mutex(false, "Wea017net_GkmStatus_Singleton");
            if (!mutex.WaitOne(0, false))
            {
                MessageBox.Show("すでに起動しています。", "GkmStatus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                try
                {
                    GkmStatus.src.UpdaterService.EnsureInstalled();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Could not prepare the updater: " + ex.Message);
                }

                bool showAfterUpdate = args.Any(argument => argument.Equals("--show-after-update", StringComparison.Ordinal));
                MainForm mainForm = new(showAfterUpdate);
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}", "GkmStatus Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
