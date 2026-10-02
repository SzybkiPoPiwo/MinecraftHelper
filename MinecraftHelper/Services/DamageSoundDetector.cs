using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Vorbis;
using NAudio.Wave;

namespace MinecraftHelper.Services
{
    internal sealed record AudioOutputDeviceInfo(string Id, string Name, bool IsDefault);

    internal sealed record DamageSoundReferenceSet(
        IReadOnlyList<IReadOnlyList<double>> Templates,
        int SourceFileCount,
        double SuggestedMinimumDb);

    internal sealed class DamageSoundProgressEventArgs : EventArgs
    {
        public DamageSoundProgressEventArgs(double similarity, double levelDb)
        {
            Similarity = similarity;
            LevelDb = levelDb;
        }

        public double Similarity { get; }
        public double LevelDb { get; }
    }

    internal sealed class DamageSoundDetectedEventArgs : EventArgs
    {
        public DamageSoundDetectedEventArgs(double similarity, double levelDb)
        {
            Similarity = similarity;
            LevelDb = levelDb;
        }

        public double Similarity { get; }
        public double LevelDb { get; }
    }

    /// <summary>
    /// Captures the selected Windows render endpoint and compares short spectral
    /// fingerprints with the fixed Minecraft damage sounds bundled with the app.
    /// </summary>
    internal sealed class DamageSoundDetector : IDisposable
    {
        internal const int FingerprintBandCount = 32;
        private const int FrameSize = 4096;
        private const int HopSize = 1024;
        private const int MaxReferenceTemplates = 24;
        private const int RequiredConsecutiveMatches = 2;
        private static readonly TimeSpan DetectionCooldown = TimeSpan.FromSeconds(3);

        private readonly object _sync = new object();
        private readonly List<float> _monoSamples = new List<float>(FrameSize * 2);
        private readonly List<double[]> _templates = new List<double[]>();
        private WasapiLoopbackCapture? _capture;
        private MMDevice? _captureDevice;
        private DateTime _lastProgressAtUtc = DateTime.MinValue;
        private DateTime _lastDetectionAtUtc = DateTime.MinValue;
        private volatile bool _isRunning;
        private volatile bool _monitoring;
        private bool _disposed;
        private double _similarityThreshold = 0.90;
        private double _minimumLevelDb = -45.0;
        private int _consecutiveMatches;

        public event EventHandler<DamageSoundProgressEventArgs>? ProgressChanged;
        public event EventHandler<DamageSoundDetectedEventArgs>? DamageDetected;
        public event EventHandler<string>? CaptureFailed;

        public bool IsRunning => _isRunning;

        public static IReadOnlyList<AudioOutputDeviceInfo> GetOutputDevices()
        {
            using var enumerator = new MMDeviceEnumerator();
            string defaultId = string.Empty;
            try
            {
                defaultId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            }
            catch
            {
                // No default render device. Active endpoints can still be shown.
            }

            return enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select(device => new AudioOutputDeviceInfo(
                    device.ID,
                    string.IsNullOrWhiteSpace(device.FriendlyName) ? device.ID : device.FriendlyName,
                    string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(device => device.IsDefault)
                .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static bool TryGetOutputDeviceForProcess(int processId, out AudioOutputDeviceInfo? matchingDevice)
        {
            matchingDevice = null;
            if (processId <= 0)
                return false;

            using var enumerator = new MMDeviceEnumerator();
            string defaultId = string.Empty;
            try
            {
                defaultId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
            }
            catch
            {
                // A matching process session on another active endpoint can still be used.
            }

            AudioOutputDeviceInfo? inactiveMatch = null;
            foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    using AudioSessionManager sessionManager = device.AudioSessionManager;
                    sessionManager.RefreshSessions();
                    SessionCollection sessions = sessionManager.Sessions;
                    for (int index = 0; index < sessions.Count; index++)
                    {
                        using AudioSessionControl session = sessions[index];
                        if (session.GetProcessID != (uint)processId)
                            continue;

                        var deviceInfo = new AudioOutputDeviceInfo(
                            device.ID,
                            string.IsNullOrWhiteSpace(device.FriendlyName) ? device.ID : device.FriendlyName,
                            string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase));
                        if (session.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive)
                        {
                            matchingDevice = deviceInfo;
                            return true;
                        }

                        inactiveMatch ??= deviceInfo;
                    }
                }
                catch
                {
                    // Some endpoints or sessions may disappear while they are enumerated.
                }
                finally
                {
                    device.Dispose();
                }
            }

            matchingDevice = inactiveMatch;
            return matchingDevice != null;
        }

