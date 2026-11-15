using System;
using System.Collections.Generic;
using System.Text;
using DiscordRPC;
using DiscordRPC.Logging;

namespace GkmStatus.src
{
    public enum RpcStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Paused,
        Error
    }

    public class DiscordRpcService: IDisposable
    {
        private DiscordRpcClient? client;
        private bool disposed;
        public RpcStatus Status { get; private set; } = RpcStatus.Disconnected;
        public string? CurrentUsername { get; private set; }
        /// <summary>接続中のユーザーの表示名（未設定ならユーザー名）。プレビューのメンバー一覧の行に使う。</summary>
        public string? CurrentDisplayName { get; private set; }
        /// <summary>接続中のユーザーのアバター画像の URL（取れなければ null）。</summary>
        public string? CurrentAvatarUrl { get; private set; }
        public string? LastErrorMessage { get; private set; }

        internal static bool IsDiscordCdnUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;
            string host = uri.Host;
            return host.Equals("cdn.discordapp.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".discordapp.net", StringComparison.OrdinalIgnoreCase);
        }

        private static string? TryGetAvatarUrl(User user)
        {
            try
            {
                // 画像の取得先は Discord から受け取った情報で決まるので、Discord の配信元（https）以外は使わない
                string url = user.GetAvatarURL(User.AvatarFormat.PNG, User.AvatarSize.x128);
                return IsDiscordCdnUrl(url) ? url : null;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Discord avatar URL", ex);
                return null;
            }
        }

        /// <summary>Discord に接続できなかったときの LastErrorMessage。画面では Discord の起動を促す一言を添えて表示する。</summary>
        public const string ConnectionFailedMessage = "Discord connection failed.";

        public event EventHandler<RpcStatus>? StatusChanged;
        public event EventHandler<string>? Ready;

        public bool IsInitialized => client?.IsInitialized ?? false;

        public void Initialize(string appId)
        {
            if (client != null)
                Deinitialize();

            CurrentUsername = null;
            CurrentDisplayName = null;
            CurrentAvatarUrl = null;
            LastErrorMessage = null;

            var session = new DiscordRpcClient(appId)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning },
                SkipIdenticalPresence = false
            };
            client = session;

            session.OnReady += (s, e) =>
            {
                if (!ReferenceEquals(client, session)) return;
                Status= RpcStatus.Connected;
                CurrentUsername = e.User.Username;
                CurrentDisplayName = string.IsNullOrWhiteSpace(e.User.DisplayName) ? e.User.Username : e.User.DisplayName;
                CurrentAvatarUrl = TryGetAvatarUrl(e.User);
                LastErrorMessage = null;
                Ready?.Invoke(this, e.User.Username);
                StatusChanged?.Invoke(this, Status);
            };

            session.OnError += (s, e) =>
            {
                if (!ReferenceEquals(client, session)) return;
                Status = RpcStatus.Error;
                LastErrorMessage = e.Message;
                StatusChanged?.Invoke(this, Status);
            };

            session.OnClose += (s, e) =>
            {
                if (!ReferenceEquals(client, session)) return;
                Status = RpcStatus.Disconnected;
                CurrentUsername = null;
                CurrentDisplayName = null;
                CurrentAvatarUrl = null;
                StatusChanged?.Invoke(this, Status);
            };

            session.OnConnectionFailed += (s, e) =>
            {
                if (!ReferenceEquals(client, session)) return;
                Status = RpcStatus.Error;
                LastErrorMessage = ConnectionFailedMessage;
                StatusChanged?.Invoke(this, Status);
            };

            Status = RpcStatus.Connecting;
            StatusChanged?.Invoke(this, Status);

            try
            {
                if(!session.Initialize())
                {
                    Status = RpcStatus.Error;
                    LastErrorMessage = "Failed to initialize Discord RPC client.";
                    StatusChanged?.Invoke(this, Status);
                }
            } catch (Exception ex)
            {
                ErrorLog.Write("Discord RPC initialize", ex);
                Status = RpcStatus.Error;
                LastErrorMessage = ex.Message;
                StatusChanged?.Invoke(this, Status);
            }
        }

        public void Deinitialize(bool preserveError = false)
        {
            var oldClient = client;
            client = null;
            if(oldClient != null)
            {
                try { oldClient.ClearPresence(); }
                catch { }
                try { oldClient.Deinitialize(); }
                catch { }
                try { oldClient.Dispose(); }
                catch { }
            }

            Status = RpcStatus.Disconnected;
            CurrentUsername = null;
            CurrentDisplayName = null;
            CurrentAvatarUrl = null;
            if (!preserveError)
                LastErrorMessage = null;
            StatusChanged?.Invoke(this, Status);
        }

        public void Clear()
        {
            if (!IsInitialized)
                return;
            client?.ClearPresence();
            Status = RpcStatus.Paused;
            StatusChanged?.Invoke(this, Status);
        }

        public void Resume()
        {
            if (Status == RpcStatus.Paused && IsInitialized)
            {
                Status = RpcStatus.Connected;
                StatusChanged?.Invoke(this, Status);
            }
        }

        public void Invoke() => client?.Invoke();

        public void UpdatePresence(RichPresence presence)
        {
            if (client == null || !client.IsInitialized)
                return;

            presence.Details = SafeTrimUtf8(presence.Details, 128);
            presence.State = SafeTrimUtf8(presence.State, 128);

            if(presence.Buttons != null)
            {
                foreach(var btn in presence.Buttons)
                    btn.Label = SafeTrimUtf8(btn.Label, 32);
            }

            client.SetPresence(presence);
        }

        internal static string SafeTrimUtf8(string? text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var builder = new StringBuilder(text.Length);
            int bytes = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                int runeBytes = rune.Utf8SequenceLength;
                if (bytes + runeBytes > maxBytes)
                    break;

                builder.Append(rune.ToString());
                bytes += runeBytes;
            }

            return builder.ToString();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                Deinitialize();
            }

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
