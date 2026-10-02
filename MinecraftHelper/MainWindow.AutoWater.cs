using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace MinecraftHelper
{
    public partial class MainWindow
    {
        private const int AutoWaterPatchSize = 9;
        private const int AutoWaterScanIntervalMs = 35;
        private const int AutoWaterSlotSettleMs = 80;
        private const int AutoWaterPickupVerificationDelayMs = 120;
        private const int AutoWaterPickupRetryDelayMs = 240;
        private const int AutoWaterMaxPickupAttempts = 6;
        private const int AutoWaterMaxRuntimeMs = 10000;

        private enum AutoWaterStage
        {
            None,
            SelectWaterSlot,
            ValidateWaterBucket,
            PlaceWater,
            WaitBeforePickup,
            VerifyPickup
        }

        private AutoWaterStage _autoWaterStage = AutoWaterStage.None;
        private bool _autoWaterBindWasDown;
        private bool _autoWaterCalibrationPending;
        private bool _autoWaterCalibrationControlWasDown;
        private bool _autoWaterRecognitionTestPending;
        private DateTime _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
        private DateTime _autoWaterStartedAtUtc = DateTime.MinValue;
        private DateTime _nextAutoWaterActionAtUtc = DateTime.MinValue;
        private DateTime _nextAutoWaterScanAtUtc = DateTime.MinValue;
        private byte[]? _autoWaterTemplateRgb;
        private int _autoWaterTemplateMatchFrames;
        private int _autoWaterTemplateMismatchFrames;
        private int _autoWaterPickupAttempts;
        private double _autoWaterLastSimilarity;

        private void NormalizeAutoWaterSettings()
        {
            _settings.AutoWaterBind ??= string.Empty;
            _settings.AutoWaterEmptyBucketTemplateBase64 ??= string.Empty;
            _settings.AutoWaterWaterSlot = Math.Clamp(_settings.AutoWaterWaterSlot, 1, 9);
            _settings.AutoWaterReturnSlot = Math.Clamp(_settings.AutoWaterReturnSlot, 1, 9);
            _settings.AutoWaterPlaceIntervalMs = Math.Clamp(
                _settings.AutoWaterPlaceIntervalMs <= 0 ? 65 : _settings.AutoWaterPlaceIntervalMs,
                25,
                500);
            _settings.AutoWaterPickupDelayMs = Math.Clamp(
                _settings.AutoWaterPickupDelayMs <= 0 ? 340 : _settings.AutoWaterPickupDelayMs,
                100,
                1500);
            _settings.AutoWaterSimilarityPercent = Math.Clamp(
                _settings.AutoWaterSimilarityPercent <= 0 ? 84 : _settings.AutoWaterSimilarityPercent,
                70,
                99);
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;

            _autoWaterTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterEmptyBucketTemplateBase64);
            if (_autoWaterTemplateRgb == null)
            {
                _settings.AutoWaterEmptyBucketTemplateBase64 = string.Empty;
                _settings.AutoWaterSampleOffsetX = 0;
                _settings.AutoWaterSampleOffsetFromBottom = 0;
            }
        }

        private static byte[]? DecodeAutoWaterTemplate(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded))
                return null;

            try
            {
                byte[] decoded = Convert.FromBase64String(encoded);
                return decoded.Length == AutoWaterPatchSize * AutoWaterPatchSize * 3 ? decoded : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private void LoadAutoWaterToUi()
        {
            _autoWaterStage = AutoWaterStage.None;
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            _autoWaterBindWasDown = false;
            _autoWaterTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterEmptyBucketTemplateBase64);

            ChkAutoWaterEnabled.IsChecked = _settings.AutoWaterEnabled;
            TxtAutoWaterBind.Text = _settings.AutoWaterBind;
            CbAutoWaterWaterSlot.SelectedIndex = Math.Clamp(_settings.AutoWaterWaterSlot, 1, 9) - 1;
            CbAutoWaterReturnSlot.SelectedIndex = Math.Clamp(_settings.AutoWaterReturnSlot, 1, 9) - 1;
            TxtAutoWaterPlaceIntervalMs.Text = _settings.AutoWaterPlaceIntervalMs.ToString(CultureInfo.InvariantCulture);
            TxtAutoWaterPickupDelayMs.Text = _settings.AutoWaterPickupDelayMs.ToString(CultureInfo.InvariantCulture);
            SlAutoWaterSimilarity.Value = _settings.AutoWaterSimilarityPercent;
            TxtAutoWaterSimilarityValue.Text = $"{_settings.AutoWaterSimilarityPercent}%";
            UpdateAutoWaterStatusLabel();
        }

        private void ReadAutoWaterFromUi()
        {
            _settings.AutoWaterEnabled = ChkAutoWaterEnabled.IsChecked == true;
            _settings.AutoWaterBind = TxtAutoWaterBind.Text.Trim();
            _settings.AutoWaterWaterSlot = GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9);
            _settings.AutoWaterReturnSlot = GetAutoWaterSelectedSlot(CbAutoWaterReturnSlot, 1);
            _settings.AutoWaterPlaceIntervalMs = Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPlaceIntervalMs.Text), 25, 500);
            _settings.AutoWaterPickupDelayMs = Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPickupDelayMs.Text), 100, 1500);
            _settings.AutoWaterSimilarityPercent = Math.Clamp((int)Math.Round(SlAutoWaterSimilarity.Value), 70, 99);
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;
        }

        private static int GetAutoWaterSelectedSlot(ComboBox comboBox, int fallback)
        {
            return comboBox.SelectedIndex is >= 0 and < 9 ? comboBox.SelectedIndex + 1 : fallback;
        }

        private void UpdateAutoWaterEnabledState()
        {
            bool enabled = ChkAutoWaterEnabled?.IsChecked == true;
            if (PanelAutoWaterExpandableContent != null)
                SetExpandableSectionState(PanelAutoWaterExpandableContent, enabled);
            if (PanelAutoWaterContent != null)
                PanelAutoWaterContent.IsEnabled = enabled;

            if (!enabled)
            {
                _autoWaterCalibrationPending = false;
                _autoWaterRecognitionTestPending = false;
                CancelAutoWater("AutoWater wyłączony.", Brushes.Orange, restoreSlot: false);
            }
        }

        private void ChkAutoWaterEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateAutoWaterEnabledState();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
        }

        private void AutoWaterConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoadingUi)
                MarkDirty();
        }

        private void AutoWaterConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoadingUi)
                MarkDirty();
        }

        private void SlAutoWaterSimilarity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtAutoWaterSimilarityValue != null)
                TxtAutoWaterSimilarityValue.Text = $"{Math.Clamp((int)Math.Round(e.NewValue), 70, 99)}%";
            if (!_isLoadingUi)
                MarkDirty();
        }

        private void BtnAutoWaterBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.AutoWater);
        }

        private void BtnAutoWaterCalibrate_Click(object sender, RoutedEventArgs e)
        {
            if (ChkAutoWaterEnabled.IsChecked != true)
            {
                SetAutoWaterStatus("Najpierw włącz moduł AutoWater.", Brushes.Orange);
                return;
            }

            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterRecognitionTestPending = false;
            _autoWaterCalibrationPending = true;
            _autoWaterCalibrationControlWasDown = IsVirtualKeyDown(VK_LCONTROL);
            SetAutoWaterStatus(
                "Kalibracja uzbrojona. Przejdź do Minecrafta, otwórz chat, najedź na fragment ikony pustego wiadra i naciśnij lewy Ctrl.",
                Brushes.Orange);
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void BtnAutoWaterTestRecognition_Click(object sender, RoutedEventArgs e)
        {
            if (_autoWaterTemplateRgb == null)
            {
                SetAutoWaterStatus("Brak wzorca. Najpierw wykonaj kalibrację pustego wiadra.", Brushes.OrangeRed);
                return;
            }

            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = true;
            _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
            SetAutoWaterStatus("Test uzbrojony. Przejdź do Minecrafta — pomiar wykona się po 2 sekundach.", Brushes.Orange);
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void BtnAutoWaterResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            _autoWaterTemplateRgb = null;
            _settings.AutoWaterEmptyBucketTemplateBase64 = string.Empty;
            _settings.AutoWaterSampleOffsetX = 0;
            _settings.AutoWaterSampleOffsetFromBottom = 0;
            SetAutoWaterStatus("Usunięto wzorzec. Wykonaj ponowną kalibrację pustego wiadra.", Brushes.Orange);
            MarkDirty();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private bool TryHandleAutoWaterCalibrationOrTest(DateTime now)
        {
            if (!_autoWaterCalibrationPending && !_autoWaterRecognitionTestPending)
                return false;

            _autoClickScheduler.Stop();
            SetAutoLeftDabHold(false);

            if (_autoWaterCalibrationPending)
            {
                bool controlDown = IsVirtualKeyDown(VK_LCONTROL);
                bool pressed = controlDown && !_autoWaterCalibrationControlWasDown;
                _autoWaterCalibrationControlWasDown = controlDown;
                if (!pressed)
                    return true;

                if (!TryCalibrateAutoWaterAtCursor(out string error))
                {
                    SetAutoWaterStatus(error, Brushes.OrangeRed);
                    return true;
                }

                _autoWaterCalibrationPending = false;
                _suppressBindToggleUntilRelease = true;
                SetAutoWaterStatus(
                    "Kalibracja gotowa. Umieść puste wiadro w slocie i użyj „Testuj rozpoznawanie”.",
                    Brushes.MediumSpringGreen);
                MarkDirty();
                RefreshOverlayHud(now);
                return true;
            }

            if (_autoWaterRecognitionTestAtUtc == DateTime.MinValue)
            {
                _autoWaterRecognitionTestAtUtc = now.AddSeconds(2);
                return true;
            }

            if (now < _autoWaterRecognitionTestAtUtc)
            {
                int remaining = Math.Max(1, (int)Math.Ceiling((_autoWaterRecognitionTestAtUtc - now).TotalSeconds));
                SetAutoWaterStatus($"Test rozpoznawania za {remaining} s…", Brushes.Orange);
                return true;
            }

            _autoWaterRecognitionTestPending = false;
            _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
            if (!TryMeasureAutoWaterSimilarity(out double similarity, out string testError))
            {
                SetAutoWaterStatus(testError, Brushes.OrangeRed);
                return true;
            }

            int threshold = GetAutoWaterSimilarityThreshold();
            bool match = similarity >= threshold;
            SetAutoWaterStatus(
                $"Test: podobieństwo {similarity:0.0}% (próg {threshold}%) — {(match ? "ROZPOZNANO PUSTE WIADRO" : "nie rozpoznano pustego wiadra")}.",
                match ? Brushes.MediumSpringGreen : Brushes.Orange);
            return true;
        }

        private bool TryCalibrateAutoWaterAtCursor(out string error)
        {
            error = string.Empty;
            if (_targetGameWindowHandle == IntPtr.Zero
                || !TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
            {
                error = "Nie mogę odczytać obszaru okna Minecrafta.";
                return false;
            }

            if (!GetCursorPos(out POINT cursor))
            {
                error = "Nie mogę odczytać pozycji kursora.";
                return false;
            }

            int radius = AutoWaterPatchSize / 2;
            if (cursor.X - radius < clientRect.Left
                || cursor.X + radius >= clientRect.Right
                || cursor.Y - radius < clientRect.Top
                || cursor.Y + radius >= clientRect.Bottom)
            {
                error = "Kursor musi znajdować się wewnątrz okna Minecrafta, na ikonie pustego wiadra.";
                return false;
            }

            var patchRect = new Drawing.Rectangle(
                cursor.X - radius,
                cursor.Y - radius,
                AutoWaterPatchSize,
                AutoWaterPatchSize);
            if (!TryCaptureAutoWaterPatch(patchRect, out byte[] patch))
            {
                error = "Nie udało się pobrać próbki ikony. Spróbuj ponownie.";
                return false;
            }

            int clientWidth = clientRect.Right - clientRect.Left;
            _settings.AutoWaterSampleOffsetX = cursor.X - (clientRect.Left + clientWidth / 2);
            _settings.AutoWaterSampleOffsetFromBottom = clientRect.Bottom - cursor.Y;
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;
            _settings.AutoWaterEmptyBucketTemplateBase64 = Convert.ToBase64String(patch);
            _autoWaterTemplateRgb = patch;
            return true;
        }

        private bool TryMeasureAutoWaterSimilarity(out double similarity, out string error)
        {
            similarity = 0;
            error = string.Empty;
            if (_autoWaterTemplateRgb == null)
            {
                error = "Brak wzorca pustego wiadra.";
                return false;
            }
            if (_targetGameWindowHandle == IntPtr.Zero
                || !TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
            {
                error = "Nie mogę odczytać obszaru okna Minecrafta.";
                return false;
            }

            int centerX = clientRect.Left + (clientRect.Right - clientRect.Left) / 2;
            int sampleX = centerX + _settings.AutoWaterSampleOffsetX;
            int sampleY = clientRect.Bottom - _settings.AutoWaterSampleOffsetFromBottom;
            int radius = AutoWaterPatchSize / 2;
            if (sampleX - radius < clientRect.Left
                || sampleX + radius >= clientRect.Right
                || sampleY - radius < clientRect.Top
                || sampleY + radius >= clientRect.Bottom)
            {
                error = "Punkt kalibracji wypada poza oknem gry. Wykonaj kalibrację ponownie dla aktualnej rozdzielczości.";
                return false;
            }

            var patchRect = new Drawing.Rectangle(
                sampleX - radius,
                sampleY - radius,
                AutoWaterPatchSize,
                AutoWaterPatchSize);
            if (!TryCaptureAutoWaterPatch(patchRect, out byte[] current))
            {
                error = "Nie udało się odczytać ikony wiadra z ekranu.";
                return false;
            }

            long difference = 0;
            for (int i = 0; i < current.Length; i++)
                difference += Math.Abs(current[i] - _autoWaterTemplateRgb[i]);
            similarity = 100.0 * (1.0 - difference / (current.Length * 255.0));
            similarity = Math.Clamp(similarity, 0.0, 100.0);
            _autoWaterLastSimilarity = similarity;
            return true;
        }

        private static bool TryCaptureAutoWaterPatch(Drawing.Rectangle screenRect, out byte[] rgb)
        {
            rgb = Array.Empty<byte>();
            try
            {
                using var bitmap = new Drawing.Bitmap(screenRect.Width, screenRect.Height, DrawingImaging.PixelFormat.Format24bppRgb);
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(
                        screenRect.Left,
                        screenRect.Top,
                        0,
                        0,
                        screenRect.Size,
                        Drawing.CopyPixelOperation.SourceCopy);
                }

                rgb = new byte[screenRect.Width * screenRect.Height * 3];
                int index = 0;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Drawing.Color pixel = bitmap.GetPixel(x, y);
                        rgb[index++] = pixel.R;
                        rgb[index++] = pixel.G;
                        rgb[index++] = pixel.B;
                    }
                }
                return true;
            }
            catch
            {
                rgb = Array.Empty<byte>();
                return false;
            }
        }

        private bool TryStartAutoWater(DateTime now)
        {
            if (_autoWaterTemplateRgb == null)
            {
                SetAutoWaterStatus("Nie uruchomiono AutoWater: najpierw skalibruj puste wiadro.", Brushes.OrangeRed);
                UpdateStatusBar("AutoWater: najpierw skalibruj puste wiadro", "Orange");
                return false;
            }
            if (IsInventoryCursorVisible())
            {
                SetAutoWaterStatus("Nie uruchomiono AutoWater: zamknij chat, ekwipunek lub inne GUI.", Brushes.OrangeRed);
                UpdateStatusBar("AutoWater: zamknij chat, ekwipunek lub inne GUI", "Orange");
                return false;
            }

            StopClickerRuntimesForExclusivePointerMacro();
            StopOtherExclusivePointerMacros();
            _testAutoFishingRuntimeEnabled = false;
            ResetTestAutoFishingRuntimeState(now);
            _autoWaterStage = AutoWaterStage.SelectWaterSlot;
            _autoWaterStartedAtUtc = now;
            _nextAutoWaterActionAtUtc = now;
            _nextAutoWaterScanAtUtc = now;
            _autoWaterTemplateMatchFrames = 0;
            _autoWaterTemplateMismatchFrames = 0;
            _autoWaterPickupAttempts = 0;
            _autoWaterLastSimilarity = 0;
            SetAutoWaterStatus("AutoWater uruchomiony: wybieranie slotu z wodą…", Brushes.DeepSkyBlue);
            UpdateStatusBar("AutoWater uruchomiony", "Orange");
            RefreshOverlayHud(now);
            return true;
        }

        private void RunAutoWaterTick(DateTime now)
        {
            if (_autoWaterStage == AutoWaterStage.None)
                return;
            if ((now - _autoWaterStartedAtUtc).TotalMilliseconds >= AutoWaterMaxRuntimeMs)
            {
                CancelAutoWater("AutoWater przerwany: przekroczono 10 sekund bez zakończenia.", Brushes.OrangeRed, restoreSlot: true);
                return;
            }
            if (IsInventoryCursorVisible())
            {
                CancelAutoWater("AutoWater przerwany: wykryto otwarty chat, ekwipunek lub inne GUI.", Brushes.OrangeRed, restoreSlot: false);
                return;
            }

            switch (_autoWaterStage)
            {
                case AutoWaterStage.SelectWaterSlot:
                    SendKeyTap(GetSlotVirtualKey(GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9)));
                    _autoWaterStage = AutoWaterStage.ValidateWaterBucket;
                    _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterSlotSettleMs);
                    SetAutoWaterStatus("Wybrano slot z wodą. Sprawdzanie ikony…", Brushes.DeepSkyBlue);
                    return;

                case AutoWaterStage.ValidateWaterBucket:
                    if (now < _nextAutoWaterActionAtUtc)
                        return;
                    if (!TryMeasureAutoWaterSimilarity(out double initialSimilarity, out string validationError))
                    {
                        CancelAutoWater($"AutoWater przerwany: {validationError}", Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (initialSimilarity >= GetAutoWaterSimilarityThreshold())
                    {
                        CancelAutoWater(
                            $"AutoWater przerwany: wybrany slot wygląda już jak puste wiadro ({initialSimilarity:0.0}%). Włóż wiadro z wodą.",
                            Brushes.OrangeRed,
                            restoreSlot: true);
                        return;
                    }
                    _autoWaterStage = AutoWaterStage.PlaceWater;
                    _nextAutoWaterActionAtUtc = now;
                    _nextAutoWaterScanAtUtc = now;
                    SetAutoWaterStatus("Stawianie wody — próby PPM i obserwacja pustego wiadra…", Brushes.DeepSkyBlue);
                    return;

                case AutoWaterStage.PlaceWater:
                    if (now >= _nextAutoWaterActionAtUtc)
                    {
                        SendMouseClick(leftButton: false, holdPulseMode: false);
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(GetAutoWaterPlaceIntervalMs());
                    }
                    if (now < _nextAutoWaterScanAtUtc)
                        return;
                    _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    if (!TryMeasureAutoWaterSimilarity(out double placementSimilarity, out string placementError))
                    {
                        CancelAutoWater($"AutoWater przerwany: {placementError}", Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (placementSimilarity >= GetAutoWaterSimilarityThreshold())
                        _autoWaterTemplateMatchFrames++;
                    else
                        _autoWaterTemplateMatchFrames = 0;
                    if (_autoWaterTemplateMatchFrames >= 2)
                    {
                        _autoWaterStage = AutoWaterStage.WaitBeforePickup;
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(GetAutoWaterPickupDelayMs());
                        SetAutoWaterStatus(
                            $"Woda postawiona ({placementSimilarity:0.0}%). Zebranie za {GetAutoWaterPickupDelayMs()} ms…",
                            Brushes.MediumSpringGreen);
                    }
                    return;

                case AutoWaterStage.WaitBeforePickup:
                    if (now < _nextAutoWaterActionAtUtc)
                        return;
                    SendMouseClick(leftButton: false, holdPulseMode: false);
                    _autoWaterPickupAttempts = 1;
                    _autoWaterTemplateMismatchFrames = 0;
                    _autoWaterStage = AutoWaterStage.VerifyPickup;
                    _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterPickupVerificationDelayMs);
                    SetAutoWaterStatus("Próba zebrania wody — weryfikacja ikony…", Brushes.DeepSkyBlue);
                    return;

                case AutoWaterStage.VerifyPickup:
                    if (now < _nextAutoWaterActionAtUtc)
                        return;
                    if (!TryMeasureAutoWaterSimilarity(out double pickupSimilarity, out string pickupError))
                    {
                        CancelAutoWater($"AutoWater: nie udało się potwierdzić zebrania — {pickupError}", Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (pickupSimilarity < GetAutoWaterSimilarityThreshold())
                        _autoWaterTemplateMismatchFrames++;
                    else
                        _autoWaterTemplateMismatchFrames = 0;
                    if (_autoWaterTemplateMismatchFrames >= 2)
                    {
                        CompleteAutoWater(pickupSimilarity);
                        return;
                    }
                    if (_autoWaterTemplateMismatchFrames > 0)
                    {
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                        return;
                    }
                    if (_autoWaterPickupAttempts >= AutoWaterMaxPickupAttempts)
                    {
                        CancelAutoWater(
                            $"AutoWater zakończony z ostrzeżeniem: nie potwierdzono zebrania wody ({pickupSimilarity:0.0}%).",
                            Brushes.Orange,
                            restoreSlot: true);
                        return;
                    }
                    SendMouseClick(leftButton: false, holdPulseMode: false);
                    _autoWaterPickupAttempts++;
                    _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterPickupRetryDelayMs);
                    SetAutoWaterStatus($"Ponowna próba zebrania wody ({_autoWaterPickupAttempts}/{AutoWaterMaxPickupAttempts})…", Brushes.Orange);
                    return;
            }
        }

        private void CompleteAutoWater(double similarity)
        {
            SendKeyTap(GetSlotVirtualKey(GetAutoWaterSelectedSlot(CbAutoWaterReturnSlot, 1)));
            ResetAutoWaterRuntimeState();
            SetAutoWaterStatus($"Gotowe. Woda zebrana, przywrócono wybrany slot (podobieństwo pustego wiadra spadło do {similarity:0.0}%).", Brushes.MediumSpringGreen);
            UpdateStatusBar("AutoWater zakończony", "Green");
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void CancelAutoWater(string message, Brush brush, bool restoreSlot, bool updateStatus = true)
        {
            bool wasActive = _autoWaterStage != AutoWaterStage.None;
            if (wasActive && restoreSlot && _targetGameWindowHandle != IntPtr.Zero && GetForegroundWindow() == _targetGameWindowHandle)
                SendKeyTap(GetSlotVirtualKey(GetAutoWaterSelectedSlot(CbAutoWaterReturnSlot, 1)));
            ResetAutoWaterRuntimeState();
            if (updateStatus && !string.IsNullOrWhiteSpace(message))
            {
                SetAutoWaterStatus(message, brush);
                if (wasActive)
                    UpdateStatusBar(message, "Orange");
            }
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void ResetAutoWaterRuntimeState()
        {
            _autoWaterStage = AutoWaterStage.None;
            _autoWaterStartedAtUtc = DateTime.MinValue;
            _nextAutoWaterActionAtUtc = DateTime.MinValue;
            _nextAutoWaterScanAtUtc = DateTime.MinValue;
            _autoWaterTemplateMatchFrames = 0;
            _autoWaterTemplateMismatchFrames = 0;
            _autoWaterPickupAttempts = 0;
        }

        private int GetAutoWaterPlaceIntervalMs()
        {
            return Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPlaceIntervalMs.Text), 25, 500);
        }

        private int GetAutoWaterPickupDelayMs()
        {
            return Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPickupDelayMs.Text), 100, 1500);
        }

        private int GetAutoWaterSimilarityThreshold()
        {
            return Math.Clamp((int)Math.Round(SlAutoWaterSimilarity.Value), 70, 99);
        }

        private void SetAutoWaterStatus(string message, Brush brush)
        {
            if (TxtAutoWaterStatus == null)
                return;
            TxtAutoWaterStatus.Text = message;
            TxtAutoWaterStatus.Foreground = brush;
        }

        private void UpdateAutoWaterStatusLabel()
        {
            if (_autoWaterTemplateRgb == null)
            {
                SetAutoWaterStatus("Brak kalibracji pustego wiadra.", Brushes.Orange);
                return;
            }

            SetAutoWaterStatus(
                $"Wzorzec gotowy: {AutoWaterPatchSize}×{AutoWaterPatchSize} px. Użyj testu, a następnie aktywuj AutoWater bindem.",
                Brushes.MediumSpringGreen);
        }

        private OverlayHudEntry BuildAutoWaterOverlayEntry(DateTime now)
        {
            string state;
            string detail;
            OverlayHudTone tone = OverlayHudTone.Warning;
            if (_autoWaterCalibrationPending)
            {
                state = "KALIBRACJA";
                detail = "Najedź na ikonę pustego wiadra i naciśnij lewy Ctrl.";
            }
            else if (_autoWaterRecognitionTestPending)
            {
                state = "TEST ROZPOZNAWANIA";
                detail = _autoWaterRecognitionTestAtUtc == DateTime.MinValue
                    ? "Przejdź do Minecrafta."
                    : $"Pomiar za {Math.Max(0, (int)Math.Ceiling((_autoWaterRecognitionTestAtUtc - now).TotalSeconds))} s.";
            }
            else
            {
                state = _autoWaterStage switch
                {
                    AutoWaterStage.SelectWaterSlot => "WYBÓR WODY",
                    AutoWaterStage.ValidateWaterBucket => "KONTROLA SLOTU",
                    AutoWaterStage.PlaceWater => "STAWIANIE WODY",
                    AutoWaterStage.WaitBeforePickup => "WODA POSTAWIONA",
                    AutoWaterStage.VerifyPickup => "ZBIERANIE WODY",
                    _ => "GOTOWY"
                };
                detail = _autoWaterStage switch
                {
                    AutoWaterStage.PlaceWater => $"PPM co {GetAutoWaterPlaceIntervalMs()} ms • podobieństwo {_autoWaterLastSimilarity:0.0}%",
                    AutoWaterStage.WaitBeforePickup => $"Zebranie za {Math.Max(0, (int)Math.Ceiling((_nextAutoWaterActionAtUtc - now).TotalMilliseconds))} ms",
                    AutoWaterStage.VerifyPickup => $"Próba {_autoWaterPickupAttempts}/{AutoWaterMaxPickupAttempts} • podobieństwo {_autoWaterLastSimilarity:0.0}%",
                    _ => "Automatyczna sekwencja AutoWater trwa."
                };
                tone = OverlayHudTone.Active;
            }

            return new OverlayHudEntry(
                "AUTOWATER",
                $"Teraz: {state}\n{detail}\nBind ponownie = natychmiastowe anulowanie",
                tone,
                Emphasize: true);
        }
    }
}
