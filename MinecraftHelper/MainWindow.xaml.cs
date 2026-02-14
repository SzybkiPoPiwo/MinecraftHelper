using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using MinecraftHelper.Models;
using MinecraftHelper.Services;
using System.Windows.Interop;

namespace MinecraftHelper
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        private bool _pendingChanges = false;
        private readonly DispatcherTimer _dirtyTimer;
        private readonly DispatcherTimer _focusTimer;
        private readonly DispatcherTimer _macroTimer;

        private bool _isMinecraftFocused;
        private IntPtr _focusedGameWindowHandle = IntPtr.Zero;
        private bool _isLoadingUi;
        private bool _isPausedByCursorVisibility;
        private bool _holdMacroRuntimeEnabled;
        private bool _autoLeftRuntimeEnabled;
        private bool _autoRightRuntimeEnabled;
        private bool _jablkaRuntimeEnabled;
        private bool _kopacz533RuntimeEnabled;
        private bool _kopacz633RuntimeEnabled;

        private bool _holdBindWasDown;
        private bool _autoLeftBindWasDown;
        private bool _autoRightBindWasDown;
        private bool _jablkaBindWasDown;
        private bool _kopacz533BindWasDown;
        private bool _kopacz633BindWasDown;
        private bool _suppressBindToggleUntilRelease;

        private DateTime _nextHoldLeftClickAtUtc = DateTime.UtcNow;
        private DateTime _nextHoldRightClickAtUtc = DateTime.UtcNow;
        private DateTime _nextAutoLeftClickAtUtc = DateTime.UtcNow;
        private DateTime _nextAutoRightClickAtUtc = DateTime.UtcNow;
        private bool _holdLeftToggleClickingEnabled;
        private bool _holdLeftToggleWasDown;
        private DateTime _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
        private bool _holdRightRuntimePressActive;
        private DateTime _nextJablkaActionAtUtc = DateTime.UtcNow;
        private bool _jablkaUseSlotOneNext = true;
        private int _jablkaCompletedCycles;
        private DateTime _nextJablkaCommandStageAtUtc = DateTime.UtcNow;
        private JablkaCommandStage _jablkaCommandStage = JablkaCommandStage.None;
        private bool _kopacz533Holding;
        private DateTime _nextKopacz533CommandAtUtc = DateTime.UtcNow;
        private DateTime _nextKopacz533StageAtUtc = DateTime.UtcNow;
        private bool _kopacz533ResumeMiningPending;
        private DateTime _nextKopacz533ResumeAtUtc = DateTime.UtcNow;
        private Kopacz533CommandStage _kopacz533CommandStage = Kopacz533CommandStage.None;
        private bool _kopacz533CommandSequenceCompleted;
        private int _kopacz533CommandIndex;
        private int _kopacz533PendingCommandIndex = -1;
        private string _kopacz533PendingCommand = string.Empty;
        private DateTime _kopacz533RuntimeStartedAtUtc = DateTime.UtcNow;
        private DateTime _nextKopacz633CommandAtUtc = DateTime.UtcNow;
        private DateTime _nextKopacz633StageAtUtc = DateTime.UtcNow;
        private bool _kopacz633ResumeMiningPending;
        private DateTime _nextKopacz633ResumeAtUtc = DateTime.UtcNow;
        private Kopacz633CommandStage _kopacz633CommandStage = Kopacz633CommandStage.None;
        private bool _kopacz633CommandSequenceCompleted;
        private int _kopacz633CommandIndex;
        private int _kopacz633PendingCommandIndex = -1;
        private string _kopacz633PendingCommand = string.Empty;
        private DateTime _kopacz633RuntimeStartedAtUtc = DateTime.UtcNow;
        private bool _kopacz633HoldingAttack;
        private Kopacz633StrafeDirection _kopacz633StrafeDirection = Kopacz633StrafeDirection.None;
        private int _kopacz633UpwardLegIndex;
        private DateTime _kopacz633MovementLegEndAtUtc = DateTime.UtcNow;
        private DateTime _nextRuntimeTileRefreshAtUtc = DateTime.UtcNow;

        private readonly Random _random = new Random();

        private enum BindTarget
        {
            None,
            HoldToggle,
            AutoLeft,
            AutoRight,
            Kopacz533,
            Kopacz633,
            JablkaZLisci
        }

        private enum JablkaCommandStage
        {
            None,
            OpenChat,
            PasteCommand,
            SubmitCommand
        }

        private enum Kopacz533CommandStage
        {
            None,
            OpenChat,
            TypeCommand,
            SubmitCommand
        }

        private enum Kopacz633CommandStage
        {
            None,
            OpenChat,
            TypeCommand,
            SubmitCommand
        }

        private enum Kopacz633StrafeDirection
        {
            None,
            Forward,
            Right,
            Backward,
            Left
        }

        private BindTarget _bindCaptureTarget = BindTarget.None;
        private readonly Dictionary<BindTarget, string> _pendingBindValues = new Dictionary<BindTarget, string>();
        private static readonly Brush BindIdleBorderBrush = new SolidColorBrush(Color.FromRgb(75, 98, 131));
        private static readonly Brush BindCaptureBorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        private static readonly Brush TileLabelBrush = new SolidColorBrush(Color.FromRgb(146, 166, 193));
        private static readonly Brush TileBindBrush = new SolidColorBrush(Color.FromRgb(56, 214, 180));
        private static readonly Brush TileOnBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
        private static readonly Brush TilePauseBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        private static readonly Brush TileOffBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107));
        private static readonly Brush TileTimeBrush = new SolidColorBrush(Color.FromRgb(245, 200, 96));
        private static readonly Brush TileValueBrush = new SolidColorBrush(Color.FromRgb(127, 200, 255));

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool GetClipCursor(out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_XBUTTON1 = 0x05;
        private const int VK_XBUTTON2 = 0x06;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int CURSOR_SHOWING = 0x00000001;
        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_A = 0x41;
        private const int VK_D = 0x44;
        private const int VK_W = 0x57;
        private const int VK_S = 0x53;
        private const int VK_T = 0x54;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_RETURN = 0x0D;
        private const int JablkaCommandCycleThreshold = 70;
        private const int JablkaDelayAfterOpenChatMs = 180;
        private const int JablkaDelayAfterInsertCommandMs = 110;
        private const int JablkaDelayAfterCommandMs = 90;
        private const int Kopacz533DelayAfterOpenChatMs = 180;
        private const int Kopacz533DelayAfterTypeCommandMs = 110;
        private const int Kopacz533DelayAfterSubmitResumeMs = 130;
        private const int Kopacz633DelayAfterOpenChatMs = 180;
        private const int Kopacz633DelayAfterTypeCommandMs = 110;
        private const int Kopacz633DelayAfterSubmitResumeMs = 130;
        private const int Kopacz633MsPerBlock = 250;
        private const int HoldLeftTogglePressMinMs = 12;

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

        private void ApplyDarkTitleBar()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;
            int attr = Environment.OSVersion.Version.Build >= 18985
                ? DWMWA_USE_IMMERSIVE_DARK_MODE
                : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;

            _ = DwmSetWindowAttribute(hwnd, attr, ref useDark, sizeof(int));
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyDarkTitleBar();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewMouseDown += MainWindow_PreviewMouseDown;

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();
            EnsureSettingsConsistency();

            _dirtyTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _dirtyTimer.Tick += (s, e) =>
            {
                _dirtyTimer.Stop();
                if (_pendingChanges)
                    AutoSaveSettings();
            };

            _focusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _focusTimer.Tick += (_, __) => _isMinecraftFocused = CheckGameFocus();

            _macroTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            _macroTimer.Tick += RunMacroTick;

            DataContext = this;

            _isLoadingUi = true;
            try
            {
                LoadToUi();
            }
            finally
            {
                _isLoadingUi = false;
            }
            UpdateEnabledStates();
            RefreshTopTiles();

            _focusTimer.Start();
            _macroTimer.Start();
            _isMinecraftFocused = CheckGameFocus();
        }

        private void EnsureSettingsConsistency()
        {
            _settings ??= new AppSettings();

            _settings.MacroLeftButton ??= new MacroButton();
            _settings.MacroRightButton ??= new MacroButton();
            _settings.HoldLeftButton ??= new MacroButton();
            _settings.HoldRightButton ??= new MacroButton();
            _settings.AutoLeftButton ??= new MacroButton();
            _settings.AutoRightButton ??= new MacroButton();

            _settings.Kopacz533Commands ??= new List<MinerCommand>();
            _settings.Kopacz633Commands ??= new List<MinerCommand>();
            _settings.WindowTitleHistory ??= new List<string>();
            _settings.JablkaZLisciCommand ??= string.Empty;

            if (IsMacroButtonEmpty(_settings.HoldLeftButton) && !IsMacroButtonEmpty(_settings.MacroLeftButton))
                CopyMacroButtonData(_settings.MacroLeftButton, _settings.HoldLeftButton);
            if (IsMacroButtonEmpty(_settings.HoldRightButton) && !IsMacroButtonEmpty(_settings.MacroRightButton))
                CopyMacroButtonData(_settings.MacroRightButton, _settings.HoldRightButton);

            if (!_settings.HoldEnabled && _settings.MacroLeftButton.Enabled)
                _settings.HoldEnabled = true;
            if (string.IsNullOrWhiteSpace(_settings.HoldToggleKey) && !string.IsNullOrWhiteSpace(_settings.MacroLeftButton.Key))
                _settings.HoldToggleKey = _settings.MacroLeftButton.Key;

            if (IsMacroButtonEmpty(_settings.AutoLeftButton) && !IsMacroButtonEmpty(_settings.MacroLeftButton))
            {
                _settings.AutoLeftButton.Key = _settings.MacroLeftButton.Key;
                _settings.AutoLeftButton.MinCps = _settings.MacroLeftButton.MinCps;
                _settings.AutoLeftButton.MaxCps = _settings.MacroLeftButton.MaxCps;
            }

            if (IsMacroButtonEmpty(_settings.AutoRightButton) && !IsMacroButtonEmpty(_settings.MacroRightButton))
            {
                _settings.AutoRightButton.Key = _settings.MacroRightButton.Key;
                _settings.AutoRightButton.MinCps = _settings.MacroRightButton.MinCps;
                _settings.AutoRightButton.MaxCps = _settings.MacroRightButton.MaxCps;
            }

            if (_settings.AutoLeftButton.Enabled || _settings.AutoRightButton.Enabled)
                _settings.HoldEnabled = false;
        }

        private static bool IsMacroButtonEmpty(MacroButton button)
        {
            return !button.Enabled
                && string.IsNullOrWhiteSpace(button.Key)
                && button.MinCps == 0
                && button.MaxCps == 0;
        }

        private static void CopyMacroButtonData(MacroButton source, MacroButton target)
        {
            target.Enabled = source.Enabled;
            target.Key = source.Key;
            target.MinCps = source.MinCps;
            target.MaxCps = source.MaxCps;
        }

        // FOCUS MINECRAFT
        private bool CheckGameFocus()
        {
            string targetWindowTitle = (_settings.TargetWindowTitle ?? string.Empty).Trim();
            bool focused = false;
            IntPtr focusedWindow = IntPtr.Zero;

            if (!string.IsNullOrWhiteSpace(targetWindowTitle))
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    _ = GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                    string currentWindowTitle = windowTitle.ToString();
                    if (!string.IsNullOrWhiteSpace(currentWindowTitle))
                    {
                        focused = currentWindowTitle.Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase);
                        if (focused)
                            focusedWindow = foregroundWindow;
                    }
                }
            }

            _focusedGameWindowHandle = focusedWindow;

            TxtMinecraftFocus.Text = focused ? "✓ Tak" : "✗ Nie";
            TxtMinecraftFocus.Foreground = focused
                ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                : new SolidColorBrush(Color.FromRgb(255, 107, 107));
            EllMinecraftFocus.Fill = focused
                ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                : new SolidColorBrush(Color.FromRgb(255, 107, 107));

            return focused;
        }

        private void LoadToUi()
        {
            UpdateStatusBar("Gotowy", "Green");

            _holdMacroRuntimeEnabled = false;
            _autoLeftRuntimeEnabled = false;
            _autoRightRuntimeEnabled = false;
            _jablkaRuntimeEnabled = false;
            _kopacz533RuntimeEnabled = false;
            _kopacz633RuntimeEnabled = false;
            ResetJablkaRuntimeState();
            ResetKopacz533RuntimeState();
            ResetKopacz633RuntimeState();
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);

            // HOLD
            ChkMacroManualEnabled.IsChecked = _settings.HoldEnabled;
            TxtMacroManualKey.Text = _settings.HoldToggleKey;
            TxtManualLeftMinCps.Text = _settings.HoldLeftButton.MinCps.ToString();
            TxtManualLeftMaxCps.Text = _settings.HoldLeftButton.MaxCps.ToString();
            TxtManualRightMinCps.Text = _settings.HoldRightButton.MinCps.ToString();
            TxtManualRightMaxCps.Text = _settings.HoldRightButton.MaxCps.ToString();

            // AUTO
            ChkAutoLeftEnabled.IsChecked = _settings.AutoLeftButton.Enabled;
            TxtAutoLeftKey.Text = _settings.AutoLeftButton.Key;
            TxtAutoLeftMinCps.Text = _settings.AutoLeftButton.MinCps.ToString();
            TxtAutoLeftMaxCps.Text = _settings.AutoLeftButton.MaxCps.ToString();

            ChkAutoRightEnabled.IsChecked = _settings.AutoRightButton.Enabled;
            TxtAutoRightKey.Text = _settings.AutoRightButton.Key;
            TxtAutoRightMinCps.Text = _settings.AutoRightButton.MinCps.ToString();
            TxtAutoRightMaxCps.Text = _settings.AutoRightButton.MaxCps.ToString();

            // KOPACZ
            ChkKopacz533Enabled.IsChecked = _settings.Kopacz533Enabled;
            TxtKopacz533Key.Text = _settings.Kopacz533Key;
            RefreshKopaczCommandsUI(533);

            ChkKopacz633Enabled.IsChecked = _settings.Kopacz633Enabled;
            TxtKopacz633Key.Text = _settings.Kopacz633Key;
            TxtKopacz633Width.Text = _settings.Kopacz633Width.ToString();
            TxtKopacz633WidthUp.Text = _settings.Kopacz633Width.ToString();
            TxtKopacz633LengthUp.Text = _settings.Kopacz633Length.ToString();
            RefreshKopaczCommandsUI(633);

            if (_settings.Kopacz633Direction == "Na wprost")
                CbKopacz633Direction.SelectedIndex = 1;
            else if (_settings.Kopacz633Direction == "Do góry")
                CbKopacz633Direction.SelectedIndex = 2;
            else
                CbKopacz633Direction.SelectedIndex = 0;
            UpdateKopaczUpwardInfoVisibility();

            TxtTargetWindowTitle.Text = _settings.TargetWindowTitle;
            TxtCurrentWindowTitle.Text = string.IsNullOrWhiteSpace(_settings.TargetWindowTitle) ? "Brak" : _settings.TargetWindowTitle;

            RefreshWindowTitleHistory();

            // JABŁKA Z LIŚCI
            ChkJablkaZLisciEnabled.IsChecked = _settings.JablkaZLisciEnabled;
            TxtJablkaZLisciKey.Text = _settings.JablkaZLisciKey;
            TxtJablkaZLisciCommand.Text = _settings.JablkaZLisciCommand;

            // EQ
            ChkPauseWhenCursorVisible.IsChecked = _settings.PauseWhenCursorVisible;

            _pendingBindValues.Clear();
            RefreshBindSaveButtons();
        }

        private static string GetBindTargetLabel(BindTarget target)
        {
            return target switch
            {
                BindTarget.HoldToggle => "HOLD (LPM + PPM)",
                BindTarget.AutoLeft => "AUTO LPM",
                BindTarget.AutoRight => "AUTO PPM",
                BindTarget.Kopacz533 => "Kopacz 5/3/3",
                BindTarget.Kopacz633 => "Kopacz 6/3/3",
                BindTarget.JablkaZLisci => "Jabłka z liści",
                _ => "bind"
            };
        }

        private static string GetSaveButtonBaseContent(BindTarget target)
        {
            return target is BindTarget.Kopacz533 or BindTarget.Kopacz633 ? "Zapisz klawisz" : "Zapisz";
        }

        private Button? GetBindSaveButton(BindTarget target)
        {
            return target switch
            {
                BindTarget.HoldToggle => BtnMacroManualCapture,
                BindTarget.AutoLeft => BtnAutoLeftCapture,
                BindTarget.AutoRight => BtnAutoRightCapture,
                BindTarget.Kopacz533 => BtnKopacz533Capture,
                BindTarget.Kopacz633 => BtnKopacz633Capture,
                BindTarget.JablkaZLisci => BtnJablkaZLisciCapture,
                _ => null
            };
        }

        private TextBox? GetBindTextBox(BindTarget target)
        {
            return target switch
            {
                BindTarget.HoldToggle => TxtMacroManualKey,
                BindTarget.AutoLeft => TxtAutoLeftKey,
                BindTarget.AutoRight => TxtAutoRightKey,
                BindTarget.Kopacz533 => TxtKopacz533Key,
                BindTarget.Kopacz633 => TxtKopacz633Key,
                BindTarget.JablkaZLisci => TxtJablkaZLisciKey,
                _ => null
            };
        }

        private static IEnumerable<BindTarget> GetAllBindTargets()
        {
            yield return BindTarget.HoldToggle;
            yield return BindTarget.AutoLeft;
            yield return BindTarget.AutoRight;
            yield return BindTarget.Kopacz533;
            yield return BindTarget.Kopacz633;
            yield return BindTarget.JablkaZLisci;
        }

        private void UpdateBindCaptureVisuals()
        {
            foreach (BindTarget target in GetAllBindTargets())
            {
                TextBox? bindBox = GetBindTextBox(target);
                if (bindBox == null)
                    continue;

                bool isCaptureActive = _bindCaptureTarget == target;
                bindBox.BorderBrush = isCaptureActive ? BindCaptureBorderBrush : BindIdleBorderBrush;
                bindBox.BorderThickness = isCaptureActive ? new Thickness(2) : new Thickness(1);
            }
        }

        private void RefreshBindSaveButton(BindTarget target)
        {
            Button? button = GetBindSaveButton(target);
            if (button == null)
                return;

            string baseContent = GetSaveButtonBaseContent(target);
            if (_pendingBindValues.TryGetValue(target, out string? pendingKey))
                button.Content = $"{baseContent} ({pendingKey})";
            else
                button.Content = baseContent;
        }

        private void RefreshBindSaveButtons()
        {
            RefreshBindSaveButton(BindTarget.HoldToggle);
            RefreshBindSaveButton(BindTarget.AutoLeft);
            RefreshBindSaveButton(BindTarget.AutoRight);
            RefreshBindSaveButton(BindTarget.Kopacz533);
            RefreshBindSaveButton(BindTarget.Kopacz633);
            RefreshBindSaveButton(BindTarget.JablkaZLisci);
            UpdateBindCaptureVisuals();
        }

        private void RefreshTopTiles()
        {
            DateTime now = DateTime.UtcNow;

            int manualLeftMin = ParseNonNegativeInt(TxtManualLeftMinCps.Text);
            int manualLeftMax = ParseNonNegativeInt(TxtManualLeftMaxCps.Text);
            int manualRightMin = ParseNonNegativeInt(TxtManualRightMinCps.Text);
            int manualRightMax = ParseNonNegativeInt(TxtManualRightMaxCps.Text);

            int autoLeftMin = ParseNonNegativeInt(TxtAutoLeftMinCps.Text);
            int autoLeftMax = ParseNonNegativeInt(TxtAutoLeftMaxCps.Text);

            int autoRightMin = ParseNonNegativeInt(TxtAutoRightMinCps.Text);
            int autoRightMax = ParseNonNegativeInt(TxtAutoRightMaxCps.Text);

            bool manualOn = ChkMacroManualEnabled.IsChecked ?? false;
            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;
            bool kop533On = ChkKopacz533Enabled.IsChecked ?? false;
            bool kop633On = ChkKopacz633Enabled.IsChecked ?? false;
            bool jablkaOn = ChkJablkaZLisciEnabled.IsChecked ?? false;
            string holdRuntimeState = GetRuntimeStateLabel(_holdMacroRuntimeEnabled);
            string autoLeftRuntimeState = GetRuntimeStateLabel(_autoLeftRuntimeEnabled);
            string autoRightRuntimeState = GetRuntimeStateLabel(_autoRightRuntimeEnabled);
            string kop533RuntimeState = GetRuntimeStateLabel(_kopacz533RuntimeEnabled);
            string kop633RuntimeState = GetRuntimeStateLabel(_kopacz633RuntimeEnabled);
            string jablkaRuntimeState = GetRuntimeStateLabel(_jablkaRuntimeEnabled);
            string holdBindLabel = GetConfiguredBindLabel(TxtMacroManualKey.Text);
            string autoLeftBindLabel = GetConfiguredBindLabel(TxtAutoLeftKey.Text);
            string autoRightBindLabel = GetConfiguredBindLabel(TxtAutoRightKey.Text);
            string kop533BindLabel = GetConfiguredBindLabel(TxtKopacz533Key.Text);
            string kop633BindLabel = GetConfiguredBindLabel(TxtKopacz633Key.Text);
            string jablkaBindLabel = GetConfiguredBindLabel(TxtJablkaZLisciKey.Text);

            if (TxtManualCps != null)
            {
                RenderManualStatus(manualOn, holdBindLabel, manualLeftMin, manualLeftMax, manualRightMin, manualRightMax, holdRuntimeState);
            }

            if (TxtAutoLeftCps != null)
            {
                RenderAutoStatus(TxtAutoLeftCps, autoLeftOn, autoLeftBindLabel, autoLeftMin, autoLeftMax, autoLeftRuntimeState);
            }

            if (TxtAutoRightCps != null)
            {
                RenderAutoStatus(TxtAutoRightCps, autoRightOn, autoRightBindLabel, autoRightMin, autoRightMax, autoRightRuntimeState);
            }

            if (TxtJablkaZLisciStatus != null)
            {
                RenderStateOnlyStatus(TxtJablkaZLisciStatus, jablkaOn, jablkaBindLabel, jablkaRuntimeState);
            }

            if (TxtKopacz533Status != null)
            {
                RenderKopacz533Status(kop533On, kop533BindLabel, kop533RuntimeState, now);
            }

            if (TxtKopacz633Status != null)
            {
                RenderKopacz633Status(kop633On, kop633BindLabel, kop633RuntimeState, now);
            }

            Brush activeBorder = (Brush)(TryFindResource("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(46, 168, 255)));
            Brush inactiveBorder = (Brush)(TryFindResource("TileBorder") ?? new SolidColorBrush(Color.FromRgb(62, 83, 110)));
            Brush runtimeActiveBorder = new SolidColorBrush(Color.FromRgb(74, 222, 128));
            Thickness normalBorderThickness = new Thickness(1);
            Thickness runtimeBorderThickness = new Thickness(2);

            bool manualRuntimeActive = manualOn && _holdMacroRuntimeEnabled;
            bool autoLeftRuntimeActive = autoLeftOn && _autoLeftRuntimeEnabled;
            bool autoRightRuntimeActive = autoRightOn && _autoRightRuntimeEnabled;
            bool kop533RuntimeActive = kop533On && _kopacz533RuntimeEnabled;
            bool kop633RuntimeActive = kop633On && _kopacz633RuntimeEnabled;
            bool jablkaRuntimeActive = jablkaOn && _jablkaRuntimeEnabled;

            BorderManualStatus.BorderBrush = manualRuntimeActive ? runtimeActiveBorder : manualOn ? activeBorder : inactiveBorder;
            BorderManualStatus.BorderThickness = manualRuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            BorderAutoLeftStatus.BorderBrush = autoLeftRuntimeActive ? runtimeActiveBorder : autoLeftOn ? activeBorder : inactiveBorder;
            BorderAutoLeftStatus.BorderThickness = autoLeftRuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            BorderAutoRightStatus.BorderBrush = autoRightRuntimeActive ? runtimeActiveBorder : autoRightOn ? activeBorder : inactiveBorder;
            BorderAutoRightStatus.BorderThickness = autoRightRuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            BorderKopacz533Status.BorderBrush = kop533RuntimeActive ? runtimeActiveBorder : kop533On ? activeBorder : inactiveBorder;
            BorderKopacz533Status.BorderThickness = kop533RuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            BorderKopacz633Status.BorderBrush = kop633RuntimeActive ? runtimeActiveBorder : kop633On ? activeBorder : inactiveBorder;
            BorderKopacz633Status.BorderThickness = kop633RuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            BorderJablkaZLisciStatus.BorderBrush = jablkaRuntimeActive ? runtimeActiveBorder : jablkaOn ? activeBorder : inactiveBorder;
            BorderJablkaZLisciStatus.BorderThickness = jablkaRuntimeActive ? runtimeBorderThickness : normalBorderThickness;

            UpdateCursorPauseTile();
        }

        private static string GetConfiguredBindLabel(string keyText)
        {
            return string.IsNullOrWhiteSpace(keyText) ? "Brak" : keyText.Trim();
        }

        private void RenderManualStatus(bool manualOn, string bindLabel, int leftMin, int leftMax, int rightMin, int rightMax, string runtimeState)
        {
            if (TxtManualCps == null)
                return;

            TxtManualCps.Inlines.Clear();
            AppendInline(TxtManualCps, "Klawisz: ", TileLabelBrush);
            AppendInline(TxtManualCps, bindLabel, TileBindBrush);
            AppendLineBreak(TxtManualCps);

            if (!manualOn)
            {
                AppendInline(TxtManualCps, "CPS: ", TileLabelBrush);
                AppendInline(TxtManualCps, "Wyłączony", TileOffBrush, FontWeights.SemiBold);
                return;
            }

            AppendInline(TxtManualCps, "LPM ", TileLabelBrush);
            AppendInline(TxtManualCps, $"{leftMin}-{leftMax}", TileValueBrush, FontWeights.SemiBold);
            AppendInline(TxtManualCps, " | PPM ", TileLabelBrush);
            AppendInline(TxtManualCps, $"{rightMin}-{rightMax}", TileValueBrush, FontWeights.SemiBold);
            AppendInline(TxtManualCps, " CPS ", TileLabelBrush);
            AppendInline(TxtManualCps, runtimeState, GetRuntimeStateBrush(runtimeState), FontWeights.SemiBold);

            if (_holdMacroRuntimeEnabled)
            {
                AppendInline(TxtManualCps, " | LPM-TGL ", TileLabelBrush);
                AppendInline(TxtManualCps, _holdLeftToggleClickingEnabled ? "ON" : "OFF", _holdLeftToggleClickingEnabled ? TileOnBrush : TileOffBrush, FontWeights.SemiBold);
                AppendInline(TxtManualCps, " | PPM-HOLD ", TileLabelBrush);
                AppendInline(TxtManualCps, _holdRightRuntimePressActive ? "ON" : "OFF", _holdRightRuntimePressActive ? TileOnBrush : TileOffBrush, FontWeights.SemiBold);
            }
        }

        private void RenderAutoStatus(TextBlock? block, bool enabled, string bindLabel, int min, int max, string runtimeState)
        {
            if (block == null)
                return;

            block.Inlines.Clear();
            AppendInline(block, "Klawisz: ", TileLabelBrush);
            AppendInline(block, bindLabel, TileBindBrush);
            AppendLineBreak(block);

            AppendInline(block, "CPS: ", TileLabelBrush);
            if (!enabled)
            {
                AppendInline(block, "Wyłączony", TileOffBrush, FontWeights.SemiBold);
                return;
            }

            AppendInline(block, $"{min}-{max}", TileValueBrush, FontWeights.SemiBold);
            AppendInline(block, " ", TileLabelBrush);
            AppendInline(block, runtimeState, GetRuntimeStateBrush(runtimeState), FontWeights.SemiBold);
        }

        private void RenderStateOnlyStatus(TextBlock? block, bool enabled, string bindLabel, string runtimeState)
        {
            if (block == null)
                return;

            block.Inlines.Clear();
            AppendInline(block, "Klawisz: ", TileLabelBrush);
            AppendInline(block, bindLabel, TileBindBrush);
            AppendLineBreak(block);
            AppendInline(block, "Stan: ", TileLabelBrush);

            if (!enabled)
            {
                AppendInline(block, "Wyłączony", TileOffBrush, FontWeights.SemiBold);
                return;
            }

            AppendInline(block, runtimeState, GetRuntimeStateBrush(runtimeState), FontWeights.SemiBold);
        }

        private void RenderSimpleOnOffStatus(TextBlock? block, bool enabled, string bindLabel)
        {
            if (block == null)
                return;

            block.Inlines.Clear();
            AppendInline(block, "Klawisz: ", TileLabelBrush);
            AppendInline(block, bindLabel, TileBindBrush);
            AppendLineBreak(block);
            AppendInline(block, "Stan: ", TileLabelBrush);
            AppendInline(block, enabled ? "Włączony" : "Wyłączony", enabled ? TileOnBrush : TileOffBrush, FontWeights.SemiBold);
        }

        private static Brush GetRuntimeStateBrush(string runtimeState)
        {
            return runtimeState == "ON"
                ? TileOnBrush
                : runtimeState == "PAUZA"
                    ? TilePauseBrush
                    : TileOffBrush;
        }

        private void RenderKopacz533Status(bool kop533On, string bindLabel, string runtimeState, DateTime now)
        {
            if (TxtKopacz533Status == null)
                return;

            TxtKopacz533Status.Inlines.Clear();

            AppendInline(TxtKopacz533Status, "Klawisz: ", TileLabelBrush);
            AppendInline(TxtKopacz533Status, bindLabel, TileBindBrush);
            AppendLineBreak(TxtKopacz533Status);

            AppendInline(TxtKopacz533Status, "Stan: ", TileLabelBrush);
            Brush stateBrush = GetRuntimeStateBrush(runtimeState);
            AppendInline(TxtKopacz533Status, runtimeState, stateBrush, FontWeights.SemiBold);

            if (!kop533On)
            {
                AppendLineBreak(TxtKopacz533Status);
                AppendInline(TxtKopacz533Status, "Następna: brak", TileLabelBrush);
                return;
            }

            if (_kopacz533RuntimeEnabled)
            {
                int elapsedSeconds = GetKopacz533ElapsedSeconds(now);
                AppendInline(TxtKopacz533Status, "  |  Czas: ", TileLabelBrush);
                AppendInline(TxtKopacz533Status, $"{elapsedSeconds}s", TileTimeBrush, FontWeights.SemiBold);
            }

            AppendLineBreak(TxtKopacz533Status);
            AppendInline(TxtKopacz533Status, "Następna: ", TileLabelBrush);

            if (_kopacz533CommandStage != Kopacz533CommandStage.None && !string.IsNullOrWhiteSpace(_kopacz533PendingCommand))
            {
                AppendInline(TxtKopacz533Status, GetStatusCommandPreview(_kopacz533PendingCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz533Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz533Status, "0s", TileTimeBrush, FontWeights.SemiBold);
                return;
            }

            if (TryPeekNextKopacz533Command(out _, out string nextCommand, out _))
            {
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextKopacz533CommandAtUtc - now).TotalSeconds));
                AppendInline(TxtKopacz533Status, GetStatusCommandPreview(nextCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz533Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz533Status, $"{remainingSeconds}s", TileTimeBrush, FontWeights.SemiBold);
                return;
            }

            AppendInline(TxtKopacz533Status, "brak", TileOffBrush, FontWeights.SemiBold);
        }

        private void RenderKopacz633Status(bool kop633On, string bindLabel, string runtimeState, DateTime now)
        {
            if (TxtKopacz633Status == null)
                return;

            TxtKopacz633Status.Inlines.Clear();

            AppendInline(TxtKopacz633Status, "Klawisz: ", TileLabelBrush);
            AppendInline(TxtKopacz633Status, bindLabel, TileBindBrush);
            AppendLineBreak(TxtKopacz633Status);

            AppendInline(TxtKopacz633Status, "Stan: ", TileLabelBrush);
            AppendInline(TxtKopacz633Status, runtimeState, GetRuntimeStateBrush(runtimeState), FontWeights.SemiBold);

            string directionLabel = CbKopacz633Direction.SelectedIndex switch
            {
                1 => "Na wprost",
                2 => "Do góry",
                _ => "Brak"
            };

            AppendLineBreak(TxtKopacz633Status);
            AppendInline(TxtKopacz633Status, "Tryb: ", TileLabelBrush);
            AppendInline(TxtKopacz633Status, directionLabel, TileValueBrush, FontWeights.SemiBold);

            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                int width = GetConfiguredKopacz633ForwardWidth();
                AppendInline(TxtKopacz633Status, " | Szer: ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{width}", TileValueBrush, FontWeights.SemiBold);
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                int width = GetConfiguredKopacz633UpwardWidth();
                int length = GetConfiguredKopacz633UpwardLength();
                AppendInline(TxtKopacz633Status, " | Szer: ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{width}", TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz633Status, " | Dł: ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{length}", TileValueBrush, FontWeights.SemiBold);
            }

            if (!kop633On)
                return;

            if (_kopacz633RuntimeEnabled)
            {
                int elapsedSeconds = GetKopacz633ElapsedSeconds(now);
                AppendInline(TxtKopacz633Status, " | Czas: ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{elapsedSeconds}s", TileTimeBrush, FontWeights.SemiBold);

                AppendLineBreak(TxtKopacz633Status);
                AppendInline(TxtKopacz633Status, "Ruch: ", TileLabelBrush);
                string movementLabel = _kopacz633StrafeDirection switch
                {
                    Kopacz633StrafeDirection.Forward => "W ^",
                    Kopacz633StrafeDirection.Right => "D ->",
                    Kopacz633StrafeDirection.Backward => "S v",
                    Kopacz633StrafeDirection.Left => "A <-",
                    _ => "STOP"
                };
                Brush movementBrush = _kopacz633StrafeDirection == Kopacz633StrafeDirection.None ? TileOffBrush : TileOnBrush;
                AppendInline(TxtKopacz633Status, movementLabel, movementBrush, FontWeights.SemiBold);

                if (_kopacz633StrafeDirection != Kopacz633StrafeDirection.None)
                {
                    int remainingMs = Math.Max(0, (int)Math.Ceiling((_kopacz633MovementLegEndAtUtc - now).TotalMilliseconds));
                    AppendInline(TxtKopacz633Status, " za ", TileLabelBrush);
                    AppendInline(TxtKopacz633Status, $"{remainingMs}ms", TileTimeBrush, FontWeights.SemiBold);
                }
            }

            AppendLineBreak(TxtKopacz633Status);
            AppendInline(TxtKopacz633Status, "Komenda: ", TileLabelBrush);

            if (_kopacz633CommandStage != Kopacz633CommandStage.None && !string.IsNullOrWhiteSpace(_kopacz633PendingCommand))
            {
                AppendInline(TxtKopacz633Status, GetStatusCommandPreview(_kopacz633PendingCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz633Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, "0s", TileTimeBrush, FontWeights.SemiBold);
                return;
            }

            if (TryPeekNextKopacz633Command(out _, out string nextCommand, out _))
            {
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextKopacz633CommandAtUtc - now).TotalSeconds));
                AppendInline(TxtKopacz633Status, GetStatusCommandPreview(nextCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz633Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{remainingSeconds}s", TileTimeBrush, FontWeights.SemiBold);
                return;
            }

            AppendInline(TxtKopacz633Status, "brak", TileOffBrush, FontWeights.SemiBold);
        }

        private static void AppendInline(TextBlock block, string text, Brush foreground, FontWeight? fontWeight = null)
        {
            var run = new Run(text)
            {
                Foreground = foreground
            };

            if (fontWeight.HasValue)
                run.FontWeight = fontWeight.Value;

            block.Inlines.Add(run);
        }

        private static void AppendLineBreak(TextBlock block)
        {
            block.Inlines.Add(new LineBreak());
        }

        private static string GetStatusCommandPreview(string command)
        {
            string value = (command ?? string.Empty).Trim();
            if (value.Length <= 22)
                return value;

            return value.Substring(0, 19) + "...";
        }

        private int GetKopacz533ElapsedSeconds(DateTime now)
        {
            double elapsed = (now - _kopacz533RuntimeStartedAtUtc).TotalSeconds;
            return Math.Max(0, (int)Math.Floor(elapsed));
        }

        private int GetKopacz633ElapsedSeconds(DateTime now)
        {
            double elapsed = (now - _kopacz633RuntimeStartedAtUtc).TotalSeconds;
            return Math.Max(0, (int)Math.Floor(elapsed));
        }

        private void RefreshLiveTopTiles(DateTime now)
        {
            bool kopaczRuntimeLive =
                _kopacz533RuntimeEnabled ||
                _kopacz533CommandStage != Kopacz533CommandStage.None ||
                _kopacz633RuntimeEnabled ||
                _kopacz633CommandStage != Kopacz633CommandStage.None;
            if (!kopaczRuntimeLive)
                return;

            if (now < _nextRuntimeTileRefreshAtUtc)
                return;

            _nextRuntimeTileRefreshAtUtc = now.AddMilliseconds(200);
            RefreshTopTiles();
        }

        private static void SetSectionVisualState(Border? section, bool enabled)
        {
            if (section == null)
                return;

            section.Opacity = enabled ? 1.0 : 0.55;
        }

        private void UpdateEnabledStates(object? sender = null, RoutedEventArgs? e = null)
        {
            bool manualOn = ChkMacroManualEnabled.IsChecked ?? false;
            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;

            TxtMacroManualKey.IsEnabled = manualOn;
            BtnMacroManualCapture.IsEnabled = manualOn;
            TxtManualLeftMinCps.IsEnabled = manualOn;
            TxtManualLeftMaxCps.IsEnabled = manualOn;
            TxtManualRightMinCps.IsEnabled = manualOn;
            TxtManualRightMaxCps.IsEnabled = manualOn;

            TxtAutoLeftKey.IsEnabled = autoLeftOn;
            BtnAutoLeftCapture.IsEnabled = autoLeftOn;
            TxtAutoLeftMinCps.IsEnabled = autoLeftOn;
            TxtAutoLeftMaxCps.IsEnabled = autoLeftOn;

            TxtAutoRightKey.IsEnabled = autoRightOn;
            BtnAutoRightCapture.IsEnabled = autoRightOn;
            TxtAutoRightMinCps.IsEnabled = autoRightOn;
            TxtAutoRightMaxCps.IsEnabled = autoRightOn;

            if (!manualOn)
            {
                _holdMacroRuntimeEnabled = false;
                ResetHoldLeftToggleState(clearToggleEnabled: true);
            }
            if (!autoLeftOn)
                _autoLeftRuntimeEnabled = false;
            if (!autoRightOn)
                _autoRightRuntimeEnabled = false;

            bool kop533On = ChkKopacz533Enabled.IsChecked ?? false;
            TxtKopacz533Key.IsEnabled = kop533On;
            BtnKopacz533Capture.IsEnabled = kop533On;
            PanelKopacz533Commands.IsEnabled = kop533On;
            BtnKopacz533AddCommand.IsEnabled = kop533On;
            if (!kop533On)
            {
                _kopacz533RuntimeEnabled = false;
                ResetKopacz533RuntimeState();
                SetKopacz533MiningHold(false);
            }

            bool kop633On = ChkKopacz633Enabled.IsChecked ?? false;
            TxtKopacz633Key.IsEnabled = kop633On;
            BtnKopacz633Capture.IsEnabled = kop633On;
            CbKopacz633Direction.IsHitTestVisible = kop633On; // blokuje myszkę gdy OFF
            CbKopacz633Direction.Focusable = kop633On;        // nie łapie focusa gdy OFF
            CbKopacz633Direction.Opacity = kop633On ? 1.0 : 0.85; // opcjonalnie lekko przygaś
            PanelKopaczNaWprost.IsEnabled = kop633On;
            PanelKopaczDoGory.IsEnabled = kop633On;
            PanelKopacz633Commands.IsEnabled = kop633On;
            BtnKopacz633AddCommand.IsEnabled = kop633On;
            if (!kop633On)
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
            }
            UpdateKopaczUpwardInfoVisibility();

            // JABŁKA Z LIŚCI
            bool jablkaOn = ChkJablkaZLisciEnabled.IsChecked ?? false;
            TxtJablkaZLisciKey.IsEnabled = jablkaOn;
            BtnJablkaZLisciCapture.IsEnabled = jablkaOn;
            TxtJablkaZLisciCommand.IsEnabled = jablkaOn;
            BtnSaveJablkaZLisciCommand.IsEnabled = jablkaOn;
            if (!jablkaOn)
            {
                _jablkaRuntimeEnabled = false;
                ResetJablkaRuntimeState();
            }

            bool cursorPauseOn = ChkPauseWhenCursorVisible.IsChecked == true;
            SetSectionVisualState(BorderManualLeftSection, manualOn);
            SetSectionVisualState(BorderManualRightSection, manualOn);
            SetSectionVisualState(BorderManualBindSection, manualOn);
            SetSectionVisualState(BorderAutoLeftSection, autoLeftOn);
            SetSectionVisualState(BorderAutoRightSection, autoRightOn);
            SetSectionVisualState(BorderJablkaSection, jablkaOn);
            SetSectionVisualState(BorderCursorPauseSection, cursorPauseOn);
            SetSectionVisualState(BorderKopacz533Section, kop533On);
            SetSectionVisualState(BorderKopacz633Section, kop633On);

            Brush activeBg = (Brush)(TryFindResource("TileBgActive") ?? new SolidColorBrush(Color.FromRgb(23, 50, 74)));
            Brush inactiveBg = (Brush)(TryFindResource("TileBg") ?? new SolidColorBrush(Color.FromRgb(30, 42, 57)));
            Brush activeBorder = (Brush)(TryFindResource("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(46, 168, 255)));
            Brush inactiveBorder = (Brush)(TryFindResource("TileBorder") ?? new SolidColorBrush(Color.FromRgb(62, 83, 110)));

            BorderManualStatus.Background = manualOn ? activeBg : inactiveBg;
            BorderManualStatus.BorderBrush = manualOn ? activeBorder : inactiveBorder;
            BorderManualStatus.Opacity = manualOn ? 1.0 : 0.85;

            BorderAutoLeftStatus.Background = autoLeftOn ? activeBg : inactiveBg;
            BorderAutoLeftStatus.BorderBrush = autoLeftOn ? activeBorder : inactiveBorder;
            BorderAutoLeftStatus.Opacity = autoLeftOn ? 1.0 : 0.85;

            BorderAutoRightStatus.Background = autoRightOn ? activeBg : inactiveBg;
            BorderAutoRightStatus.BorderBrush = autoRightOn ? activeBorder : inactiveBorder;
            BorderAutoRightStatus.Opacity = autoRightOn ? 1.0 : 0.85;

            BorderKopacz533Status.Background = kop533On ? activeBg : inactiveBg;
            BorderKopacz533Status.BorderBrush = kop533On ? activeBorder : inactiveBorder;
            BorderKopacz533Status.Opacity = kop533On ? 1.0 : 0.85;

            BorderKopacz633Status.Background = kop633On ? activeBg : inactiveBg;
            BorderKopacz633Status.BorderBrush = kop633On ? activeBorder : inactiveBorder;
            BorderKopacz633Status.Opacity = kop633On ? 1.0 : 0.85;

            BorderJablkaZLisciStatus.Background = jablkaOn ? activeBg : inactiveBg;
            BorderJablkaZLisciStatus.BorderBrush = jablkaOn ? activeBorder : inactiveBorder;
            BorderJablkaZLisciStatus.Opacity = jablkaOn ? 1.0 : 0.85;

            RefreshTopTiles();
        }

        private void RefreshWindowTitleHistory()
        {
            PanelWindowTitleHistory.Children.Clear();

            if (_settings.WindowTitleHistory == null || _settings.WindowTitleHistory.Count == 0)
            {
                PanelWindowTitleHistory.Children.Add(
                    new TextBlock
                    {
                        Text = "Brak historii",
                        Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193)),
                        Margin = new Thickness(0, 5, 0, 0)
                    });
                return;
            }

            foreach (var title in _settings.WindowTitleHistory)
                AddHistoryItem(title);
        }

        private void AddHistoryItem(string title)
        {
            Border border = new Border
            {
                BorderBrush = (Brush)FindResource("TileBorder"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(4),
                Background = (Brush)FindResource("TileBg")
            };

            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };

            TextBlock textBlock = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(216, 226, 240)),
                TextWrapping = TextWrapping.Wrap,
                Width = 200,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Button selectBtn = new Button
            {
                Content = "Ustaw",
                Width = 70,
                Height = 28,
                Padding = new Thickness(5, 0, 5, 0),
                FontSize = 11,
                Background = (Brush)FindResource("AccentBrush")
            };

            selectBtn.Click += (s, e) =>
            {
                TxtTargetWindowTitle.Text = title;
                BtnSaveTargetWindowTitle_Click(s, e);
            };

            stack.Children.Add(textBlock);
            stack.Children.Add(selectBtn);
            border.Child = stack;

            PanelWindowTitleHistory.Children.Add(border);
        }

        private void AddToWindowTitleHistory(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            _settings.WindowTitleHistory.Remove(title);
            _settings.WindowTitleHistory.Insert(0, title);

            if (_settings.WindowTitleHistory.Count > 5)
                _settings.WindowTitleHistory.RemoveAt(_settings.WindowTitleHistory.Count - 1);

            RefreshWindowTitleHistory();
        }

        private void RefreshKopaczCommandsUI(int option)
        {
            StackPanel panel = option == 533 ? PanelKopacz533Commands : PanelKopacz633Commands;
            List<MinerCommand> commands = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
            panel.Children.Clear();

            foreach (var cmd in commands)
                AddCommandRow(panel, cmd, option);
        }

        private void AddCommandRow(StackPanel panel, MinerCommand cmd, int option)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            TextBlock label = new TextBlock { Text = "Co ile sekund:", Width = 120, VerticalAlignment = VerticalAlignment.Center };
            TextBox secondsBox = new TextBox { Text = cmd.Seconds.ToString(), Width = 60, Margin = new Thickness(10, 0, 10, 0) };

            TextBlock label2 = new TextBlock { Text = "Komenda:", Width = 80, VerticalAlignment = VerticalAlignment.Center };
            TextBox commandBox = new TextBox { Text = cmd.Command, Width = 150, Margin = new Thickness(10, 0, 10, 0) };

            Button deleteBtn = new Button
            {
                Content = "Usuń",
                Width = 60,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(210, 73, 73))
            };

            deleteBtn.Click += (s, e) =>
            {
                List<MinerCommand> list = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
                list.Remove(cmd);
                RefreshKopaczCommandsUI(option);
                MarkDirty();
            };

            row.Children.Add(label);
            row.Children.Add(secondsBox);
            row.Children.Add(label2);
            row.Children.Add(commandBox);
            row.Children.Add(deleteBtn);

            panel.Children.Add(row);

            secondsBox.TextChanged += (s, e) =>
            {
                MarkDirty();
                if (int.TryParse(secondsBox.Text, out int sec))
                    cmd.Seconds = sec;
            };

            commandBox.TextChanged += (s, e) =>
            {
                cmd.Command = commandBox.Text;
                MarkDirty();
            };
        }

        private void CbKopacz633Direction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelKopaczNaWprost == null || PanelKopaczDoGory == null) return;

            if (CbKopacz633Direction.SelectedIndex == 0)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Collapsed;
                PanelKopaczDoGory.Visibility = Visibility.Collapsed;
            }
            else if (CbKopacz633Direction.SelectedIndex == 1)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Visible;
                PanelKopaczDoGory.Visibility = Visibility.Collapsed;
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Collapsed;
                PanelKopaczDoGory.Visibility = Visibility.Visible;
            }
            UpdateKopaczUpwardInfoVisibility();

            if (_kopacz633RuntimeEnabled && !IsKopacz633DirectionSelected())
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
                UpdateStatusBar("Kopacz 6/3/3 zatrzymany: wybierz tryb 'Na wprost' lub 'Do góry'", "Orange");
            }

            if (_isLoadingUi)
                return;

            MarkDirty();
        }

        private void UpdateKopaczUpwardInfoVisibility()
        {
            if (PanelKopaczUpwardAfkInfo == null)
                return;

            bool kop633Enabled = ChkKopacz633Enabled?.IsChecked == true;
            bool showAfkInfo = kop633Enabled && CbKopacz633Direction?.SelectedIndex == 2;
            PanelKopaczUpwardAfkInfo.Visibility = showAfkInfo ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StartBindCapture(BindTarget target)
        {
            _bindCaptureTarget = target;
            UpdateBindCaptureVisuals();
            UpdateStatusBar($"BINDOWANIE: {GetBindTargetLabel(target)} - naciśnij klawisz", "Orange");
            Focus();
        }

        private void BindKeyBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingUi)
                return;
            if (sender is not TextBox textBox || !textBox.IsEnabled)
                return;
            if (!Enum.TryParse(textBox.Tag?.ToString(), out BindTarget target) || target == BindTarget.None)
                return;

            StartBindCapture(target);
            e.Handled = true;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (_bindCaptureTarget == BindTarget.None)
                return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                _bindCaptureTarget = BindTarget.None;
                UpdateBindCaptureVisuals();
                UpdateStatusBar("Bindowanie anulowane", "Orange");
                e.Handled = true;
                return;
            }
            if (key == Key.None)
                return;

            string keyText = key.ToString();
            BindTarget target = _bindCaptureTarget;
            _pendingBindValues[target] = keyText;
            RefreshBindSaveButton(target);

            // Prevent accidental macro toggle while the capture key is still held.
            _suppressBindToggleUntilRelease = true;

            _bindCaptureTarget = BindTarget.None;
            UpdateBindCaptureVisuals();
            UpdateStatusBar($"Wybrano klawisz: {keyText} ({GetBindTargetLabel(target)}) - kliknij \"Zapisz\"", "Orange");
            e.Handled = true;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (_bindCaptureTarget == BindTarget.None)
                return;

            string? keyText = e.ChangedButton switch
            {
                MouseButton.XButton1 => "MouseX1",
                MouseButton.XButton2 => "MouseX2",
                MouseButton.Middle => "MouseMiddle",
                _ => null
            };

            if (keyText == null)
                return;

            BindTarget target = _bindCaptureTarget;
            _pendingBindValues[target] = keyText;
            RefreshBindSaveButton(target);

            _suppressBindToggleUntilRelease = true;

            _bindCaptureTarget = BindTarget.None;
            UpdateBindCaptureVisuals();
            UpdateStatusBar($"Wybrano klawisz: {keyText} ({GetBindTargetLabel(target)}) - kliknij \"Zapisz\"", "Orange");
            e.Handled = true;
        }

        private void ConfirmPendingBind(BindTarget target)
        {
            if (!_pendingBindValues.TryGetValue(target, out string? keyText))
            {
                UpdateStatusBar($"Kliknij pole \"Klawisz\" dla {GetBindTargetLabel(target)}, potem naciśnij klawisz i kliknij \"Zapisz\".", "Orange");
                return;
            }

            TextBox? textBox = GetBindTextBox(target);
            if (textBox == null)
                return;

            textBox.Text = keyText;
            _pendingBindValues.Remove(target);
            RefreshBindSaveButton(target);

            MarkDirty();
            UpdateStatusBar($"Zapisano klawisz {keyText} dla {GetBindTargetLabel(target)}", "Green");
        }

        private void BtnMacroManualCapture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.HoldToggle);
        }

        private void BtnAutoLeftCapture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.AutoLeft);
        }

        private void BtnAutoRightCapture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.AutoRight);
        }

        private void BtnKopacz533Capture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.Kopacz533);
        }

        private void BtnKopacz633Capture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.Kopacz633);
        }

        private void BtnJablkaZLisciCapture_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.JablkaZLisci);
        }

        private void TxtJablkaZLisciCommand_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateStatusBar("Niezapisana komenda jabłek - kliknij \"Zapisz komendę\"", "Orange");
        }

        private void BtnSaveJablkaZLisciCommand_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            try
            {
                ReadFromUi(includeWindowTitle: false);
                _settings.JablkaZLisciCommand = TxtJablkaZLisciCommand.Text.Trim();
                _settingsService.Save(_settings);

                _pendingChanges = false;
                _dirtyTimer.Stop();
                TxtSettingsSaved.Text = "✓ Tak";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));

                UpdateStatusBar("Komenda jabłek zapisana", "Green");
            }
            catch (Exception ex)
            {
                UpdateStatusBar("Błąd zapisu komendy: " + ex.Message, "Red");
            }
        }

        // DODAWANIE KOMEND
        private void BtnKopacz533AddCommand_Click(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz533Commands.Add(new MinerCommand { Seconds = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(533);
            MarkDirty();
        }

        private void BtnKopacz633AddCommand_Click(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz633Commands.Add(new MinerCommand { Seconds = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(633);
            MarkDirty();
        }

        // AUTO-SAVE
        private void AnyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            MarkDirty();
            RefreshTopTiles();
        }

        private void TxtMinutesToSecondsInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtMinutesToSecondsOutput == null)
                return;

            string raw = TxtMinutesToSecondsInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                TxtMinutesToSecondsOutput.Text = "Wpisz liczbę minut, aby przeliczyć na sekundy.";
                TxtMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193));
                return;
            }

            string normalized = raw.Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) || minutes < 0)
            {
                TxtMinutesToSecondsOutput.Text = "Nieprawidłowa wartość. Przykład: 3 lub 30,5";
                TxtMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                return;
            }

            double seconds = minutes * 60.0;
            var pl = CultureInfo.GetCultureInfo("pl-PL");
            string minutesText = minutes.ToString("0.##", pl);
            string secondsText = seconds.ToString("0.##", pl);
            TxtMinutesToSecondsOutput.Text = $"{minutesText} min = {secondsText} s";
            TxtMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
        }

        private void TxtTargetWindowTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateStatusBar("Niezapisany tytuł okna - kliknij \"Zapisz okno\"", "Orange");
        }

        private void MarkDirty()
        {
            _pendingChanges = true;

            TxtSettingsSaved.Text = "✗ Nie";
            TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(255, 107, 107));

            _dirtyTimer.Stop();
            _dirtyTimer.Start();
        }

        private void AutoSaveSettings()
        {
            try
            {
                ReadFromUi(includeWindowTitle: false);
                _settingsService.Save(_settings);

                _pendingChanges = false;

                TxtSettingsSaved.Text = "✓ Tak";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));

                UpdateStatusBar("Ustawienia zapisane", "Green");
            }
            catch (Exception ex)
            {
                _pendingChanges = true;

                TxtSettingsSaved.Text = "Błąd";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(255, 107, 107));

                UpdateStatusBar("Błąd zapisu: " + ex.Message, "Red");
            }
        }

        private void ReadFromUi(bool includeWindowTitle = true)
        {
            // HOLD
            _settings.HoldEnabled = ChkMacroManualEnabled.IsChecked ?? false;
            _settings.HoldToggleKey = TxtMacroManualKey.Text.Trim();
            _settings.HoldLeftButton.MinCps = ParseNonNegativeInt(TxtManualLeftMinCps.Text);
            _settings.HoldLeftButton.MaxCps = ParseNonNegativeInt(TxtManualLeftMaxCps.Text);
            _settings.HoldRightButton.MinCps = ParseNonNegativeInt(TxtManualRightMinCps.Text);
            _settings.HoldRightButton.MaxCps = ParseNonNegativeInt(TxtManualRightMaxCps.Text);

            // AUTO
            _settings.AutoLeftButton.Enabled = ChkAutoLeftEnabled.IsChecked ?? false;
            _settings.AutoLeftButton.Key = TxtAutoLeftKey.Text.Trim();
            _settings.AutoLeftButton.MinCps = ParseNonNegativeInt(TxtAutoLeftMinCps.Text);
            _settings.AutoLeftButton.MaxCps = ParseNonNegativeInt(TxtAutoLeftMaxCps.Text);

            _settings.AutoRightButton.Enabled = ChkAutoRightEnabled.IsChecked ?? false;
            _settings.AutoRightButton.Key = TxtAutoRightKey.Text.Trim();
            _settings.AutoRightButton.MinCps = ParseNonNegativeInt(TxtAutoRightMinCps.Text);
            _settings.AutoRightButton.MaxCps = ParseNonNegativeInt(TxtAutoRightMaxCps.Text);

            // Legacy mirror for older settings format compatibility
            _settings.MacroLeftButton.Enabled = _settings.HoldEnabled;
            _settings.MacroLeftButton.Key = _settings.HoldToggleKey;
            _settings.MacroLeftButton.MinCps = _settings.HoldLeftButton.MinCps;
            _settings.MacroLeftButton.MaxCps = _settings.HoldLeftButton.MaxCps;

            _settings.MacroRightButton.Enabled = _settings.HoldEnabled;
            _settings.MacroRightButton.Key = _settings.HoldToggleKey;
            _settings.MacroRightButton.MinCps = _settings.HoldRightButton.MinCps;
            _settings.MacroRightButton.MaxCps = _settings.HoldRightButton.MaxCps;

            // KOPACZ
            _settings.Kopacz533Enabled = ChkKopacz533Enabled.IsChecked ?? false;
            _settings.Kopacz633Enabled = ChkKopacz633Enabled.IsChecked ?? false;
            _settings.Kopacz533Key = TxtKopacz533Key.Text.Trim();
            _settings.Kopacz633Key = TxtKopacz633Key.Text.Trim();

            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                _settings.Kopacz633Direction = "Na wprost";
                _settings.Kopacz633Width = ParseNonNegativeInt(TxtKopacz633Width.Text);
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                _settings.Kopacz633Direction = "Do góry";
                _settings.Kopacz633Width = ParseNonNegativeInt(TxtKopacz633WidthUp.Text);
                _settings.Kopacz633Length = ParseNonNegativeInt(TxtKopacz633LengthUp.Text);
            }
            else
            {
                _settings.Kopacz633Direction = "";
            }

            // JABŁKA Z LIŚCI
            _settings.JablkaZLisciEnabled = ChkJablkaZLisciEnabled.IsChecked ?? false;
            _settings.JablkaZLisciKey = TxtJablkaZLisciKey.Text.Trim();

            // EQ
            _settings.PauseWhenCursorVisible = ChkPauseWhenCursorVisible.IsChecked ?? true;

            if (includeWindowTitle)
            {
                _settings.TargetWindowTitle = TxtTargetWindowTitle.Text.Trim();
                TxtCurrentWindowTitle.Text = string.IsNullOrWhiteSpace(_settings.TargetWindowTitle) ? "Brak" : _settings.TargetWindowTitle;
            }
        }

        private static int ParseNonNegativeInt(string value)
        {
            if (int.TryParse(value, out int parsed) && parsed >= 0)
                return parsed;
            return 0;
        }

        private string GetRuntimeStateLabel(bool enabled)
        {
            if (!enabled)
                return "OFF";
            if (_isPausedByCursorVisibility)
                return "PAUZA";
            return "ON";
        }

        private void UpdateCursorPauseTile()
        {
            if (BorderCursorPauseStatus == null || TxtCursorPauseStatus == null)
                return;

            Brush activeBg = (Brush)(TryFindResource("TileBgActive") ?? new SolidColorBrush(Color.FromRgb(23, 50, 74)));
            Brush inactiveBg = (Brush)(TryFindResource("TileBg") ?? new SolidColorBrush(Color.FromRgb(30, 42, 57)));
            Brush inactiveBorder = (Brush)(TryFindResource("TileBorder") ?? new SolidColorBrush(Color.FromRgb(62, 83, 110)));
            Brush activeBorder = (Brush)(TryFindResource("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(46, 168, 255)));

            bool pauseOptionEnabled = ChkPauseWhenCursorVisible?.IsChecked == true;

            if (!pauseOptionEnabled)
            {
                BorderCursorPauseStatus.Background = inactiveBg;
                BorderCursorPauseStatus.BorderBrush = inactiveBorder;
                BorderCursorPauseStatus.Opacity = 0.85;
                TxtCursorPauseStatus.Text = "Wyłączona";
                TxtCursorPauseStatus.Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193));
                return;
            }

            if (_isPausedByCursorVisibility)
            {
                BorderCursorPauseStatus.Background = activeBg;
                BorderCursorPauseStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                BorderCursorPauseStatus.Opacity = 1.0;
                TxtCursorPauseStatus.Text = "Aktywna (kursor)";
                TxtCursorPauseStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                return;
            }

            BorderCursorPauseStatus.Background = activeBg;
            BorderCursorPauseStatus.BorderBrush = activeBorder;
            BorderCursorPauseStatus.Opacity = 1.0;
            TxtCursorPauseStatus.Text = "Gotowa";
            TxtCursorPauseStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
        }

        private static bool IsCursorCurrentlyVisible()
        {
            CURSORINFO cursorInfo = new CURSORINFO
            {
                cbSize = Marshal.SizeOf<CURSORINFO>()
            };

            if (!GetCursorInfo(out cursorInfo))
                return false;

            return (cursorInfo.flags & CURSOR_SHOWING) == CURSOR_SHOWING;
        }

        private bool IsInventoryCursorVisible()
        {
            if (!IsCursorCurrentlyVisible())
                return false;

            if (_focusedGameWindowHandle != IntPtr.Zero && IsCursorClippedToWindowClient(_focusedGameWindowHandle))
                return false;

            return true;
        }

        private static bool IsCursorClippedToWindowClient(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;
            if (!GetClipCursor(out RECT clipRect))
                return false;
            if (!GetClientRect(windowHandle, out RECT clientRect))
                return false;

            POINT topLeft = new POINT { X = clientRect.Left, Y = clientRect.Top };
            POINT bottomRight = new POINT { X = clientRect.Right, Y = clientRect.Bottom };

            if (!ClientToScreen(windowHandle, ref topLeft))
                return false;
            if (!ClientToScreen(windowHandle, ref bottomRight))
                return false;

            RECT clientRectOnScreen = new RECT
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = bottomRight.X,
                Bottom = bottomRight.Y
            };

            const int tolerance = 4;
            return Math.Abs(clipRect.Left - clientRectOnScreen.Left) <= tolerance
                && Math.Abs(clipRect.Top - clientRectOnScreen.Top) <= tolerance
                && Math.Abs(clipRect.Right - clientRectOnScreen.Right) <= tolerance
                && Math.Abs(clipRect.Bottom - clientRectOnScreen.Bottom) <= tolerance;
        }

        private void SetCursorPauseState(bool paused)
        {
            if (_isPausedByCursorVisibility == paused)
                return;

            _isPausedByCursorVisibility = paused;

            if (paused)
                UpdateStatusBar("Pauza makra: widoczny kursor (ekwipunek/GUI)", "Orange");
            else
                UpdateStatusBar("Makro wznowione", "Orange");

            RefreshTopTiles();
        }

        private static bool TryGetVirtualKey(string keyText, out int virtualKey)
        {
            virtualKey = 0;

            if (string.IsNullOrWhiteSpace(keyText))
                return false;
            string normalized = keyText.Trim();
            switch (normalized.ToUpperInvariant())
            {
                case "MOUSEMIDDLE":
                    virtualKey = VK_MBUTTON;
                    return true;
                case "MOUSEX1":
                    virtualKey = VK_XBUTTON1;
                    return true;
                case "MOUSEX2":
                    virtualKey = VK_XBUTTON2;
                    return true;
            }

            if (!Enum.TryParse(normalized, true, out Key key))
                return false;

            virtualKey = KeyInterop.VirtualKeyFromKey(key);
            return virtualKey != 0;
        }

        private static bool IsVirtualKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsBindPressed(string keyText, ref bool wasDown)
        {
            if (!TryGetVirtualKey(keyText, out int virtualKey))
            {
                wasDown = false;
                return false;
            }

            bool isDown = IsVirtualKeyDown(virtualKey);
            bool justPressed = isDown && !wasDown;
            wasDown = isDown;
            return justPressed;
        }

        private static bool IsConfiguredBindKeyDown(string keyText)
        {
            return TryGetVirtualKey(keyText, out int virtualKey) && IsVirtualKeyDown(virtualKey);
        }

        private bool TryToggleHoldLeftClicking(DateTime now)
        {
            bool leftDown = IsVirtualKeyDown(VK_LBUTTON);

            if (!leftDown)
            {
                if (!_holdLeftToggleWasDown)
                    return false;

                _holdLeftToggleWasDown = false;

                if (_holdLeftToggleDownStartedAtUtc == DateTime.MinValue)
                    return false;

                double heldMs = (now - _holdLeftToggleDownStartedAtUtc).TotalMilliseconds;
                _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;

                if (heldMs < HoldLeftTogglePressMinMs)
                    return false;

                _holdLeftToggleClickingEnabled = !_holdLeftToggleClickingEnabled;
                _nextHoldLeftClickAtUtc = now;
                return true;
            }

            if (_holdLeftToggleWasDown)
                return false;

            _holdLeftToggleWasDown = true;
            _holdLeftToggleDownStartedAtUtc = now;
            return false;
        }

        private void ResetHoldLeftToggleState(bool clearToggleEnabled)
        {
            if (clearToggleEnabled)
                _holdLeftToggleClickingEnabled = false;

            _holdLeftToggleWasDown = false;
            _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
            _nextHoldLeftClickAtUtc = DateTime.UtcNow;
            _holdRightRuntimePressActive = false;
        }

        private void RunMacroTick(object? sender, EventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (_bindCaptureTarget != BindTarget.None)
                return;

            if (_suppressBindToggleUntilRelease)
            {
                bool holdDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                bool autoLeftDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                bool autoRightDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                bool jablkaDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                bool kop533Down = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                bool kop633Down = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);

                _holdBindWasDown = holdDown;
                _autoLeftBindWasDown = autoLeftDown;
                _autoRightBindWasDown = autoRightDown;
                _jablkaBindWasDown = jablkaDown;
                _kopacz533BindWasDown = kop533Down;
                _kopacz633BindWasDown = kop633Down;

                if (holdDown || autoLeftDown || autoRightDown || jablkaDown || kop533Down || kop633Down)
                    return;

                _suppressBindToggleUntilRelease = false;
            }

            // Bind toggles can be changed only while Minecraft window has focus.
            if (!_isMinecraftFocused)
            {
                // Keep key state in sync to avoid accidental toggle right after refocus.
                _holdBindWasDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                _jablkaBindWasDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                _kopacz533BindWasDown = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                _kopacz633BindWasDown = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);

                SetCursorPauseState(false);
                SetKopacz533MiningHold(false);
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetHoldLeftToggleState(clearToggleEnabled: false);
                return;
            }

            bool changed = false;

            bool holdModeSelected = ChkMacroManualEnabled.IsChecked == true;
            bool autoLeftModeSelected = ChkAutoLeftEnabled.IsChecked == true;
            bool autoRightModeSelected = ChkAutoRightEnabled.IsChecked == true;
            bool jablkaModeSelected = ChkJablkaZLisciEnabled.IsChecked == true;
            bool kop533ModeSelected = ChkKopacz533Enabled.IsChecked == true;
            bool kop633ModeSelected = ChkKopacz633Enabled.IsChecked == true;
            bool internalCommandTyping =
                _jablkaCommandStage != JablkaCommandStage.None ||
                _kopacz533CommandStage != Kopacz533CommandStage.None ||
                _kopacz633CommandStage != Kopacz633CommandStage.None;

            if (internalCommandTyping)
            {
                // Prevent self-trigger: internal typed keys (chat commands) cannot toggle bind states.
                _holdBindWasDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                _jablkaBindWasDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                _kopacz533BindWasDown = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                _kopacz633BindWasDown = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);
            }

            if (!internalCommandTyping && IsBindPressed(TxtMacroManualKey.Text, ref _holdBindWasDown) && holdModeSelected)
            {
                _holdMacroRuntimeEnabled = !_holdMacroRuntimeEnabled;
                ResetHoldLeftToggleState(clearToggleEnabled: true);
                UpdateStatusBar(_holdMacroRuntimeEnabled ? "HOLD aktywowane" : "HOLD wyłączone", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtAutoLeftKey.Text, ref _autoLeftBindWasDown) && autoLeftModeSelected)
            {
                _autoLeftRuntimeEnabled = !_autoLeftRuntimeEnabled;
                UpdateStatusBar(_autoLeftRuntimeEnabled ? "AUTO LPM aktywowane" : "AUTO LPM wyłączone", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtAutoRightKey.Text, ref _autoRightBindWasDown) && autoRightModeSelected)
            {
                _autoRightRuntimeEnabled = !_autoRightRuntimeEnabled;
                UpdateStatusBar(_autoRightRuntimeEnabled ? "AUTO PPM aktywowane" : "AUTO PPM wyłączone", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtJablkaZLisciKey.Text, ref _jablkaBindWasDown) && jablkaModeSelected)
            {
                _jablkaRuntimeEnabled = !_jablkaRuntimeEnabled;
                ResetJablkaRuntimeState();
                UpdateStatusBar(_jablkaRuntimeEnabled ? "Jabłka z liści aktywowane" : "Jabłka z liści wyłączone", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtKopacz533Key.Text, ref _kopacz533BindWasDown) && kop533ModeSelected)
            {
                _kopacz533RuntimeEnabled = !_kopacz533RuntimeEnabled;
                if (_kopacz533RuntimeEnabled)
                    StartKopacz533Runtime(DateTime.UtcNow);
                else
                {
                    SetKopacz533MiningHold(false);
                    ResetKopacz533RuntimeState();
                }

                UpdateStatusBar(_kopacz533RuntimeEnabled ? "Kopacz 5/3/3 aktywowany" : "Kopacz 5/3/3 wyłączony", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtKopacz633Key.Text, ref _kopacz633BindWasDown) && kop633ModeSelected)
            {
                bool invalidDirection = false;
                _kopacz633RuntimeEnabled = !_kopacz633RuntimeEnabled;
                if (_kopacz633RuntimeEnabled)
                {
                    if (!IsKopacz633DirectionSelected())
                    {
                        _kopacz633RuntimeEnabled = false;
                        invalidDirection = true;
                    }
                    else
                    {
                        StartKopacz633Runtime(DateTime.UtcNow);
                    }
                }
                else
                {
                    SetKopacz633AttackHold(false);
                    SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                    ResetKopacz633RuntimeState();
                }

                if (invalidDirection)
                    UpdateStatusBar("Kopacz 6/3/3: wybierz kierunek 'Na wprost' lub 'Do góry'", "Orange");
                else
                    UpdateStatusBar(_kopacz633RuntimeEnabled ? "Kopacz 6/3/3 aktywowany" : "Kopacz 6/3/3 wyłączony", "Orange");
                changed = true;
            }

            if (!holdModeSelected && _holdMacroRuntimeEnabled)
            {
                _holdMacroRuntimeEnabled = false;
                ResetHoldLeftToggleState(clearToggleEnabled: true);
                changed = true;
            }
            if (!autoLeftModeSelected && _autoLeftRuntimeEnabled)
            {
                _autoLeftRuntimeEnabled = false;
                changed = true;
            }
            if (!autoRightModeSelected && _autoRightRuntimeEnabled)
            {
                _autoRightRuntimeEnabled = false;
                changed = true;
            }
            if (!jablkaModeSelected && _jablkaRuntimeEnabled)
            {
                _jablkaRuntimeEnabled = false;
                ResetJablkaRuntimeState();
                changed = true;
            }
            if (!kop533ModeSelected && _kopacz533RuntimeEnabled)
            {
                _kopacz533RuntimeEnabled = false;
                SetKopacz533MiningHold(false);
                ResetKopacz533RuntimeState();
                changed = true;
            }
            if (!kop633ModeSelected && _kopacz633RuntimeEnabled)
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
                changed = true;
            }

            if (changed)
                RefreshTopTiles();

            DateTime now = DateTime.UtcNow;
            bool pauseWhenCursorVisible = ChkPauseWhenCursorVisible.IsChecked == true;
            bool jablkaCommandInProgress = _jablkaCommandStage != JablkaCommandStage.None;
            bool anyCursorPauseMacroRuntimeActive =
                (holdModeSelected && _holdMacroRuntimeEnabled) ||
                (autoLeftModeSelected && _autoLeftRuntimeEnabled) ||
                (autoRightModeSelected && _autoRightRuntimeEnabled) ||
                (jablkaModeSelected && _jablkaRuntimeEnabled);

            // Cursor-pause applies only to PVP/Jabłka modes (not Kopacz).
            // During internal command sequence (chat open -> type -> enter) ignore cursor pause.
            bool shouldPauseForCursor = pauseWhenCursorVisible
                && anyCursorPauseMacroRuntimeActive
                && !jablkaCommandInProgress
                && IsInventoryCursorVisible();

            SetCursorPauseState(shouldPauseForCursor);
            if (shouldPauseForCursor)
            {
                _nextHoldLeftClickAtUtc = now;
                _nextHoldRightClickAtUtc = now;
                ResetHoldLeftToggleState(clearToggleEnabled: false);
                _nextAutoLeftClickAtUtc = now;
                _nextAutoRightClickAtUtc = now;
                ResetJablkaRuntimeState(now);
                ResetKopacz533RuntimeState(now);
                SetKopacz533MiningHold(false);
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState(now);
                return;
            }

            if (holdModeSelected && _holdMacroRuntimeEnabled)
            {
                if (TryToggleHoldLeftClicking(now))
                {
                    UpdateStatusBar(_holdLeftToggleClickingEnabled ? "HOLD LPM: ON (kliknij LPM ponownie aby wyłączyć)" : "HOLD LPM: OFF", "Orange");
                    RefreshTopTiles();
                }

                bool rightHoldWasActive = _holdRightRuntimePressActive;
                bool rightHoldActive = IsVirtualKeyDown(VK_RBUTTON);
                if (rightHoldActive != rightHoldWasActive)
                {
                    _holdRightRuntimePressActive = rightHoldActive;
                    RefreshTopTiles();
                }

                if (_holdLeftToggleClickingEnabled)
                {
                    TryPerformClick(ref _nextHoldLeftClickAtUtc, TxtManualLeftMinCps.Text, TxtManualLeftMaxCps.Text, leftButton: true, now, holdPulseMode: false);
                }
                else
                    _nextHoldLeftClickAtUtc = now;

                if (rightHoldActive)
                    TryPerformClick(ref _nextHoldRightClickAtUtc, TxtManualRightMinCps.Text, TxtManualRightMaxCps.Text, leftButton: false, now, holdPulseMode: true);
                else
                {
                    _nextHoldRightClickAtUtc = now;
                    if (rightHoldWasActive)
                        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                }
            }
            else
            {
                _nextHoldLeftClickAtUtc = now;
                _nextHoldRightClickAtUtc = now;
                ResetHoldLeftToggleState(clearToggleEnabled: true);
            }

            if (autoLeftModeSelected && _autoLeftRuntimeEnabled)
                TryPerformClick(ref _nextAutoLeftClickAtUtc, TxtAutoLeftMinCps.Text, TxtAutoLeftMaxCps.Text, leftButton: true, now, holdPulseMode: false);
            else
                _nextAutoLeftClickAtUtc = now;

            if (autoRightModeSelected && _autoRightRuntimeEnabled)
                TryPerformClick(ref _nextAutoRightClickAtUtc, TxtAutoRightMinCps.Text, TxtAutoRightMaxCps.Text, leftButton: false, now, holdPulseMode: false);
            else
                _nextAutoRightClickAtUtc = now;

            if (kop533ModeSelected && _kopacz533RuntimeEnabled)
                RunKopacz533Tick(now);
            else
            {
                SetKopacz533MiningHold(false);
                ResetKopacz533RuntimeState(now);
            }

            if (kop633ModeSelected && _kopacz633RuntimeEnabled)
                RunKopacz633Tick(now);
            else
            {
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState(now);
            }

            if (jablkaModeSelected && _jablkaRuntimeEnabled)
            {
                if (!TryProcessJablkaCommand(now))
                    TryPerformJablkaAction(now);
            }
            else
            {
                ResetJablkaRuntimeState(now);
            }

            RefreshLiveTopTiles(now);
        }

        private void StartKopacz533Runtime(DateTime now)
        {
            ResetKopacz533RuntimeState(now);
            _kopacz533RuntimeStartedAtUtc = now;

            if (TryPeekNextKopacz533Command(out _, out _, out int firstDelaySeconds))
            {
                _kopacz533CommandSequenceCompleted = false;
                _nextKopacz533CommandAtUtc = now.AddSeconds(firstDelaySeconds);
            }
            else
            {
                _kopacz533CommandSequenceCompleted = true;
                _nextKopacz533CommandAtUtc = DateTime.MaxValue;
            }

            _nextRuntimeTileRefreshAtUtc = now;
        }

        private bool IsKopacz633DirectionSelected()
        {
            int selectedIndex = CbKopacz633Direction.SelectedIndex;
            return selectedIndex == 1 || selectedIndex == 2;
        }

        private int GetConfiguredKopacz633ForwardWidth()
        {
            return Math.Max(1, ParseNonNegativeInt(TxtKopacz633Width.Text));
        }

        private int GetConfiguredKopacz633UpwardWidth()
        {
            return Math.Max(1, ParseNonNegativeInt(TxtKopacz633WidthUp.Text));
        }

        private int GetConfiguredKopacz633UpwardLength()
        {
            return Math.Max(1, ParseNonNegativeInt(TxtKopacz633LengthUp.Text));
        }

        private void StartKopacz633Runtime(DateTime now)
        {
            ResetKopacz633RuntimeState(now);
            _kopacz633RuntimeStartedAtUtc = now;

            if (TryPeekNextKopacz633Command(out _, out _, out int firstDelaySeconds))
            {
                _kopacz633CommandSequenceCompleted = false;
                _nextKopacz633CommandAtUtc = now.AddSeconds(firstDelaySeconds);
            }
            else
            {
                _kopacz633CommandSequenceCompleted = true;
                _nextKopacz633CommandAtUtc = DateTime.MaxValue;
            }

            _kopacz633UpwardLegIndex = 0;
            StartKopacz633NextMovementLeg(now);
            SetKopacz633AttackHold(true);
            _nextRuntimeTileRefreshAtUtc = now;
        }

        private void RunKopacz633Tick(DateTime now)
        {
            if (!IsKopacz633DirectionSelected())
            {
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                return;
            }

            if (TryProcessKopacz633Command(now))
                return;

            if (_kopacz633ResumeMiningPending)
            {
                if (now < _nextKopacz633ResumeAtUtc)
                    return;

                _kopacz633ResumeMiningPending = false;
            }

            SetKopacz633AttackHold(true);

            if (_kopacz633StrafeDirection == Kopacz633StrafeDirection.None || now >= _kopacz633MovementLegEndAtUtc)
                StartKopacz633NextMovementLeg(now);

            if (_kopacz633CommandSequenceCompleted)
                return;

            if (now < _nextKopacz633CommandAtUtc)
                return;

            if (!TryPeekNextKopacz633Command(out int commandIndex, out string command, out int delaySeconds))
            {
                _kopacz633CommandSequenceCompleted = true;
                _nextKopacz633CommandAtUtc = DateTime.MaxValue;
                return;
            }

            _kopacz633PendingCommandIndex = commandIndex;
            _kopacz633PendingCommand = command;
            _kopacz633CommandStage = Kopacz633CommandStage.OpenChat;
            _nextKopacz633StageAtUtc = now;
            _nextKopacz633CommandAtUtc = now.AddSeconds(Math.Max(1, delaySeconds));
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
        }

        private void StartKopacz633NextMovementLeg(DateTime now)
        {
            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                int widthBlocks = GetConfiguredKopacz633ForwardWidth();
                Kopacz633StrafeDirection nextDirection = _kopacz633StrafeDirection == Kopacz633StrafeDirection.Right
                    ? Kopacz633StrafeDirection.Left
                    : Kopacz633StrafeDirection.Right;

                StartKopacz633MovementLeg(nextDirection, widthBlocks, now);
                return;
            }

            if (CbKopacz633Direction.SelectedIndex == 2)
            {
                int widthBlocks = GetConfiguredKopacz633UpwardWidth();
                int lengthBlocks = GetConfiguredKopacz633UpwardLength();

                Kopacz633StrafeDirection legDirection = _kopacz633UpwardLegIndex switch
                {
                    0 => Kopacz633StrafeDirection.Forward,
                    1 => Kopacz633StrafeDirection.Right,
                    2 => Kopacz633StrafeDirection.Backward,
                    _ => Kopacz633StrafeDirection.Left
                };

                int legBlocks = _kopacz633UpwardLegIndex % 2 == 0 ? lengthBlocks : widthBlocks;
                _kopacz633UpwardLegIndex = (_kopacz633UpwardLegIndex + 1) % 4;
                StartKopacz633MovementLeg(legDirection, legBlocks, now);
                return;
            }

            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            _kopacz633MovementLegEndAtUtc = now;
        }

        private void StartKopacz633MovementLeg(Kopacz633StrafeDirection direction, int widthBlocks, DateTime now)
        {
            widthBlocks = Math.Max(1, widthBlocks);
            SetKopacz633StrafeDirection(direction);
            _kopacz633MovementLegEndAtUtc = now.AddMilliseconds(widthBlocks * Kopacz633MsPerBlock);
        }

        private void RunKopacz533Tick(DateTime now)
        {
            if (TryProcessKopacz533Command(now))
                return;

            if (_kopacz533ResumeMiningPending)
            {
                if (now < _nextKopacz533ResumeAtUtc)
                    return;

                _kopacz533ResumeMiningPending = false;
            }

            SetKopacz533MiningHold(true);

            if (_kopacz533CommandSequenceCompleted)
                return;

            if (now < _nextKopacz533CommandAtUtc)
                return;

            if (!TryPeekNextKopacz533Command(out int commandIndex, out string command, out int delaySeconds))
            {
                _kopacz533CommandSequenceCompleted = true;
                _nextKopacz533CommandAtUtc = DateTime.MaxValue;
                return;
            }

            _kopacz533PendingCommandIndex = commandIndex;
            _kopacz533PendingCommand = command;
            _kopacz533CommandStage = Kopacz533CommandStage.OpenChat;
            _nextKopacz533StageAtUtc = now;
            _nextKopacz533CommandAtUtc = now.AddSeconds(Math.Max(1, delaySeconds));
            SetKopacz533MiningHold(false);
        }

        private bool TryProcessKopacz533Command(DateTime now)
        {
            if (_kopacz533CommandStage == Kopacz533CommandStage.None)
                return false;

            if (now < _nextKopacz533StageAtUtc)
                return true;

            switch (_kopacz533CommandStage)
            {
                case Kopacz533CommandStage.OpenChat:
                    SendKeyTap(VK_T);
                    _kopacz533CommandStage = Kopacz533CommandStage.TypeCommand;
                    _nextKopacz533StageAtUtc = now.AddMilliseconds(Kopacz533DelayAfterOpenChatMs);
                    return true;

                case Kopacz533CommandStage.TypeCommand:
                    if (!SendTextByKeyboard(_kopacz533PendingCommand))
                    {
                        UpdateStatusBar("Błąd: nie udało się wpisać komendy kopacza 5/3/3", "Red");
                        _nextKopacz533CommandAtUtc = now.AddSeconds(1);
                        _kopacz533CommandStage = Kopacz533CommandStage.None;
                        _kopacz533PendingCommandIndex = -1;
                        _kopacz533PendingCommand = string.Empty;
                        SetKopacz533MiningHold(true);
                        return true;
                    }

                    _kopacz533CommandStage = Kopacz533CommandStage.SubmitCommand;
                    _nextKopacz533StageAtUtc = now.AddMilliseconds(Kopacz533DelayAfterTypeCommandMs);
                    return true;

                case Kopacz533CommandStage.SubmitCommand:
                    SendKeyTap(VK_RETURN);
                    if (_kopacz533PendingCommandIndex >= 0)
                        _kopacz533CommandIndex = _kopacz533PendingCommandIndex + 1;

                    if (TryPeekNextKopacz533Command(out _, out _, out int nextDelaySeconds))
                    {
                        _kopacz533CommandSequenceCompleted = false;
                        _nextKopacz533CommandAtUtc = now.AddSeconds(nextDelaySeconds);
                    }
                    else
                    {
                        _kopacz533CommandSequenceCompleted = true;
                        _nextKopacz533CommandAtUtc = DateTime.MaxValue;
                    }

                    _kopacz533CommandStage = Kopacz533CommandStage.None;
                    _kopacz533PendingCommandIndex = -1;
                    _kopacz533PendingCommand = string.Empty;
                    // Force re-arm of mining after chat closes to avoid sticky "no dig" state.
                    SetKopacz533MiningHold(false);
                    _kopacz533ResumeMiningPending = true;
                    _nextKopacz533ResumeAtUtc = now.AddMilliseconds(Kopacz533DelayAfterSubmitResumeMs);
                    return true;

                default:
                    return false;
            }
        }

        private bool TryProcessKopacz633Command(DateTime now)
        {
            if (_kopacz633CommandStage == Kopacz633CommandStage.None)
                return false;

            if (now < _nextKopacz633StageAtUtc)
                return true;

            switch (_kopacz633CommandStage)
            {
                case Kopacz633CommandStage.OpenChat:
                    SendKeyTap(VK_T);
                    _kopacz633CommandStage = Kopacz633CommandStage.TypeCommand;
                    _nextKopacz633StageAtUtc = now.AddMilliseconds(Kopacz633DelayAfterOpenChatMs);
                    return true;

                case Kopacz633CommandStage.TypeCommand:
                    if (!SendTextByKeyboard(_kopacz633PendingCommand))
                    {
                        UpdateStatusBar("Błąd: nie udało się wpisać komendy kopacza 6/3/3", "Red");
                        _nextKopacz633CommandAtUtc = now.AddSeconds(1);
                        _kopacz633CommandStage = Kopacz633CommandStage.None;
                        _kopacz633PendingCommandIndex = -1;
                        _kopacz633PendingCommand = string.Empty;
                        SetKopacz633AttackHold(true);
                        return true;
                    }

                    _kopacz633CommandStage = Kopacz633CommandStage.SubmitCommand;
                    _nextKopacz633StageAtUtc = now.AddMilliseconds(Kopacz633DelayAfterTypeCommandMs);
                    return true;

                case Kopacz633CommandStage.SubmitCommand:
                    SendKeyTap(VK_RETURN);
                    if (_kopacz633PendingCommandIndex >= 0)
                        _kopacz633CommandIndex = _kopacz633PendingCommandIndex + 1;

                    if (TryPeekNextKopacz633Command(out _, out _, out int nextDelaySeconds))
                    {
                        _kopacz633CommandSequenceCompleted = false;
                        _nextKopacz633CommandAtUtc = now.AddSeconds(nextDelaySeconds);
                    }
                    else
                    {
                        _kopacz633CommandSequenceCompleted = true;
                        _nextKopacz633CommandAtUtc = DateTime.MaxValue;
                    }

                    _kopacz633CommandStage = Kopacz633CommandStage.None;
                    _kopacz633PendingCommandIndex = -1;
                    _kopacz633PendingCommand = string.Empty;
                    SetKopacz633AttackHold(false);
                    SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                    _kopacz633ResumeMiningPending = true;
                    _nextKopacz633ResumeAtUtc = now.AddMilliseconds(Kopacz633DelayAfterSubmitResumeMs);
                    return true;

                default:
                    return false;
            }
        }

        private bool TryPeekNextKopacz633Command(out int commandIndex, out string command, out int delaySeconds)
        {
            return TryGetKopacz633CommandAtOrAfter(_kopacz633CommandIndex, out commandIndex, out command, out delaySeconds);
        }

        private bool TryGetKopacz633CommandAtOrAfter(int startIndex, out int commandIndex, out string command, out int delaySeconds)
        {
            commandIndex = -1;
            command = string.Empty;
            delaySeconds = 3;

            if (_settings.Kopacz633Commands == null || _settings.Kopacz633Commands.Count == 0)
                return false;

            int count = _settings.Kopacz633Commands.Count;
            if (startIndex < 0)
                startIndex = 0;
            else if (startIndex >= count)
                startIndex = 0;

            for (int attempt = 0; attempt < count; attempt++)
            {
                int i = (startIndex + attempt) % count;
                MinerCommand cmd = _settings.Kopacz633Commands[i];

                string cmdText = (cmd.Command ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cmdText))
                    continue;

                commandIndex = i;
                command = cmdText;
                delaySeconds = Math.Max(1, cmd.Seconds);
                return true;
            }

            return false;
        }

        private bool TryPeekNextKopacz533Command(out int commandIndex, out string command, out int delaySeconds)
        {
            return TryGetKopacz533CommandAtOrAfter(_kopacz533CommandIndex, out commandIndex, out command, out delaySeconds);
        }

        private bool TryGetKopacz533CommandAtOrAfter(int startIndex, out int commandIndex, out string command, out int delaySeconds)
        {
            commandIndex = -1;
            command = string.Empty;
            delaySeconds = 3;

            if (_settings.Kopacz533Commands == null || _settings.Kopacz533Commands.Count == 0)
                return false;

            int count = _settings.Kopacz533Commands.Count;
            if (startIndex < 0)
                startIndex = 0;
            else if (startIndex >= count)
                startIndex = 0;

            // Wrap around the list so after the last command we return to the first one.
            for (int attempt = 0; attempt < count; attempt++)
            {
                int i = (startIndex + attempt) % count;
                MinerCommand cmd = _settings.Kopacz533Commands[i];

                string cmdText = (cmd.Command ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(cmdText))
                    continue;

                commandIndex = i;
                command = cmdText;
                delaySeconds = Math.Max(1, cmd.Seconds);
                return true;
            }

            return false;
        }

        private void SetKopacz533MiningHold(bool enabled)
        {
            if (_kopacz533Holding == enabled)
                return;

            if (enabled)
            {
                SendKeyDown(VK_SHIFT);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                SendKeyUp(VK_SHIFT);
            }

            _kopacz533Holding = enabled;
        }

        private void SetKopacz633AttackHold(bool enabled)
        {
            if (_kopacz633HoldingAttack == enabled)
                return;

            if (enabled)
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            else
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

            _kopacz633HoldingAttack = enabled;
        }

        private void SetKopacz633StrafeDirection(Kopacz633StrafeDirection direction)
        {
            if (_kopacz633StrafeDirection == direction)
                return;

            switch (_kopacz633StrafeDirection)
            {
                case Kopacz633StrafeDirection.Forward:
                    SendKeyUp(VK_W);
                    break;
                case Kopacz633StrafeDirection.Right:
                    SendKeyUp(VK_D);
                    break;
                case Kopacz633StrafeDirection.Backward:
                    SendKeyUp(VK_S);
                    break;
                case Kopacz633StrafeDirection.Left:
                    SendKeyUp(VK_A);
                    break;
            }

            switch (direction)
            {
                case Kopacz633StrafeDirection.Forward:
                    SendKeyDown(VK_W);
                    break;
                case Kopacz633StrafeDirection.Right:
                    SendKeyDown(VK_D);
                    break;
                case Kopacz633StrafeDirection.Backward:
                    SendKeyDown(VK_S);
                    break;
                case Kopacz633StrafeDirection.Left:
                    SendKeyDown(VK_A);
                    break;
                default:
                    SendKeyUp(VK_W);
                    SendKeyUp(VK_D);
                    SendKeyUp(VK_S);
                    SendKeyUp(VK_A);
                    break;
            }

            _kopacz633StrafeDirection = direction;
        }

        private bool TryPerformClick(ref DateTime nextClickAtUtc, string minText, string maxText, bool leftButton, DateTime now, bool holdPulseMode)
        {
            if (!TryGetCpsRange(minText, maxText, out int minCps, out int maxCps))
            {
                nextClickAtUtc = now.AddMilliseconds(100);
                return false;
            }

            if (now < nextClickAtUtc)
                return false;

            SendMouseClick(leftButton, holdPulseMode);

            int cps = minCps == maxCps ? minCps : _random.Next(minCps, maxCps + 1);
            double delayMs = 1000.0 / cps;
            nextClickAtUtc = now.AddMilliseconds(delayMs);
            return true;
        }

        private void TryPerformJablkaAction(DateTime now)
        {
            if (now < _nextJablkaActionAtUtc)
                return;

            bool slotOneNow = _jablkaUseSlotOneNext;

            if (slotOneNow)
            {
                SendKeyTap(VK_1);
                SendMouseClick(leftButton: true, holdPulseMode: false);
            }
            else
            {
                SendKeyTap(VK_2);
                SendMouseClick(leftButton: false, holdPulseMode: false);
                _jablkaCompletedCycles++;

                if (_jablkaCompletedCycles >= JablkaCommandCycleThreshold)
                {
                    if (HasJablkaCommandConfigured())
                    {
                        _jablkaCommandStage = JablkaCommandStage.OpenChat;
                        _nextJablkaCommandStageAtUtc = now;
                    }
                    else
                    {
                        _jablkaCompletedCycles = 0;
                    }
                }
            }

            _jablkaUseSlotOneNext = !slotOneNow;

            // Slot 1 (nożyce + LPM) needs a slightly longer gap before switching to slot 2.
            int nextDelayMs = slotOneNow ? 75 : 40;
            _nextJablkaActionAtUtc = now.AddMilliseconds(nextDelayMs);
        }

        private bool TryProcessJablkaCommand(DateTime now)
        {
            if (_jablkaCommandStage == JablkaCommandStage.None)
                return false;

            if (now < _nextJablkaCommandStageAtUtc)
                return true;

            switch (_jablkaCommandStage)
            {
                case JablkaCommandStage.OpenChat:
                    SendKeyTap(VK_1);
                    SendKeyTap(VK_T);
                    _jablkaCommandStage = JablkaCommandStage.PasteCommand;
                    _nextJablkaCommandStageAtUtc = now.AddMilliseconds(JablkaDelayAfterOpenChatMs);
                    return true;

                case JablkaCommandStage.PasteCommand:
                    if (!TryInsertJablkaCommand())
                    {
                        UpdateStatusBar("Błąd: nie udało się wstawić komendy jabłek", "Red");
                        ResetJablkaRuntimeState(now.AddMilliseconds(JablkaDelayAfterCommandMs));
                        return true;
                    }

                    _jablkaCommandStage = JablkaCommandStage.SubmitCommand;
                    _nextJablkaCommandStageAtUtc = now.AddMilliseconds(JablkaDelayAfterInsertCommandMs);
                    return true;

                case JablkaCommandStage.SubmitCommand:
                    SendKeyTap(VK_RETURN);
                    ResetJablkaRuntimeState(now.AddMilliseconds(JablkaDelayAfterCommandMs));
                    return true;

                default:
                    return false;
            }
        }

        private bool HasJablkaCommandConfigured()
        {
            return !string.IsNullOrWhiteSpace(TxtJablkaZLisciCommand.Text);
        }

        private bool TryInsertJablkaCommand()
        {
            string command = TxtJablkaZLisciCommand.Text.Trim();
            if (string.IsNullOrWhiteSpace(command))
                return false;

            return SendTextByKeyboard(command);
        }

        private static bool SendTextByKeyboard(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char ch in text)
            {
                if (!TrySendCharByKeyboard(ch))
                    return false;
            }

            return true;
        }

        private static bool TrySendCharByKeyboard(char ch)
        {
            short vkInfo = VkKeyScan(ch);
            if (vkInfo == -1)
                return false;

            int virtualKey = vkInfo & 0xFF;
            int shiftState = (vkInfo >> 8) & 0xFF;

            bool needsShift = (shiftState & 1) != 0;
            bool needsCtrl = (shiftState & 2) != 0;
            bool needsAlt = (shiftState & 4) != 0;

            if (needsShift)
                SendKeyDown(VK_SHIFT);
            if (needsCtrl)
                SendKeyDown(VK_CONTROL);
            if (needsAlt)
                SendKeyDown(VK_MENU);

            SendKeyTap(virtualKey);

            if (needsAlt)
                SendKeyUp(VK_MENU);
            if (needsCtrl)
                SendKeyUp(VK_CONTROL);
            if (needsShift)
                SendKeyUp(VK_SHIFT);

            return true;
        }

        private void ResetJablkaRuntimeState()
        {
            ResetJablkaRuntimeState(DateTime.UtcNow);
        }

        private void ResetJablkaRuntimeState(DateTime now)
        {
            _nextJablkaActionAtUtc = now;
            _jablkaUseSlotOneNext = true;
            _jablkaCompletedCycles = 0;
            _jablkaCommandStage = JablkaCommandStage.None;
            _nextJablkaCommandStageAtUtc = now;
        }

        private void ResetKopacz533RuntimeState()
        {
            ResetKopacz533RuntimeState(DateTime.UtcNow);
        }

        private void ResetKopacz533RuntimeState(DateTime now)
        {
            _nextKopacz533CommandAtUtc = now;
            _nextKopacz533StageAtUtc = now;
            _nextKopacz533ResumeAtUtc = now;
            _nextRuntimeTileRefreshAtUtc = now;
            _kopacz533ResumeMiningPending = false;
            _kopacz533CommandStage = Kopacz533CommandStage.None;
            _kopacz533CommandSequenceCompleted = false;
            _kopacz533CommandIndex = 0;
            _kopacz533PendingCommandIndex = -1;
            _kopacz533PendingCommand = string.Empty;
            _kopacz533RuntimeStartedAtUtc = now;
        }

        private void ResetKopacz633RuntimeState()
        {
            ResetKopacz633RuntimeState(DateTime.UtcNow);
        }

        private void ResetKopacz633RuntimeState(DateTime now)
        {
            _nextKopacz633CommandAtUtc = now;
            _nextKopacz633StageAtUtc = now;
            _nextKopacz633ResumeAtUtc = now;
            _nextRuntimeTileRefreshAtUtc = now;
            _kopacz633ResumeMiningPending = false;
            _kopacz633CommandStage = Kopacz633CommandStage.None;
            _kopacz633CommandSequenceCompleted = false;
            _kopacz633CommandIndex = 0;
            _kopacz633PendingCommandIndex = -1;
            _kopacz633PendingCommand = string.Empty;
            _kopacz633RuntimeStartedAtUtc = now;
            _kopacz633MovementLegEndAtUtc = now;
            _kopacz633UpwardLegIndex = 0;
            _kopacz633StrafeDirection = Kopacz633StrafeDirection.None;
        }

        private static bool TryGetCpsRange(string minText, string maxText, out int minCps, out int maxCps)
        {
            minCps = 0;
            maxCps = 0;

            if (!int.TryParse(minText, out int min) || !int.TryParse(maxText, out int max))
                return false;

            if (min <= 0 && max > 0)
                min = max;
            else if (max <= 0 && min > 0)
                max = min;
            else if (min <= 0 || max <= 0)
                return false;

            if (max < min)
                (min, max) = (max, min);

            minCps = min;
            maxCps = max;
            return true;
        }

        private static void SendMouseClick(bool leftButton, bool holdPulseMode)
        {
            if (leftButton)
            {
                if (holdPulseMode)
                {
                    // While physical LMB is held, emit release->press pulse for extra click.
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                }
                else
                {
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                }
                return;
            }

            if (holdPulseMode)
            {
                // While physical RMB is held, emit release->press pulse for extra click.
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
            }
            else
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            }
        }

        private static void SendKeyTap(int virtualKey)
        {
            SendKeyDown(virtualKey);
            SendKeyUp(virtualKey);
        }

        private static void SendKeyDown(int virtualKey)
        {
            byte vk = (byte)(virtualKey & 0xFF);
            keybd_event(vk, 0, 0, UIntPtr.Zero);
        }

        private static void SendKeyUp(int virtualKey)
        {
            byte vk = (byte)(virtualKey & 0xFF);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private void UpdateStatusBar(string message, string colorName)
        {
            if (TxtStatusBar == null) return;

            TxtStatusBar.Text = message;
            TxtStatusBar.Foreground =
                colorName == "Red" ? new SolidColorBrush(Color.FromRgb(255, 107, 107)) :
                colorName == "Green" ? new SolidColorBrush(Color.FromRgb(56, 214, 180)) :
                colorName == "Orange" ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) :
                new SolidColorBrush(Color.FromRgb(207, 219, 235));
        }

        private void ChkMacroManualEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkMacroManualEnabled.IsChecked == true)
            {
                ChkAutoLeftEnabled.IsChecked = false;
                ChkAutoRightEnabled.IsChecked = false;

                // NOWE: przy HOLD wyłącz "Jabłka z liści"
                ChkJablkaZLisciEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoLeftEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoLeftEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoRightEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoRightEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkJablkaZLisciEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            // może działać z Auto LPM/PPM, ale nie z HOLD:
            if (ChkJablkaZLisciEnabled.IsChecked == true && ChkMacroManualEnabled.IsChecked == true)
                ChkMacroManualEnabled.IsChecked = false;

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkPauseWhenCursorVisible_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkPauseWhenCursorVisible.IsChecked != true)
                SetCursorPauseState(false);

            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkKopacz533Enabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkKopacz533Enabled.IsChecked == true)
            {
                ChkKopacz633Enabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz633Enabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkKopacz633Enabled.IsChecked == true)
            {
                ChkKopacz533Enabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void BtnSaveTargetWindowTitle_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            try
            {
                ReadFromUi(includeWindowTitle: false);

                string title = TxtTargetWindowTitle.Text.Trim();
                _settings.TargetWindowTitle = title;
                TxtCurrentWindowTitle.Text = string.IsNullOrWhiteSpace(title) ? "Brak" : title;

                AddToWindowTitleHistory(title);
                _settingsService.Save(_settings);

                _pendingChanges = false;
                _dirtyTimer.Stop();
                TxtSettingsSaved.Text = "✓ Tak";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));

                _isMinecraftFocused = CheckGameFocus();
                UpdateStatusBar("Tytuł okna gry zapisany", "Green");
            }
            catch (Exception ex)
            {
                UpdateStatusBar("Błąd zapisu tytułu okna: " + ex.Message, "Red");
            }
        }

        // IMPORT / EXPORT
        private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                FileName = Path.GetFileName(_settingsService.SettingsFilePath),
                InitialDirectory = _settingsService.SettingsDirectoryPath
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ReadFromUi(includeWindowTitle: false);
                    _settingsService.ExportToFile(_settings, dlg.FileName);
                    UpdateStatusBar("Ustawienia wyeksportowane", "Green");
                }
                catch (Exception ex)
                {
                    UpdateStatusBar("Błąd eksportu: " + ex.Message, "Red");
                }
            }
        }

        private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                InitialDirectory = _settingsService.SettingsDirectoryPath
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _settings = _settingsService.ImportFromFile(dlg.FileName);
                    EnsureSettingsConsistency();
                    _settingsService.Save(_settings);
                    _isLoadingUi = true;
                    try
                    {
                        LoadToUi();
                    }
                    finally
                    {
                        _isLoadingUi = false;
                    }
                    UpdateEnabledStates();
                    RefreshTopTiles();
                    _isMinecraftFocused = CheckGameFocus();
                    UpdateStatusBar("Ustawienia wczytane", "Green");

                    _pendingChanges = false;
                    TxtSettingsSaved.Text = "✓ Tak";
                    TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                    EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                }
                catch (Exception ex)
                {
                    UpdateStatusBar("Błąd importu: " + ex.Message, "Red");
                }
            }
        }

        // KLIKALNY GITHUB
        private void Github_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _dirtyTimer.Stop();
            _focusTimer.Stop();
            _macroTimer.Stop();
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            base.OnClosed(e);
        }
    }
}

