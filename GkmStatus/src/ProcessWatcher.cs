using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace GkmStatus.src
{
    public class ProcessWatcher : IDisposable
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly string _processName;
        private bool _wasRunning;
        private bool _disposed;

        public event EventHandler? ProcessStarted;

        public event EventHandler? ProcessStopped;

        public bool IsRunning { get; private set; }

        public ProcessWatcher(string processName, int interval = 3000)
        {
            _processName = processName;
            _timer = new System.Windows.Forms.Timer { Interval = interval };
            _timer.Tick += OnTick;
        }

        public bool Enabled
        {
            get => _timer.Enabled;
            set
            {
                if (value == _timer.Enabled)
                    return;

                if (value)
                    _timer.Start();
                else
                    _timer.Stop();
            }
        }

        public void ForceCheck() => OnTick(this, EventArgs.Empty);

        private void OnTick(object? sender, EventArgs e)
        {
            var processes = Process.GetProcessesByName(_processName);
            try
            {
                bool isNowRunning = processes.Length > 0;

                if (isNowRunning && !_wasRunning)
                {
                    IsRunning = true;
                    ProcessStarted?.Invoke(this, EventArgs.Empty);
                }
                else if (!isNowRunning && _wasRunning)
                {
                    IsRunning = false;
                    ProcessStopped?.Invoke(this, EventArgs.Empty);
                }

                _wasRunning = isNowRunning;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
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
