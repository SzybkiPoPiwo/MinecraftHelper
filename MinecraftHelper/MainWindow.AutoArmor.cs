using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MinecraftHelper.Models;
using MinecraftHelper.Services;
using Drawing = System.Drawing;

namespace MinecraftHelper
{
    public partial class MainWindow
    {
        private const int AutoArmorPieceCount = 4;
        private const int AutoArmorOpenInventoryDelayMs = 320;
        private const int AutoArmorCursorSettleMs = 15;
        private const int AutoArmorCloseInventoryDelayMs = 160;
        private const int AutoArmorMaximumRuntimeMs = 8000;

        private static readonly string[] AutoArmorPieceNames =
        {
            "Hełm",
            "Napierśnik",
            "Spodnie",
            "Buty"
        };

        private enum AutoArmorStage
        {
            None,
            OpenInventory,
            PrepareSwap,
            MoveToClickTarget,
            ClickTarget,
            CloseInventory,
            WaitForClose
        }

        private readonly record struct AutoArmorClickTarget(Drawing.Point Point, string Label);

        private AutoArmorStage _autoArmorStage = AutoArmorStage.None;
        private readonly List<AutoArmorClickTarget> _autoArmorClickPlan = new();
        private bool _autoArmorBindWasDown;
        private bool _autoArmorInventoryOpened;
        private bool _autoArmorCancelPending;
        private bool _autoArmorCalibrationPending;
        private bool _autoArmorCalibrationEquippedSlot;
        private bool _autoArmorCalibrationControlWasDown;
        private int _autoArmorCalibrationPieceIndex = -1;
        private int _autoArmorClickIndex;
        private DateTime _autoArmorStartedAtUtc = DateTime.MinValue;
        private DateTime _nextAutoArmorActionAtUtc = DateTime.MinValue;

        private bool IsAutoArmorRunning => _autoArmorStage != AutoArmorStage.None;

        private void NormalizeAutoArmorSettings()
        {
            _settings.AutoArmorBind ??= string.Empty;
            _settings.AutoArmorClickDelayMs = Math.Clamp(
                _settings.AutoArmorClickDelayMs <= 0 ? 45 : _settings.AutoArmorClickDelayMs,
                25,
                200);
            _settings.AutoArmorEquippedSlots = NormalizeAutoArmorPointList(_settings.AutoArmorEquippedSlots);
            _settings.AutoArmorInventorySlots = NormalizeAutoArmorPointList(_settings.AutoArmorInventorySlots);
        }

        private static List<RelativeScreenPointSetting> NormalizeAutoArmorPointList(
            List<RelativeScreenPointSetting>? points)
        {
            var normalized = points?
                .Take(AutoArmorPieceCount)
                .Select(point => point ?? new RelativeScreenPointSetting())
                .ToList()
                ?? new List<RelativeScreenPointSetting>();
            while (normalized.Count < AutoArmorPieceCount)
                normalized.Add(new RelativeScreenPointSetting());
            return normalized;
        }

        private void LoadAutoArmorToUi()
        {
            ResetAutoArmorRuntimeState();
            _autoArmorCalibrationPending = false;
            _autoArmorCalibrationPieceIndex = -1;
            ChkAutoArmorEnabled.IsChecked = _settings.AutoArmorEnabled;
            TxtAutoArmorBind.Text = _settings.AutoArmorBind;
            TxtAutoArmorClickDelayMs.Text = _settings.AutoArmorClickDelayMs.ToString(CultureInfo.InvariantCulture);
            RefreshAutoArmorCalibrationUi();
            UpdateAutoArmorReadyStatus();
        }

        private void ReadAutoArmorFromUi()
        {
            _settings.AutoArmorEnabled = ChkAutoArmorEnabled.IsChecked == true;
            _settings.AutoArmorBind = TxtAutoArmorBind.Text.Trim();
            _settings.AutoArmorClickDelayMs = GetAutoArmorClickDelayMs();
        }

