using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MinecraftHelper.Services
{
    /// <summary>
    /// Runs regular auto-clicks independently from the WPF dispatcher. Screen capture,
    /// OCR and HUD rendering can therefore no longer stall the click cadence.
    /// </summary>
    internal sealed class AutoClickScheduler : IDisposable
    {
        public const int MaximumCps = 100;

        private readonly object _sync = new object();
        private readonly Random _random = new Random();
        private readonly ClickChannel _left = new ClickChannel(leftButton: true);
        private readonly ClickChannel _right = new ClickChannel(leftButton: false);
        private bool _disposed;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public AutoClickScheduler()
        {
            _left.Timer = new Timer(OnTimer, _left, Timeout.Infinite, Timeout.Infinite);
            _right.Timer = new Timer(OnTimer, _right, Timeout.Infinite, Timeout.Infinite);
        }

        public void Update(
            bool leftEnabled,
            int leftMinCps,
            int leftMaxCps,
            bool rightEnabled,
            int rightMinCps,
            int rightMaxCps,
            IntPtr targetWindow)
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                UpdateChannel(_left, leftEnabled, leftMinCps, leftMaxCps, targetWindow);
                UpdateChannel(_right, rightEnabled, rightMinCps, rightMaxCps, targetWindow);
            }
        }

        public void Stop()
        {
            Update(false, 1, 1, false, 1, 1, IntPtr.Zero);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                DisableChannel(_left);
                DisableChannel(_right);
                _left.Timer?.Dispose();
                _right.Timer?.Dispose();
                _left.Timer = null;
                _right.Timer = null;
            }
        }

        private void UpdateChannel(ClickChannel channel, bool enabled, int minCps, int maxCps, IntPtr targetWindow)
        {
            enabled = enabled && targetWindow != IntPtr.Zero && minCps > 0 && maxCps > 0;
            minCps = Math.Clamp(minCps, 1, MaximumCps);
            maxCps = Math.Clamp(maxCps, 1, MaximumCps);
            if (maxCps < minCps)
                (minCps, maxCps) = (maxCps, minCps);

            bool changed = channel.Enabled != enabled
                || channel.MinCps != minCps
                || channel.MaxCps != maxCps
                || channel.TargetWindow != targetWindow;
            if (!changed)
                return;

            channel.Generation++;
            channel.Enabled = enabled;
            channel.MinCps = minCps;
            channel.MaxCps = maxCps;
            channel.TargetWindow = targetWindow;

            channel.Timer?.Change(enabled ? 0 : Timeout.Infinite, Timeout.Infinite);
        }

        private static void DisableChannel(ClickChannel channel)
        {
            channel.Generation++;
            channel.Enabled = false;
            channel.TargetWindow = IntPtr.Zero;
            channel.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimer(object? state)
        {
            if (state is not ClickChannel channel)
                return;

            lock (_sync)
            {
                if (_disposed || !channel.Enabled || channel.Timer == null)
                    return;

                int generation = channel.Generation;
                int cps = channel.MinCps == channel.MaxCps
                    ? channel.MinCps
                    : _random.Next(channel.MinCps, channel.MaxCps + 1);
                int nextDelayMs = Math.Max(1, (int)Math.Round(1000.0 / cps));

                // Check focus at the instant of injection instead of relying on the
                // slower UI focus timer. This prevents clicks leaking to another app.
                if (channel.TargetWindow != IntPtr.Zero && GetForegroundWindow() == channel.TargetWindow)
                    NativeInput.SendMouseClick(channel.LeftButton, holdPulseMode: false);

                if (!_disposed && channel.Enabled && channel.Generation == generation)
                    channel.Timer.Change(nextDelayMs, Timeout.Infinite);
            }
        }

        private sealed class ClickChannel
        {
            public ClickChannel(bool leftButton)
            {
                LeftButton = leftButton;
            }

            public bool LeftButton { get; }
            public Timer? Timer { get; set; }
            public bool Enabled { get; set; }
            public int MinCps { get; set; } = 1;
            public int MaxCps { get; set; } = 1;
            public IntPtr TargetWindow { get; set; }
            public int Generation { get; set; }
        }
    }
}
