using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;
using TesseractEngine = Tesseract.TesseractEngine;
using TesseractPix = Tesseract.Pix;
using TesseractPage = Tesseract.Page;
using TesseractEngineMode = Tesseract.EngineMode;
using TesseractPageSegMode = Tesseract.PageSegMode;

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
        private readonly DispatcherTimer _f3AnalysisTimer;

        private bool _isMinecraftFocused;
        private IntPtr _focusedGameWindowHandle = IntPtr.Zero;
        private bool _isLoadingUi = true;
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
        private bool _testCaptureBindWasDown;
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
        private DateTime _nextBindyStageAtUtc = DateTime.UtcNow;
        private BindyCommandStage _bindyCommandStage = BindyCommandStage.None;
        private string _bindyPendingCommand = string.Empty;
        private string _bindyPendingEntryName = string.Empty;
        private string _bindyLastExecutedName = string.Empty;
        private DateTime _bindyLastExecutedAtUtc = DateTime.MinValue;
        private readonly Dictionary<string, bool> _bindyBindWasDownById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private BindyEntry? _bindyCaptureEntry;
        private TextBox? _bindyCaptureTextBox;
        private DateTime _nextRuntimeTileRefreshAtUtc = DateTime.UtcNow;
        private OverlayHudWindow? _overlayHud;
        private Forms.NotifyIcon? _trayIcon;
        private bool _isExitRequested;
        private bool _isMinimizedToTray;
        private bool _trayMinimizeBehaviorEnabled;
        private bool _isF3AnalysisInProgress;
        private bool _isTestCaptureSelectionInProgress;
        private readonly object _f3TesseractLock = new object();
        private TesseractEngine? _f3TesseractEngine;
        private int _f3ConsecutiveReadFailures;
        private const double OverlayScreenMargin = 16;
        private const int F3AnalysisIntervalMs = 350;
        private const int F3CaptureWidth = 520;
        private const int F3CaptureHeight = 230;
        private const int F3CaptureMargin = 0;
        private const int F3ReadFailureTolerance = 3;

        private readonly Random _random = new Random();
        private static readonly Regex F3EntityOnlyLineRegex = new Regex(@"^\W*E\s*[:;.,]?\s*([0-9IlOo]{1,2})\s*[/\\|:;.,]\s*([0-9IlOo]{1,3})(?:\W.*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex F3EntityFromBlockRegex = new Regex(@"(?:^|[^A-Za-z0-9])E\s*[:;.,]?\s*([0-9IlOo]{1,2})\s*[/\\|:;.,]\s*([0-9IlOo]{1,3})(?=$|[^A-Za-z0-9])", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private enum BindTarget
        {
            None,
            HoldToggle,
            AutoLeft,
            AutoRight,
            Kopacz533,
            Kopacz633,
            JablkaZLisci,
            TestCaptureArea
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

        private enum BindyCommandStage
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

        private sealed class ProcessTargetOption
        {
            public int ProcessId { get; init; }
            public string ProcessName { get; init; } = string.Empty;
            public string WindowTitle { get; init; } = string.Empty;

            public override string ToString()
            {
                return $"{ProcessName} [{ProcessId}] - {WindowTitle}";
            }
        }

        private readonly record struct F3TelemetryRead(bool Success, string BestText, int VisibleNow, int LoadedNow, bool HasEntityRatio);

        private BindTarget _bindCaptureTarget = BindTarget.None;
        private readonly Dictionary<BindTarget, string> _pendingBindValues = new Dictionary<BindTarget, string>();
        private readonly Dictionary<string, string> _pendingBindyBindValuesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _bindySaveButtonsById = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

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
        private const int VK_ESCAPE = 0x1B;
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
        private const int BindyDelayAfterOpenChatMs = 180;
        private const int BindyDelayAfterTypeCommandMs = 110;
        private const int BindyDelayAfterSubmitCommandMs = 90;
        private const int BindyHudNotificationMs = 2600;
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
            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            PreviewMouseDown += MainWindow_PreviewMouseDown;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            InitializeTrayIcon();

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

            _f3AnalysisTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(F3AnalysisIntervalMs)
            };
            _f3AnalysisTimer.Tick += RunF3AnalysisTick;

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
            _f3AnalysisTimer.Start();
            _isMinecraftFocused = CheckGameFocus();
            UpdateTestF3Estimator();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyDarkTitleBar();

            // If Windows launches the app minimized (e.g. shortcut setting),
            // do not auto-hide it to tray on startup.
            if (WindowState == WindowState.Minimized && !_isMinimizedToTray)
            {
                WindowState = WindowState.Normal;
                Activate();
            }

            _trayMinimizeBehaviorEnabled = true;
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Minecraft Helper",
                Visible = true,
                Icon = TryLoadTrayIcon() ?? System.Drawing.SystemIcons.Application
            };

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Pokaż", null, (_, __) => RestoreFromTray());
            contextMenu.Items.Add("Zamknij", null, (_, __) => ExitFromTray());
            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (_, __) => RestoreFromTray();
        }

        private static System.Drawing.Icon? TryLoadTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(iconPath))
                    return new System.Drawing.Icon(iconPath);
            }
            catch
            {
                // Ignore and use next fallback.
            }

            try
            {
                string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executablePath))
                    return System.Drawing.Icon.ExtractAssociatedIcon(executablePath);
            }
            catch
            {
                // Ignore and use default application icon.
            }

            return null;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (!_trayMinimizeBehaviorEnabled)
                return;

            if (WindowState == WindowState.Minimized)
                MinimizeToTray();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExitRequested)
                return;

            if (!_trayMinimizeBehaviorEnabled)
                return;

            e.Cancel = true;
            MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            if (_isMinimizedToTray)
                return;

            Hide();
            ShowInTaskbar = false;
            _isMinimizedToTray = true;
        }

        private void RestoreFromTray()
        {
            if (!_isMinimizedToTray)
                return;

            ShowInTaskbar = true;
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _isMinimizedToTray = false;
        }

        private void ExitFromTray()
        {
            _isExitRequested = true;
            Close();
        }

        private static OverlayCorner ParseOverlayCorner(string? raw)
        {
            return raw?.Trim().ToLowerInvariant() switch
            {
                "leftbottom" => OverlayCorner.BottomLeft,
                "righttop" => OverlayCorner.TopRight,
                "lefttop" => OverlayCorner.TopLeft,
                _ => OverlayCorner.BottomRight
            };
        }

        private static string ToOverlayCornerSetting(OverlayCorner corner)
        {
            return corner switch
            {
                OverlayCorner.BottomLeft => "LeftBottom",
                OverlayCorner.TopRight => "RightTop",
                OverlayCorner.TopLeft => "LeftTop",
                _ => "RightBottom"
            };
        }

        private OverlayCorner GetSelectedOverlayCorner()
        {
            int selectedIndex = CbOverlayCorner?.SelectedIndex ?? -1;
            return selectedIndex switch
            {
                1 => OverlayCorner.BottomLeft,
                2 => OverlayCorner.TopRight,
                3 => OverlayCorner.TopLeft,
                0 => OverlayCorner.BottomRight,
                _ => ParseOverlayCorner(_settings.OverlayCorner)
            };
        }

        private int GetSelectedOverlayMonitorIndex()
        {
            int fallback = Math.Max(0, _settings.OverlayMonitorIndex);
            if (CbOverlayMonitor == null)
                return fallback;
            if (CbOverlayMonitor.SelectedIndex >= 0)
                return CbOverlayMonitor.SelectedIndex;
            return fallback;
        }

        private Rect ToDipRect(Drawing.Rectangle pixelRect)
        {
            Matrix transform = Matrix.Identity;
            PresentationSource? source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
                transform = source.CompositionTarget.TransformFromDevice;

            Point topLeft = transform.Transform(new Point(pixelRect.Left, pixelRect.Top));
            Point bottomRight = transform.Transform(new Point(pixelRect.Right, pixelRect.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private Rect GetSelectedOverlayWorkArea()
        {
            Forms.Screen[] screens = Forms.Screen.AllScreens;
            if (screens == null || screens.Length == 0)
                return SystemParameters.WorkArea;

            int index = Math.Clamp(GetSelectedOverlayMonitorIndex(), 0, screens.Length - 1);
            return ToDipRect(screens[index].WorkingArea);
        }

        private void RefreshOverlayMonitorChoices()
        {
            if (CbOverlayMonitor == null)
                return;

            int selectedIndex = Math.Max(0, _settings.OverlayMonitorIndex);
            if (!_isLoadingUi && CbOverlayMonitor.SelectedIndex >= 0)
                selectedIndex = CbOverlayMonitor.SelectedIndex;

            CbOverlayMonitor.Items.Clear();
            Forms.Screen[] screens = Forms.Screen.AllScreens;
            if (screens == null || screens.Length == 0)
            {
                CbOverlayMonitor.Items.Add("Monitor 1");
                CbOverlayMonitor.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < screens.Length; i++)
            {
                Forms.Screen screen = screens[i];
                var bounds = screen.WorkingArea;
                CbOverlayMonitor.Items.Add($"Monitor {i + 1} ({bounds.Width}x{bounds.Height})");
            }

            CbOverlayMonitor.SelectedIndex = Math.Clamp(selectedIndex, 0, screens.Length - 1);
        }

        private void UpdateOverlayLayout()
        {
            Rect workArea = GetSelectedOverlayWorkArea();
            OverlayCorner corner = GetSelectedOverlayCorner();
            bool animationsEnabled = ChkOverlayAnimationsEnabled?.IsChecked ?? _settings.OverlayAnimationsEnabled;

            if (_overlayHud != null)
            {
                _overlayHud.SetAnimationsEnabled(animationsEnabled);
                _overlayHud.SetLayout(workArea, corner, OverlayScreenMargin);
            }
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
            _settings.BindyCommands ??= new List<MinerCommand>();
            _settings.BindyEntries ??= new List<BindyEntry>();
            _settings.JablkaZLisciCommand ??= string.Empty;
            _settings.TestCustomCaptureBind ??= string.Empty;
            _settings.BindyKey ??= string.Empty;
            _settings.TargetProcessName ??= string.Empty;
            _settings.OverlayCorner ??= "RightBottom";
            if (_settings.OverlayMonitorIndex < 0)
                _settings.OverlayMonitorIndex = 0;
            if (_settings.TargetProcessId < 0)
                _settings.TargetProcessId = 0;
            if (_settings.TestCustomCaptureX < 0)
                _settings.TestCustomCaptureX = 0;
            if (_settings.TestCustomCaptureY < 0)
                _settings.TestCustomCaptureY = 0;
            if (_settings.TestCustomCaptureWidth < 0)
                _settings.TestCustomCaptureWidth = 0;
            if (_settings.TestCustomCaptureHeight < 0)
                _settings.TestCustomCaptureHeight = 0;
            if (!_settings.HoldLeftEnabled && !_settings.HoldRightEnabled)
            {
                _settings.HoldLeftEnabled = true;
                _settings.HoldRightEnabled = true;
            }

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

            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry entry = _settings.BindyEntries[i];
                entry.Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id.Trim();
                entry.Name ??= string.Empty;
                entry.Key ??= string.Empty;
                entry.Command ??= string.Empty;
            }

            // Migration from old format (one global bind + list of commands) to new format (bind + command per row).
            if (_settings.BindyEntries.Count == 0)
            {
                string legacyKey = (_settings.BindyKey ?? string.Empty).Trim();
                for (int i = 0; i < _settings.BindyCommands.Count; i++)
                {
                    string cmd = (_settings.BindyCommands[i].Command ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(cmd))
                        continue;

                    _settings.BindyEntries.Add(new BindyEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Name = string.Empty,
                        Key = legacyKey,
                        Command = cmd
                    });
                }
            }
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

        private static List<ProcessTargetOption> GetRunningWindowProcesses()
        {
            var results = new List<ProcessTargetOption>();
            Process[] all = Process.GetProcesses();
            for (int i = 0; i < all.Length; i++)
            {
                Process process = all[i];
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero)
                        continue;

                    string title = (process.MainWindowTitle ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    results.Add(new ProcessTargetOption
                    {
                        ProcessId = process.Id,
                        ProcessName = process.ProcessName ?? string.Empty,
                        WindowTitle = title
                    });
                }
                catch
                {
                    // Ignore processes that cannot be inspected.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return results
                .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.WindowTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private ProcessTargetOption? GetSelectedTargetProcessOption()
        {
            if (CbTargetProcessList?.SelectedItem is ProcessTargetOption option)
                return option;
            return null;
        }

        private string BuildTargetProcessDisplayText()
        {
            string processName = (_settings.TargetProcessName ?? string.Empty).Trim();
            int processId = _settings.TargetProcessId;
            string windowTitle = (_settings.TargetWindowTitle ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(processName))
            {
                string processPart = processId > 0 ? $"{processName} [{processId}]" : processName;
                if (!string.IsNullOrWhiteSpace(windowTitle))
                    return $"{processPart} - {windowTitle}";
                return processPart;
            }

            return string.IsNullOrWhiteSpace(windowTitle) ? "Brak" : windowTitle;
        }

        private void RefreshTargetProcessChoices()
        {
            if (CbTargetProcessList == null)
                return;

            ProcessTargetOption? currentSelection = GetSelectedTargetProcessOption();
            List<ProcessTargetOption> options = GetRunningWindowProcesses();

            CbTargetProcessList.Items.Clear();
            bool previousLoading = _isLoadingUi;
            _isLoadingUi = true;
            try
            {
                for (int i = 0; i < options.Count; i++)
                    CbTargetProcessList.Items.Add(options[i]);

                if (options.Count == 0)
                {
                    CbTargetProcessList.IsEnabled = false;
                    CbTargetProcessList.SelectedIndex = -1;
                    return;
                }

                CbTargetProcessList.IsEnabled = true;

                int selectedIndex = -1;
                if (currentSelection != null)
                    selectedIndex = options.FindIndex(o => o.ProcessId == currentSelection.ProcessId);

                if (selectedIndex < 0 && _settings.TargetProcessId > 0)
                    selectedIndex = options.FindIndex(o => o.ProcessId == _settings.TargetProcessId);

                if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(_settings.TargetProcessName))
                {
                    selectedIndex = options.FindIndex(o =>
                        string.Equals(o.ProcessName, _settings.TargetProcessName, StringComparison.OrdinalIgnoreCase));
                }

                if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(_settings.TargetWindowTitle))
                {
                    string configuredTitle = _settings.TargetWindowTitle.Trim();
                    selectedIndex = options.FindIndex(o =>
                        o.WindowTitle.Contains(configuredTitle, StringComparison.OrdinalIgnoreCase)
                        || configuredTitle.Contains(o.WindowTitle, StringComparison.OrdinalIgnoreCase));
                }

                CbTargetProcessList.SelectedIndex = selectedIndex;
            }
            finally
            {
                _isLoadingUi = previousLoading;
            }
        }

        // FOCUS MINECRAFT
        private bool CheckGameFocus()
        {
            string targetProcessName = (_settings.TargetProcessName ?? string.Empty).Trim();
            int targetProcessId = _settings.TargetProcessId;
            string targetWindowTitle = (_settings.TargetWindowTitle ?? string.Empty).Trim();
            bool focused = false;
            IntPtr focusedWindow = IntPtr.Zero;

            bool hasTargetConfigured =
                targetProcessId > 0
                || !string.IsNullOrWhiteSpace(targetProcessName)
                || !string.IsNullOrWhiteSpace(targetWindowTitle);

            if (hasTargetConfigured)
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    _ = GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                    string currentWindowTitle = windowTitle.ToString();
                    string currentProcessName = string.Empty;

                    _ = GetWindowThreadProcessId(foregroundWindow, out uint processIdRaw);
                    int processId = processIdRaw > int.MaxValue ? 0 : (int)processIdRaw;
                    if (processId > 0)
                    {
                        try
                        {
                            using Process process = Process.GetProcessById(processId);
                            currentProcessName = process.ProcessName ?? string.Empty;
                        }
                        catch
                        {
                            currentProcessName = string.Empty;
                        }
                    }

                    if (targetProcessId > 0 && processId == targetProcessId)
                    {
                        if (string.IsNullOrWhiteSpace(targetProcessName)
                            || string.Equals(currentProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            focused = true;
                        }
                    }

                    if (!focused && !string.IsNullOrWhiteSpace(targetProcessName))
                    {
                        focused = string.Equals(currentProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!focused && !string.IsNullOrWhiteSpace(targetWindowTitle) && !string.IsNullOrWhiteSpace(currentWindowTitle))
                    {
                        focused = currentWindowTitle.Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase);
                    }

                    if (focused)
                    {
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
            ResetBindyRuntimeState();
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);

            // HOLD
            ChkMacroManualEnabled.IsChecked = _settings.HoldEnabled;
            TxtMacroManualKey.Text = _settings.HoldToggleKey;
            ChkHoldLeftEnabled.IsChecked = _settings.HoldLeftEnabled;
            ChkHoldRightEnabled.IsChecked = _settings.HoldRightEnabled;
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
            RefreshTargetProcessChoices();
            TxtCurrentWindowTitle.Text = BuildTargetProcessDisplayText();

            // JABŁKA Z LIŚCI
            ChkJablkaZLisciEnabled.IsChecked = _settings.JablkaZLisciEnabled;
            TxtJablkaZLisciKey.Text = _settings.JablkaZLisciKey;
            TxtJablkaZLisciCommand.Text = _settings.JablkaZLisciCommand;

            // BINDY
            ChkBindyEnabled.IsChecked = _settings.BindyEnabled;
            RefreshBindyCommandsUI();

            // EQ
            ChkPauseWhenCursorVisible.IsChecked = _settings.PauseWhenCursorVisible;

            // TESTOWE OCR
            ChkTestEntitiesEnabled.IsChecked = _settings.TestEntitiesEnabled;
            ChkTestCustomCaptureEnabled.IsChecked = _settings.TestCustomCaptureEnabled;
            TxtTestCustomCaptureBind.Text = _settings.TestCustomCaptureBind;
            UpdateTestCustomCaptureAreaInfo();

            // OVERLAY
            ChkOverlayHudEnabled.IsChecked = _settings.OverlayHudEnabled;
            ChkOverlayAnimationsEnabled.IsChecked = _settings.OverlayAnimationsEnabled;
            RefreshOverlayMonitorChoices();
            CbOverlayCorner.SelectedIndex = ParseOverlayCorner(_settings.OverlayCorner) switch
            {
                OverlayCorner.BottomLeft => 1,
                OverlayCorner.TopRight => 2,
                OverlayCorner.TopLeft => 3,
                _ => 0
            };

            _pendingBindValues.Clear();
            _pendingBindyBindValuesById.Clear();
            RefreshBindSaveButtons();
            UpdateOverlayLayout();
        }

        private bool HasCustomCaptureAreaConfigured()
        {
            return _settings.TestCustomCaptureWidth >= 24 && _settings.TestCustomCaptureHeight >= 24;
        }

        private void UpdateTestCustomCaptureAreaInfo()
        {
            if (TxtTestCustomCaptureAreaInfo == null)
                return;

            bool customEnabled = ChkTestCustomCaptureEnabled?.IsChecked == true;
            bool hasArea = HasCustomCaptureAreaConfigured();

            if (!customEnabled)
            {
                TxtTestCustomCaptureAreaInfo.Text = hasArea
                    ? $"Tryb niestandardowy OFF. Zapisany obszar: x={_settings.TestCustomCaptureX}, y={_settings.TestCustomCaptureY}, {_settings.TestCustomCaptureWidth}x{_settings.TestCustomCaptureHeight}."
                    : "Tryb niestandardowy OFF.";
                return;
            }

            TxtTestCustomCaptureAreaInfo.Text = hasArea
                ? $"Tryb niestandardowy ON. Obszar: x={_settings.TestCustomCaptureX}, y={_settings.TestCustomCaptureY}, {_settings.TestCustomCaptureWidth}x{_settings.TestCustomCaptureHeight} (względem okna gry)."
                : "Tryb niestandardowy ON, ale brak obszaru. Kliknij \"Zaznacz obszar\".";
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
                BindTarget.TestCaptureArea => "Experimental OCR (obszar)",
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
                BindTarget.TestCaptureArea => BtnTestCustomCaptureBind,
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
                BindTarget.TestCaptureArea => TxtTestCustomCaptureBind,
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
            yield return BindTarget.TestCaptureArea;
        }

        private static string GetBindOwnerId(BindTarget target)
        {
            return target switch
            {
                BindTarget.HoldToggle => "core:hold",
                BindTarget.AutoLeft => "core:auto-left",
                BindTarget.AutoRight => "core:auto-right",
                BindTarget.Kopacz533 => "core:kopacz-533",
                BindTarget.Kopacz633 => "core:kopacz-633",
                BindTarget.JablkaZLisci => "core:jablka",
                BindTarget.TestCaptureArea => "core:test-capture",
                _ => "core:unknown"
            };
        }

        private static bool IsSameBindKey(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private string GetBindyEntryDisplayName(BindyEntry entry)
        {
            if (entry == null)
                return "Bind";

            string customName = (entry.Name ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(customName))
                return customName;

            int index = _settings.BindyEntries.IndexOf(entry);
            return index >= 0 ? $"Bind #{index + 1}" : "Bind";
        }

        private string GetBindyEntryLabel(BindyEntry entry)
        {
            return $"BINDY: {GetBindyEntryDisplayName(entry)}";
        }

        private bool TryFindBindConflict(string keyText, string ownerId, out string conflictOwnerLabel)
        {
            conflictOwnerLabel = string.Empty;
            if (string.IsNullOrWhiteSpace(keyText))
                return false;

            BindTarget[] fixedTargets =
            {
                BindTarget.HoldToggle,
                BindTarget.AutoLeft,
                BindTarget.AutoRight,
                BindTarget.Kopacz533,
                BindTarget.Kopacz633,
                BindTarget.JablkaZLisci,
                BindTarget.TestCaptureArea
            };

            for (int i = 0; i < fixedTargets.Length; i++)
            {
                BindTarget target = fixedTargets[i];
                string candidateOwnerId = GetBindOwnerId(target);
                if (string.Equals(candidateOwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                TextBox? candidateTextBox = GetBindTextBox(target);
                string candidateKey = candidateTextBox?.Text ?? string.Empty;
                if (!IsSameBindKey(keyText, candidateKey))
                    continue;

                conflictOwnerLabel = GetBindTargetLabel(target);
                return true;
            }

            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry entry = _settings.BindyEntries[i];
                if (!entry.Enabled)
                    continue;
                string id = EnsureBindyEntryId(entry);
                string bindyOwnerId = $"bindy:{id}";
                if (string.Equals(bindyOwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsSameBindKey(keyText, entry.Key))
                    continue;

                conflictOwnerLabel = GetBindyEntryLabel(entry);
                return true;
            }

            return false;
        }

        private void ShowBindConflict(string keyText, string conflictOwnerLabel, string requestedOwnerLabel)
        {
            string message = $"Klawisz {keyText} jest już przypisany do: {conflictOwnerLabel}.";
            UpdateStatusBar(message, "Orange");
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
            RefreshBindSaveButton(BindTarget.TestCaptureArea);
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
                RenderManualStatus(
                    manualOn,
                    ChkHoldLeftEnabled.IsChecked == true,
                    ChkHoldRightEnabled.IsChecked == true,
                    holdBindLabel,
                    manualLeftMin,
                    manualLeftMax,
                    manualRightMin,
                    manualRightMax,
                    holdRuntimeState);
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
            RefreshOverlayHud(now);
        }

        private static string GetConfiguredBindLabel(string keyText)
        {
            return string.IsNullOrWhiteSpace(keyText) ? "Brak" : keyText.Trim();
        }

        private void RenderManualStatus(bool manualOn, bool holdLeftEnabled, bool holdRightEnabled, string bindLabel, int leftMin, int leftMax, int rightMin, int rightMax, string runtimeState)
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
            AppendInline(TxtManualCps, holdLeftEnabled ? $"{leftMin}-{leftMax}" : "OFF", holdLeftEnabled ? TileValueBrush : TileOffBrush, FontWeights.SemiBold);
            AppendInline(TxtManualCps, " | PPM ", TileLabelBrush);
            AppendInline(TxtManualCps, holdRightEnabled ? $"{rightMin}-{rightMax}" : "OFF", holdRightEnabled ? TileValueBrush : TileOffBrush, FontWeights.SemiBold);
            AppendInline(TxtManualCps, " CPS ", TileLabelBrush);
            AppendInline(TxtManualCps, runtimeState, GetRuntimeStateBrush(runtimeState), FontWeights.SemiBold);

            if (_holdMacroRuntimeEnabled)
            {
                AppendInline(TxtManualCps, " | LPM-TGL ", TileLabelBrush);
                bool leftRuntimeOn = holdLeftEnabled && _holdLeftToggleClickingEnabled;
                AppendInline(TxtManualCps, leftRuntimeOn ? "ON" : "OFF", leftRuntimeOn ? TileOnBrush : TileOffBrush, FontWeights.SemiBold);
                AppendInline(TxtManualCps, " | PPM-HOLD ", TileLabelBrush);
                bool rightRuntimeOn = holdRightEnabled && _holdRightRuntimePressActive;
                AppendInline(TxtManualCps, rightRuntimeOn ? "ON" : "OFF", rightRuntimeOn ? TileOnBrush : TileOffBrush, FontWeights.SemiBold);
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

        private void RefreshOverlayHud(DateTime now)
        {
            bool hudEnabled = ChkOverlayHudEnabled?.IsChecked ?? _settings.OverlayHudEnabled;
            if (!hudEnabled)
            {
                _overlayHud?.UpdateEntries(Array.Empty<OverlayHudEntry>());
                UpdateOverlayLayout();
                return;
            }

            List<OverlayHudEntry> entries = BuildOverlayHudEntries(now);
            if (entries.Count == 0)
            {
                _overlayHud?.UpdateEntries(entries);
                UpdateOverlayLayout();
                return;
            }

            _overlayHud ??= new OverlayHudWindow();
            _overlayHud.UpdateEntries(entries);
            UpdateOverlayLayout();
        }

        private List<OverlayHudEntry> BuildOverlayHudEntries(DateTime now)
        {
            var entries = new List<OverlayHudEntry>();

            bool holdModeSelected = ChkMacroManualEnabled.IsChecked == true;
            bool autoLeftModeSelected = ChkAutoLeftEnabled.IsChecked == true;
            bool autoRightModeSelected = ChkAutoRightEnabled.IsChecked == true;
            bool jablkaModeSelected = ChkJablkaZLisciEnabled.IsChecked == true;
            bool kop533ModeSelected = ChkKopacz533Enabled.IsChecked == true;
            bool kop633ModeSelected = ChkKopacz633Enabled.IsChecked == true;
            bool testEntitiesModeSelected = ChkTestEntitiesEnabled.IsChecked == true;

            if (_isPausedByCursorVisibility)
            {
                entries.Add(new OverlayHudEntry(
                    "PAUZA (KURSOR)",
                    "Makra klikające są tymczasowo wstrzymane.",
                    OverlayHudTone.Warning));
            }

            if (IsBindyHudNotificationActive(now))
                entries.Add(BuildBindyExecutedOverlayEntry());

            if (holdModeSelected && _holdMacroRuntimeEnabled)
                entries.Add(BuildHoldOverlayEntry());

            if (autoLeftModeSelected && _autoLeftRuntimeEnabled)
                entries.Add(BuildAutoLeftOverlayEntry());

            if (autoRightModeSelected && _autoRightRuntimeEnabled)
                entries.Add(BuildAutoRightOverlayEntry());

            if (jablkaModeSelected && _jablkaRuntimeEnabled)
                entries.Add(BuildJablkaOverlayEntry(now));

            bool kop533Visible = kop533ModeSelected && (_kopacz533RuntimeEnabled || _kopacz533CommandStage != Kopacz533CommandStage.None || _kopacz533ResumeMiningPending);
            if (kop533Visible)
                entries.Add(BuildKopacz533OverlayEntry(now));

            bool kop633Visible = kop633ModeSelected && (_kopacz633RuntimeEnabled || _kopacz633CommandStage != Kopacz633CommandStage.None || _kopacz633ResumeMiningPending);
            if (kop633Visible)
                entries.Add(BuildKopacz633OverlayEntry(now));

            if (testEntitiesModeSelected)
            {
                OverlayHudEntry testEntry = BuildTestEntitiesOverlayEntry();
                OverlayCorner corner = GetSelectedOverlayCorner();
                if (corner is OverlayCorner.TopLeft or OverlayCorner.TopRight)
                    entries.Insert(0, testEntry);
                else
                    entries.Add(testEntry);
            }

            return entries;
        }

        private OverlayHudEntry BuildTestEntitiesOverlayEntry()
        {
            string rawValue = TxtTestLiveEntities?.Text?.Trim() ?? string.Empty;
            bool hasValue = !string.IsNullOrWhiteSpace(rawValue) && !string.Equals(rawValue, "-", StringComparison.Ordinal);
            string value = hasValue ? rawValue : "Brak danych";
            string body = $"Gracze: {value}";

            return new OverlayHudEntry(
                "WYKRYWANIE GRACZY F3",
                body,
                hasValue ? OverlayHudTone.Active : OverlayHudTone.Warning,
                Emphasize: true);
        }

        private bool IsBindyHudNotificationActive(DateTime now)
        {
            if (string.IsNullOrWhiteSpace(_bindyLastExecutedName) || _bindyLastExecutedAtUtc == DateTime.MinValue)
                return false;

            return (now - _bindyLastExecutedAtUtc).TotalMilliseconds <= BindyHudNotificationMs;
        }

        private OverlayHudEntry BuildBindyExecutedOverlayEntry()
        {
            string bindName = string.IsNullOrWhiteSpace(_bindyLastExecutedName) ? "Bind" : _bindyLastExecutedName.Trim();
            string body = $"{bindName} zostało wykonane!";
            return new OverlayHudEntry("BINDY", body, OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildHoldOverlayEntry()
        {
            int leftMin = ParseNonNegativeInt(TxtManualLeftMinCps.Text);
            int leftMax = ParseNonNegativeInt(TxtManualLeftMaxCps.Text);
            int rightMin = ParseNonNegativeInt(TxtManualRightMinCps.Text);
            int rightMax = ParseNonNegativeInt(TxtManualRightMaxCps.Text);
            bool holdLeftEnabled = ChkHoldLeftEnabled.IsChecked == true;
            bool holdRightEnabled = ChkHoldRightEnabled.IsChecked == true;
            string bindLabel = GetConfiguredBindLabel(TxtMacroManualKey.Text);
            string runtimeState = GetRuntimeStateLabel(_holdMacroRuntimeEnabled);

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"LPM: {(holdLeftEnabled ? $"{leftMin}-{leftMax} CPS" : "OFF")} | PPM: {(holdRightEnabled ? $"{rightMin}-{rightMax} CPS" : "OFF")}\n" +
                $"LPM-TGL: {(holdLeftEnabled && _holdLeftToggleClickingEnabled ? "ON" : "OFF")} | PPM-HOLD: {(holdRightEnabled && _holdRightRuntimePressActive ? "ON" : "OFF")}";

            return new OverlayHudEntry("HOLD LPM + PPM", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildAutoLeftOverlayEntry()
        {
            int min = ParseNonNegativeInt(TxtAutoLeftMinCps.Text);
            int max = ParseNonNegativeInt(TxtAutoLeftMaxCps.Text);
            string bindLabel = GetConfiguredBindLabel(TxtAutoLeftKey.Text);
            string runtimeState = GetRuntimeStateLabel(_autoLeftRuntimeEnabled);
            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"CPS: {min}-{max}";

            return new OverlayHudEntry("AUTO LPM", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildAutoRightOverlayEntry()
        {
            int min = ParseNonNegativeInt(TxtAutoRightMinCps.Text);
            int max = ParseNonNegativeInt(TxtAutoRightMaxCps.Text);
            string bindLabel = GetConfiguredBindLabel(TxtAutoRightKey.Text);
            string runtimeState = GetRuntimeStateLabel(_autoRightRuntimeEnabled);
            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"CPS: {min}-{max}";

            return new OverlayHudEntry("AUTO PPM", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildJablkaOverlayEntry(DateTime now)
        {
            string bindLabel = GetConfiguredBindLabel(TxtJablkaZLisciKey.Text);
            string runtimeState = GetRuntimeStateLabel(_jablkaRuntimeEnabled);
            string stageLine;

            if (_jablkaCommandStage != JablkaCommandStage.None)
            {
                int remainingMs = Math.Max(0, (int)Math.Ceiling((_nextJablkaCommandStageAtUtc - now).TotalMilliseconds));
                stageLine = $"Etap: {GetJablkaStageLabel(_jablkaCommandStage)} za {remainingMs}ms";
            }
            else
            {
                int remainingMs = Math.Max(0, (int)Math.Ceiling((_nextJablkaActionAtUtc - now).TotalMilliseconds));
                stageLine = $"Następna akcja za {remainingMs}ms";
            }

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"Cykl: {_jablkaCompletedCycles}/{JablkaCommandCycleThreshold} | Następny slot: {(_jablkaUseSlotOneNext ? "1" : "2")}\n" +
                stageLine;

            return new OverlayHudEntry("JABŁKA Z LIŚCI", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildKopacz533OverlayEntry(DateTime now)
        {
            string bindLabel = GetConfiguredBindLabel(TxtKopacz533Key.Text);
            string runtimeState = GetRuntimeStateLabel(_kopacz533RuntimeEnabled);
            string stageLabel = GetKopacz533StageLabel();
            int elapsedSeconds = GetKopacz533ElapsedSeconds(now);
            string commandLine = BuildKopacz533CommandOverlayLine(now);

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState} | Etap: {stageLabel}\n" +
                $"Czas: {elapsedSeconds}s\n" +
                commandLine;

            return new OverlayHudEntry("KOPACZ 5/3/3", body, OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildKopacz633OverlayEntry(DateTime now)
        {
            string bindLabel = GetConfiguredBindLabel(TxtKopacz633Key.Text);
            string runtimeState = GetRuntimeStateLabel(_kopacz633RuntimeEnabled);
            string stageLabel = GetKopacz633StageLabel();
            string directionLabel = GetKopacz633DirectionLabel();
            string movementLabel = GetKopacz633MovementOverlayLabel(now);
            string commandLine = BuildKopacz633CommandOverlayLine(now);

            string sizeLabel;
            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                int width = GetConfiguredKopacz633ForwardWidth();
                sizeLabel = $"Szer: {width}";
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                int width = GetConfiguredKopacz633UpwardWidth();
                int length = GetConfiguredKopacz633UpwardLength();
                sizeLabel = $"Szer: {width} | Dł: {length}";
            }
            else
            {
                sizeLabel = "Szer: - | Dł: -";
            }

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState} | Etap: {stageLabel}\n" +
                $"Tryb: {directionLabel} | {sizeLabel}\n" +
                $"{movementLabel}\n" +
                commandLine;

            return new OverlayHudEntry("KOPACZ 6/3/3", body, OverlayHudTone.Active);
        }

        private string BuildKopacz533CommandOverlayLine(DateTime now)
        {
            if (_kopacz533CommandStage != Kopacz533CommandStage.None && !string.IsNullOrWhiteSpace(_kopacz533PendingCommand))
            {
                string pendingIndex = _kopacz533PendingCommandIndex >= 0 ? (_kopacz533PendingCommandIndex + 1).ToString(CultureInfo.InvariantCulture) : "?";
                return $"Komenda #{pendingIndex}: {GetStatusCommandPreview(_kopacz533PendingCommand)} (teraz)";
            }

            if (TryPeekNextKopacz533Command(out int commandIndex, out string command, out _))
            {
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextKopacz533CommandAtUtc - now).TotalSeconds));
                return $"Następna #{commandIndex + 1}: {GetStatusCommandPreview(command)} za {remainingSeconds}s";
            }

            return "Komenda: brak";
        }

        private string BuildKopacz633CommandOverlayLine(DateTime now)
        {
            if (_kopacz633CommandStage != Kopacz633CommandStage.None && !string.IsNullOrWhiteSpace(_kopacz633PendingCommand))
            {
                string pendingIndex = _kopacz633PendingCommandIndex >= 0 ? (_kopacz633PendingCommandIndex + 1).ToString(CultureInfo.InvariantCulture) : "?";
                return $"Komenda #{pendingIndex}: {GetStatusCommandPreview(_kopacz633PendingCommand)} (teraz)";
            }

            if (TryPeekNextKopacz633Command(out int commandIndex, out string command, out _))
            {
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextKopacz633CommandAtUtc - now).TotalSeconds));
                return $"Następna #{commandIndex + 1}: {GetStatusCommandPreview(command)} za {remainingSeconds}s";
            }

            return "Komenda: brak";
        }

        private string GetKopacz533StageLabel()
        {
            if (_kopacz533ResumeMiningPending)
                return "Wznawianie kopania";

            return _kopacz533CommandStage switch
            {
                Kopacz533CommandStage.OpenChat => "Otwieranie chatu",
                Kopacz533CommandStage.TypeCommand => "Wpisywanie komendy",
                Kopacz533CommandStage.SubmitCommand => "Wysyłanie komendy",
                _ => _kopacz533RuntimeEnabled ? "Kopanie" : "Oczekiwanie"
            };
        }

        private string GetKopacz633StageLabel()
        {
            if (_kopacz633ResumeMiningPending)
                return "Wznawianie kopania";

            return _kopacz633CommandStage switch
            {
                Kopacz633CommandStage.OpenChat => "Otwieranie chatu",
                Kopacz633CommandStage.TypeCommand => "Wpisywanie komendy",
                Kopacz633CommandStage.SubmitCommand => "Wysyłanie komendy",
                _ => _kopacz633RuntimeEnabled ? "Kopanie" : "Oczekiwanie"
            };
        }

        private string GetKopacz633DirectionLabel()
        {
            return CbKopacz633Direction.SelectedIndex switch
            {
                1 => "Na wprost",
                2 => "Do góry",
                _ => "Brak"
            };
        }

        private string GetKopacz633MovementOverlayLabel(DateTime now)
        {
            string movementLabel = _kopacz633StrafeDirection switch
            {
                Kopacz633StrafeDirection.Forward => "W ^",
                Kopacz633StrafeDirection.Right => "D ->",
                Kopacz633StrafeDirection.Backward => "S v",
                Kopacz633StrafeDirection.Left => "A <-",
                _ => "STOP"
            };

            if (_kopacz633StrafeDirection == Kopacz633StrafeDirection.None)
                return $"Ruch: {movementLabel}";

            int remainingMs = Math.Max(0, (int)Math.Ceiling((_kopacz633MovementLegEndAtUtc - now).TotalMilliseconds));
            return $"Ruch: {movementLabel} za {remainingMs}ms";
        }

        private static string GetJablkaStageLabel(JablkaCommandStage stage)
        {
            return stage switch
            {
                JablkaCommandStage.OpenChat => "Otwieranie chatu",
                JablkaCommandStage.PasteCommand => "Wpisywanie komendy",
                JablkaCommandStage.SubmitCommand => "Wysyłanie komendy",
                _ => "Brak"
            };
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
            bool holdLeftOn = manualOn && (ChkHoldLeftEnabled.IsChecked ?? true);
            bool holdRightOn = manualOn && (ChkHoldRightEnabled.IsChecked ?? true);
            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;

            TxtMacroManualKey.IsEnabled = manualOn;
            BtnMacroManualCapture.IsEnabled = manualOn;
            ChkHoldLeftEnabled.IsEnabled = manualOn;
            ChkHoldRightEnabled.IsEnabled = manualOn;
            TxtManualLeftMinCps.IsEnabled = holdLeftOn;
            TxtManualLeftMaxCps.IsEnabled = holdLeftOn;
            TxtManualRightMinCps.IsEnabled = holdRightOn;
            TxtManualRightMaxCps.IsEnabled = holdRightOn;

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
            if (!holdLeftOn)
            {
                _holdLeftToggleClickingEnabled = false;
                _holdLeftToggleWasDown = false;
                _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
                _nextHoldLeftClickAtUtc = DateTime.UtcNow;
            }
            if (!holdRightOn)
            {
                _holdRightRuntimePressActive = false;
                _nextHoldRightClickAtUtc = DateTime.UtcNow;
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
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

            bool bindyOn = ChkBindyEnabled.IsChecked ?? false;
            if (PanelBindyContent != null)
            {
                PanelBindyContent.Visibility = bindyOn ? Visibility.Visible : Visibility.Collapsed;
                PanelBindyContent.IsEnabled = bindyOn;
            }
            if (PanelBindyCommands != null)
                PanelBindyCommands.IsEnabled = bindyOn;
            if (BtnBindyAddCommand != null)
                BtnBindyAddCommand.IsEnabled = bindyOn;
            if (!bindyOn)
                ResetBindyRuntimeState();

            bool cursorPauseOn = ChkPauseWhenCursorVisible.IsChecked == true;
            bool testEntitiesOn = ChkTestEntitiesEnabled?.IsChecked == true;
            bool testCustomOn = testEntitiesOn && ChkTestCustomCaptureEnabled?.IsChecked == true;
            if (PanelTestEntitiesContent != null)
                PanelTestEntitiesContent.Visibility = testEntitiesOn ? Visibility.Visible : Visibility.Collapsed;
            if (TxtTestCustomCaptureBind != null)
                TxtTestCustomCaptureBind.IsEnabled = testCustomOn;
            if (BtnTestCustomCaptureBind != null)
                BtnTestCustomCaptureBind.IsEnabled = testCustomOn;
            if (BtnTestSelectCaptureArea != null)
                BtnTestSelectCaptureArea.IsEnabled = testCustomOn;
            if (BtnTestResetCaptureData != null)
                BtnTestResetCaptureData.IsEnabled = testEntitiesOn;

            SetSectionVisualState(BorderManualLeftSection, holdLeftOn);
            SetSectionVisualState(BorderManualRightSection, holdRightOn);
            SetSectionVisualState(BorderManualBindSection, manualOn);
            SetSectionVisualState(BorderAutoLeftSection, autoLeftOn);
            SetSectionVisualState(BorderAutoRightSection, autoRightOn);
            SetSectionVisualState(BorderJablkaSection, jablkaOn);
            SetSectionVisualState(BorderCursorPauseSection, cursorPauseOn);
            SetSectionVisualState(BorderKopacz533Section, kop533On);
            SetSectionVisualState(BorderKopacz633Section, kop633On);
            SetSectionVisualState(BorderBindySection, bindyOn);
            SetSectionVisualState(BorderTestCustomCaptureSection, testCustomOn);

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

        private void RefreshKopaczCommandsUI(int option)
        {
            StackPanel panel = option == 533 ? PanelKopacz533Commands : PanelKopacz633Commands;
            List<MinerCommand> commands = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
            panel.Children.Clear();

            foreach (var cmd in commands)
                AddCommandRow(panel, cmd, option);
        }

        private void RefreshBindyCommandsUI()
        {
            if (PanelBindyCommands == null)
                return;

            PanelBindyCommands.Children.Clear();
            _bindySaveButtonsById.Clear();
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry entry = _settings.BindyEntries[i];
                entry.Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id.Trim();
                entry.Name ??= string.Empty;
                entry.Key ??= string.Empty;
                entry.Command ??= string.Empty;
                existingIds.Add(entry.Id);
                AddBindyCommandRow(entry);
            }

            var toRemove = new List<string>();
            foreach (string id in _bindyBindWasDownById.Keys)
            {
                if (!existingIds.Contains(id))
                    toRemove.Add(id);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _bindyBindWasDownById.Remove(toRemove[i]);

            var pendingToRemove = new List<string>();
            foreach (string id in _pendingBindyBindValuesById.Keys)
            {
                if (!existingIds.Contains(id))
                    pendingToRemove.Add(id);
            }

            for (int i = 0; i < pendingToRemove.Count; i++)
                _pendingBindyBindValuesById.Remove(pendingToRemove[i]);
        }

        private void StartBindyRowCapture(BindyEntry entry, TextBox keyBox)
        {
            if (_isLoadingUi)
                return;

            if (_bindyCaptureTextBox != null)
            {
                _bindyCaptureTextBox.BorderBrush = BindIdleBorderBrush;
                _bindyCaptureTextBox.BorderThickness = new Thickness(1);
            }

            _bindyCaptureEntry = entry;
            _bindyCaptureTextBox = keyBox;
            _bindyCaptureTextBox.BorderBrush = BindCaptureBorderBrush;
            _bindyCaptureTextBox.BorderThickness = new Thickness(2);
            UpdateStatusBar("BINDOWANIE: BINDY (wiersz) - naciśnij klawisz", "Orange");
            Focus();
        }

        private void CancelBindyRowCapture(bool showStatus)
        {
            if (_bindyCaptureTextBox != null)
            {
                _bindyCaptureTextBox.BorderBrush = BindIdleBorderBrush;
                _bindyCaptureTextBox.BorderThickness = new Thickness(1);
            }

            _bindyCaptureEntry = null;
            _bindyCaptureTextBox = null;
            if (showStatus)
                UpdateStatusBar("Bindowanie BINDY anulowane", "Orange");
        }

        private void RefreshBindySaveButton(string entryId)
        {
            if (!_bindySaveButtonsById.TryGetValue(entryId, out Button? saveButton) || saveButton == null)
                return;

            if (_pendingBindyBindValuesById.TryGetValue(entryId, out string? pendingKey))
                saveButton.Content = $"Zapisz ({pendingKey})";
            else
                saveButton.Content = "Zapisz";
        }

        private void ConfirmPendingBindyEntry(BindyEntry entry, TextBox keyTextBox)
        {
            string entryId = EnsureBindyEntryId(entry);
            if (!_pendingBindyBindValuesById.TryGetValue(entryId, out string? keyText))
            {
                UpdateStatusBar($"{GetBindyEntryLabel(entry)}: najpierw wybierz klawisz, potem kliknij \"Zapisz\".", "Orange");
                return;
            }

            string ownerId = $"bindy:{entryId}";
            if (TryFindBindConflict(keyText, ownerId, out string conflictOwnerLabel))
            {
                ShowBindConflict(keyText, conflictOwnerLabel, GetBindyEntryLabel(entry));
                return;
            }

            entry.Key = keyText;
            keyTextBox.Text = keyText;
            _pendingBindyBindValuesById.Remove(entryId);
            RefreshBindySaveButton(entryId);
            MarkDirty();
            UpdateStatusBar($"Zapisano klawisz {keyText} dla {GetBindyEntryLabel(entry)}", "Green");
        }

        private void AddBindyCommandRow(BindyEntry entry)
        {
            string entryId = EnsureBindyEntryId(entry);
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            CheckBox enabledCheck = new CheckBox
            {
                IsChecked = entry.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            TextBlock enabledLabel = new TextBlock
            {
                Text = "ON",
                Width = 34,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193))
            };
            TextBlock nameLabel = new TextBlock { Text = "Nazwa:", Width = 58, VerticalAlignment = VerticalAlignment.Center };
            TextBox nameBox = new TextBox
            {
                Text = entry.Name,
                Width = 145,
                Margin = new Thickness(8, 0, 12, 0)
            };
            TextBlock keyLabel = new TextBlock { Text = "Klawisz:", Width = 62, VerticalAlignment = VerticalAlignment.Center };
            TextBox keyBox = new TextBox
            {
                Text = entry.Key,
                Width = 90,
                Margin = new Thickness(10, 0, 8, 0),
                IsReadOnly = true,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180)),
                BorderBrush = BindIdleBorderBrush,
                BorderThickness = new Thickness(1)
            };
            Button saveBtn = new Button
            {
                Content = "Zapisz",
                Width = 80,
                Margin = new Thickness(0, 0, 12, 0)
            };
            saveBtn.Click += (_, __) => ConfirmPendingBindyEntry(entry, keyBox);
            keyBox.PreviewMouseLeftButtonDown += (_, e) =>
            {
                StartBindyRowCapture(entry, keyBox);
                e.Handled = true;
            };

            TextBlock commandLabel = new TextBlock { Text = "Komenda:", Width = 80, VerticalAlignment = VerticalAlignment.Center };
            TextBox commandBox = new TextBox { Text = entry.Command, Width = 260, Margin = new Thickness(10, 0, 10, 0) };

            Button deleteBtn = new Button
            {
                Content = "Usuń",
                Width = 60,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(210, 73, 73))
            };

            deleteBtn.Click += (_, __) =>
            {
                if (_bindyCaptureEntry == entry)
                    CancelBindyRowCapture(showStatus: false);

                _settings.BindyEntries.Remove(entry);
                _bindyBindWasDownById.Remove(entry.Id);
                _pendingBindyBindValuesById.Remove(entryId);
                _bindySaveButtonsById.Remove(entryId);
                RefreshBindyCommandsUI();
                MarkDirty();
            };

            enabledCheck.Checked += (_, __) =>
            {
                entry.Enabled = true;
                string key = (entry.Key ?? string.Empty).Trim();
                string ownerId = $"bindy:{entryId}";
                if (!string.IsNullOrWhiteSpace(key) && TryFindBindConflict(key, ownerId, out string conflictOwnerLabel))
                {
                    enabledCheck.IsChecked = false;
                    ShowBindConflict(key, conflictOwnerLabel, GetBindyEntryLabel(entry));
                    return;
                }

                MarkDirty();
            };

            enabledCheck.Unchecked += (_, __) =>
            {
                entry.Enabled = false;
                MarkDirty();
            };

            row.Children.Add(enabledCheck);
            row.Children.Add(enabledLabel);
            row.Children.Add(nameLabel);
            row.Children.Add(nameBox);
            row.Children.Add(keyLabel);
            row.Children.Add(keyBox);
            row.Children.Add(saveBtn);
            row.Children.Add(commandLabel);
            row.Children.Add(commandBox);
            row.Children.Add(deleteBtn);

            PanelBindyCommands.Children.Add(row);
            _bindySaveButtonsById[entryId] = saveBtn;
            RefreshBindySaveButton(entryId);

            nameBox.TextChanged += (_, __) =>
            {
                entry.Name = nameBox.Text;
                MarkDirty();
            };

            commandBox.TextChanged += (_, __) =>
            {
                entry.Command = commandBox.Text;
                MarkDirty();
            };
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

            if (_bindCaptureTarget == BindTarget.None && _bindyCaptureEntry == null)
                return;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                if (_bindyCaptureEntry != null)
                {
                    CancelBindyRowCapture(showStatus: true);
                    e.Handled = true;
                    return;
                }

                _bindCaptureTarget = BindTarget.None;
                UpdateBindCaptureVisuals();
                UpdateStatusBar("Bindowanie anulowane", "Orange");
                e.Handled = true;
                return;
            }
            if (key == Key.None)
                return;

            string keyText = key.ToString();
            if (_bindyCaptureEntry != null)
            {
                BindyEntry bindyEntry = _bindyCaptureEntry;
                string entryId = EnsureBindyEntryId(bindyEntry);
                string entryLabel = GetBindyEntryLabel(bindyEntry);
                _pendingBindyBindValuesById[entryId] = keyText;
                RefreshBindySaveButton(entryId);

                _suppressBindToggleUntilRelease = true;
                CancelBindyRowCapture(showStatus: false);
                UpdateStatusBar($"Wybrano klawisz: {keyText} ({entryLabel}) - kliknij \"Zapisz\".", "Orange");
                e.Handled = true;
                return;
            }

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

            if (_bindCaptureTarget == BindTarget.None && _bindyCaptureEntry == null)
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

            if (_bindyCaptureEntry != null)
            {
                BindyEntry bindyEntry = _bindyCaptureEntry;
                string entryId = EnsureBindyEntryId(bindyEntry);
                string entryLabel = GetBindyEntryLabel(bindyEntry);
                _pendingBindyBindValuesById[entryId] = keyText;
                RefreshBindySaveButton(entryId);

                _suppressBindToggleUntilRelease = true;
                CancelBindyRowCapture(showStatus: false);
                UpdateStatusBar($"Wybrano klawisz: {keyText} ({entryLabel}) - kliknij \"Zapisz\".", "Orange");
                e.Handled = true;
                return;
            }

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

            string ownerId = GetBindOwnerId(target);
            if (TryFindBindConflict(keyText, ownerId, out string conflictOwnerLabel))
            {
                ShowBindConflict(keyText, conflictOwnerLabel, GetBindTargetLabel(target));
                return;
            }

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

        private void BtnTestCustomCaptureBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.TestCaptureArea);
        }

        private async void BtnTestSelectCaptureArea_Click(object sender, RoutedEventArgs e)
        {
            await BeginTestCaptureAreaSelectionAsync(triggeredByBind: false);
        }

        private void BtnTestResetCaptureData_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (_isTestCaptureSelectionInProgress)
            {
                UpdateStatusBar("Najpierw zakończ zaznaczanie obszaru OCR", "Orange");
                return;
            }

            ClearTestF3LiveReadings();

            _settings.TestCustomCaptureX = 0;
            _settings.TestCustomCaptureY = 0;
            _settings.TestCustomCaptureWidth = 0;
            _settings.TestCustomCaptureHeight = 0;

            UpdateTestCustomCaptureAreaInfo();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
            UpdateStatusBar("Zresetowano dane E i obszar OCR. Zaznacz obszar ponownie.", "Green");
        }

        private void BeginTestCaptureAreaSelectionFromBind()
        {
            if (_isTestCaptureSelectionInProgress)
                return;

            _ = BeginTestCaptureAreaSelectionAsync(triggeredByBind: true);
        }

        private async Task BeginTestCaptureAreaSelectionAsync(bool triggeredByBind)
        {
            if (_isTestCaptureSelectionInProgress || _isLoadingUi)
                return;

            if (!_isMinecraftFocused || _focusedGameWindowHandle == IntPtr.Zero)
            {
                UpdateStatusBar("Najpierw ustaw fokus na okno Minecrafta", "Orange");
                return;
            }

            if (!TryGetWindowClientRectOnScreen(_focusedGameWindowHandle, out RECT clientRect))
            {
                UpdateStatusBar("Nie mogę odczytać rozmiaru okna gry", "Orange");
                return;
            }

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < 100 || clientHeight < 80)
            {
                UpdateStatusBar("Okno gry jest za małe do zaznaczania", "Orange");
                return;
            }

            _isTestCaptureSelectionInProgress = true;
            try
            {
                UpdateStatusBar("Zaznacz obszar OCR: przytrzymaj LPM i przeciągnij (Esc anuluje)", "Orange");
                Drawing.Rectangle clientBounds = new Drawing.Rectangle(clientRect.Left, clientRect.Top, clientWidth, clientHeight);
                Drawing.Rectangle? screenSelection = await Task.Run(() => CaptureScreenSelectionWithinBounds(clientBounds));
                if (!screenSelection.HasValue)
                {
                    UpdateStatusBar(triggeredByBind
                        ? "Bind OCR: anulowano zaznaczanie obszaru"
                        : "Anulowano zaznaczanie obszaru OCR", "Orange");
                    return;
                }

                Drawing.Rectangle selectedRect = screenSelection.Value;
                int relativeX = Math.Clamp(selectedRect.Left - clientRect.Left, 0, Math.Max(0, clientWidth - 1));
                int relativeY = Math.Clamp(selectedRect.Top - clientRect.Top, 0, Math.Max(0, clientHeight - 1));
                int maxWidth = Math.Max(1, clientWidth - relativeX);
                int maxHeight = Math.Max(1, clientHeight - relativeY);
                int relativeWidth = Math.Clamp(selectedRect.Width, 24, maxWidth);
                int relativeHeight = Math.Clamp(selectedRect.Height, 24, maxHeight);

                _settings.TestCustomCaptureX = relativeX;
                _settings.TestCustomCaptureY = relativeY;
                _settings.TestCustomCaptureWidth = relativeWidth;
                _settings.TestCustomCaptureHeight = relativeHeight;

                if (ChkTestCustomCaptureEnabled != null && ChkTestCustomCaptureEnabled.IsChecked != true)
                    ChkTestCustomCaptureEnabled.IsChecked = true;

                UpdateTestCustomCaptureAreaInfo();

                MarkDirty();
                UpdateStatusBar($"Zapisano obszar OCR: x={relativeX}, y={relativeY}, {relativeWidth}x{relativeHeight}", "Green");
            }
            finally
            {
                _isTestCaptureSelectionInProgress = false;
            }
        }

        private static Drawing.Rectangle? CaptureScreenSelectionWithinBounds(Drawing.Rectangle bounds)
        {
            if (bounds.Width < 40 || bounds.Height < 40)
                return null;

            Drawing.Rectangle previousFrame = Drawing.Rectangle.Empty;
            bool frameDrawn = false;
            bool dragStarted = false;
            Drawing.Point dragStart = Drawing.Point.Empty;

            while (true)
            {
                if (IsVirtualKeyDown(VK_ESCAPE))
                    return null;

                if (!GetCursorPos(out POINT cursorPointRaw))
                {
                    Thread.Sleep(10);
                    continue;
                }

                Drawing.Point cursorPoint = ClampPointToBounds(new Drawing.Point(cursorPointRaw.X, cursorPointRaw.Y), bounds);
                bool leftDown = IsVirtualKeyDown(VK_LBUTTON);

                if (!dragStarted)
                {
                    if (leftDown)
                    {
                        dragStarted = true;
                        dragStart = cursorPoint;
                    }

                    Thread.Sleep(10);
                    continue;
                }

                Drawing.Rectangle currentFrame = CreateNormalizedSelectionRect(dragStart, cursorPoint, bounds);
                if (frameDrawn)
                    DrawReversibleSelectionFrame(previousFrame);

                if (leftDown)
                {
                    if (currentFrame.Width >= 2 && currentFrame.Height >= 2)
                    {
                        DrawReversibleSelectionFrame(currentFrame);
                        previousFrame = currentFrame;
                        frameDrawn = true;
                    }
                    else
                    {
                        frameDrawn = false;
                    }

                    Thread.Sleep(10);
                    continue;
                }

                if (frameDrawn)
                    DrawReversibleSelectionFrame(previousFrame);

                if (currentFrame.Width < 24 || currentFrame.Height < 24)
                    return null;

                return currentFrame;
            }
        }

        private static void DrawReversibleSelectionFrame(Drawing.Rectangle frameRect)
        {
            if (frameRect.Width <= 0 || frameRect.Height <= 0)
                return;

            Forms.ControlPaint.DrawReversibleFrame(frameRect, Drawing.Color.White, Forms.FrameStyle.Dashed);
        }

        private static Drawing.Point ClampPointToBounds(Drawing.Point point, Drawing.Rectangle bounds)
        {
            int clampedX = Math.Clamp(point.X, bounds.Left, bounds.Right - 1);
            int clampedY = Math.Clamp(point.Y, bounds.Top, bounds.Bottom - 1);
            return new Drawing.Point(clampedX, clampedY);
        }

        private static Drawing.Rectangle CreateNormalizedSelectionRect(Drawing.Point start, Drawing.Point end, Drawing.Rectangle bounds)
        {
            int left = Math.Clamp(Math.Min(start.X, end.X), bounds.Left, bounds.Right - 1);
            int right = Math.Clamp(Math.Max(start.X, end.X), bounds.Left + 1, bounds.Right);
            int top = Math.Clamp(Math.Min(start.Y, end.Y), bounds.Top, bounds.Bottom - 1);
            int bottom = Math.Clamp(Math.Max(start.Y, end.Y), bounds.Top + 1, bounds.Bottom);
            return Drawing.Rectangle.FromLTRB(left, top, right, bottom);
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

        private void BtnBindyAddCommand_Click(object sender, RoutedEventArgs e)
        {
            _settings.BindyEntries.Add(new BindyEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Enabled = true,
                Name = string.Empty,
                Key = string.Empty,
                Command = string.Empty
            });
            RefreshBindyCommandsUI();
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

        private void CbTargetProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            ProcessTargetOption? selected = GetSelectedTargetProcessOption();
            if (selected != null)
            {
                TxtTargetWindowTitle.Text = selected.WindowTitle;
                UpdateStatusBar("Niezapisany wybór procesu - kliknij \"Zapisz program\"", "Orange");
            }
        }

        private void BtnRefreshTargetProcessList_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            RefreshTargetProcessChoices();
            ProcessTargetOption? selected = GetSelectedTargetProcessOption();
            if (selected == null)
                UpdateStatusBar("Odświeżono listę procesów. Wybierz proces gry.", "Orange");
            else
                UpdateStatusBar("Odświeżono listę procesów.", "Green");
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
            _settings.HoldLeftEnabled = ChkHoldLeftEnabled.IsChecked ?? true;
            _settings.HoldRightEnabled = ChkHoldRightEnabled.IsChecked ?? true;
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
            _settings.MacroLeftButton.Enabled = _settings.HoldEnabled && _settings.HoldLeftEnabled;
            _settings.MacroLeftButton.Key = _settings.HoldToggleKey;
            _settings.MacroLeftButton.MinCps = _settings.HoldLeftButton.MinCps;
            _settings.MacroLeftButton.MaxCps = _settings.HoldLeftButton.MaxCps;

            _settings.MacroRightButton.Enabled = _settings.HoldEnabled && _settings.HoldRightEnabled;
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

            // BINDY
            _settings.BindyEnabled = ChkBindyEnabled.IsChecked ?? false;
            _settings.BindyKey = _settings.BindyEntries.Count > 0 ? (_settings.BindyEntries[0].Key ?? string.Empty).Trim() : string.Empty;
            _settings.BindyCommands = new List<MinerCommand>();
            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                string cmd = (_settings.BindyEntries[i].Command ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cmd))
                    _settings.BindyCommands.Add(new MinerCommand { Seconds = 0, Command = cmd });
            }

            // EQ
            _settings.PauseWhenCursorVisible = ChkPauseWhenCursorVisible.IsChecked ?? true;
            _settings.TestEntitiesEnabled = ChkTestEntitiesEnabled.IsChecked ?? true;
            _settings.TestCustomCaptureEnabled = ChkTestCustomCaptureEnabled.IsChecked ?? false;
            _settings.TestCustomCaptureBind = TxtTestCustomCaptureBind.Text.Trim();
            _settings.OverlayHudEnabled = ChkOverlayHudEnabled.IsChecked ?? true;
            _settings.OverlayAnimationsEnabled = ChkOverlayAnimationsEnabled.IsChecked ?? true;
            _settings.OverlayMonitorIndex = Math.Max(0, CbOverlayMonitor?.SelectedIndex ?? 0);
            _settings.OverlayCorner = ToOverlayCornerSetting(GetSelectedOverlayCorner());

            if (includeWindowTitle)
            {
                ProcessTargetOption? selectedProcess = GetSelectedTargetProcessOption();
                if (selectedProcess != null)
                {
                    _settings.TargetProcessId = selectedProcess.ProcessId;
                    _settings.TargetProcessName = selectedProcess.ProcessName;
                    _settings.TargetWindowTitle = selectedProcess.WindowTitle;
                    TxtTargetWindowTitle.Text = selectedProcess.WindowTitle;
                }
                else
                {
                    _settings.TargetProcessId = 0;
                    _settings.TargetProcessName = string.Empty;
                    _settings.TargetWindowTitle = TxtTargetWindowTitle.Text.Trim();
                }

                TxtCurrentWindowTitle.Text = BuildTargetProcessDisplayText();
            }
        }

        private static int ParseNonNegativeInt(string value)
        {
            if (int.TryParse(value, out int parsed) && parsed >= 0)
                return parsed;
            return 0;
        }

        private void UpdateTestF3Estimator()
        {
            if (TxtTestLiveEntities != null)
                TxtTestLiveEntities.Text = "-";
        }

        private void UpdateTestF3Status(string state, bool success)
        {
            // Kompas i dodatkowe statusy są wyłączone w trybie testowym.
        }

        private void ClearTestF3LiveReadings()
        {
            if (TxtTestLiveEntities != null)
                TxtTestLiveEntities.Text = "-";

            _f3ConsecutiveReadFailures = 0;
        }

        private void RegisterF3ReadFailure(bool hardReset)
        {
            if (hardReset)
            {
                _f3ConsecutiveReadFailures = F3ReadFailureTolerance;
                ClearTestF3LiveReadings();
                return;
            }

            _f3ConsecutiveReadFailures++;
            if (_f3ConsecutiveReadFailures >= F3ReadFailureTolerance)
                ClearTestF3LiveReadings();
        }

        private void MarkF3ReadSuccess()
        {
            _f3ConsecutiveReadFailures = 0;
        }

        private void UpdateTestF3Estimator(int visibleNow, int loadedNow, bool hasEntityRatio)
        {
            if (TxtTestLiveEntities == null)
                return;

            if (hasEntityRatio && loadedNow > 0)
            {
                TxtTestLiveEntities.Text = $"{visibleNow}/{loadedNow}";
                return;
            }

            TxtTestLiveEntities.Text = "-";
        }

        private async void RunF3AnalysisTick(object? sender, EventArgs e)
        {
            if (_isLoadingUi || _isF3AnalysisInProgress)
                return;
            if (ChkTestEntitiesEnabled?.IsChecked != true)
                return;

            if (!_isMinecraftFocused || _focusedGameWindowHandle == IntPtr.Zero)
            {
                UpdateTestF3Status("Czekam na fokus gry", success: false);
                RegisterF3ReadFailure(hardReset: true);
                RefreshOverlayHud(DateTime.UtcNow);
                return;
            }

            if (!EnsureF3TesseractEngine())
            {
                UpdateTestF3Status("Brak pliku OCR eng.traineddata", success: false);
                RegisterF3ReadFailure(hardReset: true);
                RefreshOverlayHud(DateTime.UtcNow);
                return;
            }

            if (!TryGetF3CaptureArea(_focusedGameWindowHandle, out Drawing.Rectangle captureArea))
            {
                bool customEnabled = ChkTestCustomCaptureEnabled?.IsChecked == true;
                UpdateTestF3Status(customEnabled
                    ? "Brak obszaru OCR - zaznacz obszar"
                    : "Nie mogę wyznaczyć obszaru F3", success: false);
                RegisterF3ReadFailure(hardReset: true);
                RefreshOverlayHud(DateTime.UtcNow);
                return;
            }

            _isF3AnalysisInProgress = true;
            try
            {
                F3TelemetryRead telemetryRead = await ReadF3TelemetryAsync(captureArea);
                if (!telemetryRead.Success)
                {
                    UpdateTestF3Status("Brak czytelnych danych F3", success: false);
                    RegisterF3ReadFailure(hardReset: false);
                    RefreshOverlayHud(DateTime.UtcNow);
                    return;
                }

                int visibleNow = telemetryRead.VisibleNow;
                int loadedNow = telemetryRead.LoadedNow;
                bool hasEntityRatio = telemetryRead.HasEntityRatio;

                UpdateTestF3Estimator(visibleNow, loadedNow, hasEntityRatio);
                MarkF3ReadSuccess();
                RefreshOverlayHud(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                UpdateTestF3Status("Błąd OCR: " + ex.Message, success: false);
                RegisterF3ReadFailure(hardReset: false);
                RefreshOverlayHud(DateTime.UtcNow);
            }
            finally
            {
                _isF3AnalysisInProgress = false;
            }
        }

        private bool TryGetF3CaptureArea(IntPtr windowHandle, out Drawing.Rectangle captureArea)
        {
            if (ChkTestCustomCaptureEnabled?.IsChecked == true)
            {
                if (TryGetCustomF3CaptureArea(windowHandle, out captureArea))
                    return true;

                captureArea = Drawing.Rectangle.Empty;
                return false;
            }

            return TryGetDefaultF3CaptureArea(windowHandle, out captureArea);
        }

        private bool TryGetCustomF3CaptureArea(IntPtr windowHandle, out Drawing.Rectangle captureArea)
        {
            captureArea = Drawing.Rectangle.Empty;
            if (!HasCustomCaptureAreaConfigured())
                return false;
            if (!TryGetWindowClientRectOnScreen(windowHandle, out RECT clientRect))
                return false;

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < 40 || clientHeight < 40)
                return false;

            int offsetX = Math.Clamp(_settings.TestCustomCaptureX, 0, Math.Max(0, clientWidth - 24));
            int offsetY = Math.Clamp(_settings.TestCustomCaptureY, 0, Math.Max(0, clientHeight - 24));
            int width = Math.Clamp(_settings.TestCustomCaptureWidth, 24, Math.Max(24, clientWidth - offsetX));
            int height = Math.Clamp(_settings.TestCustomCaptureHeight, 24, Math.Max(24, clientHeight - offsetY));

            if (offsetX + width > clientWidth)
                width = clientWidth - offsetX;
            if (offsetY + height > clientHeight)
                height = clientHeight - offsetY;
            if (width <= 0 || height <= 0)
                return false;

            captureArea = new Drawing.Rectangle(clientRect.Left + offsetX, clientRect.Top + offsetY, width, height);
            return true;
        }

        private static bool TryGetDefaultF3CaptureArea(IntPtr windowHandle, out Drawing.Rectangle captureArea)
        {
            captureArea = Drawing.Rectangle.Empty;
            if (!TryGetWindowClientRectOnScreen(windowHandle, out RECT clientRect))
                return false;

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < 220 || clientHeight < 120)
                return false;

            int width = Math.Min(F3CaptureWidth, Math.Max(220, clientWidth - F3CaptureMargin * 2));
            int height = Math.Min(F3CaptureHeight, Math.Max(120, clientHeight - F3CaptureMargin * 2));
            captureArea = new Drawing.Rectangle(clientRect.Left + F3CaptureMargin, clientRect.Top + F3CaptureMargin, width, height);
            return captureArea.Width > 0 && captureArea.Height > 0;
        }

        private static bool TryGetWindowClientRectOnScreen(IntPtr windowHandle, out RECT clientRectOnScreen)
        {
            clientRectOnScreen = default;
            if (windowHandle == IntPtr.Zero)
                return false;
            if (!GetClientRect(windowHandle, out RECT clientRect))
                return false;

            POINT topLeft = new POINT { X = clientRect.Left, Y = clientRect.Top };
            POINT bottomRight = new POINT { X = clientRect.Right, Y = clientRect.Bottom };
            if (!ClientToScreen(windowHandle, ref topLeft))
                return false;
            if (!ClientToScreen(windowHandle, ref bottomRight))
                return false;

            clientRectOnScreen = new RECT
            {
                Left = topLeft.X,
                Top = topLeft.Y,
                Right = bottomRight.X,
                Bottom = bottomRight.Y
            };

            return true;
        }

        private bool EnsureF3TesseractEngine()
        {
            if (_f3TesseractEngine != null)
                return true;

            string? tessDataPath = ResolveTessDataPath();
            if (string.IsNullOrWhiteSpace(tessDataPath))
                return false;

            try
            {
                _f3TesseractEngine = new TesseractEngine(tessDataPath, "eng", TesseractEngineMode.Default);
                _f3TesseractEngine.DefaultPageSegMode = TesseractPageSegMode.SparseText;
                _f3TesseractEngine.SetVariable("tessedit_char_whitelist", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:/;|\\-() ");
                _f3TesseractEngine.SetVariable("preserve_interword_spaces", "1");
                return true;
            }
            catch
            {
                _f3TesseractEngine?.Dispose();
                _f3TesseractEngine = null;
                return false;
            }
        }

        private static string? ResolveTessDataPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "tessdata"),
                Path.Combine(baseDirectory, "x64", "tessdata"),
                Path.Combine(baseDirectory, "runtimes", "win-x64", "native", "tessdata")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "eng.traineddata")))
                    return candidate;
            }

            return null;
        }

        private async Task<F3TelemetryRead> ReadF3TelemetryAsync(Drawing.Rectangle captureArea)
        {
            if (_f3TesseractEngine == null)
                return new F3TelemetryRead(false, string.Empty, 0, 0, false);

            return await Task.Run(() =>
            {
                using Drawing.Bitmap screenshot = new Drawing.Bitmap(captureArea.Width, captureArea.Height, DrawingImaging.PixelFormat.Format32bppArgb);
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(screenshot))
                {
                    graphics.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, Drawing.CopyPixelOperation.SourceCopy);
                }

                using Drawing.Bitmap prepared = PrepareBitmapForF3Ocr(screenshot);
                string preparedText = RunOcrOnBitmap(prepared, TesseractPageSegMode.SparseText);
                string rawText = RunOcrOnBitmap(screenshot, TesseractPageSegMode.SparseText);
                string rawBlockText = RunOcrOnBitmap(screenshot, TesseractPageSegMode.SingleBlock);

                var attempts = new List<(string Source, string Text)>
                {
                    ("prepared", preparedText),
                    ("raw", rawText),
                    ("raw-block", rawBlockText)
                };

                for (int i = 0; i < attempts.Count; i++)
                {
                    string candidateText = attempts[i].Text;
                    if (!TryExtractF3Telemetry(candidateText, out int visibleNow, out int loadedNow, out bool hasEntityRatio))
                        continue;

                    return new F3TelemetryRead(true, candidateText, visibleNow, loadedNow, hasEntityRatio);
                }

                return new F3TelemetryRead(false, attempts[0].Text, 0, 0, false);
            });
        }

        private string RunOcrOnBitmap(Drawing.Bitmap bitmap, TesseractPageSegMode pageSegMode)
        {
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, DrawingImaging.ImageFormat.Png);
            byte[] imageBytes = memoryStream.ToArray();

            lock (_f3TesseractLock)
            {
                if (_f3TesseractEngine == null)
                    return string.Empty;

                TesseractPageSegMode previousMode = _f3TesseractEngine.DefaultPageSegMode;
                using TesseractPix pix = TesseractPix.LoadFromMemory(imageBytes);
                try
                {
                    _f3TesseractEngine.DefaultPageSegMode = pageSegMode;
                    using TesseractPage page = _f3TesseractEngine.Process(pix);
                    return page.GetText() ?? string.Empty;
                }
                finally
                {
                    _f3TesseractEngine.DefaultPageSegMode = previousMode;
                }
            }
        }

        private static Drawing.Bitmap PrepareBitmapForF3Ocr(Drawing.Bitmap source)
        {
            const int scale = 2;
            var scaled = new Drawing.Bitmap(source.Width * scale, source.Height * scale, DrawingImaging.PixelFormat.Format32bppArgb);
            using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.SmoothingMode = Drawing2D.SmoothingMode.None;
                graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half;
                graphics.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed;
                graphics.DrawImage(
                    source,
                    new Drawing.Rectangle(0, 0, scaled.Width, scaled.Height),
                    new Drawing.Rectangle(0, 0, source.Width, source.Height),
                    Drawing.GraphicsUnit.Pixel);
            }

            Drawing.Rectangle rect = new Drawing.Rectangle(0, 0, scaled.Width, scaled.Height);
            DrawingImaging.BitmapData bitmapData = scaled.LockBits(rect, DrawingImaging.ImageLockMode.ReadWrite, DrawingImaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = bitmapData.Stride;
                int absStride = Math.Abs(stride);
                int bytes = absStride * bitmapData.Height;
                byte[] buffer = new byte[bytes];
                Marshal.Copy(bitmapData.Scan0, buffer, 0, bytes);

                for (int y = 0; y < bitmapData.Height; y++)
                {
                    int rowOffset = stride >= 0 ? y * stride : (bitmapData.Height - 1 - y) * absStride;
                    for (int x = 0; x < bitmapData.Width; x++)
                    {
                        int pixelOffset = rowOffset + x * 4;
                        byte b = buffer[pixelOffset];
                        byte g = buffer[pixelOffset + 1];
                        byte r = buffer[pixelOffset + 2];

                        int max = Math.Max(r, Math.Max(g, b));
                        int min = Math.Min(r, Math.Min(g, b));
                        int luminance = (r * 299 + g * 587 + b * 114) / 1000;
                        int saturationRange = max - min;

                        bool likelyWhiteText = saturationRange <= 38 && luminance >= 165;
                        bool likelyLightGrayText = saturationRange <= 24 && luminance >= 142;
                        bool likelyText = likelyWhiteText || likelyLightGrayText;
                        if (!likelyText && saturationRange <= 14 && luminance >= 180)
                            likelyText = true;
                        byte value = likelyText ? (byte)0 : (byte)255;

                        buffer[pixelOffset] = value;
                        buffer[pixelOffset + 1] = value;
                        buffer[pixelOffset + 2] = value;
                        buffer[pixelOffset + 3] = 255;
                    }
                }

                Marshal.Copy(buffer, 0, bitmapData.Scan0, bytes);
            }
            finally
            {
                scaled.UnlockBits(bitmapData);
            }

            return scaled;
        }

        private static bool TryExtractF3Telemetry(string rawText, out int visibleNow, out int loadedNow, out bool hasEntityRatio)
        {
            visibleNow = 0;
            loadedNow = 0;
            hasEntityRatio = false;

            string filtered = FilterOcrTextForEParsing(rawText);
            if (!TryExtractEntitiesFromESection(filtered, out visibleNow, out loadedNow))
                return false;

            hasEntityRatio = true;
            return true;
        }

        private static bool TryExtractEntitiesFromESection(string rawText, out int visibleNow, out int loadedNow)
        {
            visibleNow = 0;
            loadedNow = 0;
            if (string.IsNullOrWhiteSpace(rawText))
                return false;

            string normalized = NormalizeOcrText(rawText);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = CompactWhitespace(lines[i]).Trim();
                if (line.Length == 0)
                    continue;

                if (!TryParseEntityLine(line, out int parsedVisible, out int parsedLoaded))
                    continue;

                visibleNow = parsedVisible;
                loadedNow = parsedLoaded;
                return true;
            }

            Match blockMatch = F3EntityFromBlockRegex.Match(normalized);
            if (blockMatch.Success)
            {
                string left = NormalizeEntityNumberToken(blockMatch.Groups[1].Value);
                string right = NormalizeEntityNumberToken(blockMatch.Groups[2].Value);
                if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVisible)
                    && int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLoaded)
                    && parsedLoaded > 0
                    && parsedVisible >= 0
                    && parsedVisible <= parsedLoaded)
                {
                    visibleNow = parsedVisible;
                    loadedNow = parsedLoaded;
                    return true;
                }
            }

            return false;
        }

        private static string FilterOcrTextForEParsing(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            var builder = new StringBuilder(rawText.Length);
            for (int i = 0; i < rawText.Length; i++)
            {
                char c = rawText[i];
                if (char.IsLetterOrDigit(c)
                    || char.IsWhiteSpace(c)
                    || c == ':' || c == ';' || c == '/' || c == '\\'
                    || c == '|' || c == '.' || c == ',' || c == '-'
                    || c == '(' || c == ')')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static bool TryParseEntityLine(string rawLine, out int visibleNow, out int loadedNow)
        {
            visibleNow = 0;
            loadedNow = 0;
            if (string.IsNullOrWhiteSpace(rawLine))
                return false;

            string normalizedLine = CompactWhitespace(rawLine).Trim();
            if (normalizedLine.Length == 0)
                return false;

            Match match = F3EntityOnlyLineRegex.Match(normalizedLine);
            if (!match.Success)
                return false;

            string left = NormalizeEntityNumberToken(match.Groups[1].Value);
            string right = NormalizeEntityNumberToken(match.Groups[2].Value);
            if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVisible))
                return false;
            if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedLoaded))
                return false;
            if (parsedLoaded <= 0)
                return false;
            if (parsedVisible < 0 || parsedVisible > parsedLoaded)
                return false;

            visibleNow = parsedVisible;
            loadedNow = parsedLoaded;
            return true;
        }

        private static string NormalizeEntityNumberToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return string.Empty;

            string normalized = token
                .Replace('I', '1')
                .Replace('l', '1')
                .Replace('O', '0')
                .Replace('o', '0');

            var builder = new StringBuilder(normalized.Length);
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsDigit(c))
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static string NormalizeOcrText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            string text = rawText
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\u00A0', ' ');

            string[] lines = text.Split('\n');
            var builder = new StringBuilder(text.Length);

            foreach (string rawLine in lines)
            {
                string line = CompactWhitespace(rawLine);
                if (line.Length == 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(line);
            }

            return builder.ToString();
        }

        private static string CompactWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            bool hasPendingSpace = false;
            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    hasPendingSpace = builder.Length > 0;
                    continue;
                }

                if (hasPendingSpace)
                {
                    builder.Append(' ');
                    hasPendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
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

        private static string EnsureBindyEntryId(BindyEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Id))
                return entry.Id;

            entry.Id = Guid.NewGuid().ToString("N");
            return entry.Id;
        }

        private void SyncBindyKeyStates()
        {
            var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry entry = _settings.BindyEntries[i];
                string id = EnsureBindyEntryId(entry);
                validIds.Add(id);
                _bindyBindWasDownById[id] = entry.Enabled && IsConfiguredBindKeyDown(entry.Key);
            }

            var staleIds = new List<string>();
            foreach (string id in _bindyBindWasDownById.Keys)
            {
                if (!validIds.Contains(id))
                    staleIds.Add(id);
            }

            for (int i = 0; i < staleIds.Count; i++)
                _bindyBindWasDownById.Remove(staleIds[i]);
        }

        private bool IsAnyBindyKeyDown()
        {
            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry entry = _settings.BindyEntries[i];
                if (!entry.Enabled)
                    continue;

                string key = (entry.Key ?? string.Empty).Trim();
                if (IsConfiguredBindKeyDown(key))
                    return true;
            }

            return false;
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

            if (_bindCaptureTarget != BindTarget.None || _bindyCaptureEntry != null)
                return;

            if (_suppressBindToggleUntilRelease)
            {
                bool holdDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                bool autoLeftDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                bool autoRightDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                bool jablkaDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                bool kop533Down = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                bool kop633Down = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);
                bool testCaptureDown = IsConfiguredBindKeyDown(TxtTestCustomCaptureBind.Text);
                bool bindyDown = IsAnyBindyKeyDown();

                _holdBindWasDown = holdDown;
                _autoLeftBindWasDown = autoLeftDown;
                _autoRightBindWasDown = autoRightDown;
                _jablkaBindWasDown = jablkaDown;
                _kopacz533BindWasDown = kop533Down;
                _kopacz633BindWasDown = kop633Down;
                _testCaptureBindWasDown = testCaptureDown;

                if (holdDown || autoLeftDown || autoRightDown || jablkaDown || kop533Down || kop633Down || testCaptureDown || bindyDown)
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
                _testCaptureBindWasDown = IsConfiguredBindKeyDown(TxtTestCustomCaptureBind.Text);
                SyncBindyKeyStates();

                SetCursorPauseState(false);
                SetKopacz533MiningHold(false);
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetHoldLeftToggleState(clearToggleEnabled: false);
                ResetBindyRuntimeState();
                return;
            }

            bool changed = false;

            bool holdModeSelected = ChkMacroManualEnabled.IsChecked == true;
            bool autoLeftModeSelected = ChkAutoLeftEnabled.IsChecked == true;
            bool autoRightModeSelected = ChkAutoRightEnabled.IsChecked == true;
            bool jablkaModeSelected = ChkJablkaZLisciEnabled.IsChecked == true;
            bool kop533ModeSelected = ChkKopacz533Enabled.IsChecked == true;
            bool kop633ModeSelected = ChkKopacz633Enabled.IsChecked == true;
            bool bindyModeSelected = ChkBindyEnabled.IsChecked == true;
            bool testCaptureModeSelected = ChkTestEntitiesEnabled.IsChecked == true && ChkTestCustomCaptureEnabled.IsChecked == true;
            bool internalCommandTyping =
                _jablkaCommandStage != JablkaCommandStage.None ||
                _kopacz533CommandStage != Kopacz533CommandStage.None ||
                _kopacz633CommandStage != Kopacz633CommandStage.None ||
                _bindyCommandStage != BindyCommandStage.None;

            if (internalCommandTyping)
            {
                // Prevent self-trigger: internal typed keys (chat commands) cannot toggle bind states.
                _holdBindWasDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                _jablkaBindWasDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                _kopacz533BindWasDown = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                _kopacz633BindWasDown = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);
                _testCaptureBindWasDown = IsConfiguredBindKeyDown(TxtTestCustomCaptureBind.Text);
                SyncBindyKeyStates();
            }

            if (!internalCommandTyping && testCaptureModeSelected && IsBindPressed(TxtTestCustomCaptureBind.Text, ref _testCaptureBindWasDown))
                BeginTestCaptureAreaSelectionFromBind();

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
                {
                    UpdateStatusBar("Kopacz 6/3/3: wybierz kierunek 'Na wprost' lub 'Do góry'", "Orange");
                }
                else
                {
                    UpdateStatusBar(_kopacz633RuntimeEnabled ? "Kopacz 6/3/3 aktywowany" : "Kopacz 6/3/3 wyłączony", "Orange");
                }
                changed = true;
            }

            if (!internalCommandTyping && bindyModeSelected && _bindyCommandStage == BindyCommandStage.None && TryGetPressedBindyEntry(out BindyEntry bindyEntry))
            {
                StartBindyRuntime(DateTime.UtcNow, bindyEntry);
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
            if (!bindyModeSelected)
            {
                SyncBindyKeyStates();
                if (_bindyCommandStage != BindyCommandStage.None)
                {
                    ResetBindyRuntimeState();
                    changed = true;
                }
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
                bool holdLeftEnabled = ChkHoldLeftEnabled.IsChecked == true;
                bool holdRightEnabled = ChkHoldRightEnabled.IsChecked == true;

                if (holdLeftEnabled)
                {
                    if (TryToggleHoldLeftClicking(now))
                    {
                        UpdateStatusBar(_holdLeftToggleClickingEnabled ? "HOLD LPM: ON (kliknij LPM ponownie aby wyłączyć)" : "HOLD LPM: OFF", "Orange");
                        RefreshTopTiles();
                    }

                    if (_holdLeftToggleClickingEnabled)
                        TryPerformClick(ref _nextHoldLeftClickAtUtc, TxtManualLeftMinCps.Text, TxtManualLeftMaxCps.Text, leftButton: true, now, holdPulseMode: false);
                    else
                        _nextHoldLeftClickAtUtc = now;
                }
                else
                {
                    _holdLeftToggleClickingEnabled = false;
                    _holdLeftToggleWasDown = false;
                    _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
                    _nextHoldLeftClickAtUtc = now;
                }

                bool rightHoldWasActive = _holdRightRuntimePressActive;
                bool rightHoldActive = holdRightEnabled && IsVirtualKeyDown(VK_RBUTTON);
                if (rightHoldActive != rightHoldWasActive)
                {
                    _holdRightRuntimePressActive = rightHoldActive;
                    RefreshTopTiles();
                }

                if (holdRightEnabled && rightHoldActive)
                {
                    TryPerformClick(ref _nextHoldRightClickAtUtc, TxtManualRightMinCps.Text, TxtManualRightMaxCps.Text, leftButton: false, now, holdPulseMode: true);
                }
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

            if (bindyModeSelected)
                RunBindyTick(now);
            else
                ResetBindyRuntimeState(now);

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

        private bool TryGetPressedBindyEntry(out BindyEntry entry)
        {
            entry = null!;
            var staleIds = new HashSet<string>(_bindyBindWasDownById.Keys, StringComparer.OrdinalIgnoreCase);
            BindyEntry? detectedEntry = null;

            for (int i = 0; i < _settings.BindyEntries.Count; i++)
            {
                BindyEntry current = _settings.BindyEntries[i];
                string id = EnsureBindyEntryId(current);
                staleIds.Remove(id);

                if (!current.Enabled)
                {
                    _bindyBindWasDownById[id] = false;
                    continue;
                }

                string key = (current.Key ?? string.Empty).Trim();
                bool isDown = !string.IsNullOrWhiteSpace(key) && IsConfiguredBindKeyDown(key);
                bool wasDown = _bindyBindWasDownById.TryGetValue(id, out bool previous) && previous;
                _bindyBindWasDownById[id] = isDown;

                if (!isDown || wasDown)
                    continue;

                string command = (current.Command ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                detectedEntry = current;
                break;
            }

            foreach (string staleId in staleIds)
                _bindyBindWasDownById.Remove(staleId);

            if (detectedEntry == null)
                return false;

            entry = detectedEntry;
            return true;
        }

        private void StartBindyRuntime(DateTime now, BindyEntry entry)
        {
            if (entry == null)
                return;

            string command = (entry.Command ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(command))
                return;

            ResetBindyRuntimeState(now);
            _bindyPendingCommand = command;
            _bindyPendingEntryName = GetBindyEntryDisplayName(entry);
            _bindyCommandStage = BindyCommandStage.OpenChat;
            _nextBindyStageAtUtc = now;
            UpdateStatusBar($"BINDY: uruchomiono \"{_bindyPendingEntryName}\"", "Orange");
        }

        private void RunBindyTick(DateTime now)
        {
            if (_bindyCommandStage == BindyCommandStage.None)
                return;

            if (_jablkaCommandStage != JablkaCommandStage.None || _kopacz533CommandStage != Kopacz533CommandStage.None || _kopacz633CommandStage != Kopacz633CommandStage.None)
                return;

            if (!TryProcessBindyCommand(now))
                ResetBindyRuntimeState(now);
        }

        private bool TryProcessBindyCommand(DateTime now)
        {
            if (_bindyCommandStage == BindyCommandStage.None)
                return false;

            if (now < _nextBindyStageAtUtc)
                return true;

            switch (_bindyCommandStage)
            {
                case BindyCommandStage.OpenChat:
                    SendKeyTap(VK_T);
                    _bindyCommandStage = BindyCommandStage.TypeCommand;
                    _nextBindyStageAtUtc = now.AddMilliseconds(BindyDelayAfterOpenChatMs);
                    return true;

                case BindyCommandStage.TypeCommand:
                    if (!SendTextByKeyboard(_bindyPendingCommand))
                    {
                        UpdateStatusBar("BINDY: błąd wpisywania komendy", "Red");
                        ResetBindyRuntimeState(now.AddSeconds(1));
                        return true;
                    }

                    _bindyCommandStage = BindyCommandStage.SubmitCommand;
                    _nextBindyStageAtUtc = now.AddMilliseconds(BindyDelayAfterTypeCommandMs);
                    return true;

                case BindyCommandStage.SubmitCommand:
                    SendKeyTap(VK_RETURN);
                    string executedName = string.IsNullOrWhiteSpace(_bindyPendingEntryName) ? "Bind" : _bindyPendingEntryName.Trim();
                    _bindyLastExecutedName = executedName;
                    _bindyLastExecutedAtUtc = now;
                    UpdateStatusBar($"BINDY: {executedName} zostało wykonane", "Green");
                    RefreshOverlayHud(now);
                    ResetBindyRuntimeState(now.AddMilliseconds(BindyDelayAfterSubmitCommandMs));
                    return true;

                default:
                    return false;
            }
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

        private void ResetBindyRuntimeState()
        {
            ResetBindyRuntimeState(DateTime.UtcNow);
        }

        private void ResetBindyRuntimeState(DateTime now)
        {
            _nextBindyStageAtUtc = now;
            _bindyCommandStage = BindyCommandStage.None;
            _bindyPendingCommand = string.Empty;
            _bindyPendingEntryName = string.Empty;
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

        private void ChkHoldLeftEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkHoldLeftEnabled.IsChecked != true && ChkHoldRightEnabled.IsChecked != true)
                ChkHoldRightEnabled.IsChecked = true;

            UpdateEnabledStates();
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkHoldRightEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkHoldRightEnabled.IsChecked != true && ChkHoldLeftEnabled.IsChecked != true)
                ChkHoldLeftEnabled.IsChecked = true;

            UpdateEnabledStates();
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkBindyEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkBindyEnabled.IsChecked != true)
                ResetBindyRuntimeState();

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkOverlaySettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateOverlayLayout();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
        }

        private void CbOverlayMonitor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateOverlayLayout();
            MarkDirty();
        }

        private void CbOverlayCorner_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateOverlayLayout();
            MarkDirty();
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

        private void ChkTestEntitiesEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkTestEntitiesEnabled.IsChecked != true)
                ClearTestF3LiveReadings();

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkTestCustomCaptureEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            UpdateTestCustomCaptureAreaInfo();
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

                ProcessTargetOption? selectedProcess = GetSelectedTargetProcessOption();
                if (selectedProcess != null)
                {
                    _settings.TargetProcessId = selectedProcess.ProcessId;
                    _settings.TargetProcessName = selectedProcess.ProcessName;
                    _settings.TargetWindowTitle = selectedProcess.WindowTitle;
                    TxtTargetWindowTitle.Text = selectedProcess.WindowTitle;
                }
                else
                {
                    string legacyTitle = TxtTargetWindowTitle.Text.Trim();
                    if (string.IsNullOrWhiteSpace(legacyTitle))
                    {
                        UpdateStatusBar("Wybierz proces z listy i kliknij \"Zapisz program\"", "Orange");
                        return;
                    }

                    _settings.TargetProcessId = 0;
                    _settings.TargetProcessName = string.Empty;
                    _settings.TargetWindowTitle = legacyTitle;
                }

                TxtCurrentWindowTitle.Text = BuildTargetProcessDisplayText();
                _settingsService.Save(_settings);

                _pendingChanges = false;
                _dirtyTimer.Stop();
                TxtSettingsSaved.Text = "✓ Tak";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));

                _isMinecraftFocused = CheckGameFocus();
                UpdateStatusBar("Program gry zapisany", "Green");
            }
            catch (Exception ex)
            {
                UpdateStatusBar("Błąd zapisu programu gry: " + ex.Message, "Red");
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
            _isExitRequested = true;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _dirtyTimer.Stop();
            _focusTimer.Stop();
            _macroTimer.Stop();
            _f3AnalysisTimer.Stop();
            if (_overlayHud != null)
            {
                _overlayHud.Close();
                _overlayHud = null;
            }
            lock (_f3TesseractLock)
            {
                _f3TesseractEngine?.Dispose();
                _f3TesseractEngine = null;
            }
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            ResetBindyRuntimeState();
            base.OnClosed(e);
        }
    }
}


