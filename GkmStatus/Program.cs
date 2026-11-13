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

            // 想定外の例外でアプリが終了するときも、原因をログに残す
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                    GkmStatus.src.ErrorLog.Write("Unhandled exception", exception);
            };

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();

            using var mutex = new System.Threading.Mutex(false, "Wea017net_GkmStatus_Singleton");
            if (!mutex.WaitOne(0, false))
            {
                MessageBox.Show("すでに起動しています。", "GkmStatus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            GkmStatus.src.UpdaterService.CleanupTemporaryUpdaterFromArguments(args);
            GkmStatus.src.UpdaterService.CleanupStaleTemporaryUpdaters();

            try
            {
                bool showAfterUpdate = args.Any(argument => argument.Equals("--show-after-update", StringComparison.Ordinal));
                MainForm mainForm = new(showAfterUpdate);
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                GkmStatus.src.ErrorLog.Write("Startup", ex);
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}", "GkmStatus Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
