using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MinecraftHelper.Services;

namespace MinecraftHelper;

public partial class MainWindow
{
    private const int PvpActionSlotSettleMs = 40;
    private const int PvpActionReturnDelayMs = 50;

    private enum PvpQuickActionKind
    {
        None,
        AutoBlock,
        Snowball,
        Pearl,
        Rod
    }

    private enum PvpQuickActionStage
    {
        None,
        SelectItem,
        PressRightButton,
        ReleaseRightButton,
        RestoreSword
    }

    private PvpQuickActionKind _pvpQuickActionKind;
    private PvpQuickActionStage _pvpQuickActionStage;
    private DateTime _nextPvpQuickActionAtUtc = DateTime.MinValue;
    private bool _pvpQuickActionRightButtonDown;
    private bool _pvpAutoBlockBindWasDown;
    private bool _pvpSnowballBindWasDown;
    private bool _pvpPearlBindWasDown;
    private bool _pvpRodBindWasDown;

    private bool IsPvpQuickActionRunning => _pvpQuickActionStage != PvpQuickActionStage.None;

    private void NormalizePvpQuickActionSettings()
    {
        _settings.PvpAutoBlockBind ??= string.Empty;
        _settings.PvpSnowballBind ??= string.Empty;
        _settings.PvpPearlBind ??= string.Empty;
        _settings.PvpRodBind ??= string.Empty;
        _settings.PvpSwordSlot = Math.Clamp(_settings.PvpSwordSlot, 1, 9);
        _settings.PvpBlockSlot = Math.Clamp(_settings.PvpBlockSlot, 1, 9);
        _settings.PvpSnowballSlot = Math.Clamp(_settings.PvpSnowballSlot, 1, 9);
        _settings.PvpPearlSlot = Math.Clamp(_settings.PvpPearlSlot, 1, 9);
        _settings.PvpRodSlot = Math.Clamp(_settings.PvpRodSlot, 1, 9);
        _settings.PvpRodRightClickHoldMs = Math.Clamp(
            _settings.PvpRodRightClickHoldMs <= 0 ? 80 : _settings.PvpRodRightClickHoldMs,
            20,
            1000);
    }

    private void LoadPvpQuickActionsToUi()
    {
        TxtPvpAutoBlockBind.Text = _settings.PvpAutoBlockBind;
        TxtPvpSnowballBind.Text = _settings.PvpSnowballBind;
        TxtPvpPearlBind.Text = _settings.PvpPearlBind;
        TxtPvpRodBind.Text = _settings.PvpRodBind;
        CbPvpSwordSlot.SelectedIndex = _settings.PvpSwordSlot - 1;
        CbPvpBlockSlot.SelectedIndex = _settings.PvpBlockSlot - 1;
        CbPvpSnowballSlot.SelectedIndex = _settings.PvpSnowballSlot - 1;
        CbPvpPearlSlot.SelectedIndex = _settings.PvpPearlSlot - 1;
        CbPvpRodSlot.SelectedIndex = _settings.PvpRodSlot - 1;
        TxtPvpRodHoldMs.Text = _settings.PvpRodRightClickHoldMs.ToString(CultureInfo.InvariantCulture);
        SetPvpQuickActionStatus("Gotowe. Ustaw sloty i przypisz wybrane bindy.", Brushes.MediumSpringGreen);
    }

    private void ReadPvpQuickActionsFromUi()
    {
        _settings.PvpAutoBlockBind = TxtPvpAutoBlockBind.Text.Trim();
        _settings.PvpSnowballBind = TxtPvpSnowballBind.Text.Trim();
        _settings.PvpPearlBind = TxtPvpPearlBind.Text.Trim();
        _settings.PvpRodBind = TxtPvpRodBind.Text.Trim();
        _settings.PvpSwordSlot = GetPvpQuickActionSelectedSlot(CbPvpSwordSlot, 1);
        _settings.PvpBlockSlot = GetPvpQuickActionSelectedSlot(CbPvpBlockSlot, 5);
        _settings.PvpSnowballSlot = GetPvpQuickActionSelectedSlot(CbPvpSnowballSlot, 2);
        _settings.PvpPearlSlot = GetPvpQuickActionSelectedSlot(CbPvpPearlSlot, 3);
        _settings.PvpRodSlot = GetPvpQuickActionSelectedSlot(CbPvpRodSlot, 4);
        _settings.PvpRodRightClickHoldMs = Math.Clamp(ParsePvpQuickActionMilliseconds(TxtPvpRodHoldMs.Text, 80), 20, 1000);
    }

