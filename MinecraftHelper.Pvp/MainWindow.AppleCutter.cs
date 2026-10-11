using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MinecraftHelper.Services;

namespace MinecraftHelper;

public partial class MainWindow
{
    private const int AppleCutterPlacementCps = 40;
    private const int AppleCutterPlacementIntervalMs = 1000 / AppleCutterPlacementCps;
    private const int AppleCutterChatOpenDelayMs = 180;
    private const int AppleCutterCommandSubmitDelayMs = 110;
    private const int AppleCutterRepairCooldownMs = 350;

    private enum AppleCutterStage
    {
        None,
        SelectShears,
        WaitBeforeBreak,
        Breaking,
        WaitBeforeRepair,
        RepairTypeCommand,
        RepairSubmit,
        RepairCooldown,
        SelectLeaves,
        WaitBeforePlace,
        Placing
    }

    private AppleCutterStage _appleCutterStage;
    private bool _appleCutterBindWasDown;
    private bool _appleCutterLeftButtonHeld;
    private DateTime _nextAppleCutterActionAtUtc = DateTime.MinValue;
    private DateTime _appleCutterPlacementEndsAtUtc = DateTime.MinValue;
    private int _appleCutterCyclesCompleted;

    private bool IsAppleCutterRunning => _appleCutterStage != AppleCutterStage.None;

    private bool IsAppleCutterRepairStage => _appleCutterStage is
        AppleCutterStage.RepairTypeCommand or
        AppleCutterStage.RepairSubmit or
        AppleCutterStage.RepairCooldown;

    private void NormalizeAppleCutterSettings()
    {
        _settings.AppleCutterBind ??= string.Empty;
        _settings.AppleCutterRepairCommand = NormalizeAppleCutterCommand(_settings.AppleCutterRepairCommand);
        _settings.AppleCutterShearsSlot = Math.Clamp(_settings.AppleCutterShearsSlot, 1, 9);
        _settings.AppleCutterLeavesSlot = Math.Clamp(_settings.AppleCutterLeavesSlot, 1, 9);
        (_settings.AppleCutterSwitchMinMs, _settings.AppleCutterSwitchMaxMs) = NormalizeAppleCutterRange(
            _settings.AppleCutterSwitchMinMs,
            _settings.AppleCutterSwitchMaxMs,
            35,
            55);
        (_settings.AppleCutterBreakMinMs, _settings.AppleCutterBreakMaxMs) = NormalizeAppleCutterRange(
            _settings.AppleCutterBreakMinMs,
            _settings.AppleCutterBreakMaxMs,
            90,
            140);
        (_settings.AppleCutterPlaceMinMs, _settings.AppleCutterPlaceMaxMs) = NormalizeAppleCutterRange(
            _settings.AppleCutterPlaceMinMs,
            _settings.AppleCutterPlaceMaxMs,
            50,
            90);
        _settings.AppleCutterRepairEveryCycles = Math.Clamp(
            _settings.AppleCutterRepairEveryCycles <= 0 ? 40 : _settings.AppleCutterRepairEveryCycles,
            1,
            10000);
    }

    private static (int Minimum, int Maximum) NormalizeAppleCutterRange(
        int minimum,
        int maximum,
        int fallbackMinimum,
        int fallbackMaximum)
    {
        minimum = Math.Clamp(minimum <= 0 ? fallbackMinimum : minimum, 10, 5000);
        maximum = Math.Clamp(maximum <= 0 ? fallbackMaximum : maximum, 10, 5000);
        return minimum <= maximum ? (minimum, maximum) : (maximum, minimum);
    }

    private static string NormalizeAppleCutterCommand(string? value)
    {
        string command = string.IsNullOrWhiteSpace(value) ? "/repair" : value.Trim();
        return command.StartsWith('/') ? command : $"/{command}";
    }

