using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace MinecraftHelper.Services
{
    /// <summary>
    /// Collects low-overhead mouse/click timing statistics while an auto-clicker is active.
    /// Samples are aggregated in memory and written on a background thread so the global
    /// low-level mouse hook never performs file I/O.
    /// </summary>
    internal sealed class MacroDiagnosticsService : IDisposable
    {
        private const string LogsFolderName = "Minecraft Helper";
        private const string LogFileName = "macro-diagnostics.log";
        private const string PreviousLogFileName = "macro-diagnostics.previous.log";
        private const long MaximumLogFileBytes = 4L * 1024L * 1024L;

        private readonly object _stateSync = new object();
        private readonly object _fileSync = new object();
        private readonly ConcurrentQueue<string> _pendingLines = new ConcurrentQueue<string>();
        private readonly Timer _sampleTimer;

        private volatile bool _active;
        private bool _disposed;
        private int _writerScheduled;
        private int _sessionNumber;
        private int _activeSessionNumber;
        private string _lastConfiguration = string.Empty;

        private bool _hasLastMousePoint;
        private int _lastMouseX;
        private int _lastMouseY;
        private long _lastUiTickTimestamp;

        private long _moveCount;
        private long _movePath;
        private long _hookLagTotalMs;
        private long _hookLagMaxMs;
        private long _moveDeltaMax;
        private long _uiTickCount;
        private long _uiGapMaxMicroseconds;
        private long _leftClickCount;
        private long _leftSendTotalMicroseconds;
        private long _leftSendMaxMicroseconds;
        private long _leftSendFailures;
        private long _rightClickCount;
        private long _rightSendTotalMicroseconds;
        private long _rightSendMaxMicroseconds;
        private long _rightSendFailures;

        public MacroDiagnosticsService()
        {
            _sampleTimer = new Timer(OnSampleTimer, null, 1000, 1000);
        }

        public string LogFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            LogsFolderName,
            LogFileName);

        public void UpdateMacroState(
            bool leftEnabled,
            int leftMinCps,
            int leftMaxCps,
            bool rightEnabled,
            int rightMinCps,
            int rightMaxCps,
            bool rightHoldPulseMode,
            IntPtr targetWindow)
        {
            bool active = leftEnabled || rightEnabled;
            string configuration = BuildConfiguration(
                leftEnabled,
                leftMinCps,
                leftMaxCps,
                rightEnabled,
                rightMinCps,
                rightMaxCps,
                rightHoldPulseMode,
                targetWindow);

            lock (_stateSync)
            {
                if (_disposed)
                    return;

                if (active && !_active)
                {
                    ResetCounters();
                    _activeSessionNumber = ++_sessionNumber;
                    _lastConfiguration = configuration;
                    _active = true;
                    QueueLine($"START session={_activeSessionNumber} {configuration}");
                    return;
                }

                if (!active && _active)
                {
                    int session = _activeSessionNumber;
                    _active = false;
                    FlushSample(session, force: true);
                    QueueLine($"STOP session={session}");
                    _lastConfiguration = string.Empty;
                    _hasLastMousePoint = false;
                    Interlocked.Exchange(ref _lastUiTickTimestamp, 0);
                    return;
                }

                if (active && !string.Equals(configuration, _lastConfiguration, StringComparison.Ordinal))
                {
                    _lastConfiguration = configuration;
                    QueueLine($"CONFIG session={_activeSessionNumber} {configuration}");
                }
            }
        }

        public void RecordMouseMove(uint eventTimeMilliseconds, int x, int y)
        {
            if (!_active)
                return;

            uint now = unchecked((uint)Environment.TickCount);
            uint lag = unchecked(now - eventTimeMilliseconds);
            // Ignore an impossible/stale timestamp rather than polluting the maximum.
            if (lag <= 60_000)
            {
                Interlocked.Add(ref _hookLagTotalMs, lag);
                UpdateMaximum(ref _hookLagMaxMs, lag);
            }

            if (_hasLastMousePoint)
            {
                long delta = Math.Abs((long)x - _lastMouseX) + Math.Abs((long)y - _lastMouseY);
                Interlocked.Add(ref _movePath, delta);
                UpdateMaximum(ref _moveDeltaMax, delta);
            }

            _lastMouseX = x;
            _lastMouseY = y;
            _hasLastMousePoint = true;
            Interlocked.Increment(ref _moveCount);
        }

        public void RecordUiTick()
        {
            if (!_active)
                return;

            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Exchange(ref _lastUiTickTimestamp, now);
            Interlocked.Increment(ref _uiTickCount);
            if (previous <= 0)
                return;

            long gapMicroseconds = (long)Math.Round((now - previous) * 1_000_000.0 / Stopwatch.Frequency);
            UpdateMaximum(ref _uiGapMaxMicroseconds, gapMicroseconds);
        }

        public void RecordClick(bool leftButton, long elapsedMicroseconds, bool success)
        {
            if (!_active)
                return;

            elapsedMicroseconds = Math.Max(0, elapsedMicroseconds);
            if (leftButton)
            {
                Interlocked.Increment(ref _leftClickCount);
                Interlocked.Add(ref _leftSendTotalMicroseconds, elapsedMicroseconds);
                UpdateMaximum(ref _leftSendMaxMicroseconds, elapsedMicroseconds);
                if (!success)
                    Interlocked.Increment(ref _leftSendFailures);
                return;
            }

            Interlocked.Increment(ref _rightClickCount);
            Interlocked.Add(ref _rightSendTotalMicroseconds, elapsedMicroseconds);
            UpdateMaximum(ref _rightSendMaxMicroseconds, elapsedMicroseconds);
            if (!success)
                Interlocked.Increment(ref _rightSendFailures);
        }

        public void Dispose()
        {
            lock (_stateSync)
            {
                if (_disposed)
                    return;

                if (_active)
                {
                    int session = _activeSessionNumber;
                    _active = false;
                    FlushSample(session, force: true);
                    QueueLine($"STOP session={session} reason=application_exit");
                }

                _disposed = true;
                _sampleTimer.Dispose();
            }

            DrainPendingLines();
        }

        private void OnSampleTimer(object? state)
        {
            if (!_active)
                return;

            int session = _activeSessionNumber;
            FlushSample(session, force: false);
        }

        private void FlushSample(int session, bool force)
        {
            long moves = Interlocked.Exchange(ref _moveCount, 0);
            long movePath = Interlocked.Exchange(ref _movePath, 0);
            long hookLagTotalMs = Interlocked.Exchange(ref _hookLagTotalMs, 0);
            long hookLagMaxMs = Interlocked.Exchange(ref _hookLagMaxMs, 0);
            long moveDeltaMax = Interlocked.Exchange(ref _moveDeltaMax, 0);
            long uiTicks = Interlocked.Exchange(ref _uiTickCount, 0);
            long uiGapMaxUs = Interlocked.Exchange(ref _uiGapMaxMicroseconds, 0);
            long leftClicks = Interlocked.Exchange(ref _leftClickCount, 0);
            long leftSendTotalUs = Interlocked.Exchange(ref _leftSendTotalMicroseconds, 0);
            long leftSendMaxUs = Interlocked.Exchange(ref _leftSendMaxMicroseconds, 0);
            long leftFailures = Interlocked.Exchange(ref _leftSendFailures, 0);
            long rightClicks = Interlocked.Exchange(ref _rightClickCount, 0);
            long rightSendTotalUs = Interlocked.Exchange(ref _rightSendTotalMicroseconds, 0);
            long rightSendMaxUs = Interlocked.Exchange(ref _rightSendMaxMicroseconds, 0);
            long rightFailures = Interlocked.Exchange(ref _rightSendFailures, 0);

            if (!force && moves == 0 && leftClicks == 0 && rightClicks == 0 && uiTicks == 0)
                return;

            double hookLagAverageMs = moves > 0 ? hookLagTotalMs / (double)moves : 0;
            double uiGapMaxMs = uiGapMaxUs / 1000.0;
            double leftSendAverageUs = leftClicks > 0 ? leftSendTotalUs / (double)leftClicks : 0;
            double rightSendAverageUs = rightClicks > 0 ? rightSendTotalUs / (double)rightClicks : 0;

            QueueLine(string.Format(
                CultureInfo.InvariantCulture,
                "SAMPLE session={0} moves={1} path={2} max_delta={3} hook_lag_avg_ms={4:F2} hook_lag_max_ms={5} ui_ticks={6} ui_gap_max_ms={7:F2} L_clicks={8} L_send_avg_us={9:F1} L_send_max_us={10} L_fail={11} R_clicks={12} R_send_avg_us={13:F1} R_send_max_us={14} R_fail={15}",
                session,
                moves,
                movePath,
                moveDeltaMax,
                hookLagAverageMs,
                hookLagMaxMs,
                uiTicks,
                uiGapMaxMs,
                leftClicks,
                leftSendAverageUs,
                leftSendMaxUs,
                leftFailures,
                rightClicks,
                rightSendAverageUs,
                rightSendMaxUs,
                rightFailures));
        }

        private void ResetCounters()
        {
            Interlocked.Exchange(ref _moveCount, 0);
            Interlocked.Exchange(ref _movePath, 0);
            Interlocked.Exchange(ref _hookLagTotalMs, 0);
            Interlocked.Exchange(ref _hookLagMaxMs, 0);
            Interlocked.Exchange(ref _moveDeltaMax, 0);
            Interlocked.Exchange(ref _uiTickCount, 0);
            Interlocked.Exchange(ref _uiGapMaxMicroseconds, 0);
            Interlocked.Exchange(ref _leftClickCount, 0);
            Interlocked.Exchange(ref _leftSendTotalMicroseconds, 0);
            Interlocked.Exchange(ref _leftSendMaxMicroseconds, 0);
            Interlocked.Exchange(ref _leftSendFailures, 0);
            Interlocked.Exchange(ref _rightClickCount, 0);
            Interlocked.Exchange(ref _rightSendTotalMicroseconds, 0);
            Interlocked.Exchange(ref _rightSendMaxMicroseconds, 0);
            Interlocked.Exchange(ref _rightSendFailures, 0);
            Interlocked.Exchange(ref _lastUiTickTimestamp, 0);
            _hasLastMousePoint = false;
        }

        private static string BuildConfiguration(
            bool leftEnabled,
            int leftMinCps,
            int leftMaxCps,
            bool rightEnabled,
            int rightMinCps,
            int rightMaxCps,
            bool rightHoldPulseMode,
            IntPtr targetWindow)
        {
            string left = leftEnabled ? $"{leftMinCps}-{leftMaxCps}" : "off";
            string right = rightEnabled ? $"{rightMinCps}-{rightMaxCps}" : "off";
            return $"LPM={left} PPM={right} PPM_hold={rightHoldPulseMode} target=0x{targetWindow.ToInt64():X}";
        }

        private void QueueLine(string message)
        {
            _pendingLines.Enqueue($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}");
            ScheduleWriter();
        }

        private void ScheduleWriter()
        {
            if (Interlocked.CompareExchange(ref _writerScheduled, 1, 0) != 0)
                return;

            ThreadPool.QueueUserWorkItem(_ => DrainPendingLines());
        }

        private void DrainPendingLines()
        {
            try
            {
                var builder = new StringBuilder();
                while (_pendingLines.TryDequeue(out string? line))
                    builder.AppendLine(line);

                if (builder.Length == 0)
                    return;

                lock (_fileSync)
                {
                    string path = LogFilePath;
                    string directory = Path.GetDirectoryName(path) ?? string.Empty;
                    Directory.CreateDirectory(directory);
                    RotateLogIfNeeded(path, builder.Length);
                    File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never interfere with mouse input or macro execution.
            }
            finally
            {
                Interlocked.Exchange(ref _writerScheduled, 0);
                if (!_pendingLines.IsEmpty)
                    ScheduleWriter();
            }
        }

        private static void RotateLogIfNeeded(string path, int pendingCharacters)
        {
            if (!File.Exists(path))
                return;

            long estimatedBytes = new FileInfo(path).Length + (pendingCharacters * 2L);
            if (estimatedBytes <= MaximumLogFileBytes)
                return;

            string previousPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, PreviousLogFileName);
            File.Move(path, previousPath, overwrite: true);
        }

        private static void UpdateMaximum(ref long target, long value)
        {
            long current = Interlocked.Read(ref target);
            while (value > current)
            {
                long observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}
