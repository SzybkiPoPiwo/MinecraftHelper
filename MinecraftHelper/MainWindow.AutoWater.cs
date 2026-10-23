using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MinecraftHelper.Services;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace MinecraftHelper
{
    public partial class MainWindow
    {
        private const int AutoWaterPatchSize = 9;
        private const int AutoWaterScanIntervalMs = 35;
        private const int AutoWaterSlotSettleMs = 80;
        private const int AutoWaterSlotValidationMs = 600;
        private const int AutoWaterPickupVerificationDelayMs = 120;
        private const int AutoWaterPickupRetryDelayMs = 240;
        private const int AutoWaterMaxPickupAttempts = 6;
        private const int AutoWaterMaxRuntimeMs = 10000;
        private const double AutoWaterRecognitionMargin = 3.0;
        private const double AutoWaterMaximumTemplateSimilarity = 92.0;

        private enum AutoWaterStage
        {
            None,
            SelectWaterSlot,
            ValidateWaterBucket,
            PlaceWater,
            ConfirmPlacement,
            WaitBeforePickup,
            VerifyPickup
        }

        private enum AutoWaterBucketState { Unknown, Empty, Water }

        private AutoWaterStage _autoWaterStage = AutoWaterStage.None;
        private bool _autoWaterBindWasDown;
        private bool _autoWaterCalibrationPending;
        private bool _autoWaterCalibratingFullBucket;
        private bool _autoWaterCalibrationControlWasDown;
        private bool _autoWaterRecognitionTestPending;
        private DateTime _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
        private DateTime _autoWaterStartedAtUtc = DateTime.MinValue;
        private DateTime _nextAutoWaterActionAtUtc = DateTime.MinValue;
        private DateTime _nextAutoWaterScanAtUtc = DateTime.MinValue;
        private DateTime _autoWaterValidationDeadlineUtc = DateTime.MinValue;
        private byte[]? _autoWaterTemplateRgb;
        private byte[]? _autoWaterFullTemplateRgb;
        private int _autoWaterTemplateMatchFrames;
        private int _autoWaterPickupAttempts;
        private int _autoWaterRuntimeWaterSlot;
        private int _autoWaterRuntimeReturnSlot;
        private double _autoWaterLastSimilarity;
        private double _autoWaterLastFullSimilarity;

        private bool HasCompleteAutoWaterCalibration =>
            _autoWaterTemplateRgb != null
            && _autoWaterFullTemplateRgb != null
            && _settings.AutoWaterCalibrationClientWidth > 0
            && _settings.AutoWaterCalibrationClientHeight > 0
            && _settings.AutoWaterCalibrationSlot == GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9);

        private void NormalizeAutoWaterSettings()
        {
            _settings.AutoWaterBind ??= string.Empty;
            _settings.AutoWaterEmptyBucketTemplateBase64 ??= string.Empty;
            _settings.AutoWaterFullBucketTemplateBase64 ??= string.Empty;
            _settings.AutoWaterWaterSlot = Math.Clamp(_settings.AutoWaterWaterSlot, 1, 9);
            _settings.AutoWaterReturnSlot = Math.Clamp(_settings.AutoWaterReturnSlot, 1, 9);
            _settings.AutoWaterPlaceIntervalMs = Math.Clamp(
                _settings.AutoWaterPlaceIntervalMs <= 0 ? 65 : _settings.AutoWaterPlaceIntervalMs, 25, 500);
            _settings.AutoWaterPickupDelayMs = Math.Clamp(
                _settings.AutoWaterPickupDelayMs <= 0 ? 340 : _settings.AutoWaterPickupDelayMs, 100, 1500);
            _settings.AutoWaterSimilarityPercent = Math.Clamp(
                _settings.AutoWaterSimilarityPercent <= 0 ? 84 : _settings.AutoWaterSimilarityPercent, 70, 99);
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;
            _autoWaterTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterEmptyBucketTemplateBase64);
            _autoWaterFullTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterFullBucketTemplateBase64);
            if (_autoWaterTemplateRgb == null)
            {
                _settings.AutoWaterEmptyBucketTemplateBase64 = string.Empty;
                _settings.AutoWaterFullBucketTemplateBase64 = string.Empty;
                _autoWaterFullTemplateRgb = null;
            }
            else if (_autoWaterFullTemplateRgb == null
                || CalculateAutoWaterSimilarity(_autoWaterTemplateRgb, _autoWaterFullTemplateRgb) > AutoWaterMaximumTemplateSimilarity)
            {
                _settings.AutoWaterFullBucketTemplateBase64 = string.Empty;
                _autoWaterFullTemplateRgb = null;
            }
        }

        private static byte[]? DecodeAutoWaterTemplate(string encoded)
        {
            int byteCount = AutoWaterPatchSize * AutoWaterPatchSize * 3;
            if (string.IsNullOrWhiteSpace(encoded) || encoded.Length != (byteCount + 2) / 3 * 4)
                return null;
            try
            {
                byte[] decoded = Convert.FromBase64String(encoded);
                return decoded.Length == byteCount ? decoded : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private void LoadAutoWaterToUi()
        {
            ResetAutoWaterRuntimeState();
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            _autoWaterBindWasDown = false;
            _autoWaterTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterEmptyBucketTemplateBase64);
            _autoWaterFullTemplateRgb = DecodeAutoWaterTemplate(_settings.AutoWaterFullBucketTemplateBase64);
            ChkAutoWaterEnabled.IsChecked = _settings.AutoWaterEnabled;
            TxtAutoWaterBind.Text = _settings.AutoWaterBind;
            CbAutoWaterWaterSlot.SelectedIndex = _settings.AutoWaterWaterSlot - 1;
            CbAutoWaterReturnSlot.SelectedIndex = _settings.AutoWaterReturnSlot - 1;
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
            _settings.AutoWaterPlaceIntervalMs = GetAutoWaterPlaceIntervalMs();
            _settings.AutoWaterPickupDelayMs = GetAutoWaterPickupDelayMs();
            _settings.AutoWaterSimilarityPercent = GetAutoWaterSimilarityThreshold();
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;
        }

        private static int GetAutoWaterSelectedSlot(ComboBox comboBox, int fallback) =>
            comboBox.SelectedIndex is >= 0 and < 9 ? comboBox.SelectedIndex + 1 : fallback;

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
            if (_isLoadingUi) return;
            UpdateAutoWaterEnabledState();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
        }

        private void AutoWaterConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoadingUi) MarkDirty();
        }

        private void AutoWaterConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi) return;
            CancelAutoWater("Zmieniono konfigurację AutoWater.", Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            UpdateAutoWaterStatusLabel();
            MarkDirty();
        }

        private void SlAutoWaterSimilarity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtAutoWaterSimilarityValue != null)
                TxtAutoWaterSimilarityValue.Text = $"{Math.Clamp((int)Math.Round(e.NewValue), 70, 99)}%";
            if (!_isLoadingUi) MarkDirty();
        }

        private void BtnAutoWaterBind_Click(object sender, RoutedEventArgs e) =>
            ConfirmPendingBind(BindTarget.AutoWater);

        private void BtnAutoWaterCalibrate_Click(object sender, RoutedEventArgs e)
        {
            if (ChkAutoWaterEnabled.IsChecked != true)
            {
                SetAutoWaterStatus("Najpierw włącz moduł AutoWater.", Brushes.Orange);
                return;
            }
            bool full = sender is Button button && string.Equals(button.Tag?.ToString(), "Full", StringComparison.Ordinal);
            if (full && (_autoWaterTemplateRgb == null || _settings.AutoWaterCalibrationClientWidth <= 0
                || _settings.AutoWaterCalibrationSlot != GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9)))
            {
                SetAutoWaterStatus("Najpierw skalibruj puste wiadro na hotbarze dla wybranego slotu.", Brushes.OrangeRed);
                return;
            }
            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterRecognitionTestPending = false;
            _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
            _autoWaterCalibrationPending = true;
            _autoWaterCalibratingFullBucket = full;
            _autoWaterCalibrationControlWasDown = IsVirtualKeyDown(VK_LCONTROL);
            SetAutoWaterStatus(full
                ? "Kalibracja wody: włóż wiadro z wodą w ten sam slot, otwórz czat i naciśnij lewy Ctrl. Odczytam wcześniej zapisany punkt."
                : "Kalibracja pustego wiadra: umieść je w wybranym slocie HOTBARA, otwórz CZAT (nie EQ), najedź na ikonę nad polem czatu i naciśnij lewy Ctrl.",
                Brushes.Orange);
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void BtnAutoWaterTestRecognition_Click(object sender, RoutedEventArgs e)
        {
            if (!HasCompleteAutoWaterCalibration)
            {
                UpdateAutoWaterStatusLabel();
                return;
            }
            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = true;
            _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
            SetAutoWaterStatus("Test uzbrojony. Wróć do Minecrafta i zamknij GUI — od tej chwili odliczę 2 sekundy.", Brushes.Orange);
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void BtnAutoWaterResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            _autoWaterTemplateRgb = null;
            _autoWaterFullTemplateRgb = null;
            _settings.AutoWaterEmptyBucketTemplateBase64 = string.Empty;
            _settings.AutoWaterFullBucketTemplateBase64 = string.Empty;
            _settings.AutoWaterSampleOffsetX = 0;
            _settings.AutoWaterSampleOffsetFromBottom = 0;
            _settings.AutoWaterCalibrationClientWidth = 0;
            _settings.AutoWaterCalibrationClientHeight = 0;
            _settings.AutoWaterCalibrationSlot = 0;
            SetAutoWaterStatus("Usunięto oba wzorce. Skalibruj puste wiadro, następnie wiadro z wodą.", Brushes.Orange);
            MarkDirty();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private bool IsAutoWaterTargetFocused() =>
            _targetGameWindowHandle != IntPtr.Zero && GetForegroundWindow() == _targetGameWindowHandle;

        private bool TryHandleAutoWaterCalibrationOrTest(DateTime now)
        {
            if (!_autoWaterCalibrationPending && !_autoWaterRecognitionTestPending) return false;
            StopClickerRuntimesForExclusivePointerMacro();
            SetAutoLeftDabHold(false);
            bool controlDown = IsVirtualKeyDown(VK_LCONTROL);
            if (!IsAutoWaterTargetFocused())
            {
                _autoWaterCalibrationControlWasDown = controlDown;
                _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
                SetAutoWaterStatus("Oczekiwanie na aktywne okno Minecrafta. Kalibracja i test nie odczytują innych okien.", Brushes.Orange);
                return true;
            }
            if (_autoWaterCalibrationPending)
            {
                bool pressed = controlDown && !_autoWaterCalibrationControlWasDown;
                _autoWaterCalibrationControlWasDown = controlDown;
                if (!pressed) return true;
                if (!TryCalibrateAutoWaterAtCursor(out string error))
                {
                    SetAutoWaterStatus(error, Brushes.OrangeRed);
                    return true;
                }
                _autoWaterCalibrationPending = false;
                _suppressBindToggleUntilRelease = true;
                SetAutoWaterStatus(_autoWaterCalibratingFullBucket
                    ? "Oba wzorce gotowe. Zamknij czat i przetestuj osobno puste wiadro oraz wiadro z wodą."
                    : "Puste wiadro zapisane. Teraz wybierz „Kalibruj wodę”, umieść wiadro z wodą w tym samym slocie i użyj Ctrl przy otwartym czacie.",
                    Brushes.MediumSpringGreen);
                MarkDirty();
                RefreshOverlayHud(now);
                return true;
            }
            if (IsInventoryCursorVisible())
            {
                _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
                SetAutoWaterStatus("Test czeka na zamknięcie czatu, EQ lub menu. Potem odliczy pełne 2 sekundy.", Brushes.Orange);
                return true;
            }
            if (_autoWaterRecognitionTestAtUtc == DateTime.MinValue)
            {
                _autoWaterRecognitionTestAtUtc = now.AddSeconds(2);
                return true;
            }
            if (now < _autoWaterRecognitionTestAtUtc)
            {
                SetAutoWaterStatus($"Test rozpoznawania za {Math.Max(1, (int)Math.Ceiling((_autoWaterRecognitionTestAtUtc - now).TotalSeconds))} s…", Brushes.Orange);
                return true;
            }
            _autoWaterRecognitionTestPending = false;
            _autoWaterRecognitionTestAtUtc = DateTime.MinValue;
            if (!TryReadAutoWaterBucket(out AutoWaterBucketState state, out string testError))
            {
                SetAutoWaterStatus(testError, Brushes.OrangeRed);
                return true;
            }
            string result = state switch
            {
                AutoWaterBucketState.Empty => "PUSTE WIADRO",
                AutoWaterBucketState.Water => "WIADRO Z WODĄ",
                _ => "NIEZNANY PRZEDMIOT / NIEPEWNY ODCZYT"
            };
            SetAutoWaterStatus($"Test: {result}. Puste {_autoWaterLastSimilarity:0.0}% • woda {_autoWaterLastFullSimilarity:0.0}%.",
                state == AutoWaterBucketState.Unknown ? Brushes.OrangeRed : Brushes.MediumSpringGreen);
            return true;
        }

        private bool TryCalibrateAutoWaterAtCursor(out string error)
        {
            error = string.Empty;
            if (!IsAutoWaterTargetFocused())
            {
                error = "Kalibruj wyłącznie w aktywnym, zapisanym oknie Minecrafta.";
                return false;
            }
            if (!IsInventoryCursorVisible())
            {
                error = "Otwórz czat Minecrafta, aby ustawić punkt na ikonie w hotbarze. Nie otwieraj EQ.";
                return false;
            }
            if (_autoWaterCalibratingFullBucket)
            {
                if (!TryGetAutoWaterSampleRect(out Drawing.Rectangle savedRect, out error, requireFullTemplate: false)
                    || !TryCaptureAutoWaterPatch(savedRect, out byte[] full)
                    || !IsAutoWaterTargetFocused())
                {
                    if (string.IsNullOrEmpty(error)) error = "Nie udało się odczytać wzorca wiadra z wodą.";
                    return false;
                }
                if (CalculateAutoWaterSimilarity(_autoWaterTemplateRgb!, full) > AutoWaterMaximumTemplateSimilarity)
                {
                    error = "Wzorce są zbyt podobne. Włóż wiadro Z WODĄ. Jeśli już je masz, wybierz przy kalibracji pustego wiadra inny fragment, który zmienia wygląd po nalaniu wody.";
                    return false;
                }
                _autoWaterFullTemplateRgb = full;
                _settings.AutoWaterFullBucketTemplateBase64 = Convert.ToBase64String(full);
                return true;
            }
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect) || !GetCursorPos(out POINT cursor))
            {
                error = "Nie mogę odczytać okna Minecrafta lub kursora.";
                return false;
            }
            int radius = AutoWaterPatchSize / 2;
            var patchRect = new Drawing.Rectangle(cursor.X - radius, cursor.Y - radius, AutoWaterPatchSize, AutoWaterPatchSize);
            int slot = GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9);
            if (!IsAutoWaterHotbarSample(clientRect, patchRect, slot))
            {
                error = $"Najedź na ikonę w slocie {slot} HOTBARA, ponad polem CZATU — nie na wiadro w EQ. Cała próbka 9×9 px musi być widoczna. W razie potrzeby ustaw GUI Scale: Medium/Large.";
                return false;
            }
            if (!TryCaptureAutoWaterPatch(patchRect, out byte[] patch) || !IsAutoWaterTargetFocused())
            {
                error = "Nie udało się pobrać próbki ikony. Spróbuj ponownie.";
                return false;
            }
            _settings.AutoWaterSampleOffsetX = cursor.X - (clientRect.Left + (clientRect.Right - clientRect.Left) / 2);
            _settings.AutoWaterSampleOffsetFromBottom = clientRect.Bottom - cursor.Y;
            _settings.AutoWaterSamplePatchSize = AutoWaterPatchSize;
            _settings.AutoWaterCalibrationClientWidth = clientRect.Right - clientRect.Left;
            _settings.AutoWaterCalibrationClientHeight = clientRect.Bottom - clientRect.Top;
            _settings.AutoWaterCalibrationSlot = slot;
            _settings.AutoWaterEmptyBucketTemplateBase64 = Convert.ToBase64String(patch);
            _settings.AutoWaterFullBucketTemplateBase64 = string.Empty;
            _autoWaterTemplateRgb = patch;
            _autoWaterFullTemplateRgb = null;
            return true;
        }

        private static bool IsAutoWaterHotbarSample(RECT client, Drawing.Rectangle patch, int slot)
        {
            int width = client.Right - client.Left;
            int height = client.Bottom - client.Top;
            if (width <= 0 || height <= 0 || slot is < 1 or > 9) return false;
            int maxScale = 1;
            while (width / (maxScale + 1) >= 320 && height / (maxScale + 1) >= 240) maxScale++;
            // Minecraft 1.8 hotbar item: center - 88 + 20*index, bottom - 19,
            // 16x16 GUI pixels. Only its part ABOVE the chat input is usable.
            for (int scale = 1; scale <= maxScale; scale++)
            {
                int left = client.Left + width / 2 - 88 * scale + (slot - 1) * 20 * scale;
                int top = client.Bottom - 19 * scale;
                if (patch.Left >= left && patch.Right <= left + 16 * scale
                    && patch.Top >= top && patch.Bottom <= client.Bottom - 14 * scale)
                    return true;
            }
            return false;
        }

        private bool TryGetAutoWaterSampleRect(out Drawing.Rectangle patch, out string error, bool requireFullTemplate = true)
        {
            patch = Drawing.Rectangle.Empty;
            error = string.Empty;
            if (_autoWaterTemplateRgb == null || (requireFullTemplate && !HasCompleteAutoWaterCalibration))
            {
                error = "Wymagana nowa kalibracja: puste wiadro i wiadro z wodą, w tym samym slocie hotbara.";
                return false;
            }
            if (!IsAutoWaterTargetFocused())
            {
                error = "Okno Minecrafta nie jest aktywne.";
                return false;
            }
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT client))
            {
                error = "Nie mogę odczytać obszaru okna Minecrafta.";
                return false;
            }
            if (client.Right - client.Left != _settings.AutoWaterCalibrationClientWidth
                || client.Bottom - client.Top != _settings.AutoWaterCalibrationClientHeight
                || _settings.AutoWaterCalibrationSlot != GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9))
            {
                error = "Zmieniono rozmiar okna lub slot wody. Skalibruj oba wzorce ponownie.";
                return false;
            }
            long x = (long)client.Left + (client.Right - client.Left) / 2 + _settings.AutoWaterSampleOffsetX;
            long y = (long)client.Bottom - _settings.AutoWaterSampleOffsetFromBottom;
            int radius = AutoWaterPatchSize / 2;
            if (x - radius < client.Left || x + radius >= client.Right || y - radius < client.Top || y + radius >= client.Bottom)
            {
                error = "Punkt kalibracji wypada poza oknem gry. Skalibruj ponownie.";
                return false;
            }
            patch = new Drawing.Rectangle((int)x - radius, (int)y - radius, AutoWaterPatchSize, AutoWaterPatchSize);
            if (!IsAutoWaterHotbarSample(client, patch, _settings.AutoWaterCalibrationSlot))
            {
                error = "Stary punkt nie leży na hotbarze ponad czatem. Skalibruj puste wiadro oraz wodę ponownie.";
                return false;
            }
            return true;
        }

        private bool TryReadAutoWaterBucket(out AutoWaterBucketState state, out string error)
        {
            state = AutoWaterBucketState.Unknown;
            if (!TryGetAutoWaterSampleRect(out Drawing.Rectangle patch, out error)) return false;
            if (!TryCaptureAutoWaterPatch(patch, out byte[] current) || !IsAutoWaterTargetFocused())
            {
                error = "Nie udało się odczytać ikony wiadra z ekranu.";
                return false;
            }
            _autoWaterLastSimilarity = CalculateAutoWaterSimilarity(current, _autoWaterTemplateRgb!);
            _autoWaterLastFullSimilarity = CalculateAutoWaterSimilarity(current, _autoWaterFullTemplateRgb!);
            int threshold = GetAutoWaterSimilarityThreshold();
            if (_autoWaterLastSimilarity >= threshold && _autoWaterLastSimilarity - _autoWaterLastFullSimilarity >= AutoWaterRecognitionMargin)
                state = AutoWaterBucketState.Empty;
            else if (_autoWaterLastFullSimilarity >= threshold && _autoWaterLastFullSimilarity - _autoWaterLastSimilarity >= AutoWaterRecognitionMargin)
                state = AutoWaterBucketState.Water;
            return true;
        }

        private static double CalculateAutoWaterSimilarity(byte[] current, byte[] template)
        {
            if (current.Length == 0 || current.Length != template.Length) return 0;
            long difference = 0;
            for (int i = 0; i < current.Length; i++) difference += Math.Abs(current[i] - template[i]);
            return Math.Clamp(100.0 * (1.0 - difference / (current.Length * 255.0)), 0.0, 100.0);
        }

        private static bool TryCaptureAutoWaterPatch(Drawing.Rectangle screenRect, out byte[] rgb)
        {
            rgb = Array.Empty<byte>();
            try
            {
                using var bitmap = new Drawing.Bitmap(screenRect.Width, screenRect.Height, DrawingImaging.PixelFormat.Format24bppRgb);
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0, screenRect.Size, Drawing.CopyPixelOperation.SourceCopy);
                rgb = new byte[screenRect.Width * screenRect.Height * 3];
                int index = 0;
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Drawing.Color pixel = bitmap.GetPixel(x, y);
                        rgb[index++] = pixel.R;
                        rgb[index++] = pixel.G;
                        rgb[index++] = pixel.B;
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
            if (ChkAutoWaterEnabled.IsChecked != true) return false;
            if (!HasCompleteAutoWaterCalibration)
            {
                UpdateAutoWaterStatusLabel();
                UpdateStatusBar("AutoWater: wymagana kalibracja pustego wiadra i wody", "Orange");
                return false;
            }
            if (!IsAutoWaterTargetFocused() || IsInventoryCursorVisible())
            {
                SetAutoWaterStatus("Nie uruchomiono AutoWater: aktywuj zapisane okno Minecrafta i zamknij chat, EQ lub menu.", Brushes.OrangeRed);
                return false;
            }
            if (IsAutoArmorRunning)
            {
                SetAutoWaterStatus("Nie uruchomiono AutoWater: trwa podmiana zbroi.", Brushes.OrangeRed);
                return false;
            }
            if (!TryGetAutoWaterSampleRect(out _, out string error))
            {
                SetAutoWaterStatus(error, Brushes.OrangeRed);
                return false;
            }
            StopClickerRuntimesForExclusivePointerMacro();
            StopOtherExclusivePointerMacros();
            _testAutoFishingRuntimeEnabled = false;
            ResetTestAutoFishingRuntimeState(now);
            _autoWaterRuntimeWaterSlot = GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9);
            _autoWaterRuntimeReturnSlot = GetAutoWaterSelectedSlot(CbAutoWaterReturnSlot, 1);
            _autoWaterStage = AutoWaterStage.SelectWaterSlot;
            _autoWaterStartedAtUtc = now;
            _nextAutoWaterActionAtUtc = now;
            _nextAutoWaterScanAtUtc = now;
            _autoWaterTemplateMatchFrames = 0;
            _autoWaterPickupAttempts = 0;
            _autoWaterLastSimilarity = 0;
            _autoWaterLastFullSimilarity = 0;
            SetAutoWaterStatus("AutoWater uruchomiony: wybieranie slotu z wodą…", Brushes.DeepSkyBlue);
            UpdateStatusBar("AutoWater uruchomiony", "Orange");
            RefreshOverlayHud(now);
            return true;
        }

        private bool TrySelectAutoWaterSlot(int slot)
        {
            if (!IsAutoWaterTargetFocused() || IsInventoryCursorVisible()) return false;
            int key = GetSlotVirtualKey(slot);
            bool down = NativeInput.SendKey(key, down: true);
            bool up = NativeInput.SendKey(key, down: false);
            if (!up) NativeInput.SendKey(key, down: false); // Best-effort key release after a failed UP.
            return down && up;
        }

        private bool TryClickAutoWater()
        {
            if (!IsAutoWaterTargetFocused() || IsInventoryCursorVisible()
                || !NativeInput.SendMouseClick(leftButton: false, holdPulseMode: false))
            {
                CancelAutoWater("AutoWater przerwany: nie udało się bezpiecznie wysłać PPM.", Brushes.OrangeRed, restoreSlot: true);
                return false;
            }
            return true;
        }

        private void RunAutoWaterTick(DateTime now)
        {
            if (_autoWaterStage == AutoWaterStage.None) return;
            if (!IsAutoWaterTargetFocused())
            {
                CancelAutoWater("AutoWater przerwany po utracie fokusu.", Brushes.OrangeRed, restoreSlot: false);
                return;
            }
            if (IsInventoryCursorVisible())
            {
                CancelAutoWater("AutoWater przerwany: otwarty chat, EQ lub menu.", Brushes.OrangeRed, restoreSlot: false);
                return;
            }
            if ((now - _autoWaterStartedAtUtc).TotalMilliseconds >= AutoWaterMaxRuntimeMs)
            {
                CancelAutoWater("AutoWater przerwany: przekroczono 10 sekund bez zakończenia.", Brushes.OrangeRed, restoreSlot: true);
                return;
            }
            switch (_autoWaterStage)
            {
                case AutoWaterStage.SelectWaterSlot:
                    if (!TrySelectAutoWaterSlot(_autoWaterRuntimeWaterSlot))
                    {
                        CancelAutoWater("AutoWater przerwany: nie udało się wybrać slotu wody.", Brushes.OrangeRed, restoreSlot: false);
                        return;
                    }
                    _autoWaterStage = AutoWaterStage.ValidateWaterBucket;
                    _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterSlotSettleMs);
                    _autoWaterValidationDeadlineUtc = now.AddMilliseconds(AutoWaterSlotValidationMs);
                    SetAutoWaterStatus("Wybrano slot. Sprawdzam wzorzec WIADRA Z WODĄ…", Brushes.DeepSkyBlue);
                    return;

                case AutoWaterStage.ValidateWaterBucket:
                    if (now < _nextAutoWaterActionAtUtc) return;
                    if (!TryReadAutoWaterBucket(out AutoWaterBucketState initial, out string validationError))
                    {
                        CancelAutoWater(validationError, Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (initial == AutoWaterBucketState.Water)
                    {
                        _autoWaterStage = AutoWaterStage.PlaceWater;
                        _nextAutoWaterActionAtUtc = now;
                        _nextAutoWaterScanAtUtc = now;
                        SetAutoWaterStatus("Rozpoznano wodę. Próby PPM ze sprawdzaniem ikony PRZED kliknięciem…", Brushes.DeepSkyBlue);
                    }
                    else if (initial == AutoWaterBucketState.Empty || now >= _autoWaterValidationDeadlineUtc)
                        CancelAutoWater(initial == AutoWaterBucketState.Empty
                            ? "Wybrany slot zawiera puste wiadro. Włóż wiadro z wodą."
                            : "Nie rozpoznano wiadra Z WODĄ w wybranym slocie. Nie wysłano PPM.",
                            Brushes.OrangeRed, restoreSlot: true);
                    else
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    return;

                case AutoWaterStage.PlaceWater:
                    // Always read BEFORE a due click, even if the regular scan is
                    // not due. The first EMPTY observation freezes placement PPM.
                    if (now < _nextAutoWaterScanAtUtc && now < _nextAutoWaterActionAtUtc) return;
                    _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    if (!TryReadAutoWaterBucket(out AutoWaterBucketState placement, out string placementError))
                    {
                        CancelAutoWater(placementError, Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (placement == AutoWaterBucketState.Empty)
                    {
                        _autoWaterStage = AutoWaterStage.ConfirmPlacement;
                        _autoWaterTemplateMatchFrames = 1;
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(GetAutoWaterPickupDelayMs());
                        SetAutoWaterStatus("Wykryto puste wiadro. Wstrzymano PPM — potwierdzanie postawienia wody…", Brushes.MediumSpringGreen);
                        return;
                    }
                    if (placement == AutoWaterBucketState.Unknown)
                    {
                        _nextAutoWaterActionAtUtc = _nextAutoWaterScanAtUtc;
                        return; // An unfamiliar image is NOT a full water bucket.
                    }
                    if (now >= _nextAutoWaterActionAtUtc && TryClickAutoWater())
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(GetAutoWaterPlaceIntervalMs());
                    return;

                case AutoWaterStage.ConfirmPlacement:
                    if (now < _nextAutoWaterScanAtUtc) return;
                    _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    if (!TryReadAutoWaterBucket(out AutoWaterBucketState confirmation, out string confirmationError))
                    {
                        CancelAutoWater(confirmationError, Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (confirmation == AutoWaterBucketState.Water)
                    {
                        CancelAutoWater("Nie potwierdzono stabilnego pustego wiadra. Wstrzymano dalsze kliknięcia.", Brushes.Orange, restoreSlot: true);
                        return;
                    }
                    _autoWaterTemplateMatchFrames = confirmation == AutoWaterBucketState.Empty ? _autoWaterTemplateMatchFrames + 1 : 0;
                    if (_autoWaterTemplateMatchFrames >= 2)
                    {
                        _autoWaterStage = AutoWaterStage.WaitBeforePickup;
                        SetAutoWaterStatus("Puste wiadro potwierdzone. Czekam do ustawionego czasu zebrania — bez PPM.", Brushes.MediumSpringGreen);
                    }
                    return;

                case AutoWaterStage.WaitBeforePickup:
                    if (now < _nextAutoWaterActionAtUtc) return;
                    if (!TryReadAutoWaterBucket(out AutoWaterBucketState beforePickup, out string beforePickupError))
                    {
                        CancelAutoWater(beforePickupError, Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (beforePickup == AutoWaterBucketState.Unknown)
                    {
                        CancelAutoWater("Nie rozpoznano wiadra przed zebraniem wody. Nie wysłano PPM.", Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    _autoWaterTemplateMatchFrames = beforePickup == AutoWaterBucketState.Water ? 1 : 0;
                    if (beforePickup == AutoWaterBucketState.Empty)
                    {
                        if (!TryClickAutoWater()) return;
                        _autoWaterPickupAttempts = 1;
                        _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterPickupRetryDelayMs);
                        _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterPickupVerificationDelayMs);
                    }
                    else
                    {
                        // Already filled (e.g. manually collected): never place
                        // the water again just to satisfy the pickup sequence.
                        _nextAutoWaterActionAtUtc = DateTime.MaxValue;
                        _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    }
                    _autoWaterStage = AutoWaterStage.VerifyPickup;
                    SetAutoWaterStatus("Potwierdzanie ikony WIADRA Z WODĄ…", Brushes.DeepSkyBlue);
                    return;

                case AutoWaterStage.VerifyPickup:
                    if (now < _nextAutoWaterScanAtUtc) return;
                    _nextAutoWaterScanAtUtc = now.AddMilliseconds(AutoWaterScanIntervalMs);
                    if (!TryReadAutoWaterBucket(out AutoWaterBucketState pickup, out string pickupError))
                    {
                        CancelAutoWater(pickupError, Brushes.OrangeRed, restoreSlot: true);
                        return;
                    }
                    if (pickup == AutoWaterBucketState.Water)
                    {
                        if (++_autoWaterTemplateMatchFrames >= 2) CompleteAutoWater();
                        return;
                    }
                    if (_autoWaterTemplateMatchFrames > 0)
                    {
                        CancelAutoWater("Ikona wiadra po zebraniu jest niestabilna. Nie ponawiam PPM.", Brushes.Orange, restoreSlot: true);
                        return;
                    }
                    if (pickup == AutoWaterBucketState.Unknown || now < _nextAutoWaterActionAtUtc) return;
                    if (_autoWaterPickupAttempts >= AutoWaterMaxPickupAttempts)
                    {
                        CancelAutoWater("Nie potwierdzono zebrania wody po 6 próbach. Sprawdź wiadro.", Brushes.Orange, restoreSlot: true);
                        return;
                    }
                    if (!TryClickAutoWater()) return;
                    _autoWaterPickupAttempts++;
                    _nextAutoWaterActionAtUtc = now.AddMilliseconds(AutoWaterPickupRetryDelayMs);
                    SetAutoWaterStatus($"Próba zebrania {_autoWaterPickupAttempts}/{AutoWaterMaxPickupAttempts} — oczekiwanie na wzorzec wody…", Brushes.Orange);
                    return;
            }
        }

        private void CompleteAutoWater()
        {
            bool restored = TrySelectAutoWaterSlot(_autoWaterRuntimeReturnSlot);
            ResetAutoWaterRuntimeState();
            SetAutoWaterStatus(restored
                ? "Gotowe. Potwierdzono ikonę wiadra Z WODĄ i przywrócono wybrany slot."
                : "Potwierdzono ikonę wiadra Z WODĄ, ale nie udało się przywrócić slotu.",
                restored ? Brushes.MediumSpringGreen : Brushes.Orange);
            UpdateStatusBar(restored ? "AutoWater zakończony" : "AutoWater: sprawdź slot powrotu", restored ? "Green" : "Orange");
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void CancelAutoWater(string message, Brush brush, bool restoreSlot, bool updateStatus = true)
        {
            if (_autoWaterStage != AutoWaterStage.None && restoreSlot)
                TrySelectAutoWaterSlot(_autoWaterRuntimeReturnSlot);
            ResetAutoWaterRuntimeState();
            if (updateStatus && !string.IsNullOrWhiteSpace(message))
            {
                SetAutoWaterStatus(message, brush);
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
            _autoWaterValidationDeadlineUtc = DateTime.MinValue;
            _autoWaterTemplateMatchFrames = 0;
            _autoWaterPickupAttempts = 0;
            _autoWaterRuntimeWaterSlot = 0;
            _autoWaterRuntimeReturnSlot = 0;
        }

        private int GetAutoWaterPlaceIntervalMs() => Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPlaceIntervalMs.Text), 25, 500);
        private int GetAutoWaterPickupDelayMs() => Math.Clamp(ParseNonNegativeInt(TxtAutoWaterPickupDelayMs.Text), 100, 1500);
        private int GetAutoWaterSimilarityThreshold() => Math.Clamp((int)Math.Round(SlAutoWaterSimilarity.Value), 70, 99);

        private void SetAutoWaterStatus(string message, Brush brush)
        {
            if (TxtAutoWaterStatus == null) return;
            TxtAutoWaterStatus.Text = message;
            TxtAutoWaterStatus.Foreground = brush;
        }

        private void UpdateAutoWaterStatusLabel()
        {
            string message = HasCompleteAutoWaterCalibration
                ? "Oba wzorce gotowe. Najpierw przetestuj rozpoznawanie pustego wiadra i wiadra z wodą."
                : _autoWaterTemplateRgb != null && _settings.AutoWaterCalibrationClientWidth > 0
                    && _settings.AutoWaterCalibrationSlot == GetAutoWaterSelectedSlot(CbAutoWaterWaterSlot, 9)
                    ? "Puste wiadro zapisane. Brakuje wzorca WIADRA Z WODĄ — wybierz „Kalibruj wodę”."
                    : "Wymagana nowa kalibracja HOTBARA: puste wiadro, następnie wiadro z wodą. Stary pojedynczy wzorzec nie wystarcza.";
            SetAutoWaterStatus(message, HasCompleteAutoWaterCalibration ? Brushes.MediumSpringGreen : Brushes.Orange);
        }

        private OverlayHudEntry BuildAutoWaterOverlayEntry(DateTime now)
        {
            string state = _autoWaterCalibrationPending
                ? (_autoWaterCalibratingFullBucket ? "KALIBRACJA WODY" : "KALIBRACJA PUSTEGO WIADRA")
                : _autoWaterRecognitionTestPending ? "TEST ROZPOZNAWANIA"
                : _autoWaterStage switch
                {
                    AutoWaterStage.SelectWaterSlot => "WYBÓR WODY",
                    AutoWaterStage.ValidateWaterBucket => "KONTROLA WIADRA Z WODĄ",
                    AutoWaterStage.PlaceWater => "STAWIANIE WODY",
                    AutoWaterStage.ConfirmPlacement => "POTWIERDZANIE — PPM WSTRZYMANY",
                    AutoWaterStage.WaitBeforePickup => "OCZEKIWANIE NA ZEBRANIE",
                    AutoWaterStage.VerifyPickup => "POTWIERDZANIE ZEBRANIA",
                    _ => "GOTOWY"
                };
            string detail = _autoWaterCalibrationPending
                ? "Aktywuj Minecraft, otwórz CZAT (nie EQ) i użyj lewego Ctrl."
                : _autoWaterRecognitionTestPending
                    ? (!IsAutoWaterTargetFocused() ? "Oczekiwanie na Minecraft."
                        : _autoWaterRecognitionTestAtUtc == DateTime.MinValue ? "Zamknij GUI — pomiar po 2 sekundach."
                        : $"Pomiar za {Math.Max(0, (int)Math.Ceiling((_autoWaterRecognitionTestAtUtc - now).TotalSeconds))} s.")
                    : _autoWaterStage == AutoWaterStage.WaitBeforePickup || _autoWaterStage == AutoWaterStage.ConfirmPlacement
                        ? $"Bez PPM • zebranie za {Math.Max(0, (int)Math.Ceiling((_nextAutoWaterActionAtUtc - now).TotalMilliseconds))} ms"
                        : $"Puste {_autoWaterLastSimilarity:0.0}% • woda {_autoWaterLastFullSimilarity:0.0}% • próby zebrania {_autoWaterPickupAttempts}/{AutoWaterMaxPickupAttempts}";
            string cancelHint = _autoWaterCalibrationPending || _autoWaterRecognitionTestPending
                ? "Wyłącz moduł, aby anulować kalibrację/test bez usuwania wzorców."
                : "Bind ponownie = anulowanie";
            return new OverlayHudEntry("AUTOWATER", $"Teraz: {state}\n{detail}\n{cancelHint}",
                _autoWaterCalibrationPending || _autoWaterRecognitionTestPending ? OverlayHudTone.Warning : OverlayHudTone.Active,
                Emphasize: true);
        }
    }
}