        public static DamageSoundReferenceSet LoadReferenceFiles(IEnumerable<string> filePaths)
        {
            var templates = new List<IReadOnlyList<double>>();
            int loadedFiles = 0;
            foreach (string filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (!System.IO.File.Exists(filePath))
                    throw new System.IO.FileNotFoundException("Brak pliku wzorca dźwięku obrażeń.", filePath);

                using var reader = new VorbisWaveReader(filePath);
                ISampleProvider sampleProvider = reader.ToSampleProvider();
                int channels = Math.Max(1, sampleProvider.WaveFormat.Channels);
                int sampleRate = sampleProvider.WaveFormat.SampleRate;
                var interleavedBuffer = new float[FrameSize * channels];
                var monoSamples = new List<float>();
                int read;
                while ((read = sampleProvider.Read(interleavedBuffer, 0, interleavedBuffer.Length)) > 0)
                {
                    int completeFrames = read / channels;
                    for (int frameIndex = 0; frameIndex < completeFrames; frameIndex++)
                    {
                        double mixed = 0;
                        int sourceOffset = frameIndex * channels;
                        for (int channel = 0; channel < channels; channel++)
                            mixed += interleavedBuffer[sourceOffset + channel];
                        monoSamples.Add((float)(mixed / channels));
                    }
                }

                List<ReferenceCandidate> candidates = CreateReferenceCandidates(monoSamples, sampleRate);
                if (candidates.Count == 0)
                    throw new InvalidOperationException($"Plik {System.IO.Path.GetFileName(filePath)} nie zawiera użytecznego dźwięku.");

                var selectedForFile = new List<ReferenceCandidate>();
                foreach (ReferenceCandidate candidate in candidates.OrderByDescending(candidate => candidate.LevelDb))
                {
                    if (selectedForFile.Any(selected => CosineSimilarity(selected.Fingerprint, candidate.Fingerprint) > 0.9995))
                        continue;
                    selectedForFile.Add(candidate);
                    if (selectedForFile.Count >= 6)
                        break;
                }
                foreach (ReferenceCandidate candidate in selectedForFile)
                    templates.Add(candidate.Fingerprint.ToList());
                loadedFiles++;
            }

            if (loadedFiles == 0 || templates.Count == 0)
                throw new InvalidOperationException("Nie znaleziono wzorców dźwięku obrażeń.");

            return new DamageSoundReferenceSet(
                templates.Take(MaxReferenceTemplates).ToList(),
                loadedFiles,
                SuggestedMinimumDb: -55.0);
        }

        private static List<ReferenceCandidate> CreateReferenceCandidates(IReadOnlyList<float> samples, int sampleRate)
        {
            var candidates = new List<ReferenceCandidate>();
            if (samples.Count == 0 || sampleRate <= 0)
                return candidates;

            // Include partially-filled windows as well. Runtime WASAPI frames are
            // not aligned to the beginning of the Minecraft sound event.
            for (int start = -FrameSize + HopSize; start < samples.Count; start += HopSize / 2)
            {
                var frame = new float[FrameSize];
                for (int i = 0; i < FrameSize; i++)
                {
                    int sourceIndex = start + i;
                    if (sourceIndex >= 0 && sourceIndex < samples.Count)
                        frame[i] = samples[sourceIndex];
                }
                double levelDb = CalculateLevelDb(frame);
                if (levelDb < -70.0)
                    continue;
                candidates.Add(new ReferenceCandidate(CreateFingerprint(frame, sampleRate), levelDb));
            }

            if (candidates.Count == 0)
                return candidates;
            double peakDb = candidates.Max(candidate => candidate.LevelDb);
            return candidates
                .Where(candidate => candidate.LevelDb >= Math.Max(-65.0, peakDb - 20.0))
                .ToList();
        }

