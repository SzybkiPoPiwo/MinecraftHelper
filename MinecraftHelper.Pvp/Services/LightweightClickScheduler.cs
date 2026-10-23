using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MinecraftHelper.Services;

internal sealed class LightweightClickScheduler : IDisposable
{
    private readonly object _sync = new();
    private readonly Random _random = new();
    private readonly Channel _left = new(leftButton: true);
    private readonly Channel _right = new(leftButton: false);
    private readonly Func<int, bool> _isPhysicalPointerDown;
    private readonly Func<bool> _shouldSuppressClick;
    private bool _disposed;
    private bool _timerResolutionActive;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeBeginPeriod(uint periodMilliseconds);

    [DllImport("winmm.dll", ExactSpelling = true)]
    private static extern uint timeEndPeriod(uint periodMilliseconds);

    public LightweightClickScheduler(
        Func<int, bool> isPhysicalPointerDown,
        Func<bool> shouldSuppressClick)
    {
        _isPhysicalPointerDown = isPhysicalPointerDown ?? throw new ArgumentNullException(nameof(isPhysicalPointerDown));
        _shouldSuppressClick = shouldSuppressClick ?? throw new ArgumentNullException(nameof(shouldSuppressClick));
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
        int leftRequiredHoldKey,
        int leftRequiredPointerKey,
        int rightRequiredHoldKey,
        int rightRequiredPointerKey,
        IntPtr targetWindow)
    {
        bool startLeft;
        bool startRight;
        lock (_sync)
        {
            if (_disposed)
                return;

            startLeft = UpdateChannel(
                _left,
                leftEnabled,
                leftMinCps,
                leftMaxCps,
                leftRequiredHoldKey,
                leftRequiredPointerKey,
                targetWindow);
            startRight = UpdateChannel(
                _right,
                rightEnabled,
                rightMinCps,
                rightMaxCps,
                rightRequiredHoldKey,
                rightRequiredPointerKey,
                targetWindow);
            UpdateTimerResolution();
        }

        if (startLeft)
            OnTimer(_left);
        if (startRight)
            OnTimer(_right);
    }

    public void UpdateLeft(
        bool enabled,
        int minCps,
        int maxCps,
        int requiredHoldKey,
        int requiredPointerKey,
        IntPtr targetWindow)
    {
        bool startLeft;
        lock (_sync)
        {
            if (_disposed)
                return;

            startLeft = UpdateChannel(
                _left,
                enabled,
                minCps,
                maxCps,
                requiredHoldKey,
                requiredPointerKey,
                targetWindow);
            UpdateTimerResolution();
        }

        if (startLeft)
            OnTimer(_left);
    }

    public void Stop()
    {
        Update(false, 1, 1, false, 1, 1, 0, 0, 0, 0, IntPtr.Zero);
    }

    public void StopLeftImmediately() => StopImmediately(_left);

