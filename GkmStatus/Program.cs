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
            bool ownsMutex;
            try
            {
                ownsMutex = mutex.WaitOne(0, false);
            }
            catch (System.Threading.AbandonedMutexException)
            {
                // 前のインスタンスが強制終了などで Mutex を解放せずに終わった場合。所有権はこのプロセスに移っている
                ownsMutex = true;
            }
            if (!ownsMutex)
            {
                // 起動中のインスタンスにウィンドウを表示させる
                // （前面化の権限を渡してから、全トップレベルウィンドウへ通知する）
                GkmStatus.src.native.Native.AllowSetForegroundWindow(GkmStatus.src.native.Native.ASFW_ANY);
                int message = GkmStatus.src.native.Native.WM_SHOW_EXISTING_INSTANCE;
                if (message == 0 || !GkmStatus.src.native.Native.PostMessage(
                        GkmStatus.src.native.Native.HWND_BROADCAST, message, IntPtr.Zero, IntPtr.Zero))
                {
                    MessageBox.Show("すでに起動しています。", "GkmStatus", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