        private int GetAutoArmorClickDelayMs()
        {
            if (TxtAutoArmorClickDelayMs != null
                && int.TryParse(
                    TxtAutoArmorClickDelayMs.Text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int delay))
            {
                return Math.Clamp(delay, 25, 200);
            }

            return Math.Clamp(_settings.AutoArmorClickDelayMs, 25, 200);
        }

        private void ChkAutoArmorEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateAutoArmorEnabledState();
            if (ChkAutoArmorEnabled.IsChecked != true)
            {
                _autoArmorCalibrationPending = false;
                _autoArmorCalibrationPieceIndex = -1;
                if (IsAutoArmorRunning)
                    RequestCancelAutoArmor("Auto zbroja wyłączona — kończę bezpiecznie podmianę bieżącego elementu.");
                RefreshAutoArmorCalibrationUi();
            }
            MarkDirty();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void UpdateAutoArmorEnabledState()
        {
            bool enabled = ChkAutoArmorEnabled?.IsChecked == true;
            if (PanelAutoArmorExpandableContent != null)
                SetExpandableSectionState(PanelAutoArmorExpandableContent, enabled);
            if (PanelAutoArmorContent != null)
                PanelAutoArmorContent.IsEnabled = enabled;
        }

        private void AutoArmorConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoadingUi)
                MarkDirty();
        }

        private void BtnAutoArmorBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.AutoArmor);
        }

        private void BtnAutoArmorCalibrationSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button
                || !TryParseAutoArmorCalibrationTag(button.Tag?.ToString(), out bool equippedSlot, out int pieceIndex))
            {
                return;
            }
            if (IsAutoArmorRunning)
            {
                SetAutoArmorStatus("Najpierw poczekaj na zakończenie podmiany zestawu.", Brushes.OrangeRed);
                return;
            }

            _autoArmorCalibrationEquippedSlot = equippedSlot;
            _autoArmorCalibrationPieceIndex = pieceIndex;
            _autoArmorCalibrationPending = true;
            _autoArmorCalibrationControlWasDown = IsVirtualKeyDown(VK_LCONTROL);
            string location = equippedSlot ? "założony set" : "drugi set w EQ";
            SetAutoArmorStatus(
                $"Kalibracja: {AutoArmorPieceNames[pieceIndex]} ({location}). Otwórz EQ, najedź na właściwy slot i naciśnij lewy Ctrl.",
                Brushes.Orange);
            RefreshAutoArmorCalibrationUi();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private static bool TryParseAutoArmorCalibrationTag(
            string? tag,
            out bool equippedSlot,
            out int pieceIndex)
        {
            equippedSlot = false;
            pieceIndex = -1;
            string[] parts = (tag ?? string.Empty).Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out pieceIndex)
                || pieceIndex < 0
                || pieceIndex >= AutoArmorPieceCount)
            {
                return false;
            }

            equippedSlot = string.Equals(parts[0], "Equipped", StringComparison.OrdinalIgnoreCase);
            return equippedSlot || string.Equals(parts[0], "Inventory", StringComparison.OrdinalIgnoreCase);
        }

        private void BtnAutoArmorResetCalibration_Click(object sender, RoutedEventArgs e)
        {
            if (IsAutoArmorRunning)
            {
                SetAutoArmorStatus("Nie można resetować kalibracji podczas podmiany zestawu.", Brushes.OrangeRed);
                return;
            }

            _settings.AutoArmorEquippedSlots = NormalizeAutoArmorPointList(null);
            _settings.AutoArmorInventorySlots = NormalizeAutoArmorPointList(null);
            _autoArmorCalibrationPending = false;
            _autoArmorCalibrationPieceIndex = -1;
            RefreshAutoArmorCalibrationUi();
            SetAutoArmorStatus("Usunięto kalibrację. Ustaw ponownie wszystkie osiem pól.", Brushes.Orange);
            MarkDirty();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void BtnAutoArmorCancelCalibration_Click(object sender, RoutedEventArgs e)
        {
            _autoArmorCalibrationPending = false;
            _autoArmorCalibrationPieceIndex = -1;
            RefreshAutoArmorCalibrationUi();
            UpdateAutoArmorReadyStatus();
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private bool TryHandleAutoArmorCalibration(DateTime now)
        {
            if (!_autoArmorCalibrationPending)
                return false;

            _autoClickScheduler.Stop();
            SetAutoLeftDabHold(false);
            ReleaseHoldRightInjectedButton();

            bool controlDown = IsVirtualKeyDown(VK_LCONTROL);
            bool pressed = controlDown && !_autoArmorCalibrationControlWasDown;
            _autoArmorCalibrationControlWasDown = controlDown;
            if (!pressed)
                return true;

            if (!IsInventoryCursorVisible())
            {
                SetAutoArmorStatus("Nie zapisano pola: otwórz ekwipunek Minecrafta i spróbuj ponownie.", Brushes.OrangeRed);
                return true;
            }
            if (!GetCursorPos(out POINT cursor)
                || !TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
            {
                SetAutoArmorStatus("Nie udało się odczytać kursora lub obszaru okna gry.", Brushes.OrangeRed);
                return true;
            }
            if (cursor.X < clientRect.Left || cursor.X >= clientRect.Right
                || cursor.Y < clientRect.Top || cursor.Y >= clientRect.Bottom)
            {
                SetAutoArmorStatus("Kursor musi znajdować się nad slotem w oknie Minecrafta.", Brushes.OrangeRed);
                return true;
            }

            int centerX = clientRect.Left + (clientRect.Right - clientRect.Left) / 2;
            int centerY = clientRect.Top + (clientRect.Bottom - clientRect.Top) / 2;
            RelativeScreenPointSetting point = GetAutoArmorCalibrationPoint(
                _autoArmorCalibrationEquippedSlot,
                _autoArmorCalibrationPieceIndex);
            point.Configured = true;
            point.OffsetX = cursor.X - centerX;
            point.OffsetY = cursor.Y - centerY;

            string location = _autoArmorCalibrationEquippedSlot ? "założony set" : "drugi set w EQ";
            string pieceName = AutoArmorPieceNames[_autoArmorCalibrationPieceIndex];
            _autoArmorCalibrationPending = false;
            _autoArmorCalibrationPieceIndex = -1;
            _suppressBindToggleUntilRelease = true;
            RefreshAutoArmorCalibrationUi();
            SetAutoArmorStatus($"Zapisano: {pieceName} ({location}).", Brushes.MediumSpringGreen);
            MarkDirty();
            RefreshOverlayHud(now);
            return true;
        }

        private RelativeScreenPointSetting GetAutoArmorCalibrationPoint(bool equippedSlot, int pieceIndex)
        {
            List<RelativeScreenPointSetting> points = equippedSlot
                ? _settings.AutoArmorEquippedSlots
                : _settings.AutoArmorInventorySlots;
            return points[Math.Clamp(pieceIndex, 0, AutoArmorPieceCount - 1)];
        }

        private void RefreshAutoArmorCalibrationUi()
        {
            for (int pieceIndex = 0; pieceIndex < AutoArmorPieceCount; pieceIndex++)
            {
                UpdateAutoArmorCalibrationButton(
                    GetAutoArmorCalibrationButton(equippedSlot: true, pieceIndex),
                    equippedSlot: true,
                    pieceIndex);
                UpdateAutoArmorCalibrationButton(
                    GetAutoArmorCalibrationButton(equippedSlot: false, pieceIndex),
                    equippedSlot: false,
                    pieceIndex);
            }
        }

        private Button? GetAutoArmorCalibrationButton(bool equippedSlot, int pieceIndex)
        {
            return (equippedSlot, pieceIndex) switch
            {
                (true, 0) => BtnAutoArmorEquippedHelmet,
                (true, 1) => BtnAutoArmorEquippedChestplate,
                (true, 2) => BtnAutoArmorEquippedLeggings,
                (true, 3) => BtnAutoArmorEquippedBoots,
                (false, 0) => BtnAutoArmorInventoryHelmet,
                (false, 1) => BtnAutoArmorInventoryChestplate,
                (false, 2) => BtnAutoArmorInventoryLeggings,
                (false, 3) => BtnAutoArmorInventoryBoots,
                _ => null
            };
        }

        private void UpdateAutoArmorCalibrationButton(Button? button, bool equippedSlot, int pieceIndex)
        {
            if (button == null)
                return;

            RelativeScreenPointSetting point = GetAutoArmorCalibrationPoint(equippedSlot, pieceIndex);
            bool armed = _autoArmorCalibrationPending
                && _autoArmorCalibrationEquippedSlot == equippedSlot
                && _autoArmorCalibrationPieceIndex == pieceIndex;
            button.Content = armed
                ? $"{AutoArmorPieceNames[pieceIndex]} — czekam na Ctrl"
                : point.Configured
                    ? $"{AutoArmorPieceNames[pieceIndex]} — ustawiono ✓"
                    : $"{AutoArmorPieceNames[pieceIndex]} — ustaw Ctrl";
            button.BorderBrush = armed
                ? Brushes.Orange
                : point.Configured
                    ? Brushes.MediumSpringGreen
                    : new SolidColorBrush(Color.FromRgb(75, 98, 131));
            button.BorderThickness = new Thickness(armed ? 2 : 1);
        }

        private bool HasCompleteAutoArmorCalibration()
        {
            return _settings.AutoArmorEquippedSlots.Count == AutoArmorPieceCount
                && _settings.AutoArmorInventorySlots.Count == AutoArmorPieceCount
                && _settings.AutoArmorEquippedSlots.All(point => point.Configured)
                && _settings.AutoArmorInventorySlots.All(point => point.Configured);
        }

        private void UpdateAutoArmorReadyStatus()
        {
            int configured = _settings.AutoArmorEquippedSlots.Count(point => point.Configured)
                + _settings.AutoArmorInventorySlots.Count(point => point.Configured);
            if (configured == AutoArmorPieceCount * 2)
            {
                SetAutoArmorStatus("Kalibracja gotowa: 8/8 pól. Podmiana uruchomi się bindem.", Brushes.MediumSpringGreen);
            }
            else
            {
                SetAutoArmorStatus(
                    $"Kalibracja: {configured}/8 pól. Ustaw brakujące miejsca za pomocą lewego Ctrl.",
                    Brushes.Orange);
            }
        }

        private bool TryStartAutoArmor(DateTime now)
        {
            if (IsAutoArmorRunning)
                return false;
            if (_autoArmorCalibrationPending)
            {
                SetAutoArmorStatus("Najpierw zakończ albo anuluj kalibrację pola.", Brushes.OrangeRed);
                return false;
            }
            if (!HasCompleteAutoArmorCalibration())
            {
                SetAutoArmorStatus("Nie uruchomiono: skalibruj wszystkie osiem pól.", Brushes.OrangeRed);
                UpdateStatusBar("Auto zbroja: brakuje pełnej kalibracji 8/8", "Red");
                return false;
            }
            if (IsInventoryCursorVisible())
            {
                SetAutoArmorStatus("Nie uruchomiono: zamknij chat, ekwipunek lub inne GUI.", Brushes.OrangeRed);
                UpdateStatusBar("Auto zbroja: uruchamiaj bindem z zamkniętym GUI", "Red");
                return false;
            }
            if (_inventoryCleanupStage != InventoryCleanupStage.None
                || _autoReconnectStage != AutoReconnectStage.None
                || _autoWaterStage != AutoWaterStage.None
                || _testFastUpExitRuntimeEnabled
                || _testAutoFishingRuntimeEnabled
                || _kopacz533RuntimeEnabled
                || _kopacz633RuntimeEnabled)
            {
                SetAutoArmorStatus("Nie uruchomiono: inna sekwencja automatyczna jest aktywna.", Brushes.OrangeRed);
                UpdateStatusBar("Auto zbroja: najpierw zatrzymaj inną aktywną sekwencję", "Red");
                return false;
            }

            _autoClickScheduler.Stop();
            SetAutoLeftDabHold(false);
            ReleaseHoldRightInjectedButton();
            _autoArmorClickPlan.Clear();
            _autoArmorClickIndex = 0;
            _autoArmorCancelPending = false;
            _autoArmorInventoryOpened = false;
            _autoArmorStartedAtUtc = now;
            _autoArmorStage = AutoArmorStage.OpenInventory;
            _nextAutoArmorActionAtUtc = now;
            SetAutoArmorStatus("Uruchomiono: otwieranie ekwipunku…", Brushes.DeepSkyBlue);
            UpdateStatusBar("Auto zbroja: rozpoczęto podmianę zestawu", "Orange");
            RefreshOverlayHud(now);
            return true;
        }

        private void RequestCancelAutoArmor(string message)
        {
            if (!IsAutoArmorRunning)
                return;

            _autoArmorCancelPending = true;
            SetAutoArmorStatus(message, Brushes.Orange);
            UpdateStatusBar("Auto zbroja: bezpieczne anulowanie podmiany", "Orange");
        }

        private bool CanCancelAutoArmorImmediately()
        {
            if (_autoArmorStage is AutoArmorStage.OpenInventory
                or AutoArmorStage.PrepareSwap
                or AutoArmorStage.CloseInventory
                or AutoArmorStage.WaitForClose)
            {
                return true;
            }

            // A complete swap of one element uses three clicks. Only after all
            // three clicks is the cursor empty and both pieces stored safely.
            return _autoArmorClickIndex % 3 == 0;
        }

        private void RunAutoArmorTick(DateTime now)
        {
            if (!IsAutoArmorRunning)
                return;
            if (_autoArmorCancelPending && CanCancelAutoArmorImmediately())
            {
                CancelAutoArmor("Auto zbroja anulowana po bezpiecznym zakończeniu podmiany elementu.", Brushes.Orange, closeInventory: true);
                return;
            }
            if (!_autoArmorCancelPending
                && (now - _autoArmorStartedAtUtc).TotalMilliseconds >= AutoArmorMaximumRuntimeMs)
            {
                if (CanCancelAutoArmorImmediately())
                {
                    CancelAutoArmor("Auto zbroja przerwana: przekroczono limit czasu.", Brushes.OrangeRed, closeInventory: true);
                    return;
                }

                _autoArmorCancelPending = true;
                SetAutoArmorStatus("Przekroczono limit czasu — kończę podmianę trzymanego elementu.", Brushes.OrangeRed);
            }
            if (now < _nextAutoArmorActionAtUtc)
                return;

            switch (_autoArmorStage)
            {
                case AutoArmorStage.OpenInventory:
                    SendKeyTap(VK_E);
                    _autoArmorInventoryOpened = true;
                    _autoArmorStage = AutoArmorStage.PrepareSwap;
                    _nextAutoArmorActionAtUtc = now.AddMilliseconds(AutoArmorOpenInventoryDelayMs);
                    SetAutoArmorStatus("Otwieranie EQ i przygotowanie podmiany…", Brushes.DeepSkyBlue);
                    return;

                case AutoArmorStage.PrepareSwap:
                    if (!IsInventoryCursorVisible())
                    {
                        CancelAutoArmor("Nie wykryto otwartego ekwipunku.", Brushes.OrangeRed, closeInventory: false);
                        return;
                    }
                    if (!TryBuildAutoArmorClickPlan(out string planError))
                    {
                        CancelAutoArmor(planError, Brushes.OrangeRed, closeInventory: true);
                        return;
                    }

                    _autoArmorClickIndex = 0;
                    _autoArmorStage = AutoArmorStage.MoveToClickTarget;
                    _nextAutoArmorActionAtUtc = now;
                    SetAutoArmorStatus("Rozpoczynam podmianę: hełm 0/4…", Brushes.DeepSkyBlue);
                    return;

                case AutoArmorStage.MoveToClickTarget:
                    if (_autoArmorClickIndex >= _autoArmorClickPlan.Count)
                    {
                        _autoArmorStage = AutoArmorStage.CloseInventory;
                        _nextAutoArmorActionAtUtc = now.AddMilliseconds(GetAutoArmorClickDelayMs());
                        return;
                    }

                    Drawing.Point point = _autoArmorClickPlan[_autoArmorClickIndex].Point;
                    if (!NativeInput.SetCursorPosition(point.X, point.Y))
                    {
                        CancelAutoArmor("Auto zbroja przerwana: nie udało się ustawić kursora.", Brushes.OrangeRed, closeInventory: true);
                        return;
                    }

                    _autoArmorStage = AutoArmorStage.ClickTarget;
                    _nextAutoArmorActionAtUtc = now.AddMilliseconds(AutoArmorCursorSettleMs);
                    return;

                case AutoArmorStage.ClickTarget:
                    SendMouseClick(leftButton: true, holdPulseMode: false);
                    _autoArmorClickIndex++;
                    _autoArmorStage = AutoArmorStage.MoveToClickTarget;
                    _nextAutoArmorActionAtUtc = now.AddMilliseconds(GetAutoArmorClickDelayMs());
                    UpdateAutoArmorProgressStatus();
                    return;

                case AutoArmorStage.CloseInventory:
                    SendKeyTap(VK_E);
                    _autoArmorInventoryOpened = false;
                    _autoArmorStage = AutoArmorStage.WaitForClose;
                    _nextAutoArmorActionAtUtc = now.AddMilliseconds(AutoArmorCloseInventoryDelayMs);
                    SetAutoArmorStatus("Podmiana zakończona. Zamykanie ekwipunku…", Brushes.DeepSkyBlue);
                    return;

                case AutoArmorStage.WaitForClose:
                    CompleteAutoArmor(now);
                    return;
            }
        }

        private bool TryBuildAutoArmorClickPlan(out string error)
        {
            error = string.Empty;
            _autoArmorClickPlan.Clear();
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
            {
                error = "Nie udało się odczytać obszaru okna Minecrafta.";
                return false;
            }

            int centerX = clientRect.Left + (clientRect.Right - clientRect.Left) / 2;
            int centerY = clientRect.Top + (clientRect.Bottom - clientRect.Top) / 2;
            for (int pieceIndex = 0; pieceIndex < AutoArmorPieceCount; pieceIndex++)
            {
                RelativeScreenPointSetting equipped = _settings.AutoArmorEquippedSlots[pieceIndex];
                RelativeScreenPointSetting inventory = _settings.AutoArmorInventorySlots[pieceIndex];
                if (!equipped.Configured || !inventory.Configured)
                {
                    error = $"Brak pełnej kalibracji dla: {AutoArmorPieceNames[pieceIndex]}.";
                    return false;
                }

                Drawing.Point equippedPoint = new(centerX + equipped.OffsetX, centerY + equipped.OffsetY);
                Drawing.Point inventoryPoint = new(centerX + inventory.OffsetX, centerY + inventory.OffsetY);
                if (!IsAutoArmorPointInsideClient(equippedPoint, clientRect)
                    || !IsAutoArmorPointInsideClient(inventoryPoint, clientRect))
                {
                    error = "Kalibracja nie pasuje do bieżącego rozmiaru okna. Ustaw osiem pól ponownie.";
                    return false;
                }

                string pieceName = AutoArmorPieceNames[pieceIndex].ToLowerInvariant();
                _autoArmorClickPlan.Add(new AutoArmorClickTarget(equippedPoint, $"podniesienie: {pieceName} założony"));
                _autoArmorClickPlan.Add(new AutoArmorClickTarget(inventoryPoint, $"zamiana: {pieceName} z EQ"));
                _autoArmorClickPlan.Add(new AutoArmorClickTarget(equippedPoint, $"założenie: {pieceName} z EQ"));
            }

            return true;
        }

        private static bool IsAutoArmorPointInsideClient(Drawing.Point point, RECT clientRect)
        {
            return point.X >= clientRect.Left
                && point.X < clientRect.Right
                && point.Y >= clientRect.Top
                && point.Y < clientRect.Bottom;
        }

        private void UpdateAutoArmorProgressStatus()
        {
            int completedPieces = Math.Clamp(_autoArmorClickIndex / 3, 0, AutoArmorPieceCount);
            if (_autoArmorClickIndex >= _autoArmorClickPlan.Count)
            {
                SetAutoArmorStatus("Podmieniono 4/4 elementy. Zamykanie EQ…", Brushes.DeepSkyBlue);
                return;
            }

            SetAutoArmorStatus(
                $"Podmiana zestawu: {completedPieces}/4. Następnie: {_autoArmorClickPlan[_autoArmorClickIndex].Label}.",
                Brushes.DeepSkyBlue);
        }

        private void CompleteAutoArmor(DateTime now)
        {
            ResetAutoArmorRuntimeState();
            SetAutoArmorStatus("Gotowe: założony set i drugi set w EQ zostały podmienione.", Brushes.MediumSpringGreen);
            UpdateStatusBar("Auto zbroja: zestawy podmienione poprawnie", "Green");
            RefreshOverlayHud(now);
        }

        private void CancelAutoArmor(string message, Brush color, bool closeInventory)
        {
            bool shouldCloseInventory = closeInventory
                && _autoArmorInventoryOpened
                && _targetGameWindowHandle != IntPtr.Zero
                && GetForegroundWindow() == _targetGameWindowHandle;
            if (shouldCloseInventory)
                SendKeyTap(VK_E);

            ResetAutoArmorRuntimeState();
            SetAutoArmorStatus(message, color);
            UpdateStatusBar(message, color == Brushes.OrangeRed ? "Red" : "Orange");
            RefreshOverlayHud(DateTime.UtcNow);
        }

        private void ResetAutoArmorRuntimeState()
        {
            _autoArmorStage = AutoArmorStage.None;
            _autoArmorInventoryOpened = false;
            _autoArmorCancelPending = false;
            _autoArmorClickIndex = 0;
            _autoArmorClickPlan.Clear();
            _autoArmorStartedAtUtc = DateTime.MinValue;
            _nextAutoArmorActionAtUtc = DateTime.MinValue;
        }

        private void SetAutoArmorStatus(string message, Brush color)
        {
            if (TxtAutoArmorStatus == null)
                return;

            TxtAutoArmorStatus.Text = message;
            TxtAutoArmorStatus.Foreground = color;
        }

        private OverlayHudEntry BuildAutoArmorOverlayEntry()
        {
            string phase;
            string detail;
            OverlayHudTone tone = OverlayHudTone.Active;
            if (_autoArmorCalibrationPending)
            {
                string location = _autoArmorCalibrationEquippedSlot ? "założony set" : "drugi set w EQ";
                phase = "KALIBRACJA POLA";
                detail = $"{AutoArmorPieceNames[_autoArmorCalibrationPieceIndex]} ({location})\nNajedź na slot i naciśnij lewy Ctrl.";
                tone = OverlayHudTone.Warning;
            }
            else
            {
                phase = _autoArmorStage switch
                {
                    AutoArmorStage.OpenInventory => "OTWIERANIE EQ",
                    AutoArmorStage.PrepareSwap => "PRZYGOTOWANIE PODMIANY",
                    AutoArmorStage.MoveToClickTarget or AutoArmorStage.ClickTarget => "PODMIANA ZESTAWU",
                    AutoArmorStage.CloseInventory or AutoArmorStage.WaitForClose => "ZAMYKANIE EQ",
                    _ => "GOTOWE"
                };
                detail = _autoArmorCancelPending
                    ? "Anulowanie po zakończeniu bieżącego elementu."
                    : _autoArmorClickPlan.Count > 0 && _autoArmorClickIndex < _autoArmorClickPlan.Count
                        ? $"Następnie: {_autoArmorClickPlan[_autoArmorClickIndex].Label}."
                        : "Trwa przygotowanie sekwencji.";
            }

            return new OverlayHudEntry(
                "AUTO ZBROJA",
                $"Teraz: {phase}\n{detail}",
                tone,
                Emphasize: true);
        }
    }
}
