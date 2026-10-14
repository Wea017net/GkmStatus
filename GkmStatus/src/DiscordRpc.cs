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
        public string? LastErrorMessage { get; private set; }

        public event EventHandler<RpcStatus>? StatusChanged;
        public event EventHandler<string>? Ready;

        public bool IsInitialized => client?.IsInitialized ?? false;

        public void Initialize(string appId)
        {
            if (client != null)
                Deinitialize();

            CurrentUsername = null;
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
                StatusChanged?.Invoke(this, Status);
            };

            session.OnConnectionFailed += (s, e) =>
            {
                if (!ReferenceEquals(client, session)) return;
                Status = RpcStatus.Error;
                LastErrorMessage = "Discord connection failed.";
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