    private void LoadAppleCutterToUi()
    {
        ResetAppleCutterRuntimeState();
        _appleCutterBindWasDown = false;
        ChkAppleCutterEnabled.IsChecked = _settings.AppleCutterEnabled;
        TxtAppleCutterBind.Text = _settings.AppleCutterBind;
        CbAppleCutterShearsSlot.SelectedIndex = _settings.AppleCutterShearsSlot - 1;
        CbAppleCutterLeavesSlot.SelectedIndex = _settings.AppleCutterLeavesSlot - 1;
        TxtAppleCutterSwitchMinMs.Text = _settings.AppleCutterSwitchMinMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterSwitchMaxMs.Text = _settings.AppleCutterSwitchMaxMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterBreakMinMs.Text = _settings.AppleCutterBreakMinMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterBreakMaxMs.Text = _settings.AppleCutterBreakMaxMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterPlaceMinMs.Text = _settings.AppleCutterPlaceMinMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterPlaceMaxMs.Text = _settings.AppleCutterPlaceMaxMs.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterRepairEvery.Text = _settings.AppleCutterRepairEveryCycles.ToString(CultureInfo.InvariantCulture);
        TxtAppleCutterRepairCommand.Text = _settings.AppleCutterRepairCommand;
        SetAppleCutterStatus("Gotowe. Ustaw sloty, czasy i bind.", Brushes.MediumSpringGreen);
    }

    private void ReadAppleCutterFromUi()
    {
        _settings.AppleCutterEnabled = ChkAppleCutterEnabled.IsChecked == true;
        _settings.AppleCutterBind = TxtAppleCutterBind.Text.Trim();
        _settings.AppleCutterShearsSlot = GetAppleCutterSelectedSlot(CbAppleCutterShearsSlot, 1);
        _settings.AppleCutterLeavesSlot = GetAppleCutterSelectedSlot(CbAppleCutterLeavesSlot, 2);
        (_settings.AppleCutterSwitchMinMs, _settings.AppleCutterSwitchMaxMs) = ReadAppleCutterRange(
            TxtAppleCutterSwitchMinMs.Text,
            TxtAppleCutterSwitchMaxMs.Text,
            35,
            55);
        (_settings.AppleCutterBreakMinMs, _settings.AppleCutterBreakMaxMs) = ReadAppleCutterRange(
            TxtAppleCutterBreakMinMs.Text,
            TxtAppleCutterBreakMaxMs.Text,
            90,
            140);
        (_settings.AppleCutterPlaceMinMs, _settings.AppleCutterPlaceMaxMs) = ReadAppleCutterRange(
            TxtAppleCutterPlaceMinMs.Text,
            TxtAppleCutterPlaceMaxMs.Text,
            50,
            90);
        int repairEvery = ReadAppleCutterInt(TxtAppleCutterRepairEvery.Text, 40);
        _settings.AppleCutterRepairEveryCycles = Math.Clamp(repairEvery <= 0 ? 40 : repairEvery, 1, 10000);
        _settings.AppleCutterRepairCommand = NormalizeAppleCutterCommand(TxtAppleCutterRepairCommand.Text);
    }

    private static (int Minimum, int Maximum) ReadAppleCutterRange(
        string minimumText,
        string maximumText,
        int fallbackMinimum,
        int fallbackMaximum)
    {
        int minimum = Math.Clamp(ReadAppleCutterInt(minimumText, fallbackMinimum), 10, 5000);
        int maximum = Math.Clamp(ReadAppleCutterInt(maximumText, fallbackMaximum), 10, 5000);
        return minimum <= maximum ? (minimum, maximum) : (maximum, minimum);
    }

    private static int ReadAppleCutterInt(string? text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    private static int GetAppleCutterSelectedSlot(ComboBox comboBox, int fallback)
    {
        return comboBox.SelectedIndex is >= 0 and < 9 ? comboBox.SelectedIndex + 1 : fallback;
    }

    private void UpdateAppleCutterEnabledState()
    {
        bool enabled = ChkAppleCutterEnabled?.IsChecked == true;
        if (PanelAppleCutterExpandableContent != null)
            SetExpandableSectionState(PanelAppleCutterExpandableContent, enabled);
        if (PanelAppleCutterContent != null)
            PanelAppleCutterContent.IsEnabled = enabled;

        if (!enabled && IsAppleCutterRunning)
            CancelAppleCutter("Ścinanie jabłek wyłączone — zwolniono LPM i PPM.", Brushes.Orange);
    }

    private void ChkAppleCutterEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi)
            return;

