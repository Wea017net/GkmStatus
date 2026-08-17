using System;
using System.Windows.Forms;
using System.Diagnostics;

namespace GkmStatus.src.Discord
{
    /// <summary>
    /// WinForms 向け Discord Rich Presence マネージャー
    /// UI スレッドをブロックしないように実装
    /// </summary>
    public class DiscordPresenceManager : IDisposable
    {
        private readonly long _clientId;
        private readonly Timer _callbackTimer;
        private bool _initialized = false;
        private bool _disposed = false;

        /// <summary>
        /// 初期化成功イベント
        /// </summary>
        public event EventHandler<EventArgs> InitializationSuccess;

        /// <summary>
        /// 初期化失敗イベント
        /// </summary>
        public event EventHandler<ErrorEventArgs> InitializationFailed;

        /// <summary>
        /// Presence 更新完了イベント
        /// </summary>
        public event EventHandler<EventArgs> PresenceUpdated;

        /// <summary>
        /// 更新失敗イベント
        /// </summary>
        public event EventHandler<ErrorEventArgs> UpdateFailed;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="clientId">Discord アプリケーション ID</param>
        public DiscordPresenceManager(long clientId)
        {
            _clientId = clientId;
            _callbackTimer = new Timer
            {
                Interval = 100  // 100ms ごとにコールバック処理を実行
            };
            _callbackTimer.Tick += (s, e) => ProcessCallbacks();
        }

        /// <summary>
        /// Discord クライアントを初期化
        /// </summary>
        /// <returns>成功した場合 true、失敗した場合 false</returns>
        public bool Initialize()
        {
            if (_initialized)
                return true;

            try
            {
                int result = NativeInterop.DiscordCreate(_clientId, 0);
                if (result == 0)
                {
                    _initialized = true;
                    _callbackTimer.Start();
                    InitializationSuccess?.Invoke(this, EventArgs.Empty);
                    Debug.WriteLine("Discord client initialized successfully");
                    return true;
                }
                else
                {
                    string errorMsg = NativeInterop.GetLastError();
                    var exception = new Exception($"DiscordCreate failed with code {result}: {errorMsg}");
                    InitializationFailed?.Invoke(this, new ErrorEventArgs(exception));
                    Debug.WriteLine($"Discord initialization failed: {errorMsg}");
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                var exception = new DllNotFoundException(
                    "discord_social_sdk.dll not found. Please ensure the Discord Social SDK DLL is in the application directory or system PATH.");
                InitializationFailed?.Invoke(this, new ErrorEventArgs(exception));
                Debug.WriteLine("discord_social_sdk.dll not found");
                return false;
            }
            catch (Exception ex)
            {
                InitializationFailed?.Invoke(this, new ErrorEventArgs(ex));
                Debug.WriteLine($"Discord initialization error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rich Presence を更新
        /// </summary>
        /// <param name="details">詳細テキスト（例："Playing with Alice"）</param>
        /// <param name="detailsUrl">Details テキストをクリックしたときに開く URL</param>
        /// <param name="state">状態テキスト（例："In Lobby"）</param>
        /// <param name="stateUrl">State テキストをクリックしたときに開く URL</param>
        /// <param name="largeImageKey">大画像キー（例："app"）</param>
        /// <param name="largeImageText">大画像ホバーテキスト</param>
        /// <param name="startTime">アクティビティ開始時刻（null の場合は現在時刻）</param>
        public void UpdatePresence(
            string details = "",
            string detailsUrl = "",
            string state = "",
            string stateUrl = "",
            string largeImageKey = "app",
            string largeImageText = "GkmStatus",
            DateTime? startTime = null)
        {
            if (!_initialized)
            {
                Debug.WriteLine("Discord not initialized");
                return;
            }

            try
            {
                var activity = new NativeInterop.DiscordActivity
                {
                    Type = (int)NativeInterop.ActivityType.Playing,
                    ApplicationId = _clientId,
                    Name = "GkmStatus",
                    Details = details ?? "",
                    DetailsUrl = detailsUrl ?? "",
                    State = state ?? "",
                    StateUrl = stateUrl ?? "",
                    Timestamps = new NativeInterop.DiscordActivityTimestamps
                    {
                        Start = startTime?.ToUnixTimestamp() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        End = 0
                    },
                    Assets = new NativeInterop.DiscordActivityAssets
                    {
                        LargeImage = largeImageKey,
                        LargeText = largeImageText,
                        SmallImage = "",
                        SmallText = ""
                    }
                };

                // コールバックはここでは使用しないため IntPtr.Zero を渡す
                NativeInterop.ActivityManager_UpdateActivity(ref activity, IntPtr.Zero);
                PresenceUpdated?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("Presence updated successfully");
            }
            catch (Exception ex)
            {
                UpdateFailed?.Invoke(this, new ErrorEventArgs(ex));
                Debug.WriteLine($"Failed to update presence: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear Presence（アクティビティをクリア）
        /// </summary>
        public void ClearPresence()
        {
            UpdatePresence("", "", "", "");
        }

        /// <summary>
        /// ネイティブ側のコールバック処理を実行
        /// 定期的に呼び出すことで非同期処理が適切に処理される
        /// </summary>
        private void ProcessCallbacks()
        {
            if (!_initialized || _disposed)
                return;

            try
            {
                NativeInterop.DiscordRunCallbacks();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing callbacks: {ex.Message}");
            }
        }

        /// <summary>
        /// リソースをクリーンアップ
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _callbackTimer?.Stop();
                _callbackTimer?.Dispose();

                if (_initialized)
                {
                    try
                    {
                        NativeInterop.DiscordDestroy();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error destroying Discord client: {ex.Message}");
                    }
                    _initialized = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during Dispose: {ex.Message}");
            }
            finally
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~DiscordPresenceManager()
        {
            Dispose();
        }
    }

    /// <summary>
    /// DateTime を Unix タイムスタンプに変換するヘルパー拡張メソッド
    /// </summary>
    internal static class DateTimeExtensions
    {
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            return (long)dateTime.ToUniversalTime().Subtract(
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
