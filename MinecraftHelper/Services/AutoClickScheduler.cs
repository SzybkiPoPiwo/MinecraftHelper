using System;
using System.Diagnostics;
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
        private bool _highResolutionTimerActive;

        private const uint TimerResolutionMs = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("winmm.dll", ExactSpelling = true)]
        private static extern uint timeBeginPeriod(uint periodMilliseconds);

        [DllImport("winmm.dll", ExactSpelling = true)]
        private static extern uint timeEndPeriod(uint periodMilliseconds);

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
            bool rightHoldPulseMode,
            IntPtr targetWindow)
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                UpdateChannel(_left, leftEnabled, leftMinCps, leftMaxCps, holdPulseMode: false, targetWindow);
                UpdateChannel(_right, rightEnabled, rightMinCps, rightMaxCps, rightHoldPulseMode, targetWindow);
                UpdateTimerResolutionState();
            }
        }

        public void Stop()
        {
            Update(false, 1, 1, false, 1, 1, rightHoldPulseMode: false, IntPtr.Zero);
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
                UpdateTimerResolutionState();
                _left.Timer?.Dispose();
                _right.Timer?.Dispose();
                _left.Timer = null;
                _right.Timer = null;
            }
        }

        private void UpdateChannel(
            ClickChannel channel,
            bool enabled,
            int minCps,
            int maxCps,
            bool holdPulseMode,
            IntPtr targetWindow)
        {
            enabled = enabled && targetWindow != IntPtr.Zero && minCps > 0 && maxCps > 0;
            minCps = Math.Clamp(minCps, 1, MaximumCps);
            maxCps = Math.Clamp(maxCps, 1, MaximumCps);
            if (maxCps < minCps)
                (minCps, maxCps) = (maxCps, minCps);

            bool changed = channel.Enabled != enabled
                || channel.MinCps != minCps
                || channel.MaxCps != maxCps
                || channel.HoldPulseMode != holdPulseMode
                || channel.TargetWindow != targetWindow;
            if (!changed)
                return;

            channel.Generation++;
            channel.Enabled = enabled;
            channel.MinCps = minCps;
            channel.MaxCps = maxCps;
            channel.HoldPulseMode = holdPulseMode;
            channel.TargetWindow = targetWindow;
            channel.NextClickTimestamp = enabled ? Stopwatch.GetTimestamp() : 0;

            channel.Timer?.Change(enabled ? 0 : Timeout.Infinite, Timeout.Infinite);
        }

        private static void DisableChannel(ClickChannel channel)
        {
            channel.Generation++;
            channel.Enabled = false;
            channel.TargetWindow = IntPtr.Zero;
            channel.NextClickTimestamp = 0;
            channel.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void UpdateTimerResolutionState()
        {
            bool shouldBeActive = !_disposed && (_left.Enabled || _right.Enabled);
            if (shouldBeActive == _highResolutionTimerActive)
                return;

            if (shouldBeActive)
            {
                _highResolutionTimerActive = timeBeginPeriod(TimerResolutionMs) == 0;
                return;
            }

            if (_highResolutionTimerActive)
                _ = timeEndPeriod(TimerResolutionMs);
            _highResolutionTimerActive = false;
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
                long nowTimestamp = Stopwatch.GetTimestamp();
                if (channel.NextClickTimestamp > nowTimestamp)
                {
                    ScheduleAtTimestamp(channel, channel.NextClickTimestamp, nowTimestamp);
                    return;
                }

                int cps = channel.MinCps == channel.MaxCps
                    ? channel.MinCps
                    : _random.Next(channel.MinCps, channel.MaxCps + 1);
                long intervalTicks = Math.Max(1, (long)Math.Round(Stopwatch.Frequency / (double)cps));

                // Check focus at the instant of injection instead of relying on the
                // slower UI focus timer. This prevents clicks leaking to another app.
                if (channel.TargetWindow != IntPtr.Zero && GetForegroundWindow() == channel.TargetWindow)
                    NativeInput.SendMouseClick(channel.LeftButton, channel.HoldPulseMode);

                if (!_disposed && channel.Enabled && channel.Generation == generation)
                {
                    // Keep the cadence tied to the planned deadline, not to the actual
                    // callback time. This compensates for Windows timer jitter instead
                    // of adding the delay to every click (20 CPS drifting to ~15 CPS).
                    long plannedTimestamp = channel.NextClickTimestamp > 0
                        ? channel.NextClickTimestamp
                        : nowTimestamp;
                    long nextTimestamp = plannedTimestamp + intervalTicks;
                    long afterClickTimestamp = Stopwatch.GetTimestamp();
                    if (nextTimestamp <= afterClickTimestamp)
                    {
                        long skippedIntervals = ((afterClickTimestamp - nextTimestamp) / intervalTicks) + 1;
                        nextTimestamp += skippedIntervals * intervalTicks;
                    }

                    channel.NextClickTimestamp = nextTimestamp;
                    ScheduleAtTimestamp(channel, nextTimestamp, afterClickTimestamp);
                }
            }
        }

        private static void ScheduleAtTimestamp(ClickChannel channel, long targetTimestamp, long nowTimestamp)
        {
            double remainingMilliseconds = Math.Max(
                0,
                (targetTimestamp - nowTimestamp) * 1000.0 / Stopwatch.Frequency);
            int delayMilliseconds = Math.Max(1, (int)Math.Ceiling(remainingMilliseconds));
            channel.Timer?.Change(delayMilliseconds, Timeout.Infinite);
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
            public bool HoldPulseMode { get; set; }
            public IntPtr TargetWindow { get; set; }
            public int Generation { get; set; }
            public long NextClickTimestamp { get; set; }
        }
    }
}
