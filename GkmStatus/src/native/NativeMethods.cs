using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GkmStatus.src.native
{
    [SupportedOSPlatform("windows")]
    internal static partial class Native
    {
        public const int EM_SETCUEBANNER = 0x1501;

        [LibraryImport("user32.dll", EntryPoint = "SendMessageW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyIcon(IntPtr hIcon);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        public static readonly IntPtr HWND_BROADCAST = new(0xFFFF);
        public const int ASFW_ANY = -1;

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        public static partial int RegisterWindowMessage(string lpString);

        [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AllowSetForegroundWindow(int dwProcessId);

        public const int MSGFLT_ALLOW = 1;

        /// <summary>権限の低いプロセスから届くメッセージを、このウィンドウで受け取るかどうかを設定する。</summary>
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ChangeWindowMessageFilterEx(IntPtr hWnd, int message, int action, IntPtr changeFilterStruct);

        /// <summary>
        /// 二重起動されたときに、起動中のインスタンスへウィンドウ表示を依頼するためのメッセージ
        /// </summary>
        public static readonly int WM_SHOW_EXISTING_INSTANCE = RegisterWindowMessage("Wea017net_GkmStatus_ShowExistingInstance");

        public static void SetPlaceholder(IntPtr handle, string placeholderText) { SendMessage(handle, EM_SETCUEBANNER, 0, placeholderText); }
        public static void SetPlaceholder(TextBox textBox, string placeholderText) { SetPlaceholder(textBox.Handle, placeholderText); }

    }
}