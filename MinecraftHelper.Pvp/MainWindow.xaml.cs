using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MinecraftHelper.Models;
using MinecraftHelper.Services;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;

namespace MinecraftHelper;

public partial class MainWindow : Window
{
    private const int VkLeftButton = 0x01;
    private const int VkRightButton = 0x02;
    private const int VkMiddleButton = 0x04;
    private const int VkXButton1 = 0x05;
    private const int VkXButton2 = 0x06;
    private const int VK_RETURN = 0x0D;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_E = 0x45;
    private const int VK_O = 0x4F;
    private const int CursorShowing = 0x00000001;
    private const int SmCxCursor = 13;
    private const int SmCyCursor = 14;
    private const uint DiNormal = 0x0003;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly object CursorCacheLock = new();
    private static readonly Dictionary<IntPtr, bool> CursorBlankCache = new();

    private readonly PvpSettingsService _settingsService = new();
    private readonly PhysicalMouseStateTracker _physicalMouseTracker = new();
    private readonly LightweightClickScheduler _autoClickScheduler;
    private readonly DispatcherTimer _runtimeTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly bool _isFirstRun;
    private readonly string _currentAppVersion;
    private readonly bool _showStartupNotice;
    private readonly Dictionary<string, string> _pendingBindValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _bindyKeyStates = new(StringComparer.OrdinalIgnoreCase);
    private PvpSettings _settings;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;
    private Drawing.Icon? _trayIconImage;
    private bool _trayHintShown;
    private bool _allowApplicationExit;
    private IntPtr _targetGameWindowHandle;
    private bool _isLoadingUi = true;
    private bool _pendingChanges;
    private bool _autoLeftRuntimeEnabled;
    private bool _autoRightRuntimeEnabled;
    private bool _autoLeftBindWasDown;
    private bool _autoRightBindWasDown;
    private bool _autoLeftComboTriggerWasDown;
    private bool _autoLeftComboStopWasDown;
    private bool _autoRightComboTriggerWasDown;
    private bool _autoRightComboStopWasDown;
    private bool _autoLeftDabHeld;
    private bool _suppressBindToggleUntilRelease;
    private string _activeCaptureTarget = string.Empty;
    private OverlayHudWindow? _overlayHud;
    private DateTime _lastFocusUiRefreshUtc = DateTime.MinValue;
    private DateTime _nextCursorProbeUtc = DateTime.MinValue;
    private DateTime _lastOverlayRefreshUtc = DateTime.MinValue;
    private DateTime _lastOverlayLayoutRefreshUtc = DateTime.MinValue;
    private DateTime _nextRuntimeUiRefreshUtc = DateTime.MinValue;
    private DateTime _nextTargetResolveAtUtc = DateTime.MinValue;
    private DateTime _nextTargetStatusRefreshUtc = DateTime.MinValue;
    private string _lastOverlaySignature = string.Empty;
    private bool _isTargetFocused;
    private DateTime _nextPhysicalMouseTrackerRetryUtc = DateTime.MinValue;
    private bool _physicalMouseTrackerWarningShown;

    private static readonly TimeSpan RuntimeWorkflowInterval = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan RuntimeInputInterval = TimeSpan.FromMilliseconds(15);
    private static readonly TimeSpan RuntimeIdleInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan TargetResolveRetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TargetStatusRefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan OverlayLayoutRefreshInterval = TimeSpan.FromSeconds(2);
    private bool _cachedCursorVisible;
    private volatile bool _schedulerCursorGuardEnabled;
    private DateTime _bindyNextActionAtUtc = DateTime.MinValue;
    private BindyStage _bindyStage;
    private string _bindyPendingCommand = string.Empty;
    private string _bindyPendingName = string.Empty;
    private string _bindyLastName = string.Empty;
    private DateTime _bindyLastAtUtc = DateTime.MinValue;

    // Lightweight PVP build intentionally has no mining/reconnect runtimes. These
    // zero-state fields satisfy the shared, tested AutoArmor guard conditions.
    private InventoryCleanupStage _inventoryCleanupStage = InventoryCleanupStage.None;
    private AutoReconnectStage _autoReconnectStage = AutoReconnectStage.None;
    private bool _testFastUpExitRuntimeEnabled = false;
    private bool _testAutoFishingRuntimeEnabled = false;
    private bool _kopacz533RuntimeEnabled = false;
    private bool _kopacz633RuntimeEnabled = false;

    private enum InventoryCleanupStage { None }
    private enum AutoReconnectStage { None }
    private enum BindyStage { None, OpenChat, TypeCommand, Submit }