        UpdateAppleCutterEnabledState();
        RefreshOverlayHud(DateTime.UtcNow);
        MarkDirty();
    }

    private void AppleCutterConfig_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoadingUi)
            MarkDirty();
    }

    private void AppleCutterConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoadingUi)
            MarkDirty();
    }

    private void HandleAppleCutterBind(DateTime now, bool cursorVisible)
    {
        if (ChkAppleCutterEnabled.IsChecked != true)
            return;

        if (!IsBindPressed(TxtAppleCutterBind.Text, ref _appleCutterBindWasDown))
            return;

        if (IsAppleCutterRunning)
        {
            CancelAppleCutter("Ścinanie jabłek zatrzymane bindem — zwolniono LPM i PPM.", Brushes.Orange);
            return;
        }

        if (cursorVisible || IsInventoryCursorVisible())
        {
            SetAppleCutterStatus("Nie uruchomiono: zamknij chat, ekwipunek lub inne GUI.", Brushes.OrangeRed);
            UpdateStatusBar("Jabłka: zamknij GUI Minecrafta", "Orange");
            return;
        }

        TryStartAppleCutter(now);
    }

    private bool TryStartAppleCutter(DateTime now)
    {
        int shearsSlot = GetAppleCutterSelectedSlot(CbAppleCutterShearsSlot, 1);
        int leavesSlot = GetAppleCutterSelectedSlot(CbAppleCutterLeavesSlot, 2);
        if (shearsSlot == leavesSlot)
        {
            SetAppleCutterStatus("Nie uruchomiono: nożyce i liście muszą mieć różne sloty.", Brushes.OrangeRed);
            UpdateStatusBar("Jabłka: wybierz dwa różne sloty", "Orange");
            return false;
        }
        if (string.IsNullOrWhiteSpace(TxtAppleCutterBind.Text))
        {
            SetAppleCutterStatus("Nie uruchomiono: przypisz i zapisz bind.", Brushes.OrangeRed);
            return false;
        }
        if (IsAutoArmorRunning
            || _autoWaterStage != AutoWaterStage.None
            || IsPvpQuickActionRunning
            || _bindyStage != BindyStage.None)
        {
            SetAppleCutterStatus("Nie uruchomiono: inna automatyzacja jest w trakcie pracy.", Brushes.OrangeRed);
            UpdateStatusBar("Jabłka: najpierw zakończ inną automatyzację", "Orange");
            return false;
        }

        StopClickerRuntimesForExclusivePointerMacro();
        ReleaseAppleCutterInjectedButtons();
        _appleCutterCyclesCompleted = 0;
        _appleCutterStage = AppleCutterStage.SelectShears;
        _nextAppleCutterActionAtUtc = now;
        _appleCutterPlacementEndsAtUtc = DateTime.MinValue;
        SetAppleCutterStatus("Uruchomiono. Wybieranie nożyc…", Brushes.DeepSkyBlue);
        UpdateStatusBar("Ścinanie jabłek uruchomione", "Orange");
        RefreshOverlayHud(now);
        return true;
    }

    private void RunAppleCutterTick(DateTime now)
    {
        if (!IsAppleCutterRunning || now < _nextAppleCutterActionAtUtc)
            return;

        switch (_appleCutterStage)
        {
            case AppleCutterStage.SelectShears:
                SendKeyTap(GetSlotVirtualKey(GetAppleCutterSelectedSlot(CbAppleCutterShearsSlot, 1)));
                bool repairDue = _appleCutterCyclesCompleted > 0
                    && _appleCutterCyclesCompleted % GetAppleCutterRepairEveryCycles() == 0;
                _appleCutterStage = repairDue
                    ? AppleCutterStage.WaitBeforeRepair
                    : AppleCutterStage.WaitBeforeBreak;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(GetAppleCutterRandomDelay(AppleCutterDelay.Switch));
                SetAppleCutterStatus(
                    repairDue
                        ? $"Cykl {_appleCutterCyclesCompleted}: wybrano nożyce, za chwilę naprawa."
                        : $"Cykl {_appleCutterCyclesCompleted + 1}: wybrano nożyce, oczekiwanie na cięcie…",
                    Brushes.DeepSkyBlue);
                return;

            case AppleCutterStage.WaitBeforeBreak:
                _appleCutterLeftButtonHeld = NativeInput.SendMouseButton(leftButton: true, down: true);
                if (!_appleCutterLeftButtonHeld)
                {
                    CancelAppleCutter("Przerwano: nie udało się nacisnąć LPM.", Brushes.OrangeRed);
                    return;
                }
                _appleCutterStage = AppleCutterStage.Breaking;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(GetAppleCutterRandomDelay(AppleCutterDelay.Break));
                SetAppleCutterStatus($"Cięcie LPM — cykl {_appleCutterCyclesCompleted + 1}.", Brushes.DeepSkyBlue);
                return;

            case AppleCutterStage.Breaking:
                ReleaseAppleCutterLeftButton();
                _appleCutterStage = AppleCutterStage.SelectLeaves;
                _nextAppleCutterActionAtUtc = now;
                SetAppleCutterStatus("Cięcie zakończone. Wybieranie liści…", Brushes.DeepSkyBlue);
                return;

            case AppleCutterStage.WaitBeforeRepair:
                SendKeyTap(GetConfiguredChatOpenVirtualKey());
                _appleCutterStage = AppleCutterStage.RepairTypeCommand;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(AppleCutterChatOpenDelayMs);
                SetAppleCutterStatus($"Naprawa po {_appleCutterCyclesCompleted} cyklach — otwieranie chatu…", Brushes.Orange);
                return;

            case AppleCutterStage.RepairTypeCommand:
                if (!SendTextByKeyboard(NormalizeAppleCutterCommand(TxtAppleCutterRepairCommand.Text)))
                {
                    CancelAppleCutter("Przerwano: nie udało się wpisać komendy naprawy.", Brushes.OrangeRed);
                    return;
                }
                _appleCutterStage = AppleCutterStage.RepairSubmit;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(AppleCutterCommandSubmitDelayMs);
                SetAppleCutterStatus("Naprawa: wpisano komendę, zatwierdzanie…", Brushes.Orange);
                return;

            case AppleCutterStage.RepairSubmit:
                SendKeyTap(VK_RETURN);
                _appleCutterStage = AppleCutterStage.RepairCooldown;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(AppleCutterRepairCooldownMs);
                SetAppleCutterStatus("Naprawa wysłana. Krótka przerwa przed cięciem…", Brushes.MediumSpringGreen);
                return;

            case AppleCutterStage.RepairCooldown:
                _appleCutterStage = AppleCutterStage.WaitBeforeBreak;
                _nextAppleCutterActionAtUtc = now;
                return;

            case AppleCutterStage.SelectLeaves:
                SendKeyTap(GetSlotVirtualKey(GetAppleCutterSelectedSlot(CbAppleCutterLeavesSlot, 2)));
                _appleCutterStage = AppleCutterStage.WaitBeforePlace;
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(GetAppleCutterRandomDelay(AppleCutterDelay.Switch));
                SetAppleCutterStatus("Wybrano liście. Oczekiwanie na kładzenie…", Brushes.DeepSkyBlue);
                return;

            case AppleCutterStage.WaitBeforePlace:
                _appleCutterPlacementEndsAtUtc = now.AddMilliseconds(GetAppleCutterRandomDelay(AppleCutterDelay.Place));
                _appleCutterStage = AppleCutterStage.Placing;
                _nextAppleCutterActionAtUtc = now;
                SetAppleCutterStatus($"Kładzenie liści — {AppleCutterPlacementCps} CPS.", Brushes.DeepSkyBlue);
                return;

            case AppleCutterStage.Placing:
                if (now >= _appleCutterPlacementEndsAtUtc)
                {
                    _appleCutterCyclesCompleted++;
                    _appleCutterStage = AppleCutterStage.SelectShears;
                    _nextAppleCutterActionAtUtc = now;
                    SetAppleCutterStatus(
                        $"Zakończono cykl {_appleCutterCyclesCompleted}. Powrót do nożyc…",
                        Brushes.MediumSpringGreen);
                    return;
                }

                if (!NativeInput.SendMouseClick(leftButton: false, holdPulseMode: false))
                {
                    CancelAppleCutter("Przerwano: nie udało się wykonać PPM.", Brushes.OrangeRed);
                    return;
                }
                _nextAppleCutterActionAtUtc = now.AddMilliseconds(AppleCutterPlacementIntervalMs);
                return;
        }
    }

    private enum AppleCutterDelay
    {
        Switch,
        Break,
        Place
    }

    private int GetAppleCutterRandomDelay(AppleCutterDelay delay)
    {
        (int minimum, int maximum) = delay switch
        {
            AppleCutterDelay.Switch => ReadAppleCutterRange(
                TxtAppleCutterSwitchMinMs.Text,
                TxtAppleCutterSwitchMaxMs.Text,
                35,
                55),
            AppleCutterDelay.Break => ReadAppleCutterRange(
                TxtAppleCutterBreakMinMs.Text,
                TxtAppleCutterBreakMaxMs.Text,
                90,
                140),
            _ => ReadAppleCutterRange(
                TxtAppleCutterPlaceMinMs.Text,
                TxtAppleCutterPlaceMaxMs.Text,
                50,
                90)
        };
        return minimum == maximum ? minimum : Random.Shared.Next(minimum, maximum + 1);
    }

    private int GetAppleCutterRepairEveryCycles()
    {
        int value = ReadAppleCutterInt(TxtAppleCutterRepairEvery.Text, 40);
        return Math.Clamp(value <= 0 ? 40 : value, 1, 10000);
    }

    private void CancelAppleCutter(string message, Brush brush, bool updateStatus = true)
    {
        bool wasRunning = IsAppleCutterRunning;
        ReleaseAppleCutterInjectedButtons();
        ResetAppleCutterRuntimeState();
        if (updateStatus && !string.IsNullOrWhiteSpace(message))
        {
            SetAppleCutterStatus(message, brush);
            if (wasRunning)
                UpdateStatusBar(message, brush == Brushes.OrangeRed ? "Red" : "Orange");
        }
        RefreshOverlayHud(DateTime.UtcNow);
    }

    private void ResetAppleCutterRuntimeState()
    {
        _appleCutterStage = AppleCutterStage.None;
        _nextAppleCutterActionAtUtc = DateTime.MinValue;
        _appleCutterPlacementEndsAtUtc = DateTime.MinValue;
        _appleCutterCyclesCompleted = 0;
    }

    private void ReleaseAppleCutterLeftButton()
    {
        if (!_appleCutterLeftButtonHeld)
            return;
        _appleCutterLeftButtonHeld = false;
        _ = NativeInput.SendMouseButton(leftButton: true, down: false);
    }

    private void ReleaseAppleCutterInjectedButtons()
    {
        ReleaseAppleCutterLeftButton();
    }

    private void SetAppleCutterStatus(string message, Brush brush)
    {
        if (TxtAppleCutterStatus == null)
            return;
        TxtAppleCutterStatus.Text = message;
        TxtAppleCutterStatus.Foreground = brush;
    }

    private OverlayHudEntry BuildAppleCutterOverlayEntry()
    {
        string stage = _appleCutterStage switch
        {
            AppleCutterStage.SelectShears => "NOŻYCE • wybór slotu",
            AppleCutterStage.WaitBeforeBreak => "NOŻYCE • za chwilę cięcie",
            AppleCutterStage.Breaking => "CIĘCIE • LPM",
            AppleCutterStage.WaitBeforeRepair => "NAPRAWA • otwieranie chatu",
            AppleCutterStage.RepairTypeCommand => "NAPRAWA • wpisywanie komendy",
            AppleCutterStage.RepairSubmit => "NAPRAWA • zatwierdzanie",
            AppleCutterStage.RepairCooldown => "NAPRAWA • oczekiwanie",
            AppleCutterStage.SelectLeaves => "LIŚCIE • wybór slotu",
            AppleCutterStage.WaitBeforePlace => "LIŚCIE • za chwilę PPM",
            AppleCutterStage.Placing => $"LIŚCIE • {AppleCutterPlacementCps} CPS",
            _ => "GOTOWY"
        };
        int repairEvery = GetAppleCutterRepairEveryCycles();
        int nextRepair = repairEvery - (_appleCutterCyclesCompleted % repairEvery);
        bool repairingNow = _appleCutterStage is AppleCutterStage.WaitBeforeRepair
            or AppleCutterStage.RepairTypeCommand
            or AppleCutterStage.RepairSubmit
            or AppleCutterStage.RepairCooldown;
        string repairDetail = repairingNow ? "naprawa teraz" : $"naprawa za: {nextRepair}";
        string detail = $"{stage} • użycia: {_appleCutterCyclesCompleted} • {repairDetail}";
        return new OverlayHudEntry("ŚCINANIE JABŁEK", detail, OverlayHudTone.Warning);
    }
}
