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

        public static void SetPlaceholder(IntPtr handle, string placeholderText) { SendMessage(handle, EM_SETCUEBANNER, 0, placeholderText); }
        public static void SetPlaceholder(TextBox textBox, string placeholderText) { SetPlaceholder(textBox.Handle, placeholderText); }

    }
}