    private enum BindTarget
    {
        AutoLeft,
        AutoRight,
        PvpAutoBlock,
        PvpSnowball,
        PvpPearl,
        PvpRod,
        AppleCutter,
        AutoArmor,
        AutoWater,
        ChatOpen
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(out CURSORINFO cursorInfo);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref POINT point);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, char[] text, int maximumCount);

    [DllImport("user32.dll")]
    private static extern bool DrawIconEx(
        IntPtr hdc,
        int xLeft,
        int yTop,
        IntPtr hIcon,
        int width,
        int height,
        uint step,
        IntPtr flickerFreeBrush,
        uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public MainWindow()
    {
        _autoClickScheduler = new LightweightClickScheduler(
            _physicalMouseTracker.IsButtonDown,
            ShouldSuppressScheduledClick);
        InitializeComponent();
        _isFirstRun = !_settingsService.SettingsFileExists;
        _settings = _settingsService.Load();
        NormalizeSettings();
        _currentAppVersion = ReleaseNotesCatalog.CurrentVersion;
        Title = $"Minecraft Helper Auto Clicker {_currentAppVersion}";
        _showStartupNotice = _isFirstRun
            || !string.Equals(
                _settings.LastAcknowledgedVersion,
                _currentAppVersion,
                StringComparison.OrdinalIgnoreCase);

        _runtimeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RuntimeIdleInterval
        };
        _runtimeTimer.Tick += RuntimeTimer_Tick;

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(650)
        };
        _saveTimer.Tick += (_, _) => SavePendingSettings();

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;

        InitializeTrayIcon();

        _isLoadingUi = true;
        try
        {
            PopulateSlotComboBoxes();
            LoadSettingsToUi();
            RefreshProcesses(selectSavedTarget: true);
        }
        finally
        {
            _isLoadingUi = false;
        }

        UpdateModuleVisibility();
        RefreshBindyRows();
        _runtimeTimer.Start();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitleBar();
        Activate();

        if (_showStartupNotice)
        {
            await Dispatcher.InvokeAsync(
                ShowStartupNotice,
                DispatcherPriority.ApplicationIdle);
        }

        await CheckForUpdatesAsync();
    }

    private async System.Threading.Tasks.Task CheckForUpdatesAsync()
    {
        AppUpdateInfo? update = await UpdateCheckService.CheckForUpdateAsync(_currentAppVersion);
        if (update == null || !IsVisible || _allowApplicationExit)
            return;

        var dialog = new UpdateAvailableWindow(update)
        {
            Owner = this
        };
        dialog.ShowDialog();

        if (IsVisible)
            Activate();
    }

    private void ShowStartupNotice()
    {
        if (!IsVisible || _allowApplicationExit)
            return;

        var dialog = new PvpStartupNoticeWindow(
            _isFirstRun,
            _currentAppVersion,
            _settings.LastAcknowledgedVersion)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _settings.LastAcknowledgedVersion = _currentAppVersion;
                _settingsService.Save(_settings);
                _pendingChanges = false;
            }
            catch (Exception exception)
            {
                UpdateStatusBar($"Nie zapisano potwierdzenia wersji: {exception.Message}", "Red");
            }
        }

        if (IsVisible)
            Activate();
    }

    private void InitializeTrayIcon()
    {
        _trayMenu = new System.Windows.Forms.ContextMenuStrip();

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Otwórz Auto Clicker");
        openItem.Click += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
        _trayMenu.Items.Add(openItem);

        _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Zamknij Auto Clicker");
        exitItem.Click += (_, _) => Dispatcher.BeginInvoke(ExitApplication);
        _trayMenu.Items.Add(exitItem);

        try
        {
            var resource = Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/autoclicker.ico", UriKind.Absolute));
            if (resource != null)
            {
                using Stream stream = resource.Stream;
                using var icon = new Drawing.Icon(stream);
                _trayIconImage = (Drawing.Icon)icon.Clone();
            }
        }
        catch
        {
            // Ikona okna nadal pozostaje dostępna, nawet jeśli zasobnik nie odczyta zasobu.
        }

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayIconImage ?? Drawing.SystemIcons.Application,
            Text = "Minecraft Helper Auto Clicker",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(RestoreFromTray);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            return;

        HideToTray();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();

        if (_trayHintShown || _trayIcon == null)
            return;

        _trayHintShown = true;
        _trayIcon.BalloonTipTitle = "Auto Clicker działa w tle";
        _trayIcon.BalloonTipText = "Makra i bindy pozostają aktywne. Kliknij dwukrotnie ikonę, aby otworzyć program.";
        _trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(3000);
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    internal void RestoreFromBackground() => RestoreFromTray();

    internal void AllowApplicationExit() => _allowApplicationExit = true;

    private void ExitApplication()
    {
        _allowApplicationExit = true;
        Close();
    }

    private void ApplyDarkTitleBar()
    {
        IntPtr window = new WindowInteropHelper(this).Handle;
        if (window == IntPtr.Zero)
            return;

        int enabled = 1;
        int attribute = Environment.OSVersion.Version.Build >= 18985
            ? DwmwaUseImmersiveDarkMode
            : DwmwaUseImmersiveDarkModeBefore20H1;
        _ = DwmSetWindowAttribute(window, attribute, ref enabled, sizeof(int));
    }

    private void NormalizeSettings()
    {
        _settings.LastAcknowledgedVersion ??= string.Empty;
        _settings.AutoLeftButton ??= new MacroButton();
        _settings.AutoRightButton ??= new MacroButton();
        _settings.BindyEntries ??= new List<BindyEntry>();
        _settings.ChatOpenKey = NormalizeControlKey(_settings.ChatOpenKey, "T");
        _settings.AutoLeftButton.MinCps = NormalizeCps(_settings.AutoLeftButton.MinCps, 10);
        _settings.AutoLeftButton.MaxCps = NormalizeCps(_settings.AutoLeftButton.MaxCps, 12);
        _settings.AutoRightButton.MinCps = NormalizeCps(_settings.AutoRightButton.MinCps, 10);
        _settings.AutoRightButton.MaxCps = NormalizeCps(_settings.AutoRightButton.MaxCps, 12);
        if (_settings.AutoLeftPhysicalToggleMode)
        {
            _settings.AutoLeftComboMode = false;
            _settings.AutoLeftHoldBindMode = false;
        }
        else if (_settings.AutoLeftComboMode)
        {
            _settings.AutoLeftHoldBindMode = false;
        }
        if (_settings.AutoRightPhysicalToggleMode)
        {
            _settings.AutoRightComboMode = false;
            _settings.AutoRightHoldBindMode = false;
        }
        else if (_settings.AutoRightComboMode)
        {
            _settings.AutoRightHoldBindMode = false;
        }
        if (!string.IsNullOrWhiteSpace(_settings.AutoLeftButton.Key)
            && string.Equals(
                _settings.AutoLeftButton.Key.Trim(),
                _settings.AutoRightButton.Key?.Trim(),
                StringComparison.OrdinalIgnoreCase)
            && !CanShareAutoClickerBind(
                _settings.AutoLeftComboMode,
                _settings.AutoLeftHoldBindMode,
                _settings.AutoRightComboMode))
        {
            _settings.AutoRightButton.Key = string.Empty;
        }
        NormalizePvpQuickActionSettings();
        NormalizeAppleCutterSettings();
        NormalizeAutoArmorSettings();
        NormalizeAutoWaterSettings();
    }

    private static int NormalizeCps(int value, int fallback) => Math.Clamp(value <= 0 ? fallback : value, 1, 100);

    private void LoadSettingsToUi()
    {
        ChkAutoLeftEnabled.IsChecked = _settings.AutoLeftButton.Enabled;
        TxtAutoLeftKey.Text = _settings.AutoLeftButton.Key;
        TxtAutoLeftMinCps.Text = _settings.AutoLeftButton.MinCps.ToString(CultureInfo.InvariantCulture);
        TxtAutoLeftMaxCps.Text = _settings.AutoLeftButton.MaxCps.ToString(CultureInfo.InvariantCulture);
        ChkAutoLeftDabMode.IsChecked = _settings.AutoLeftDabMode;
        CbAutoLeftMode.SelectedIndex = GetSavedClickerModeIndex(
            _settings.AutoLeftPhysicalToggleMode,
            _settings.AutoLeftComboMode,
            _settings.AutoLeftHoldBindMode);

        ChkAutoRightEnabled.IsChecked = _settings.AutoRightButton.Enabled;
        TxtAutoRightKey.Text = _settings.AutoRightButton.Key;
        TxtAutoRightMinCps.Text = _settings.AutoRightButton.MinCps.ToString(CultureInfo.InvariantCulture);
        TxtAutoRightMaxCps.Text = _settings.AutoRightButton.MaxCps.ToString(CultureInfo.InvariantCulture);
        CbAutoRightMode.SelectedIndex = GetSavedClickerModeIndex(
            _settings.AutoRightPhysicalToggleMode,
            _settings.AutoRightComboMode,
            _settings.AutoRightHoldBindMode);

        ChkPauseWhenCursorVisible.IsChecked = _settings.PauseWhenCursorVisible;
        _schedulerCursorGuardEnabled = _settings.PauseWhenCursorVisible;
        ChkOverlayEnabled.IsChecked = _settings.OverlayHudEnabled;
        ChkBindyEnabled.IsChecked = _settings.BindyEnabled;
        TxtChatOpenKey.Text = _settings.ChatOpenKey;
        LoadPvpQuickActionsToUi();
        LoadAppleCutterToUi();
        LoadAutoArmorToUi();
        LoadAutoWaterToUi();
    }

    private void ReadUiToSettings()
    {
        _settings.AutoLeftButton.Enabled = ChkAutoLeftEnabled.IsChecked == true;
        _settings.AutoLeftButton.Key = TxtAutoLeftKey.Text.Trim();
        _settings.AutoLeftButton.MinCps = ReadCps(TxtAutoLeftMinCps.Text, 10);
        _settings.AutoLeftButton.MaxCps = ReadCps(TxtAutoLeftMaxCps.Text, 12);
        _settings.AutoLeftComboMode = GetSelectedMode(CbAutoLeftMode) == "Combo";
        _settings.AutoLeftHoldBindMode = GetSelectedMode(CbAutoLeftMode) == "Hold";
        _settings.AutoLeftPhysicalToggleMode = GetSelectedMode(CbAutoLeftMode) == "PhysicalToggle";
        _settings.AutoLeftDabMode = ChkAutoLeftDabMode.IsChecked == true;

        _settings.AutoRightButton.Enabled = ChkAutoRightEnabled.IsChecked == true;
        _settings.AutoRightButton.Key = TxtAutoRightKey.Text.Trim();
        _settings.AutoRightButton.MinCps = ReadCps(TxtAutoRightMinCps.Text, 10);
        _settings.AutoRightButton.MaxCps = ReadCps(TxtAutoRightMaxCps.Text, 12);
        _settings.AutoRightComboMode = GetSelectedMode(CbAutoRightMode) == "Combo";
        _settings.AutoRightHoldBindMode = GetSelectedMode(CbAutoRightMode) == "Hold";
        _settings.AutoRightPhysicalToggleMode = GetSelectedMode(CbAutoRightMode) == "PhysicalToggle";
        _settings.PauseWhenCursorVisible = ChkPauseWhenCursorVisible.IsChecked == true;
        _settings.OverlayHudEnabled = ChkOverlayEnabled.IsChecked == true;
        _settings.BindyEnabled = ChkBindyEnabled.IsChecked == true;
        _settings.ChatOpenKey = NormalizeControlKey(TxtChatOpenKey.Text, "T");
        ReadPvpQuickActionsFromUi();
        ReadAppleCutterFromUi();
        ReadAutoArmorFromUi();
        ReadAutoWaterFromUi();
    }

    private static int ReadCps(string text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Clamp(value, 1, 100)
            : fallback;
    }

    private static string GetSelectedMode(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Toggle";
    }

    private static int GetSavedClickerModeIndex(bool physicalToggleMode, bool comboMode, bool holdMode)
    {
        return physicalToggleMode ? 3 : comboMode ? 2 : holdMode ? 1 : 0;
    }

    private static bool UsesPhysicalPointerGate(string mode)
    {
        return mode is "Combo" or "PhysicalToggle";
    }

    private static bool CanShareAutoClickerBind(bool leftComboMode, bool leftHoldMode, bool rightComboMode)
    {
        return (leftComboMode || leftHoldMode) && rightComboMode;
    }

    private bool CanShareAutoClickerBind()
    {
        string leftMode = GetSelectedMode(CbAutoLeftMode);
        return CanShareAutoClickerBind(
            leftMode == "Combo",
            leftMode == "Hold",
            GetSelectedMode(CbAutoRightMode) == "Combo");
    }

    private void MarkDirty()
    {
        if (_isLoadingUi)
            return;

        _pendingChanges = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SavePendingSettings()
    {
        _saveTimer.Stop();
        if (!_pendingChanges)
            return;

        ReadUiToSettings();
        try
        {
            _settingsService.Save(_settings);
            _pendingChanges = false;
        }
        catch (Exception exception)
        {
            UpdateStatusBar($"Nie zapisano ustawień: {exception.Message}", "Red");
        }
    }

    private void RuntimeText_Changed(object sender, TextChangedEventArgs e) => MarkDirty();

    private void RuntimeSetting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingUi)
            return;

        bool sharedClickerBind = !string.IsNullOrWhiteSpace(TxtAutoLeftKey.Text)
            && string.Equals(
                TxtAutoLeftKey.Text.Trim(),
                TxtAutoRightKey.Text.Trim(),
                StringComparison.OrdinalIgnoreCase);
        if (sharedClickerBind && !CanShareAutoClickerBind())
        {
            _isLoadingUi = true;
            try
            {
                CbAutoLeftMode.SelectedIndex = GetSavedClickerModeIndex(
                    _settings.AutoLeftPhysicalToggleMode,
                    _settings.AutoLeftComboMode,
                    _settings.AutoLeftHoldBindMode);
                CbAutoRightMode.SelectedIndex = GetSavedClickerModeIndex(
                    _settings.AutoRightPhysicalToggleMode,
                    _settings.AutoRightComboMode,
                    _settings.AutoRightHoldBindMode);
            }
            finally
            {
                _isLoadingUi = false;
            }

            var dialog = BindConflictDialogWindow.CreateSharedAutoClickerModeConflict(TxtAutoLeftKey.Text);
            dialog.Owner = this;
            _ = dialog.ShowDialog();
            return;
        }

        if (ReferenceEquals(sender, CbAutoLeftMode))
        {
            _autoLeftRuntimeEnabled = false;
            _autoClickScheduler.StopLeftImmediately();
            SynchronizeComboState(
                TxtAutoLeftKey.Text,
                VkLeftButton,
                ref _autoLeftComboTriggerWasDown,
                ref _autoLeftComboStopWasDown);
        }
        else if (ReferenceEquals(sender, CbAutoRightMode))
        {
            _autoRightRuntimeEnabled = false;
            _autoClickScheduler.StopRightImmediately();
            SynchronizeComboState(
                TxtAutoRightKey.Text,
                VkRightButton,
                ref _autoRightComboTriggerWasDown,
                ref _autoRightComboStopWasDown);
        }

        UpdatePhysicalMouseTracking(DateTime.UtcNow);
        MarkDirty();
    }

    private void RuntimeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, ChkPauseWhenCursorVisible))
            _schedulerCursorGuardEnabled = ChkPauseWhenCursorVisible.IsChecked == true;

        if (!_isLoadingUi)
        {
            MarkDirty();
            RefreshOverlayHud(DateTime.UtcNow);
        }
    }

    private void ChkAutoLeftEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi)
            return;

        if (ChkAutoLeftEnabled.IsChecked != true)
        {
            _autoLeftRuntimeEnabled = false;
            _autoClickScheduler.StopLeftImmediately();
            SetAutoLeftDabHold(false);
            _autoLeftComboTriggerWasDown = false;
            _autoLeftComboStopWasDown = false;
        }
        UpdateModuleVisibility();
        UpdatePhysicalMouseTracking(DateTime.UtcNow);
        MarkDirty();
    }

    private void ChkAutoRightEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingUi)
            return;

        if (ChkAutoRightEnabled.IsChecked != true)
        {
            _autoRightRuntimeEnabled = false;
            _autoClickScheduler.StopRightImmediately();
            _autoRightComboTriggerWasDown = false;
            _autoRightComboStopWasDown = false;
        }
        UpdateModuleVisibility();
        UpdatePhysicalMouseTracking(DateTime.UtcNow);
        MarkDirty();
    }

    private void UpdateModuleVisibility()
    {
        if (PanelAutoLeftContent != null)
            PanelAutoLeftContent.Visibility = ChkAutoLeftEnabled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (PanelAutoRightContent != null)
            PanelAutoRightContent.Visibility = ChkAutoRightEnabled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateAutoArmorEnabledState();
        UpdateAutoWaterEnabledState();
        UpdateAppleCutterEnabledState();
    }

    private static void SetExpandableSectionState(FrameworkElement panel, bool enabled)
    {
        panel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e) => RefreshProcesses(selectSavedTarget: false);

    private void BtnSaveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (CbTargetProcess.SelectedItem is not WindowCandidate candidate || !IsWindow(candidate.WindowHandle))
        {
            UpdateStatusBar("Wybierz działające okno Minecrafta.", "Red");
            return;
        }

        _targetGameWindowHandle = candidate.WindowHandle;
        _settings.TargetProcessId = candidate.ProcessId;
        _settings.TargetProcessName = candidate.ProcessName;
        _settings.TargetWindowTitle = candidate.WindowTitle;
        MarkDirty();
        SavePendingSettings();
        UpdateStatusBar("Zapisano okno Minecrafta.", "Green");
        RefreshTargetStatus();
    }

    private void RefreshProcesses(bool selectSavedTarget)
    {
        List<WindowCandidate> candidates = GetMinecraftWindowCandidates();

        CbTargetProcess.ItemsSource = candidates;
        WindowCandidate? selected = null;
        if (selectSavedTarget)
        {
            selected = candidates.FirstOrDefault(candidate => candidate.ProcessId == _settings.TargetProcessId)
                ?? candidates.FirstOrDefault(candidate => string.Equals(candidate.WindowTitle, _settings.TargetWindowTitle, StringComparison.OrdinalIgnoreCase));
        }
        selected ??= candidates.Count == 1 ? candidates[0] : null;
        CbTargetProcess.SelectedItem = selected;
        if (selected != null && selectSavedTarget)
            _targetGameWindowHandle = selected.WindowHandle;
        RefreshTargetStatus();
    }

    private static List<WindowCandidate> GetMinecraftWindowCandidates()
    {
        return Process.GetProcesses()
            .Select(TryCreateMinecraftWindowCandidate)
            .Where(candidate => candidate != null)
            .Cast<WindowCandidate>()
            .OrderBy(candidate => candidate.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static WindowCandidate? TryCreateMinecraftWindowCandidate(Process process)
    {
        try
        {
            IntPtr handle = process.MainWindowHandle;
            string title = process.MainWindowTitle?.Trim() ?? string.Empty;
            if (handle == IntPtr.Zero || string.IsNullOrWhiteSpace(title))
                return null;

            bool looksLikeMinecraft = process.ProcessName.Equals("java", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("javaw", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Contains("minecraft", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Contains("blazing", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Contains("lunar", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Contains("badlion", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Minecraft ", StringComparison.OrdinalIgnoreCase)
                || title.StartsWith("Minecraft", StringComparison.OrdinalIgnoreCase)
                || title.Contains("BlazingPack", StringComparison.OrdinalIgnoreCase);
            return looksLikeMinecraft
                ? new WindowCandidate(process.Id, process.ProcessName, title, handle)
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            process.Dispose();
        }
    }

    private void TryRestoreSavedTargetWindow(DateTime now)
    {
        if (_targetGameWindowHandle != IntPtr.Zero && IsWindow(_targetGameWindowHandle))
            return;
        if (_settings.TargetProcessId <= 0 && string.IsNullOrWhiteSpace(_settings.TargetWindowTitle))
            return;
        if (now < _nextTargetResolveAtUtc)
            return;

        _targetGameWindowHandle = IntPtr.Zero;
        _nextTargetResolveAtUtc = now.Add(TargetResolveRetryInterval);
        List<WindowCandidate> candidates = GetMinecraftWindowCandidates();
        WindowCandidate? selected = candidates.FirstOrDefault(candidate => candidate.ProcessId == _settings.TargetProcessId)
            ?? candidates.FirstOrDefault(candidate => string.Equals(
                candidate.WindowTitle,
                _settings.TargetWindowTitle,
                StringComparison.OrdinalIgnoreCase))
            ?? (candidates.Count == 1 ? candidates[0] : null);
        if (selected == null)
            return;

        _targetGameWindowHandle = selected.WindowHandle;
        CbTargetProcess.ItemsSource = candidates;
        CbTargetProcess.SelectedItem = selected;
        RefreshTargetStatus();
    }

    private void RefreshTargetStatus()
    {
        if (_targetGameWindowHandle == IntPtr.Zero || !IsWindow(_targetGameWindowHandle))
        {
            _targetGameWindowHandle = IntPtr.Zero;
            TxtWindowStatus.Text = "Nie wybrano aktywnego okna Minecrafta";
            TxtWindowStatus.Foreground = Brushes.Orange;
            return;
        }

        TxtWindowStatus.Text = GetWindowTitle(_targetGameWindowHandle);
        TxtWindowStatus.Foreground = Brushes.MediumSpringGreen;
    }

    private static string GetWindowTitle(IntPtr window)
    {
        char[] buffer = new char[512];
        int length = GetWindowText(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : "Minecraft";
    }

    private void BindBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not string target)
            return;
        BeginBindCapture(target);
        e.Handled = true;
    }

    private void BeginBindCapture(string target)
    {
        _activeCaptureTarget = target;
        UpdateStatusBar("Naciśnij wybrany klawisz lub dodatkowy przycisk myszy…", "Orange");
        Activate();
        Focus();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeCaptureTarget))
            return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _activeCaptureTarget = string.Empty;
            UpdateStatusBar("Anulowano wybór klawisza.", "Orange");
            e.Handled = true;
            return;
        }

        string display = key == Key.Return ? "Enter" : key.ToString();
        StorePendingBind(_activeCaptureTarget, display);
        e.Handled = true;
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeCaptureTarget))
            return;

        string? display = e.ChangedButton switch
        {
            MouseButton.Middle => "MouseMiddle",
            MouseButton.XButton1 => "MouseX1",
            MouseButton.XButton2 => "MouseX2",
            _ => null
        };
        if (display == null)
            return;

        StorePendingBind(_activeCaptureTarget, display);
        e.Handled = true;
    }

    private void StorePendingBind(string target, string keyText)
    {
        _pendingBindValues[target] = keyText;
        _activeCaptureTarget = string.Empty;
        SetBindText(target, keyText);
        UpdateStatusBar($"Wybrano {keyText}. Kliknij Zapisz.", "Orange");
    }

    private void BtnSaveBind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
            return;
        ConfirmPendingBind(ParseBindTarget(target));
    }

    private void ConfirmPendingBind(BindTarget target)
    {
        string targetName = target.ToString();
        if (!_pendingBindValues.TryGetValue(targetName, out string? keyText))
            keyText = GetBindText(targetName);
        if (string.IsNullOrWhiteSpace(keyText))
        {
            UpdateStatusBar("Najpierw wybierz klawisz.", "Orange");
            return;
        }
        if (HasBindConflict(targetName, keyText, out string owner))
        {
            ShowBindConflict(targetName, keyText, owner);
            return;
        }

        SetBindText(targetName, keyText);
        _pendingBindValues.Remove(targetName);
        MarkDirty();
        SavePendingSettings();
        UpdateStatusBar($"Zapisano bind {keyText}.", "Green");
    }

    private void BtnClearBind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
            return;
        SetBindText(target, string.Empty);
        _pendingBindValues.Remove(target);
        if (target == "AutoLeft")
        {
            _autoLeftRuntimeEnabled = false;
            _autoClickScheduler.StopLeftImmediately();
            _autoLeftComboTriggerWasDown = false;
            _autoLeftComboStopWasDown = false;
        }
        if (target == "AutoRight")
        {
            _autoRightRuntimeEnabled = false;
            _autoClickScheduler.StopRightImmediately();
            _autoRightComboTriggerWasDown = false;
            _autoRightComboStopWasDown = false;
        }
        if (target == "AutoArmor" && IsAutoArmorRunning)
            RequestCancelAutoArmor("Usunięto bind — bezpiecznie kończę bieżący element.");
        if (target == "AutoWater" && _autoWaterStage != AutoWaterStage.None)
            CancelAutoWater("Usunięto bind AutoWater.", Brushes.Orange, restoreSlot: true);
        if (target == "AppleCutter" && IsAppleCutterRunning)
            CancelAppleCutter("Usunięto bind — ścinanie jabłek zatrzymane.", Brushes.Orange);
        MarkDirty();
        UpdateStatusBar("Usunięto bind.", "Orange");
    }

    private static BindTarget ParseBindTarget(string target)
    {
        return Enum.TryParse(target, true, out BindTarget parsed) ? parsed : BindTarget.AutoLeft;
    }

    private string GetBindText(string target)
    {
        return target switch
        {
            "AutoLeft" => TxtAutoLeftKey.Text,
            "AutoRight" => TxtAutoRightKey.Text,
            "PvpAutoBlock" => TxtPvpAutoBlockBind.Text,
            "PvpSnowball" => TxtPvpSnowballBind.Text,
            "PvpPearl" => TxtPvpPearlBind.Text,
            "PvpRod" => TxtPvpRodBind.Text,
            "AppleCutter" => TxtAppleCutterBind.Text,
            "AutoArmor" => TxtAutoArmorBind.Text,
            "AutoWater" => TxtAutoWaterBind.Text,
            "ChatOpen" => TxtChatOpenKey.Text,
            _ when target.StartsWith("Bindy:", StringComparison.OrdinalIgnoreCase) =>
                _settings.BindyEntries.FirstOrDefault(entry => entry.Id == target[6..])?.Key ?? string.Empty,
            _ => string.Empty
        };
    }

    private void SetBindText(string target, string keyText)
    {
        switch (target)
        {
            case "AutoLeft": TxtAutoLeftKey.Text = keyText; break;
            case "AutoRight": TxtAutoRightKey.Text = keyText; break;
            case "PvpAutoBlock": TxtPvpAutoBlockBind.Text = keyText; break;
            case "PvpSnowball": TxtPvpSnowballBind.Text = keyText; break;
            case "PvpPearl": TxtPvpPearlBind.Text = keyText; break;
            case "PvpRod": TxtPvpRodBind.Text = keyText; break;
            case "AppleCutter": TxtAppleCutterBind.Text = keyText; break;
            case "AutoArmor": TxtAutoArmorBind.Text = keyText; break;
            case "AutoWater": TxtAutoWaterBind.Text = keyText; break;
            case "ChatOpen": TxtChatOpenKey.Text = keyText; break;
            default:
                if (target.StartsWith("Bindy:", StringComparison.OrdinalIgnoreCase))
                    RefreshBindyRows();
                break;
        }
    }

    private bool HasBindConflict(string target, string key, out string owner)
    {
        owner = string.Empty;
        var assigned = new List<(string Target, string Key, string Label)>
        {
            ("AutoLeft", TxtAutoLeftKey.Text, "AUTO LPM"),
            ("AutoRight", TxtAutoRightKey.Text, "AUTO PPM"),
            ("PvpAutoBlock", TxtPvpAutoBlockBind.Text, "Auto blok"),
            ("PvpSnowball", TxtPvpSnowballBind.Text, "Śnieżka"),
            ("PvpPearl", TxtPvpPearlBind.Text, "Perła"),
            ("PvpRod", TxtPvpRodBind.Text, "Wędka"),
            ("AppleCutter", TxtAppleCutterBind.Text, "Ścinanie jabłek"),
            ("AutoArmor", TxtAutoArmorBind.Text, "Auto zbroja"),
            ("AutoWater", TxtAutoWaterBind.Text, "AutoWater"),
            ("ChatOpen", TxtChatOpenKey.Text, "otwieranie chatu")
        };
        assigned.AddRange(_settings.BindyEntries.Select(entry => ($"Bindy:{entry.Id}", entry.Key, $"BINDY: {GetBindyLabel(entry)}")));

        foreach ((string otherTarget, string otherKey, string label) in assigned)
        {
            if (string.Equals(otherTarget, target, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(otherKey?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // One key can drive LPM in Hold/Combo and PPM in Combo. PPM Hold always
            // needs its own key so releasing it cannot accidentally affect AUTO LPM.
            if ((target == "AutoLeft" && otherTarget == "AutoRight" || target == "AutoRight" && otherTarget == "AutoLeft")
                && CanShareAutoClickerBind())
            {
                continue;
            }

            owner = label;
            return true;
        }
        return false;
    }

    private void ShowBindConflict(string target, string keyText, string conflictOwner)
    {
        string requestedOwner = target switch
        {
            "AutoLeft" => "AUTO LPM",
            "AutoRight" => "AUTO PPM",
            "PvpAutoBlock" => "Auto blok",
            "PvpSnowball" => "Śnieżka",
            "PvpPearl" => "Perła",
            "PvpRod" => "Wędka",
            "AutoArmor" => "Auto zbroja",
            "AutoWater" => "AutoWater",
            "ChatOpen" => "otwieranie chatu",
            _ when target.StartsWith("Bindy:", StringComparison.OrdinalIgnoreCase) => "BINDY",
            _ => "wybranej funkcji"
        };
        var dialog = new BindConflictDialogWindow(requestedOwner, keyText, conflictOwner)
        {
            Owner = this
        };
        _ = dialog.ShowDialog();
    }

    private void BtnAddBindy_Click(object sender, RoutedEventArgs e)
    {
        _settings.BindyEntries.Add(new BindyEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Enabled = true,
            Name = $"Bind {_settings.BindyEntries.Count + 1}",
            Command = "/"
        });
        RefreshBindyRows();
        MarkDirty();
    }

    private void RefreshBindyRows()
    {
        if (PanelBindyCommands == null)
            return;
        PanelBindyCommands.Children.Clear();
        foreach (BindyEntry entry in _settings.BindyEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
                entry.Id = Guid.NewGuid().ToString("N");
            PanelBindyCommands.Children.Add(BuildBindyCard(entry));
        }
    }

    private Border BuildBindyCard(BindyEntry entry)
    {
        var panel = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var enabled = new CheckBox { IsChecked = entry.Enabled, Content = "Aktywny" };
        enabled.Checked += (_, _) => { entry.Enabled = true; MarkDirty(); };
        enabled.Unchecked += (_, _) => { entry.Enabled = false; MarkDirty(); };
        header.Children.Add(enabled);
        var remove = new Button { Content = "Usuń", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(9, 3, 9, 3) };
        DockPanel.SetDock(remove, Dock.Right);
        remove.Click += (_, _) =>
        {
            _settings.BindyEntries.Remove(entry);
            _bindyKeyStates.Remove(entry.Id);
            RefreshBindyRows();
            MarkDirty();
        };
        header.Children.Add(remove);
        panel.Children.Add(header);

        var nameGrid = CreateTwoColumnGrid("Nazwa:");
        var nameBox = new TextBox { Text = entry.Name };
        nameBox.TextChanged += (_, _) => { entry.Name = nameBox.Text; MarkDirty(); };
        Grid.SetColumn(nameBox, 1);
        nameGrid.Children.Add(nameBox);
        panel.Children.Add(nameGrid);

        string target = $"Bindy:{entry.Id}";
        var bindGrid = new Grid { Margin = new Thickness(0, 7, 0, 7) };
        bindGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        bindGrid.ColumnDefinitions.Add(new ColumnDefinition());
        bindGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bindGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bindGrid.Children.Add(new TextBlock { Text = "Bind:", VerticalAlignment = VerticalAlignment.Center });
        string displayedKey = _pendingBindValues.TryGetValue(target, out string? pendingKey)
            ? pendingKey
            : entry.Key;
        var bindBox = new TextBox { Text = displayedKey, IsReadOnly = true, Tag = target };
        bindBox.PreviewMouseLeftButtonDown += BindBox_PreviewMouseLeftButtonDown;
        Grid.SetColumn(bindBox, 1);
        bindGrid.Children.Add(bindBox);
        var save = new Button { Content = "Zapisz", Tag = target, Margin = new Thickness(7, 0, 0, 0) };
        Grid.SetColumn(save, 2);
        save.Click += (_, _) => ConfirmPendingBindy(entry);
        bindGrid.Children.Add(save);
        var clear = new Button { Content = "Usuń", Margin = new Thickness(7, 0, 0, 0) };
        Grid.SetColumn(clear, 3);
        clear.Click += (_, _) => { entry.Key = string.Empty; _pendingBindValues.Remove(target); RefreshBindyRows(); MarkDirty(); };
        bindGrid.Children.Add(clear);
        panel.Children.Add(bindGrid);

        var commandGrid = CreateTwoColumnGrid("Komenda:");
        var commandBox = new TextBox { Text = entry.Command };
        commandBox.TextChanged += (_, _) => { entry.Command = commandBox.Text; MarkDirty(); };
        Grid.SetColumn(commandBox, 1);
        commandGrid.Children.Add(commandBox);
        panel.Children.Add(commandGrid);

        return new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Child = panel
        };
    }

    private static Grid CreateTwoColumnGrid(string label)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        return grid;
    }

    private void ConfirmPendingBindy(BindyEntry entry)
    {
        string target = $"Bindy:{entry.Id}";
        if (!_pendingBindValues.TryGetValue(target, out string? keyText) || string.IsNullOrWhiteSpace(keyText))
        {
            UpdateStatusBar("Najpierw wybierz klawisz dla tego wiersza.", "Orange");
            return;
        }
        if (HasBindConflict(target, keyText, out string owner))
        {
            ShowBindConflict(target, keyText, owner);
            return;
        }
        entry.Key = keyText;
        _pendingBindValues.Remove(target);
        RefreshBindyRows();
        MarkDirty();
        UpdateStatusBar($"Zapisano bind {keyText} dla {GetBindyLabel(entry)}.", "Green");
    }

    private static string GetBindyLabel(BindyEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Name) ? "bez nazwy" : entry.Name.Trim();
    }

    private void RuntimeTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            RuntimeTimerTickCore();
        }
        finally
        {
            UpdateRuntimeTimerCadence();
        }
    }

    private void RuntimeTimerTickCore()
    {
        DateTime now = DateTime.UtcNow;
        TryRestoreSavedTargetWindow(now);
        bool targetFocused = _targetGameWindowHandle != IntPtr.Zero
            && IsWindow(_targetGameWindowHandle)
            && GetForegroundWindow() == _targetGameWindowHandle;
        _isTargetFocused = targetFocused;
        bool cursorGuardEnabled = ChkPauseWhenCursorVisible.IsChecked == true;
        _schedulerCursorGuardEnabled = cursorGuardEnabled;
        bool quickActionCursorGuardRequired = IsPvpQuickActionRunning
            || (IsAutoLeftClickerRuntimeActive() && HasAnyConfiguredPvpQuickActionBind());
        bool appleCutterCursorGuardRequired = IsAppleCutterRunning;
        bool shouldProbeCursor = targetFocused
            && (cursorGuardEnabled || quickActionCursorGuardRequired || appleCutterCursorGuardRequired);
        if (!shouldProbeCursor)
        {
            _cachedCursorVisible = false;
            _nextCursorProbeUtc = DateTime.MinValue;
        }
        else if (now >= _nextCursorProbeUtc)
        {
            _cachedCursorVisible = IsInventoryCursorVisible();
            _nextCursorProbeUtc = now + RuntimeInputInterval;
        }
        bool cursorVisible = shouldProbeCursor && _cachedCursorVisible;

        if ((now - _lastFocusUiRefreshUtc).TotalMilliseconds >= 250)
        {
            _lastFocusUiRefreshUtc = now;
            TxtFocus.Text = targetFocused ? "Minecraft: fokus" : "Minecraft: brak fokusu";
            TxtFocus.Foreground = targetFocused ? Brushes.MediumSpringGreen : Brushes.OrangeRed;
            if (now >= _nextTargetStatusRefreshUtc)
            {
                _nextTargetStatusRefreshUtc = now.Add(TargetStatusRefreshInterval);
                RefreshTargetStatus();
            }
        }

        if (TryHandleAutoArmorCalibration(now) || TryHandleAutoWaterCalibrationOrTest(now))
        {
            if (IsAppleCutterRunning)
                CancelAppleCutter("Ścinanie jabłek przerwane przez kalibrację innej funkcji.", Brushes.OrangeRed);
            UpdateClickScheduler(false, false, 0, 0);
            UpdatePhysicalMouseTracking(now);
            RefreshOverlayHud(now);
            return;
        }

        if (!targetFocused)
        {
            UpdateClickScheduler(false, false, 0, 0);
            SetAutoLeftDabHold(false);
            SynchronizeBindStatesWhileGameIsInactive();
            UpdatePhysicalMouseTracking(now);
            if (_bindyStage != BindyStage.None)
            {
                ResetBindyStage();
                UpdateStatusBar("BINDY: anulowano po utracie fokusu Minecrafta.", "Orange");
            }
            if (IsAutoArmorRunning)
                CancelAutoArmor("Auto zbroja przerwana po utracie fokusu. Sprawdź EQ.", Brushes.OrangeRed, closeInventory: false);
            if (_autoWaterStage != AutoWaterStage.None)
                CancelAutoWater("AutoWater przerwany po utracie fokusu.", Brushes.OrangeRed, restoreSlot: false);
            if (IsPvpQuickActionRunning)
                CancelPvpQuickAction("Akcja PVP przerwana po utracie fokusu Minecrafta.", Brushes.OrangeRed, restoreSword: false);
            if (IsAppleCutterRunning)
                CancelAppleCutter("Ścinanie jabłek przerwane po utracie fokusu — zwolniono LPM i PPM.", Brushes.OrangeRed);
            RefreshClickerStateUi(now, blockedByCursor: false, targetFocused: false);
            RefreshOverlayHud(now);
            return;
        }

        bool blockedByCursor = ChkPauseWhenCursorVisible.IsChecked == true && cursorVisible;
        if (_suppressBindToggleUntilRelease)
        {
            bool anyBindDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text)
                || IsConfiguredBindKeyDown(TxtAutoRightKey.Text)
                || IsConfiguredBindKeyDown(TxtPvpAutoBlockBind.Text)
                || IsConfiguredBindKeyDown(TxtPvpSnowballBind.Text)
                || IsConfiguredBindKeyDown(TxtPvpPearlBind.Text)
                || IsConfiguredBindKeyDown(TxtPvpRodBind.Text)
                || IsConfiguredBindKeyDown(TxtAppleCutterBind.Text)
                || IsConfiguredBindKeyDown(TxtAutoArmorBind.Text)
                || IsConfiguredBindKeyDown(TxtAutoWaterBind.Text)
                || IsVirtualKeyDown(VK_LCONTROL);
            if (anyBindDown)
                return;
            _suppressBindToggleUntilRelease = false;
        }

        if (_autoWaterStage != AutoWaterStage.None)
        {
            // Water owns slot selection and PPM until it finishes. Do not allow
            // clickers, item binds or chat commands to interrupt its timing.
            bool cancelWater = IsBindPressed(TxtAutoWaterBind.Text, ref _autoWaterBindWasDown);
            UpdateClickScheduler(false, false, 0, 0);
            SetAutoLeftDabHold(false);
            SynchronizeBindStatesWhileGameIsInactive();
            UpdatePhysicalMouseTracking(now);
            if (cancelWater)
                CancelAutoWater("AutoWater anulowany bindem.", Brushes.Orange, restoreSlot: true);
            else
                RunAutoWaterTick(now);
            RefreshClickerStateUi(now, blockedByCursor, targetFocused: true);
            RefreshOverlayHud(now);
            return;
        }

        HandleAppleCutterBind(now, cursorVisible);
        if (IsAppleCutterRunning)
        {
            UpdateClickScheduler(false, false, 0, 0);
            SetAutoLeftDabHold(false);
            UpdatePhysicalMouseTracking(now);
            if (cursorVisible && !IsAppleCutterRepairStage)
                CancelAppleCutter("Ścinanie jabłek przerwane: wykryto otwarty chat, ekwipunek lub inne GUI.", Brushes.OrangeRed);
            else
                RunAppleCutterTick(now);
            RefreshClickerStateUi(now, blockedByCursor, targetFocused: true);
            RefreshOverlayHud(now);
            return;
        }

        HandleClickerRuntime(blockedByCursor);
        HandlePvpQuickActionBinds(now, cursorVisible);
        UpdatePhysicalMouseTracking(now);
        if (IsPvpQuickActionRunning)
        {
            KeepAutoLeftClickerRunningDuringPvpAction(blockedByCursor);
            RunPvpQuickAction(now);
            RefreshClickerStateUi(now, blockedByCursor, targetFocused: true);
            RefreshOverlayHud(now);
            return;
        }

        HandleExperimentalBinds(now, blockedByCursor);
        if (_autoWaterStage == AutoWaterStage.None && !IsAutoArmorRunning)
            HandleBindyRuntime(now, blockedByCursor);

        if (IsAutoArmorRunning)
        {
            UpdateClickScheduler(false, false, 0, 0);
            RunAutoArmorTick(now);
        }
        else if (_autoWaterStage != AutoWaterStage.None)
        {
            UpdateClickScheduler(false, false, 0, 0);
            RunAutoWaterTick(now);
        }
        else
        {
            string leftMode = GetSelectedMode(CbAutoLeftMode);
            string rightMode = GetSelectedMode(CbAutoRightMode);
            int leftHoldKey = leftMode == "Hold" && TryGetVirtualKey(TxtAutoLeftKey.Text, out int leftKey) ? leftKey : 0;
            int rightHoldKey = rightMode == "Hold" && TryGetVirtualKey(TxtAutoRightKey.Text, out int rightKey) ? rightKey : 0;
            int leftPointerKey = UsesPhysicalPointerGate(leftMode) ? VkLeftButton : 0;
            int rightPointerKey = UsesPhysicalPointerGate(rightMode) ? VkRightButton : 0;
            bool leftPointerReady = leftPointerKey == 0 || _physicalMouseTracker.IsButtonDown(leftPointerKey);
            bool rightPointerReady = rightPointerKey == 0 || _physicalMouseTracker.IsButtonDown(rightPointerKey);
            UpdateClickScheduler(
                !blockedByCursor && leftPointerReady && _autoLeftRuntimeEnabled && ChkAutoLeftEnabled.IsChecked == true,
                !blockedByCursor && rightPointerReady && _autoRightRuntimeEnabled && ChkAutoRightEnabled.IsChecked == true,
                leftHoldKey,
                rightHoldKey,
                leftPointerKey,
                rightPointerKey);
        }

        string currentLeftMode = GetSelectedMode(CbAutoLeftMode);
        bool leftDabPointerReady = !UsesPhysicalPointerGate(currentLeftMode)
            || _physicalMouseTracker.IsButtonDown(VkLeftButton);
        SetAutoLeftDabHold(
            !blockedByCursor
            && leftDabPointerReady
            && _autoLeftRuntimeEnabled
            && ChkAutoLeftDabMode.IsChecked == true);
        RefreshClickerStateUi(now, blockedByCursor, targetFocused: true);
        RefreshOverlayHud(now);
    }

    private void UpdateRuntimeTimerCadence()
    {
        bool workflowActive = _isTargetFocused && (IsAutoArmorRunning
            || _autoArmorCalibrationPending
            || _autoWaterStage != AutoWaterStage.None
            || _autoWaterCalibrationPending
            || _autoWaterRecognitionTestPending
            || IsAppleCutterRunning
            || IsPvpQuickActionRunning
            || _bindyStage != BindyStage.None);
        bool inputPollingRequired = _isTargetFocused
            && (ChkAutoLeftEnabled.IsChecked == true
                || ChkAutoRightEnabled.IsChecked == true
                || ChkBindyEnabled.IsChecked == true
                || ChkAppleCutterEnabled.IsChecked == true
                || ChkAutoArmorEnabled.IsChecked == true
                || ChkAutoWaterEnabled.IsChecked == true);
        TimeSpan interval = workflowActive
            ? RuntimeWorkflowInterval
            : inputPollingRequired
                ? RuntimeInputInterval
                : RuntimeIdleInterval;
        if (_runtimeTimer.Interval != interval)
            _runtimeTimer.Interval = interval;
    }

    private void SynchronizeBindStatesWhileGameIsInactive()
    {
        _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
        _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
        SynchronizeComboState(
            TxtAutoLeftKey.Text,
            VkLeftButton,
            ref _autoLeftComboTriggerWasDown,
            ref _autoLeftComboStopWasDown);
        SynchronizeComboState(
            TxtAutoRightKey.Text,
            VkRightButton,
            ref _autoRightComboTriggerWasDown,
            ref _autoRightComboStopWasDown);
        _autoArmorBindWasDown = IsConfiguredBindKeyDown(TxtAutoArmorBind.Text);
        _autoWaterBindWasDown = IsConfiguredBindKeyDown(TxtAutoWaterBind.Text);
        _appleCutterBindWasDown = IsConfiguredBindKeyDown(TxtAppleCutterBind.Text);
        SynchronizePvpQuickActionBindStates();

        if (GetSelectedMode(CbAutoLeftMode) is not ("Toggle" or "PhysicalToggle"))
            _autoLeftRuntimeEnabled = false;
        if (GetSelectedMode(CbAutoRightMode) is not ("Toggle" or "PhysicalToggle"))
            _autoRightRuntimeEnabled = false;

        foreach (BindyEntry entry in _settings.BindyEntries)
            _bindyKeyStates[entry.Id] = IsConfiguredBindKeyDown(entry.Key);
    }

    private void RefreshClickerStateUi(DateTime now, bool blockedByCursor, bool targetFocused)
    {
        if (now < _nextRuntimeUiRefreshUtc)
            return;
        _nextRuntimeUiRefreshUtc = now.AddMilliseconds(100);
        bool rightPausedByAction = IsPvpQuickActionRunning;
        bool leftWaitingForPointer = _autoLeftRuntimeEnabled
            && GetSelectedMode(CbAutoLeftMode) == "PhysicalToggle"
            && !_physicalMouseTracker.IsButtonDown(VkLeftButton);
        bool rightWaitingForPointer = _autoRightRuntimeEnabled
            && GetSelectedMode(CbAutoRightMode) == "PhysicalToggle"
            && !_physicalMouseTracker.IsButtonDown(VkRightButton);

        string leftState = !_autoLeftRuntimeEnabled
            ? "OFF"
            : !targetFocused
                ? "BRAK FOKUSU"
                : blockedByCursor
                    ? "PAUZA"
                    : leftWaitingForPointer
                        ? "GOTOWY"
                        : "ON";
        string rightState = !_autoRightRuntimeEnabled
            ? "OFF"
            : !targetFocused
                ? "BRAK FOKUSU"
                : blockedByCursor || rightPausedByAction
                    ? "PAUZA"
                    : rightWaitingForPointer
                        ? "GOTOWY"
                        : "ON";
        TxtAutoLeftState.Text = leftState;
        TxtAutoLeftState.Foreground = leftState == "ON"
            ? Brushes.MediumSpringGreen
            : leftState == "GOTOWY"
                ? Brushes.DeepSkyBlue
            : leftState == "OFF"
                ? Brushes.OrangeRed
                : Brushes.Orange;
        TxtAutoRightState.Text = rightState;
        TxtAutoRightState.Foreground = rightState == "ON"
            ? Brushes.MediumSpringGreen
            : rightState == "GOTOWY"
                ? Brushes.DeepSkyBlue
            : rightState == "OFF"
                ? Brushes.OrangeRed
                : Brushes.Orange;
    }

    private void HandleClickerRuntime(bool blockedByCursor)
    {
        HandleSingleClicker(
            ChkAutoLeftEnabled.IsChecked == true,
            TxtAutoLeftKey.Text,
            GetSelectedMode(CbAutoLeftMode),
            VkLeftButton,
            ref _autoLeftBindWasDown,
            ref _autoLeftRuntimeEnabled,
            ref _autoLeftComboTriggerWasDown,
            ref _autoLeftComboStopWasDown,
            blockedByCursor,
            isLeft: true);
        HandleSingleClicker(
            ChkAutoRightEnabled.IsChecked == true,
            TxtAutoRightKey.Text,
            GetSelectedMode(CbAutoRightMode),
            VkRightButton,
            ref _autoRightBindWasDown,
            ref _autoRightRuntimeEnabled,
            ref _autoRightComboTriggerWasDown,
            ref _autoRightComboStopWasDown,
            blockedByCursor,
            isLeft: false);
    }

    private void HandleSingleClicker(
        bool enabled,
        string keyText,
        string mode,
        int mouseKey,
        ref bool wasDown,
        ref bool runtime,
        ref bool comboWasDown,
        ref bool comboStopWasDown,
        bool blockedByCursor,
        bool isLeft)
    {
        bool bindDown = IsConfiguredBindKeyDown(keyText);
        bool pressed = bindDown && !wasDown;
        wasDown = bindDown;
        if (!enabled || string.IsNullOrWhiteSpace(keyText))
        {
            runtime = false;
            comboWasDown = false;
            comboStopWasDown = false;
            if (isLeft) _autoClickScheduler.StopLeftImmediately();
            else _autoClickScheduler.StopRightImmediately();
            return;
        }

        switch (mode)
        {
            case "Hold":
                comboWasDown = false;
                comboStopWasDown = false;
                runtime = bindDown && !blockedByCursor;
                if (!bindDown)
                {
                    if (isLeft) _autoClickScheduler.StopLeftImmediately();
                    else _autoClickScheduler.StopRightImmediately();
                }
                break;
            case "Combo":
                bool mouseDown = _physicalMouseTracker.IsButtonDown(mouseKey);
                bool comboDown = bindDown && mouseDown;
                bool stopDown = !mouseDown;
                if (comboDown && !comboWasDown && !runtime && !blockedByCursor)
                {
                    runtime = true;
                    UpdateStatusBar(
                        isLeft ? "AUTO LPM: uruchomiony przez bind + LPM." : "AUTO PPM: uruchomiony przez bind + PPM.",
                        "Green");
                }
                if (stopDown && !comboStopWasDown && runtime)
                {
                    runtime = false;
                    if (isLeft) _autoClickScheduler.StopLeftImmediately();
                    else _autoClickScheduler.StopRightImmediately();
                    UpdateStatusBar(
                        isLeft ? "AUTO LPM: wyłączony po puszczeniu LPM." : "AUTO PPM: wyłączony po puszczeniu PPM.",
                        "Orange");
                }
                comboWasDown = comboDown;
                comboStopWasDown = stopDown;
                break;
            case "PhysicalToggle":
                comboWasDown = false;
                comboStopWasDown = false;
                if (pressed && !blockedByCursor)
                {
                    runtime = !runtime;
                    if (!runtime)
                    {
                        if (isLeft) _autoClickScheduler.StopLeftImmediately();
                        else _autoClickScheduler.StopRightImmediately();
                    }

                    string physicalButton = isLeft ? "LPM" : "PPM";
                    UpdateStatusBar(
                        runtime
                            ? $"{(isLeft ? "AUTO LPM" : "AUTO PPM")}: gotowy — trzymaj {physicalButton}."
                            : $"{(isLeft ? "AUTO LPM" : "AUTO PPM")}: wyłączony.",
                        runtime ? "Green" : "Orange");
                }
                break;
            default:
                comboWasDown = false;
                comboStopWasDown = false;
                if (pressed && !blockedByCursor)
                {
                    runtime = !runtime;
                    string label = isLeft ? "AUTO LPM" : "AUTO PPM";
                    UpdateStatusBar(
                        $"{label}: {(runtime ? "włączony" : "wyłączony")}.",
                        runtime ? "Green" : "Orange");
                }
                break;
        }
    }

    private void SynchronizeComboState(
        string bindKeyText,
        int mouseKey,
        ref bool comboWasDown,
        ref bool comboStopWasDown)
    {
        bool mouseDown = _physicalMouseTracker.IsButtonDown(mouseKey);
        comboWasDown = IsConfiguredBindKeyDown(bindKeyText) && mouseDown;
        comboStopWasDown = !mouseDown;
    }

    private void UpdatePhysicalMouseTracking(DateTime now)
    {
        string leftMode = GetSelectedMode(CbAutoLeftMode);
        string rightMode = GetSelectedMode(CbAutoRightMode);
        bool leftPointerDown = UsesPhysicalPointerGate(leftMode)
            && _physicalMouseTracker.IsButtonDown(VkLeftButton);
        bool rightPointerDown = UsesPhysicalPointerGate(rightMode)
            && _physicalMouseTracker.IsButtonDown(VkRightButton);
        bool needed = _isTargetFocused
            && ((_autoLeftRuntimeEnabled
                && ChkAutoLeftEnabled.IsChecked == true
                && leftPointerDown)
            || (_autoRightRuntimeEnabled
                && ChkAutoRightEnabled.IsChecked == true
                && rightPointerDown));

        if (!needed)
        {
            if (_physicalMouseTracker.IsMonitoring)
                _physicalMouseTracker.Stop();
            _nextPhysicalMouseTrackerRetryUtc = DateTime.MinValue;
            _physicalMouseTrackerWarningShown = false;
            return;
        }

        if (_physicalMouseTracker.IsMonitoring || now < _nextPhysicalMouseTrackerRetryUtc)
            return;

        if (_physicalMouseTracker.Start())
        {
            _physicalMouseTrackerWarningShown = false;
            return;
        }

        _nextPhysicalMouseTrackerRetryUtc = now.AddSeconds(2);
        if (UsesPhysicalPointerGate(leftMode))
        {
            _autoLeftRuntimeEnabled = false;
            _autoClickScheduler.StopLeftImmediately();
        }
        if (UsesPhysicalPointerGate(rightMode))
        {
            _autoRightRuntimeEnabled = false;
            _autoClickScheduler.StopRightImmediately();
        }
        if (!_physicalMouseTrackerWarningShown)
        {
            _physicalMouseTrackerWarningShown = true;
            UpdateStatusBar(
                $"Nie uruchomiono trybu fizycznego LPM/PPM: błąd trackera myszy Win32 {_physicalMouseTracker.LastError}.",
                "Red");
        }
    }

    private void HandleExperimentalBinds(DateTime now, bool blockedByCursor)
    {
        if (ChkAutoArmorEnabled.IsChecked == true && IsBindPressed(TxtAutoArmorBind.Text, ref _autoArmorBindWasDown))
        {
            if (IsAutoArmorRunning)
                RequestCancelAutoArmor("Anulowanie bindem — kończę bezpiecznie bieżący element.");
            else if (!blockedByCursor)
                TryStartAutoArmor(now);
        }

        if (ChkAutoWaterEnabled.IsChecked == true && IsBindPressed(TxtAutoWaterBind.Text, ref _autoWaterBindWasDown))
        {
            if (_autoWaterStage != AutoWaterStage.None)
                CancelAutoWater("AutoWater anulowany bindem.", Brushes.Orange, restoreSlot: true);
            else if (_bindyStage != BindyStage.None)
                SetAutoWaterStatus("Nie uruchomiono AutoWater: trwa wysyłanie komendy BINDY.", Brushes.Orange);
            else if (!blockedByCursor)
                TryStartAutoWater(now);
        }
    }

    private void HandleBindyRuntime(DateTime now, bool blockedByCursor)
    {
        if (_bindyStage != BindyStage.None)
        {
            RunBindyStage(now);
            return;
        }
        if (ChkBindyEnabled.IsChecked != true)
            return;

        foreach (BindyEntry entry in _settings.BindyEntries.Where(item => item.Enabled))
        {
            bool down = IsConfiguredBindKeyDown(entry.Key);
            _bindyKeyStates.TryGetValue(entry.Id, out bool wasDown);
            _bindyKeyStates[entry.Id] = down;
            if (!blockedByCursor && down && !wasDown && !string.IsNullOrWhiteSpace(entry.Command))
            {
                _bindyPendingCommand = entry.Command.Trim();
                _bindyPendingName = GetBindyLabel(entry);
                _bindyStage = BindyStage.OpenChat;
                _bindyNextActionAtUtc = now;
                UpdateStatusBar($"BINDY: {_bindyPendingName}", "Orange");
                break;
            }
        }
    }

    private void RunBindyStage(DateTime now)
    {
        if (now < _bindyNextActionAtUtc)
            return;
        switch (_bindyStage)
        {
            case BindyStage.OpenChat:
                SendKeyTap(GetConfiguredChatOpenVirtualKey());
                _bindyStage = BindyStage.TypeCommand;
                _bindyNextActionAtUtc = now.AddMilliseconds(180);
                break;
            case BindyStage.TypeCommand:
                if (!SendTextByKeyboard(_bindyPendingCommand))
                {
                    UpdateStatusBar("BINDY: nie udało się wpisać komendy.", "Red");
                    ResetBindyStage();
                    break;
                }
                _bindyStage = BindyStage.Submit;
                _bindyNextActionAtUtc = now.AddMilliseconds(110);
                break;
            case BindyStage.Submit:
                SendKeyTap(VK_RETURN);
                _bindyLastName = _bindyPendingName;
                _bindyLastAtUtc = now;
                UpdateStatusBar($"BINDY: wykonano {_bindyLastName}", "Green");
                ResetBindyStage();
                break;
        }
    }

    private void ResetBindyStage()
    {
        _bindyStage = BindyStage.None;
        _bindyPendingCommand = string.Empty;
        _bindyPendingName = string.Empty;
        _bindyNextActionAtUtc = DateTime.MinValue;
    }

    private void UpdateClickScheduler(
        bool leftEnabled,
        bool rightEnabled,
        int leftHoldKey,
        int rightHoldKey,
        int leftPointerKey = 0,
        int rightPointerKey = 0)
    {
        _autoClickScheduler.Update(
            leftEnabled,
            ReadCps(TxtAutoLeftMinCps.Text, 10),
            ReadCps(TxtAutoLeftMaxCps.Text, 12),
            rightEnabled,
            ReadCps(TxtAutoRightMinCps.Text, 10),
            ReadCps(TxtAutoRightMaxCps.Text, 12),
            leftHoldKey,
            leftPointerKey,
            rightHoldKey,
            rightPointerKey,
            _targetGameWindowHandle);
    }

    private void SetAutoLeftDabHold(bool down)
    {
        if (_autoLeftDabHeld == down)
            return;
        _autoLeftDabHeld = down;
        _ = NativeInput.SendKey(VK_O, down);
    }

    private void ReleaseHoldRightInjectedButton()
    {
        _ = NativeInput.SendMouseButton(leftButton: false, down: false);
    }

    private void StopClickerRuntimesForExclusivePointerMacro()
    {
        _autoLeftRuntimeEnabled = false;
        _autoRightRuntimeEnabled = false;
        _autoClickScheduler.Stop();
        SetAutoLeftDabHold(false);
    }

    private void StopOtherExclusivePointerMacros()
    {
        if (IsAppleCutterRunning)
            CancelAppleCutter("Ścinanie jabłek zatrzymane przez inną automatyzację.", Brushes.Orange);
    }

    private void ResetTestAutoFishingRuntimeState(DateTime now)
    {
    }

    private void UpdateEnabledStates() => UpdateModuleVisibility();

    private static int ParseNonNegativeInt(string? text)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? Math.Max(0, value)
            : 0;
    }

    private static int GetSlotVirtualKey(int slot) => 0x30 + Math.Clamp(slot, 1, 9);

    private static void SendKeyTap(int virtualKey)
    {
        _ = NativeInput.SendKey(virtualKey, down: true);
        _ = NativeInput.SendKey(virtualKey, down: false);
    }

    private static void SendKeyDown(int virtualKey) => _ = NativeInput.SendKey(virtualKey, down: true);

    private static void SendKeyUp(int virtualKey) => _ = NativeInput.SendKey(virtualKey, down: false);

    private static void SendMouseClick(bool leftButton, bool holdPulseMode)
    {
        _ = NativeInput.SendMouseClick(leftButton, holdPulseMode);
    }

    private static bool SendTextByKeyboard(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (char character in text)
        {
            short keyInfo = VkKeyScan(character);
            if (keyInfo == -1)
                return false;
            int virtualKey = keyInfo & 0xFF;
            int modifiers = (keyInfo >> 8) & 0xFF;
            if ((modifiers & 1) != 0) SendKeyDown(VK_SHIFT);
            if ((modifiers & 2) != 0) SendKeyDown(VK_CONTROL);
            if ((modifiers & 4) != 0) SendKeyDown(VK_MENU);
            SendKeyTap(virtualKey);
            if ((modifiers & 4) != 0) SendKeyUp(VK_MENU);
            if ((modifiers & 2) != 0) SendKeyUp(VK_CONTROL);
            if ((modifiers & 1) != 0) SendKeyUp(VK_SHIFT);
        }
        return true;
    }

    private int GetConfiguredChatOpenVirtualKey()
    {
        return TryGetVirtualKey(TxtChatOpenKey.Text, out int virtualKey) ? virtualKey : 0x54;
    }

    private static string NormalizeControlKey(string? keyText, string fallback)
    {
        string value = string.IsNullOrWhiteSpace(keyText) ? fallback : keyText.Trim();
        return TryGetVirtualKey(value, out int key) && key is not VkLeftButton and not VkRightButton
            ? value
            : fallback;
    }

    private static bool TryGetVirtualKey(string? keyText, out int virtualKey)
    {
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(keyText))
            return false;
        switch (keyText.Trim().ToUpperInvariant())
        {
            case "ENTER":
            case "RETURN": virtualKey = VK_RETURN; return true;
            case "MOUSEMIDDLE": virtualKey = VkMiddleButton; return true;
            case "MOUSEX1": virtualKey = VkXButton1; return true;
            case "MOUSEX2": virtualKey = VkXButton2; return true;
        }
        if (!Enum.TryParse(keyText.Trim(), true, out Key key))
            return false;
        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private static bool IsVirtualKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool IsConfiguredBindKeyDown(string? keyText)
    {
        return TryGetVirtualKey(keyText, out int virtualKey) && IsVirtualKeyDown(virtualKey);
    }

    private static bool IsBindPressed(string? keyText, ref bool wasDown)
    {
        bool down = IsConfiguredBindKeyDown(keyText);
        bool pressed = down && !wasDown;
        wasDown = down;
        return pressed;
    }

    private bool IsInventoryCursorVisible()
    {
        var cursorInfo = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(out cursorInfo) || (cursorInfo.flags & CursorShowing) == 0)
            return false;
        return !IsCursorVisuallyBlank(cursorInfo.hCursor);
    }

    private bool ShouldSuppressScheduledClick()
    {
        if (!_schedulerCursorGuardEnabled)
            return false;

        var cursorInfo = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(out cursorInfo))
            return true;
        if ((cursorInfo.flags & CursorShowing) == 0)
            return false;

        return !IsCursorVisuallyBlank(cursorInfo.hCursor);
    }

    private static bool IsCursorVisuallyBlank(IntPtr cursorHandle)
    {
        if (cursorHandle == IntPtr.Zero)
            return false;
        lock (CursorCacheLock)
        {
            if (CursorBlankCache.TryGetValue(cursorHandle, out bool cached))
                return cached;
        }
        int width = Math.Clamp(GetSystemMetrics(SmCxCursor), 1, 128);
        int height = Math.Clamp(GetSystemMetrics(SmCyCursor), 1, 128);
        bool blank = !CursorChangesBackground(cursorHandle, width, height, Drawing.Color.Black)
            && !CursorChangesBackground(cursorHandle, width, height, Drawing.Color.White);
        lock (CursorCacheLock)
        {
            if (CursorBlankCache.Count >= 24)
                CursorBlankCache.Clear();
            CursorBlankCache[cursorHandle] = blank;
        }
        return blank;
    }

    private static bool CursorChangesBackground(IntPtr cursor, int width, int height, Drawing.Color background)
    {
        using var bitmap = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format32bppArgb);
        using Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(background);
        IntPtr hdc = graphics.GetHdc();
        bool drawn;
        try
        {
            drawn = DrawIconEx(hdc, 0, 0, cursor, width, height, 0, IntPtr.Zero, DiNormal);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
        if (!drawn)
            return true;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (bitmap.GetPixel(x, y).ToArgb() != background.ToArgb())
                return true;
        return false;
    }

    private static bool TryGetWindowClientRectOnScreen(IntPtr window, out RECT screenRect)
    {
        screenRect = default;
        if (window == IntPtr.Zero || !GetClientRect(window, out RECT clientRect))
            return false;
        var topLeft = new POINT { X = clientRect.Left, Y = clientRect.Top };
        var bottomRight = new POINT { X = clientRect.Right, Y = clientRect.Bottom };
        if (!ClientToScreen(window, ref topLeft) || !ClientToScreen(window, ref bottomRight))
            return false;
        screenRect = new RECT { Left = topLeft.X, Top = topLeft.Y, Right = bottomRight.X, Bottom = bottomRight.Y };
        return screenRect.Right > screenRect.Left && screenRect.Bottom > screenRect.Top;
    }

    private void RefreshOverlayHud(DateTime now)
    {
        if (ChkOverlayEnabled?.IsChecked != true)
        {
            if (_lastOverlaySignature.Length > 0)
                _overlayHud?.UpdateEntries(Array.Empty<OverlayHudEntry>());
            _lastOverlaySignature = string.Empty;
            _lastOverlayRefreshUtc = now;
            return;
        }
        if ((now - _lastOverlayRefreshUtc).TotalMilliseconds < 100)
            return;
        _lastOverlayRefreshUtc = now;

        var entries = new List<OverlayHudEntry>();
        bool baseClickerPause = !_isTargetFocused
            || (ChkPauseWhenCursorVisible.IsChecked == true && _cachedCursorVisible);
        bool leftClickerPaused = baseClickerPause;
        bool rightClickerPaused = baseClickerPause || IsPvpQuickActionRunning;
        bool leftWaitingForPointer = GetSelectedMode(CbAutoLeftMode) == "PhysicalToggle"
            && !_physicalMouseTracker.IsButtonDown(VkLeftButton);
        bool rightWaitingForPointer = GetSelectedMode(CbAutoRightMode) == "PhysicalToggle"
            && !_physicalMouseTracker.IsButtonDown(VkRightButton);
        string rightClickerPauseMessage = IsPvpQuickActionRunning
            ? "PAUZA • szybka akcja PVP"
            : "PAUZA • brak fokusu albo otwarte GUI";
        if (_autoLeftRuntimeEnabled)
            entries.Add(new OverlayHudEntry(
                "AUTO LPM",
                leftClickerPaused
                    ? "PAUZA • brak fokusu albo otwarte GUI"
                    : leftWaitingForPointer
                        ? "GOTOWY • trzymaj LPM"
                        : $"ON • {ReadCps(TxtAutoLeftMinCps.Text, 10)}–{ReadCps(TxtAutoLeftMaxCps.Text, 12)} CPS",
                leftClickerPaused ? OverlayHudTone.Warning : OverlayHudTone.Active));
        if (_autoRightRuntimeEnabled)
            entries.Add(new OverlayHudEntry(
                "AUTO PPM",
                rightClickerPaused
                    ? rightClickerPauseMessage
                    : rightWaitingForPointer
                        ? "GOTOWY • trzymaj PPM"
                        : $"ON • {ReadCps(TxtAutoRightMinCps.Text, 10)}–{ReadCps(TxtAutoRightMaxCps.Text, 12)} CPS",
                rightClickerPaused ? OverlayHudTone.Warning : OverlayHudTone.Active));
        if (IsPvpQuickActionRunning)
            entries.Add(BuildPvpQuickActionOverlayEntry());
        if (IsAppleCutterRunning)
            entries.Add(BuildAppleCutterOverlayEntry());
        if (IsAutoArmorRunning || _autoArmorCalibrationPending)
            entries.Add(BuildAutoArmorOverlayEntry());
        if (_autoWaterStage != AutoWaterStage.None || _autoWaterCalibrationPending || _autoWaterRecognitionTestPending)
            entries.Add(BuildAutoWaterOverlayEntry(now));
        if (_bindyStage != BindyStage.None)
            entries.Add(new OverlayHudEntry("BINDY", $"Wysyłanie: {_bindyPendingName}", OverlayHudTone.Warning));
        else if (!string.IsNullOrWhiteSpace(_bindyLastName) && (now - _bindyLastAtUtc).TotalSeconds < 3)
            entries.Add(new OverlayHudEntry("BINDY", $"Wykonano: {_bindyLastName}", OverlayHudTone.Active));

        NormalizeAutoClickerHudEntrySizes(entries);

        string signature = string.Join(
            '\u001e',
            entries.Select(entry => $"{entry.Title}\u001f{entry.Body}\u001f{entry.Tone}\u001f{entry.Emphasize}"));
        if (entries.Count == 0)
        {
            if (_lastOverlaySignature.Length > 0)
                _overlayHud?.UpdateEntries(Array.Empty<OverlayHudEntry>());
            _lastOverlaySignature = string.Empty;
            return;
        }

        if (_overlayHud == null)
        {
            _overlayHud = new OverlayHudWindow();
            _overlayHud.SetAnimationsEnabled(false);
            _lastOverlayLayoutRefreshUtc = DateTime.MinValue;
        }
        _overlayHud.SetOwnerWindowHandle(_targetGameWindowHandle);
        if ((now - _lastOverlayLayoutRefreshUtc) >= OverlayLayoutRefreshInterval)
        {
            _lastOverlayLayoutRefreshUtc = now;
            _overlayHud.SetLayout(SystemParameters.WorkArea, OverlayCorner.BottomRight, 16);
        }
        if (!string.Equals(signature, _lastOverlaySignature, StringComparison.Ordinal))
        {
            _lastOverlaySignature = signature;
            _overlayHud.UpdateEntries(entries);
        }
    }

    private static void NormalizeAutoClickerHudEntrySizes(List<OverlayHudEntry> entries)
    {
        for (int index = 0; index < entries.Count; index++)
            entries[index] = entries[index] with { Emphasize = false };
    }

    private void UpdateStatusBar(string message, string colorName)
    {
        TxtStatus.Text = message;
        Brush brush = colorName switch
        {
            "Red" => Brushes.OrangeRed,
            "Orange" => Brushes.Orange,
            _ => Brushes.MediumSpringGreen
        };
        TxtStatus.Foreground = brush;
        StatusDot.Fill = brush;
    }

    private void PopulateSlotComboBoxes()
    {
        for (int slot = 1; slot <= 9; slot++)
        {
            string slotText = slot.ToString(CultureInfo.InvariantCulture);
            CbPvpSwordSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbPvpBlockSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbPvpSnowballSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbPvpPearlSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbPvpRodSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbAppleCutterShearsSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbAppleCutterLeavesSlot.Items.Add(new ComboBoxItem { Content = slotText });
            CbAutoWaterWaterSlot.Items.Add(new ComboBoxItem { Content = slot.ToString(CultureInfo.InvariantCulture) });
            CbAutoWaterReturnSlot.Items.Add(new ComboBoxItem { Content = slot.ToString(CultureInfo.InvariantCulture) });
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowApplicationExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        StateChanged -= MainWindow_StateChanged;
        _runtimeTimer.Stop();
        SavePendingSettings();
        _autoClickScheduler.Dispose();
        _physicalMouseTracker.Dispose();
        SetAutoLeftDabHold(false);
        CancelPvpQuickAction(string.Empty, Brushes.Orange, restoreSword: false, updateStatus: false);
        if (IsAppleCutterRunning)
            CancelAppleCutter(string.Empty, Brushes.Orange, updateStatus: false);
        else
            ReleaseAppleCutterInjectedButtons();
        ResetBindyStage();
        if (IsAutoArmorRunning)
            CancelAutoArmor(string.Empty, Brushes.Orange, closeInventory: false);
        if (_autoWaterStage != AutoWaterStage.None)
            CancelAutoWater(string.Empty, Brushes.Orange, restoreSlot: false, updateStatus: false);
        _overlayHud?.Close();
        _overlayHud = null;
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _trayMenu?.Dispose();
        _trayMenu = null;
        _trayIconImage?.Dispose();
        _trayIconImage = null;
    }

    private sealed record WindowCandidate(int ProcessId, string ProcessName, string WindowTitle, IntPtr WindowHandle)
    {
        public string DisplayName => $"{ProcessName} [{ProcessId}] — {WindowTitle}";
    }
}