    public void StopRightImmediately() => StopImmediately(_right);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            Disable(_left);
            Disable(_right);
            UpdateTimerResolution();
            _left.Timer?.Dispose();
            _right.Timer?.Dispose();
        }
    }

    private bool UpdateChannel(
        Channel channel,
        bool enabled,
        int minCps,
        int maxCps,
        int requiredHoldKey,
        int requiredPointerKey,
        IntPtr targetWindow)
    {
        enabled = enabled && targetWindow != IntPtr.Zero;
        minCps = Math.Clamp(minCps, 1, 100);
        maxCps = Math.Clamp(maxCps, 1, 100);
        if (maxCps < minCps)
            (minCps, maxCps) = (maxCps, minCps);

        if (channel.Enabled == enabled
            && channel.MinCps == minCps
            && channel.MaxCps == maxCps
            && channel.RequiredHoldKey == requiredHoldKey
            && channel.RequiredPointerKey == requiredPointerKey
            && channel.TargetWindow == targetWindow)
        {
            return false;
        }

        channel.Generation++;
        channel.Enabled = enabled;
        channel.MinCps = minCps;
        channel.MaxCps = maxCps;
        channel.RequiredHoldKey = requiredHoldKey;
        channel.RequiredPointerKey = requiredPointerKey;
        channel.TargetWindow = targetWindow;
        channel.NextTimestamp = enabled ? Stopwatch.GetTimestamp() : 0;
        channel.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return enabled;
    }

    private void OnTimer(object? state)
    {
        if (state is not Channel channel)
            return;

        int generation;
        long plannedTimestamp;
        long intervalTicks;
        int requiredHoldKey;
        int requiredPointerKey;
        IntPtr targetWindow;
        bool leftButton;
        lock (_sync)
        {
            if (_disposed || !channel.Enabled || channel.Timer == null)
                return;

            generation = channel.Generation;
            long now = Stopwatch.GetTimestamp();
            if (channel.NextTimestamp > now)
            {
                Schedule(channel, channel.NextTimestamp, now);
                return;
            }

            int cps = channel.MinCps == channel.MaxCps
                ? channel.MinCps
                : _random.Next(channel.MinCps, channel.MaxCps + 1);
            intervalTicks = Math.Max(1, (long)Math.Round(Stopwatch.Frequency / (double)cps));
            plannedTimestamp = channel.NextTimestamp > 0 ? channel.NextTimestamp : now;
            requiredHoldKey = channel.RequiredHoldKey;
            requiredPointerKey = channel.RequiredPointerKey;
            targetWindow = channel.TargetWindow;
            leftButton = channel.LeftButton;
        }

        if ((requiredHoldKey != 0 && !IsVirtualKeyDown(requiredHoldKey))
            || (requiredPointerKey != 0 && !_isPhysicalPointerDown(requiredPointerKey)))
        {
            StopImmediately(channel);
            return;
        }

        // The UI cursor cache is intentionally not trusted here. At high CPS a
        // click can otherwise fit between opening Minecraft's GUI and the next
        // dispatcher tick. Recheck the real cursor immediately before every
        // injected click and fail closed if the guard itself cannot be read.
        if (targetWindow != IntPtr.Zero && GetForegroundWindow() == targetWindow)
        {
            bool suppressClick;
            try
            {
                suppressClick = _shouldSuppressClick();
            }
            catch
            {
                suppressClick = true;
            }

            if (!suppressClick)
            {
                // Stop() must be a hard boundary for quick PVP actions.  Recheck
                // the generation while holding the scheduler lock so a callback
                // that was already in flight cannot inject a late click after
                // Stop() has returned.
                lock (_sync)
                {
                    if (_disposed || !channel.Enabled || channel.Generation != generation || channel.Timer == null)
                        return;

                    _ = NativeInput.SendMouseClick(leftButton, holdPulseMode: false);
                }
            }
        }

        lock (_sync)
        {
            if (_disposed || !channel.Enabled || channel.Generation != generation || channel.Timer == null)
                return;

            long next = plannedTimestamp + intervalTicks;
            long now = Stopwatch.GetTimestamp();
            if (next <= now)
                next += (((now - next) / intervalTicks) + 1) * intervalTicks;
            channel.NextTimestamp = next;
            Schedule(channel, next, now);
        }
    }

    private void StopImmediately(Channel channel)
    {
        lock (_sync)
        {
            if (_disposed || !channel.Enabled)
                return;
            Disable(channel);
            UpdateTimerResolution();
        }
    }

    private static void Disable(Channel channel)
    {
        channel.Generation++;
        channel.Enabled = false;
        channel.RequiredHoldKey = 0;
        channel.RequiredPointerKey = 0;
        channel.TargetWindow = IntPtr.Zero;
        channel.NextTimestamp = 0;
        channel.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private static void Schedule(Channel channel, long target, long now)
    {
        int delay = Math.Max(1, (int)Math.Ceiling((target - now) * 1000.0 / Stopwatch.Frequency));
        channel.Timer?.Change(delay, Timeout.Infinite);
    }

    private void UpdateTimerResolution()
    {
        bool shouldRun = !_disposed && (_left.Enabled || _right.Enabled);
        if (shouldRun == _timerResolutionActive)
            return;

        if (shouldRun)
            _timerResolutionActive = timeBeginPeriod(1) == 0;
        else
        {
            if (_timerResolutionActive)
                _ = timeEndPeriod(1);
            _timerResolutionActive = false;
        }
    }

    private static bool IsVirtualKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private sealed class Channel
    {
        public Channel(bool leftButton) => LeftButton = leftButton;
        public bool LeftButton { get; }
        public Timer? Timer { get; set; }
        public bool Enabled { get; set; }
        public int MinCps { get; set; } = 1;
        public int MaxCps { get; set; } = 1;
        public int RequiredHoldKey { get; set; }
        public int RequiredPointerKey { get; set; }
        public IntPtr TargetWindow { get; set; }
        public int Generation { get; set; }
        public long NextTimestamp { get; set; }
    }
}