        public void StartMonitoring(
            string? deviceId,
            IEnumerable<IEnumerable<double>> templates,
            int similarityPercent,
            double minimumLevelDb)
        {
            List<double[]> normalizedTemplates = templates
                .Select(values => NormalizeStoredFingerprint(values))
                .Where(values => values != null)
                .Select(values => values!)
                .Take(MaxReferenceTemplates)
                .ToList();
            if (normalizedTemplates.Count == 0)
                throw new InvalidOperationException("Nie wczytano stałych wzorców hit1–hit4.");

            lock (_sync)
            {
                ThrowIfDisposed();
                StopCaptureLocked();
                _templates.Clear();
                _templates.AddRange(normalizedTemplates);
                _similarityThreshold = Math.Clamp(similarityPercent, 70, 99) / 100.0;
                _minimumLevelDb = Math.Clamp(minimumLevelDb, -70.0, -10.0);
                _monitoring = true;
                _consecutiveMatches = 0;
                _lastProgressAtUtc = DateTime.MinValue;
                StartCaptureLocked(deviceId);
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                StopCaptureLocked();
                _monitoring = false;
                _consecutiveMatches = 0;
            }
        }

        private void StartCaptureLocked(string? deviceId)
        {
            MMDevice? selectedDevice = null;
            MMDeviceEnumerator? enumerator = null;
            try
            {
                enumerator = new MMDeviceEnumerator();
                if (!string.IsNullOrWhiteSpace(deviceId))
                {
                    try
                    {
                        selectedDevice = enumerator.GetDevice(deviceId);
                    }
                    catch
                    {
                        selectedDevice = null;
                    }
                }

                selectedDevice ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _capture = new WasapiLoopbackCapture(selectedDevice);
                _captureDevice = selectedDevice;
                selectedDevice = null;
                _capture.DataAvailable += Capture_DataAvailable;
                _capture.RecordingStopped += Capture_RecordingStopped;
                _monoSamples.Clear();
                _capture.StartRecording();
                _isRunning = true;
            }
            catch
            {
                _capture?.Dispose();
                _capture = null;
                _captureDevice?.Dispose();
                _captureDevice = null;
                _isRunning = false;
                _monitoring = false;
                throw;
            }
            finally
            {
                selectedDevice?.Dispose();
                enumerator?.Dispose();
            }
        }

        private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            List<DamageSoundProgressEventArgs>? progressEvents = null;
            DamageSoundDetectedEventArgs? detectedEvent = null;

            lock (_sync)
            {
                if (_capture == null || !_monitoring)
                    return;

                AppendMonoSamples(e.Buffer, e.BytesRecorded, _capture.WaveFormat, _monoSamples);
                while (_monoSamples.Count >= FrameSize)
                {
                    float[] frame = _monoSamples.GetRange(0, FrameSize).ToArray();
                    _monoSamples.RemoveRange(0, Math.Min(HopSize, _monoSamples.Count));
                    double levelDb = CalculateLevelDb(frame);
                    double[] fingerprint = CreateFingerprint(frame, _capture.WaveFormat.SampleRate);
                    DateTime now = DateTime.UtcNow;
                    double similarity = levelDb >= _minimumLevelDb
                        ? GetBestSimilarity(fingerprint, _templates)
                        : 0.0;
                    _consecutiveMatches = similarity >= _similarityThreshold
                        ? _consecutiveMatches + 1
                        : 0;

                    if (now - _lastProgressAtUtc >= TimeSpan.FromMilliseconds(150))
                    {
                        _lastProgressAtUtc = now;
                        progressEvents ??= new List<DamageSoundProgressEventArgs>();
                        progressEvents.Add(new DamageSoundProgressEventArgs(similarity, levelDb));
                    }

                    if (_consecutiveMatches >= RequiredConsecutiveMatches
                        && now - _lastDetectionAtUtc >= DetectionCooldown)
                    {
                        _lastDetectionAtUtc = now;
                        _consecutiveMatches = 0;
                        detectedEvent = new DamageSoundDetectedEventArgs(similarity, levelDb);
                    }
                }
            }

