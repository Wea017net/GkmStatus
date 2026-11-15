using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GkmStatus.src
{
    /// <summary>
    /// このアプリのメモリ使用量を取得する。タスクマネージャーの「メモリ」列と同じ、プライベート ワーキング セットを使う。
    /// 自分のプロセスだけを GetProcessMemoryInfo で問い合わせるので、Process クラスのように
    /// 全プロセスの情報を集めたりオブジェクトを作ったりせず、軽く取得できる。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static partial class MemoryUsage
    {
        // PROCESS_MEMORY_COUNTERS_EX2
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Cb;
            public uint PageFaultCount;
            public nuint PeakWorkingSetSize;
            public nuint WorkingSetSize;
            public nuint QuotaPeakPagedPoolUsage;
            public nuint QuotaPagedPoolUsage;
            public nuint QuotaPeakNonPagedPoolUsage;
            public nuint QuotaNonPagedPoolUsage;
            public nuint PagefileUsage;
            public nuint PeakPagefileUsage;
            public nuint PrivateUsage;
            public nuint PrivateWorkingSetSize;
            public ulong SharedCommitUsage;
        }

        [LibraryImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr GetCurrentProcess();

        /// <summary>
        /// メモリ使用量（バイト）。プライベート ワーキング セットが取れない古い Windows では、
        /// プライベートのコミット量を返す。取得できないときは null。
        /// </summary>
        public static long? GetBytes()
        {
            var counters = new ProcessMemoryCounters { Cb = (uint)Unsafe.SizeOf<ProcessMemoryCounters>() };
            if (!GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.Cb))
                return null;
            nuint bytes = counters.PrivateWorkingSetSize != 0 ? counters.PrivateWorkingSetSize : counters.PrivateUsage;
            return (long)bytes;
        }

        /// <summary>バイト数を 0.1MB 単位の整数にする（表示が変わったかを比べるのに使う）。</summary>
        public static long ToTenthsOfMegabyte(long bytes) => (long)Math.Round(bytes / (1024.0 * 1024.0) * 10, MidpointRounding.AwayFromZero);

        /// <summary>0.1MB 単位の値を「12.3 MB」の形にする。</summary>
        public static string Format(long tenthsOfMegabyte)
            => string.Create(CultureInfo.InvariantCulture, $"{tenthsOfMegabyte / 10}.{tenthsOfMegabyte % 10} MB");
    }
}
