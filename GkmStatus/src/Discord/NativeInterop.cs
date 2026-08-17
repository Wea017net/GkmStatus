using System;
using System.Runtime.InteropServices;

namespace GkmStatus.src.Discord
{
    /// <summary>
    /// Discord Social SDK のネイティブ P/Invoke 定義
    /// </summary>
    internal static class NativeInterop
    {
        // Discord Social SDK DLL
        private const string DiscordDll = "discord_social_sdk.dll";

        /// <summary>
        /// Activity タイプ定義
        /// </summary>
        public enum ActivityType
        {
            Playing = 0,
            Streaming = 1,
            Listening = 2,
            Watching = 3,
            Custom = 4,
            Competing = 5
        }

        /// <summary>
        /// アンマネージ Activity 構造体
        /// Discord Native SDK に渡すための構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DiscordActivity
        {
            // Activity の種類
            public int Type;

            // アプリケーション ID
            public long ApplicationId;

            // メインのテキスト表示（例："Playing"の後ろ）
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Name;

            // 状態テキスト（例："In Lobby"）
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string State;

            // State テキストをクリックしたときに開く URL
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string StateUrl;

            // 詳細テキスト（例："Playing with Alice"）
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Details;

            // Details テキストをクリックしたときに開く URL
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DetailsUrl;

            // Timestamps 構造体（開始時刻・終了時刻）
            public DiscordActivityTimestamps Timestamps;

            // Asset 情報（大小画像キーなど）
            public DiscordActivityAssets Assets;

            // Party 情報（マルチプレイ情報など）
            public DiscordActivityParty Party;

            // Secrets（セキュリティ関連）
            public DiscordActivitySecrets Secrets;

            // Instance（インスタンス共有など）
            public byte Instance;

            // 予約フィールド
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
            public byte[] Reserved;
        }

        /// <summary>
        /// Activity のタイムスタンプ情報
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct DiscordActivityTimestamps
        {
            // アクティビティ開始時刻（Unix タイムスタンプ）
            public long Start;

            // アクティビティ終了時刻（Unix タイムスタンプ）
            public long End;
        }

        /// <summary>
        /// Activity のアセット情報
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DiscordActivityAssets
        {
            // 大画像キー
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string LargeImage;

            // 大画像ホバーテキスト
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string LargeText;

            // 小画像キー
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SmallImage;

            // 小画像ホバーテキスト
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SmallText;
        }

        /// <summary>
        /// Activity のパーティ情報
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DiscordActivityParty
        {
            // パーティ ID
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Id;

            // 現在のパーティサイズ
            public int Size;

            // パーティの最大サイズ
            public int Max;
        }

        /// <summary>
        /// Activity のシークレット情報
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DiscordActivitySecrets
        {
            // マッチシークレット
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Match;

            // ジョインシークレット
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Join;

            // スペクテイトシークレット
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Spectate;
        }

        /// <summary>
        /// Discord クライアント初期化
        /// </summary>
        /// <param name="clientId">Discord アプリケーション ID</param>
        /// <param name="createFlags">初期化フラグ</param>
        /// <returns>初期化結果コード（0 = 成功）</returns>
        [DllImport(DiscordDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DiscordCreate(long clientId, uint createFlags);

        /// <summary>
        /// Activity（Rich Presence）を更新
        /// </summary>
        /// <param name="activity">更新する Activity 構造体</param>
        /// <param name="callback">完了時のコールバック（IntPtr は callback data）</param>
        [DllImport(DiscordDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ActivityManager_UpdateActivity(ref DiscordActivity activity, IntPtr callback);

        /// <summary>
        /// Discord クライアントのコールバック処理を実行
        /// 定期的に呼び出すことで、ネイティブ側の非同期処理を処理する
        /// </summary>
        [DllImport(DiscordDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DiscordRunCallbacks();

        /// <summary>
        /// Discord クライアントの後処理・破棄
        /// </summary>
        [DllImport(DiscordDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DiscordDestroy();

        /// <summary>
        /// 最後のエラーメッセージを取得
        /// </summary>
        /// <returns>エラーメッセージ文字列</returns>
        [DllImport(DiscordDll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr DiscordGetLastError();

        /// <summary>
        /// エラーメッセージ文字列を安全に取得
        /// </summary>
        public static string GetLastError()
        {
            try
            {
                IntPtr ptrError = DiscordGetLastError();
                if (ptrError == IntPtr.Zero)
                    return "Unknown error";
                return Marshal.PtrToStringAnsi(ptrError) ?? "Unknown error";
            }
            catch
            {
                return "Failed to retrieve error message";
            }
        }
    }
}