    private void PvpQuickActionSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoadingUi)
            MarkDirty();
    }

    private static int GetPvpQuickActionSelectedSlot(ComboBox comboBox, int fallback)
    {
        return comboBox.SelectedIndex is >= 0 and < 9 ? comboBox.SelectedIndex + 1 : fallback;
    }

    private static int ParsePvpQuickActionMilliseconds(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    private void HandlePvpQuickActionBinds(DateTime now, bool cursorVisible)
    {
        bool autoBlockPressed = IsBindPressed(TxtPvpAutoBlockBind.Text, ref _pvpAutoBlockBindWasDown);
        bool snowballPressed = IsBindPressed(TxtPvpSnowballBind.Text, ref _pvpSnowballBindWasDown);
        bool pearlPressed = IsBindPressed(TxtPvpPearlBind.Text, ref _pvpPearlBindWasDown);
        bool rodPressed = IsBindPressed(TxtPvpRodBind.Text, ref _pvpRodBindWasDown);
        bool actionBindPressed = autoBlockPressed || snowballPressed || pearlPressed || rodPressed;
        bool guiBlocked = cursorVisible
            || ((IsPvpQuickActionRunning || actionBindPressed) && IsPvpQuickActionGuiVisible());

        if (IsPvpQuickActionRunning)
        {
            if (guiBlocked)
            {
                CancelPvpQuickAction("Akcja PVP przerwana — wykryto otwarte GUI.", Brushes.OrangeRed, restoreSword: false);
                return;
            }

            if (!IsAutoLeftClickerRuntimeActive())
                CancelPvpQuickAction("Akcja PVP przerwana — AUTO LPM został wyłączony.", Brushes.Orange, restoreSword: true);
            return;
        }

        PvpQuickActionKind requestedAction = autoBlockPressed
            ? PvpQuickActionKind.AutoBlock
            : snowballPressed
                ? PvpQuickActionKind.Snowball
                : pearlPressed
                    ? PvpQuickActionKind.Pearl
                    : rodPressed
                        ? PvpQuickActionKind.Rod
                        : PvpQuickActionKind.None;
        if (requestedAction == PvpQuickActionKind.None)
            return;

        if (guiBlocked)
        {
            SetPvpQuickActionStatus("Nie wykonano akcji — zamknij ekwipunek, chat lub inne GUI.", Brushes.OrangeRed);
            UpdateStatusBar("Akcja PVP zablokowana przez otwarte GUI", "Red");
            return;
        }

        if (!IsAutoLeftClickerRuntimeActive())
        {
            SetPvpQuickActionStatus("Nie wykonano akcji — najpierw uruchom AUTO LPM.", Brushes.Orange);
            UpdateStatusBar("Akcja PVP wymaga aktywnego AUTO LPM", "Orange");
            return;
        }

        if (IsAutoArmorRunning
            || _autoWaterStage != AutoWaterStage.None
            || _bindyStage != BindyStage.None)
        {
            SetPvpQuickActionStatus("Nie wykonano akcji — inna automatyzacja jest w trakcie pracy.", Brushes.Orange);
            UpdateStatusBar("Akcja PVP czeka na zakończenie innej automatyzacji", "Orange");
            return;
        }

        StartPvpQuickAction(requestedAction, now);
    }

    private bool IsPvpQuickActionGuiVisible()
    {
        try
        {
            return IsInventoryCursorVisible();
        }
        catch
        {
            return true;
        }
    }

    private bool IsAutoLeftClickerRuntimeActive()
    {
        return _autoLeftRuntimeEnabled && ChkAutoLeftEnabled.IsChecked == true;
    }

    private bool HasAnyConfiguredPvpQuickActionBind()
    {
        return !string.IsNullOrWhiteSpace(TxtPvpAutoBlockBind.Text)
            || !string.IsNullOrWhiteSpace(TxtPvpSnowballBind.Text)
            || !string.IsNullOrWhiteSpace(TxtPvpPearlBind.Text)
            || !string.IsNullOrWhiteSpace(TxtPvpRodBind.Text);
    }

    private void StartPvpQuickAction(PvpQuickActionKind kind, DateTime now)
    {
        ReleasePvpQuickActionRightButton();
        _pvpQuickActionKind = kind;
        _pvpQuickActionStage = PvpQuickActionStage.SelectItem;
        _nextPvpQuickActionAtUtc = now;
        _autoClickScheduler.StopRightImmediately();

        string name = GetPvpQuickActionName(kind);
        SetPvpQuickActionStatus($"{name}: rozpoczynam…", Brushes.DeepSkyBlue);
        UpdateStatusBar($"Akcja PVP: {name}", "Orange");
        RefreshOverlayHud(now);
    }

    private void KeepAutoLeftClickerRunningDuringPvpAction(bool blockedByCursor)
    {
        string leftMode = GetSelectedMode(CbAutoLeftMode);
        int leftHoldKey = leftMode == "Hold" && TryGetVirtualKey(TxtAutoLeftKey.Text, out int leftKey)
            ? leftKey
            : 0;
        int leftPointerKey = UsesPhysicalPointerGate(leftMode) ? VkLeftButton : 0;
        bool leftPointerReady = leftPointerKey == 0 || _physicalMouseTracker.IsButtonDown(leftPointerKey);
        bool leftEnabled = !blockedByCursor
            && leftPointerReady
            && _autoLeftRuntimeEnabled
            && ChkAutoLeftEnabled.IsChecked == true;

        // The action owns only PPM. Keep the existing LPM channel and its next
        // click deadline; updating it must never rearm the paused right channel.
        _autoClickScheduler.UpdateLeft(
            leftEnabled,
            ReadCps(TxtAutoLeftMinCps.Text, 10),
            ReadCps(TxtAutoLeftMaxCps.Text, 12),
            leftHoldKey,
            leftPointerKey,
            _targetGameWindowHandle);
        SetAutoLeftDabHold(leftEnabled && ChkAutoLeftDabMode.IsChecked == true);
    }

    private void RunPvpQuickAction(DateTime now)
    {
        if (!IsPvpQuickActionRunning || now < _nextPvpQuickActionAtUtc)
            return;

        switch (_pvpQuickActionStage)
        {
            case PvpQuickActionStage.SelectItem:
                int itemSlot = GetPvpQuickActionItemSlot(_pvpQuickActionKind);
                SendKeyTap(GetSlotVirtualKey(itemSlot));
                _pvpQuickActionStage = PvpQuickActionStage.PressRightButton;
                _nextPvpQuickActionAtUtc = now.AddMilliseconds(PvpActionSlotSettleMs);
                SetPvpQuickActionStatus($"{GetPvpQuickActionName(_pvpQuickActionKind)}: wybrano slot {itemSlot}, używam PPM…", Brushes.DeepSkyBlue);
                break;

            case PvpQuickActionStage.PressRightButton:
                if (_pvpQuickActionKind == PvpQuickActionKind.Rod)
                {
                    _pvpQuickActionRightButtonDown = NativeInput.SendMouseButton(leftButton: false, down: true);
                    if (!_pvpQuickActionRightButtonDown)
                    {
                        CancelPvpQuickAction("Akcja PVP przerwana — nie udało się nacisnąć PPM.", Brushes.OrangeRed, restoreSword: true);
                        return;
                    }

                    _pvpQuickActionStage = PvpQuickActionStage.ReleaseRightButton;
                    _nextPvpQuickActionAtUtc = now.AddMilliseconds(GetPvpRodRightClickHoldMs());
                }
                else
                {
                    // A held use button can suppress attacks in Minecraft even
                    // while the LPM scheduler is still ON. Standard items need
                    // one complete PPM click, with no held button between ticks.
                    if (!NativeInput.SendMouseClick(leftButton: false, holdPulseMode: false))
                    {
                        CancelPvpQuickAction("Akcja PVP przerwana — nie udało się wykonać PPM.", Brushes.OrangeRed, restoreSword: true);
                        return;
                    }

                    _pvpQuickActionStage = PvpQuickActionStage.RestoreSword;
                    _nextPvpQuickActionAtUtc = now.AddMilliseconds(PvpActionReturnDelayMs);
                }
                break;

            case PvpQuickActionStage.ReleaseRightButton:
                ReleasePvpQuickActionRightButton();
                _pvpQuickActionStage = PvpQuickActionStage.RestoreSword;
                _nextPvpQuickActionAtUtc = now.AddMilliseconds(PvpActionReturnDelayMs);
                break;

            case PvpQuickActionStage.RestoreSword:
                int swordSlot = GetPvpQuickActionSelectedSlot(CbPvpSwordSlot, 1);
                SendKeyTap(GetSlotVirtualKey(swordSlot));
                CompletePvpQuickAction(swordSlot, now);
                break;
        }
    }

    private int GetPvpQuickActionItemSlot(PvpQuickActionKind kind)
    {
        return kind switch
        {
            PvpQuickActionKind.AutoBlock => GetPvpQuickActionSelectedSlot(CbPvpBlockSlot, 5),
            PvpQuickActionKind.Snowball => GetPvpQuickActionSelectedSlot(CbPvpSnowballSlot, 2),
            PvpQuickActionKind.Pearl => GetPvpQuickActionSelectedSlot(CbPvpPearlSlot, 3),
            PvpQuickActionKind.Rod => GetPvpQuickActionSelectedSlot(CbPvpRodSlot, 4),
            _ => GetPvpQuickActionSelectedSlot(CbPvpSwordSlot, 1)
        };
    }

    private int GetPvpRodRightClickHoldMs()
    {
        return Math.Clamp(ParsePvpQuickActionMilliseconds(TxtPvpRodHoldMs.Text, 80), 20, 1000);
    }

    private void CompletePvpQuickAction(int swordSlot, DateTime now)
    {
        string name = GetPvpQuickActionName(_pvpQuickActionKind);
        ResetPvpQuickActionState();
        SetPvpQuickActionStatus($"Gotowe: {name}. Przywrócono miecz ze slotu {swordSlot}.", Brushes.MediumSpringGreen);
        UpdateStatusBar($"Wykonano: {name}", "Green");
        RefreshOverlayHud(now);
    }

    private void CancelPvpQuickAction(string message, Brush brush, bool restoreSword, bool updateStatus = true)
    {
        bool wasRunning = IsPvpQuickActionRunning;
        ReleasePvpQuickActionRightButton();
        if (wasRunning
            && restoreSword
            && _targetGameWindowHandle != IntPtr.Zero
            && GetForegroundWindow() == _targetGameWindowHandle)
        {
            SendKeyTap(GetSlotVirtualKey(GetPvpQuickActionSelectedSlot(CbPvpSwordSlot, 1)));
        }

        ResetPvpQuickActionState();
        if (!string.IsNullOrWhiteSpace(message))
        {
            SetPvpQuickActionStatus(message, brush);
            if (updateStatus)
                UpdateStatusBar(message, brush == Brushes.OrangeRed ? "Red" : "Orange");
        }
    }

    private void ResetPvpQuickActionState()
    {
        _pvpQuickActionKind = PvpQuickActionKind.None;
        _pvpQuickActionStage = PvpQuickActionStage.None;
        _nextPvpQuickActionAtUtc = DateTime.MinValue;
        _pvpQuickActionRightButtonDown = false;
    }

    private void ReleasePvpQuickActionRightButton()
    {
        if (!_pvpQuickActionRightButtonDown)
            return;

        _ = NativeInput.SendMouseButton(leftButton: false, down: false);
        _pvpQuickActionRightButtonDown = false;
    }

    private void SynchronizePvpQuickActionBindStates()
    {
        _pvpAutoBlockBindWasDown = IsConfiguredBindKeyDown(TxtPvpAutoBlockBind.Text);
        _pvpSnowballBindWasDown = IsConfiguredBindKeyDown(TxtPvpSnowballBind.Text);
        _pvpPearlBindWasDown = IsConfiguredBindKeyDown(TxtPvpPearlBind.Text);
        _pvpRodBindWasDown = IsConfiguredBindKeyDown(TxtPvpRodBind.Text);
    }

    private static string GetPvpQuickActionName(PvpQuickActionKind kind)
    {
        return kind switch
        {
            PvpQuickActionKind.AutoBlock => "Auto blok",
            PvpQuickActionKind.Snowball => "Śnieżka",
            PvpQuickActionKind.Pearl => "Perła",
            PvpQuickActionKind.Rod => "Wędka",
            _ => "Akcja PVP"
        };
    }

    private OverlayHudEntry BuildPvpQuickActionOverlayEntry()
    {
        string stage = _pvpQuickActionStage switch
        {
            PvpQuickActionStage.SelectItem => "wybór przedmiotu",
            PvpQuickActionStage.PressRightButton => "użycie PPM",
            PvpQuickActionStage.ReleaseRightButton => "trzymanie PPM",
            PvpQuickActionStage.RestoreSword => "powrót na miecz",
            _ => "gotowe"
        };
        return new OverlayHudEntry(
            GetPvpQuickActionName(_pvpQuickActionKind),
            stage,
            OverlayHudTone.Warning,
            Emphasize: true);
    }

    private void SetPvpQuickActionStatus(string message, Brush brush)
    {
        if (TxtPvpQuickActionStatus == null)
            return;
        TxtPvpQuickActionStatus.Text = message;
        TxtPvpQuickActionStatus.Foreground = brush;
    }
}