            if (progressEvents != null)
            {
                foreach (DamageSoundProgressEventArgs progress in progressEvents)
                    ProgressChanged?.Invoke(this, progress);
            }
            if (detectedEvent != null)
                DamageDetected?.Invoke(this, detectedEvent);
        }

        private void Capture_RecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception == null)
                return;

            WasapiLoopbackCapture? stoppedCapture;
            MMDevice? stoppedDevice;
            lock (_sync)
            {
                if (!ReferenceEquals(sender, _capture))
                    return;
                stoppedCapture = _capture;
                stoppedDevice = _captureDevice;
                _capture = null;
                _captureDevice = null;
                _monitoring = false;
                _isRunning = false;
                _monoSamples.Clear();
            }
            if (stoppedCapture != null)
            {
                stoppedCapture.DataAvailable -= Capture_DataAvailable;
                stoppedCapture.RecordingStopped -= Capture_RecordingStopped;
            }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                stoppedCapture?.Dispose();
                stoppedDevice?.Dispose();
            });
            CaptureFailed?.Invoke(this, "Nasłuch audio został zatrzymany: " + e.Exception.Message);
        }

        private void StopCaptureLocked()
        {
            WasapiLoopbackCapture? capture = _capture;
            _capture = null;
            MMDevice? captureDevice = _captureDevice;
            _captureDevice = null;
            _isRunning = false;
            _monoSamples.Clear();
            if (capture == null)
            {
                captureDevice?.Dispose();
                return;
            }

            capture.DataAvailable -= Capture_DataAvailable;
            capture.RecordingStopped -= Capture_RecordingStopped;
            try
            {
                capture.StopRecording();
            }
            catch
            {
                // Device may already be unavailable. Dispose still releases it.
            }
            capture.Dispose();
            captureDevice?.Dispose();
        }

        internal static double[] CreateFingerprint(IReadOnlyList<float> samples, int sampleRate)
        {
            if (samples.Count != FrameSize)
                throw new ArgumentException($"Wymagane jest dokładnie {FrameSize} próbek.", nameof(samples));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            var fft = new Complex[FrameSize];
            for (int i = 0; i < FrameSize; i++)
            {
                double window = 0.5 - 0.5 * Math.Cos((2.0 * Math.PI * i) / (FrameSize - 1));
                fft[i].X = (float)(samples[i] * window);
                fft[i].Y = 0;
            }
            FastFourierTransform.FFT(true, 12, fft);

            var bands = new double[FingerprintBandCount];
            const double lowHz = 100.0;
            double highHz = Math.Min(8000.0, sampleRate / 2.0);
            double logLow = Math.Log(lowHz);
            double logHigh = Math.Log(Math.Max(lowHz + 1, highHz));
            int upperBin = Math.Min(FrameSize / 2, (int)Math.Floor(highHz * FrameSize / sampleRate));
            for (int bin = 1; bin <= upperBin; bin++)
            {
                double hz = bin * sampleRate / (double)FrameSize;
                if (hz < lowHz)
                    continue;
                int band = (int)Math.Floor((Math.Log(hz) - logLow) / Math.Max(0.0001, logHigh - logLow) * FingerprintBandCount);
                band = Math.Clamp(band, 0, FingerprintBandCount - 1);
                bands[band] += fft[bin].X * fft[bin].X + fft[bin].Y * fft[bin].Y;
            }

            for (int i = 0; i < bands.Length; i++)
                bands[i] = Math.Log10(1e-12 + bands[i]);
            NormalizeInPlace(bands);
            return bands;
        }

        internal static double GetBestSimilarity(double[] fingerprint, IReadOnlyList<double[]> templates)
        {
            double best = 0;
            foreach (double[] template in templates)
            {
                for (int shift = -2; shift <= 2; shift++)
                    best = Math.Max(best, ShiftedCosineSimilarity(fingerprint, template, shift));
            }
            return Math.Clamp(best, 0.0, 1.0);
        }

        internal static double CalculateLevelDb(IReadOnlyList<float> samples)
        {
            if (samples.Count == 0)
                return -96.0;
            double sum = 0;
            for (int i = 0; i < samples.Count; i++)
                sum += samples[i] * samples[i];
            double rms = Math.Sqrt(sum / samples.Count);
            return Math.Clamp(20.0 * Math.Log10(Math.Max(rms, 1e-8)), -96.0, 0.0);
        }

        private static double[]? NormalizeStoredFingerprint(IEnumerable<double> values)
        {
            double[] result = values.Take(FingerprintBandCount + 1).ToArray();
            if (result.Length != FingerprintBandCount || result.Any(value => !double.IsFinite(value)))
                return null;
            NormalizeInPlace(result);
            return result;
        }

        private static void NormalizeInPlace(double[] values)
        {
            double mean = values.Average();
            double lengthSquared = 0;
            for (int i = 0; i < values.Length; i++)
            {
                values[i] -= mean;
                lengthSquared += values[i] * values[i];
            }
            double length = Math.Sqrt(Math.Max(lengthSquared, 1e-12));
            for (int i = 0; i < values.Length; i++)
                values[i] /= length;
        }

        private static double ShiftedCosineSimilarity(double[] left, double[] right, int shift)
        {
            double dot = 0;
            double leftLength = 0;
            double rightLength = 0;
            for (int i = 0; i < left.Length; i++)
            {
                int rightIndex = i + shift;
                if (rightIndex < 0 || rightIndex >= right.Length)
                    continue;
                dot += left[i] * right[rightIndex];
                leftLength += left[i] * left[i];
                rightLength += right[rightIndex] * right[rightIndex];
            }
            if (leftLength <= 1e-12 || rightLength <= 1e-12)
                return 0;
            return dot / Math.Sqrt(leftLength * rightLength);
        }

        private static double CosineSimilarity(double[] left, double[] right)
        {
            double dot = 0;
            for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
                dot += left[i] * right[i];
            return dot;
        }

        private static void AppendMonoSamples(byte[] buffer, int byteCount, WaveFormat format, List<float> destination)
        {
            int channels = Math.Max(1, format.Channels);
            int bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
            int bytesPerFrame = bytesPerSample * channels;
            bool floatingPoint = format.Encoding == WaveFormatEncoding.IeeeFloat
                || (format.Encoding == WaveFormatEncoding.Extensible && format.BitsPerSample == 32);

            for (int offset = 0; offset + bytesPerFrame <= byteCount; offset += bytesPerFrame)
            {
                double mixed = 0;
                for (int channel = 0; channel < channels; channel++)
                {
                    int sampleOffset = offset + channel * bytesPerSample;
                    mixed += ReadSample(buffer, sampleOffset, bytesPerSample, floatingPoint);
                }
                destination.Add((float)Math.Clamp(mixed / channels, -1.0, 1.0));
            }
        }

        private static double ReadSample(byte[] buffer, int offset, int bytesPerSample, bool floatingPoint)
        {
            if (floatingPoint && bytesPerSample == 4)
                return BitConverter.ToSingle(buffer, offset);
            return bytesPerSample switch
            {
                2 => BitConverter.ToInt16(buffer, offset) / 32768.0,
                3 => ReadPcm24(buffer, offset) / 8388608.0,
                4 => BitConverter.ToInt32(buffer, offset) / 2147483648.0,
                _ => (buffer[offset] - 128) / 128.0
            };
        }

        private static int ReadPcm24(byte[] buffer, int offset)
        {
            int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
            if ((value & 0x800000) != 0)
                value |= unchecked((int)0xFF000000);
            return value;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DamageSoundDetector));
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                StopCaptureLocked();
                _disposed = true;
            }
        }

        private sealed record ReferenceCandidate(double[] Fingerprint, double LevelDb);
    }
}
