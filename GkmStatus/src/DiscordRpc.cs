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

            client = new DiscordRpcClient(appId)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning },
                SkipIdenticalPresence = false
            };

            client.OnReady += (s, e) =>
            {
                Status= RpcStatus.Connected;
                CurrentUsername = e.User.Username;
                LastErrorMessage = null;
                Ready?.Invoke(this, e.User.Username);
                StatusChanged?.Invoke(this, Status);
            };

            client.OnError += (s, e) =>
            {
                Status = RpcStatus.Error;
                LastErrorMessage = e.Message;
                StatusChanged?.Invoke(this, Status);
            };

            client.OnClose += (s, e) =>
            {
                Status = RpcStatus.Disconnected;
                CurrentUsername = null;
                StatusChanged?.Invoke(this, Status);
            };

            client.OnConnectionFailed += (s, e) =>
            {
                Status = RpcStatus.Error;
                LastErrorMessage = "Discord connection failed.";
                StatusChanged?.Invoke(this, Status);
            };

            Status = RpcStatus.Connecting;
            StatusChanged?.Invoke(this, Status);

            try
            {
                if(!client.Initialize())
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

        public void Deinitialize()
        {
            if(client != null)
            {
                client.ClearPresence();
                client.Deinitialize();
                client.Dispose();
                client = null;
            }

            Status = RpcStatus.Disconnected;
            CurrentUsername = null;
            LastErrorMessage = null;
            StatusChanged?.Invoke(this, Status);
        }

        public void Clear()
        {
            client?.ClearPresence();
            Status = RpcStatus.Paused;
            StatusChanged?.Invoke(this, Status);
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

        private static string SafeTrimUtf8(string? text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var enc = Encoding.UTF8;

            if(enc.GetByteCount(text) <= maxBytes)
                return text;

            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;

                if (enc.GetByteCount(text[..mid]) <= maxBytes)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return text[..lo];
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
