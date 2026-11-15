using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace GkmStatus.src.native
{
    /// <summary>
    /// 管理者権限で動いているプロセス（Updater）から、通常の権限でプロセスを起動する。
    /// そのまま起動すると子プロセスも管理者権限になってしまうため、シェル（エクスプローラー）のトークンを複製して
    /// CreateProcessWithTokenW で起動する。シェルが動いていない・権限が足りないなどで起動できなかったときは false を返すので、
    /// 呼び出し側で通常の起動に切り替える。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static unsafe partial class UnelevatedProcess
    {
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public uint cb;
            public nint lpReserved;
            public nint lpDesktop;
            public nint lpTitle;
            public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public nint lpReserved2;
            public nint hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public nint hProcess;
            public nint hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [LibraryImport("user32.dll")]
        private static partial nint GetShellWindow();

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DuplicateTokenEx(nint existingToken, uint desiredAccess, nint tokenAttributes, int impersonationLevel, int tokenType, out nint newToken);

        [LibraryImport("advapi32.dll", EntryPoint = "CreateProcessWithTokenW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateProcessWithToken(
            nint token, uint logonFlags, string applicationName, char* commandLine, uint creationFlags,
            char* environment, string currentDirectory, ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(nint handle);

        /// <summary>
        /// 通常の権限で起動する。起動できたら true。
        /// このプロセスが管理者権限でないときは何もせず false を返す（failure は null。そのまま起動すれば同じ権限になる）。
        /// 起動に失敗したときは false を返し、failure に理由を入れる。
        /// </summary>
        public static bool TryStart(string applicationPath, IReadOnlyList<string> arguments, string workingDirectory, out string? failure)
        {
            failure = null;
            if (!Environment.IsPrivilegedProcess)
                return false;
            return TryStartWithShellToken(applicationPath, arguments, workingDirectory, out failure);
        }

        /// <summary>シェルのトークンで起動する（このプロセスが管理者権限かどうかは見ない）。</summary>
        internal static bool TryStartWithShellToken(string applicationPath, IReadOnlyList<string> arguments, string workingDirectory, out string? failure)
        {
            nint process = 0, shellToken = 0, primaryToken = 0;
            try
            {
                nint shellWindow = GetShellWindow();
                if (shellWindow == 0)
                    return Fail("GetShellWindow", 0, out failure);
                GetWindowThreadProcessId(shellWindow, out uint shellProcessId);
                if (shellProcessId == 0)
                    return Fail("GetWindowThreadProcessId", 0, out failure);

                process = OpenProcess(PROCESS_QUERY_INFORMATION, false, shellProcessId);
                if (process == 0)
                    return Fail("OpenProcess", Marshal.GetLastPInvokeError(), out failure);
                if (!OpenProcessToken(process, TOKEN_DUPLICATE, out shellToken))
                    return Fail("OpenProcessToken", Marshal.GetLastPInvokeError(), out failure);

                const uint access = TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID;
                if (!DuplicateTokenEx(shellToken, access, 0, SecurityImpersonation, TokenPrimary, out primaryToken))
                    return Fail("DuplicateTokenEx", Marshal.GetLastPInvokeError(), out failure);

                // コマンドラインと環境変数は、書き換えられてもよい自前のバッファーで渡す
                char[] commandLine = (BuildCommandLine(applicationPath, arguments) + '\0').ToCharArray();
                char[] environment = BuildEnvironmentBlock().ToCharArray();
                var startupInfo = new STARTUPINFO { cb = (uint)sizeof(STARTUPINFO) };
                bool started;
                PROCESS_INFORMATION processInformation;
                fixed (char* commandLinePointer = commandLine)
                fixed (char* environmentPointer = environment)
                {
                    started = CreateProcessWithToken(primaryToken, 0, applicationPath, commandLinePointer, CREATE_UNICODE_ENVIRONMENT,
                        environmentPointer, workingDirectory, ref startupInfo, out processInformation);
                }
                if (!started)
                    return Fail("CreateProcessWithTokenW", Marshal.GetLastPInvokeError(), out failure);

                CloseHandle(processInformation.hThread);
                CloseHandle(processInformation.hProcess);
                failure = null;
                return true;
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
                return false;
            }
            finally
            {
                if (primaryToken != 0) CloseHandle(primaryToken);
                if (shellToken != 0) CloseHandle(shellToken);
                if (process != 0) CloseHandle(process);
            }
        }

        private static bool Fail(string step, int error, out string? failure)
        {
            failure = error == 0 ? $"{step} failed." : $"{step} failed with Win32 error {error}.";
            return false;
        }

        /// <summary>実行ファイルのパスと引数を、Windows の標準の規則（CommandLineToArgvW）で読める 1 行にする。</summary>
        internal static string BuildCommandLine(string applicationPath, IEnumerable<string> arguments)
        {
            var builder = new StringBuilder();
            AppendArgument(builder, applicationPath);
            foreach (string argument in arguments)
            {
                builder.Append(' ');
                AppendArgument(builder, argument);
            }
            return builder.ToString();
        }

        private static void AppendArgument(StringBuilder builder, string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
            {
                builder.Append(argument);
                return;
            }

            // 空白や引用符を含む引数は引用符で囲む。引用符の前の \ は 2 倍にし、引用符には \ を付ける
            builder.Append('"');
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                    builder.Append('\\', backslashes * 2 + 1);
                else
                    builder.Append('\\', backslashes);
                builder.Append(c);
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2);
            builder.Append('"');
        }

        /// <summary>このプロセスの環境変数を、CreateProcess に渡す形式（名前順の「名前=値\0」の並び + 終端の \0）にする。</summary>
        private static string BuildEnvironmentBlock()
        {
            var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string name && name.Length > 0)
                    variables[name] = entry.Value as string ?? string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var (name, value) in variables)
                builder.Append(name).Append('=').Append(value).Append('\0');
            if (variables.Count == 0) builder.Append('\0'); // 空のブロックは \0 が 2 つ
            builder.Append('\0');
            return builder.ToString();
        }
    }
}
