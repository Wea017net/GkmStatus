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
        static void Main()
        {
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
                MainForm mainForm = new();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"起動エラー: {ex.Message}\n{ex.StackTrace}", "GkmStatus Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}