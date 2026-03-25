using GkmStatus.src.native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GkmStatus.src.ui
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _trayIcon;
        private readonly Icon _baseIcon;
        private Icon? _currentStatusIcon;
        private IntPtr _statusIconHandle = IntPtr.Zero;
        private bool _disposed;

        private readonly ToolStripItem _detailsItem;
        private readonly ToolStripItem _stateItem;
        private readonly ToolStripItem _produceItem;
        private readonly ToolStripItem _connectItem;
        private readonly ToolStripItem _pauseItem;
        private readonly ToolStripItem _disconnectItem;
        private readonly ToolStripSeparator _settingsSeparator;

        public TrayIconManager(NotifyIcon trayIcon, Icon baseIcon)
        {
            _trayIcon = trayIcon;
            _baseIcon = baseIcon;

            var menu = _trayIcon.ContextMenuStrip!;
            _detailsItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuDetails");
            _stateItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuState");
            _produceItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuProduce");
            _connectItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuConnect");
            _pauseItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuPause");
            _disconnectItem = menu.Items.Cast<ToolStripItem>().First(i => i.Name == "trayMenuDisconnect");
            _settingsSeparator = menu.Items.Cast<ToolStripItem>().OfType<ToolStripSeparator>().First(s => s.Tag?.ToString() == "SepSettings");
        }

        public void UpdateStatusIcon(Color? color, string? statusText = null)
        {
            string appDisplayName = I18n.T(I18n.Text_List.App_Name);
            _trayIcon.Text = string.IsNullOrEmpty(statusText) ? appDisplayName : $"{appDisplayName} - {statusText}";

            CleanupIcon();

            if (color == null)
            {
                _trayIcon.Icon = _baseIcon;
                return;
            }

            try
            {
                using var bitmap = _baseIcon.ToBitmap();
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    int size = bitmap.Width / 4;
                    int x = bitmap.Width - size - 2;
                    int y = 2;

                    using var brush = new SolidBrush(color.Value);
                    g.FillEllipse(brush, x, y, size, size);

                    using var pen = new Pen(Color.White, 1);
                    g.DrawEllipse(pen, x, y, size, size);
                }

                _statusIconHandle = bitmap.GetHicon();
                _currentStatusIcon = Icon.FromHandle(_statusIconHandle);
                _trayIcon.Icon = _currentStatusIcon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update tray icon: {ex.Message}");
                _trayIcon.Icon = _baseIcon;
            }
        }

        public void UpdateMenuState(RpcStatus status)
        {
            bool isConnected = status is RpcStatus.Connected or RpcStatus.Paused;
            bool isPaused = status == RpcStatus.Paused;
            bool showSettings = isConnected && !isPaused;

            _detailsItem.Visible = showSettings;
            _stateItem.Visible = showSettings;
            _produceItem.Visible = showSettings;
            _settingsSeparator.Visible = showSettings;

            _connectItem.Visible = !isConnected || isPaused;
            _pauseItem.Visible = isConnected && !isPaused;
            _disconnectItem.Visible = isConnected;
        }

        public void ShowNotification(string title, string message, string? url = null)
        {
            _trayIcon.Tag = url;
            _trayIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }

        private void CleanupIcon()
        {
            if (_statusIconHandle != IntPtr.Zero)
            {
                Native.DestroyIcon(_statusIconHandle);
                _statusIconHandle = IntPtr.Zero;
            }

            _currentStatusIcon?.Dispose();
            _currentStatusIcon = null;
        }

        public void SetProduceMenuEnabled(bool enabled)
        {
            _produceItem?.Enabled = enabled;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                CleanupIcon();
                _trayIcon.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
