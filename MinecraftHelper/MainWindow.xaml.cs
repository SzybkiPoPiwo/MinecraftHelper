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
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
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
        private readonly MiningLogService _miningLogService;
        private AppSettings _settings;
        private readonly bool _isFirstRun;
        private readonly string _currentAppVersion;
        private readonly bool _showStartupNotice;

        private bool _pendingChanges = false;
        private readonly DispatcherTimer _dirtyTimer;
        private readonly DispatcherTimer _focusTimer;
        private readonly DispatcherTimer _macroTimer;
        private readonly DispatcherTimer _autoReconnectTimer;
        private readonly DispatcherTimer _transientStatusTimer;
        private readonly DispatcherTimer _bindyHudClearTimer;
        private readonly MacroDiagnosticsService _macroDiagnosticsService;
        private readonly AutoClickScheduler _autoClickScheduler;
        private readonly DamageSoundDetector _damageSoundDetector;

        private bool _isMinecraftFocused;
        private IntPtr _targetGameWindowHandle = IntPtr.Zero;
        private bool _isLoadingUi = true;
        private readonly Dictionary<FrameworkElement, bool> _expandableSectionStates = new();
        private bool _isPausedByCursorVisibility;
        private bool _holdMacroRuntimeEnabled;
        private bool _autoLeftRuntimeEnabled;
        private bool _autoLeftDabHolding;
        private bool _autoRightRuntimeEnabled;
        private bool _jablkaRuntimeEnabled;
        private bool _kopacz533RuntimeEnabled;
        private bool _kopacz633RuntimeEnabled;
        private bool _testFastUpExitRuntimeEnabled;
        private bool _testAutoFishingRuntimeEnabled;
        private bool _emergencyDamageSoundMonitoringForMiner;
        private bool _emergencyDamageSoundManualTestActive;
        private bool _emergencyDamageSoundHandlingAlarm;
        private DateTime _emergencyDamageSoundManualTestUntilUtc = DateTime.MinValue;
        private DateTime _emergencyDamageSoundLastAlarmAtUtc = DateTime.MinValue;
        private bool _emergencyReconnectActive;
        private bool _emergencyReconnectSoundGuardActive;
        private bool _emergencyReconnectResumeKopacz533;
        private bool _emergencyReconnectResumeKopacz633;
        private bool _emergencyReconnectShutdownAfterHome;
        private int _emergencyReconnectInventoryAttempts;

        private bool _holdBindWasDown;
        private bool _autoLeftBindWasDown;
        private bool _autoRightBindWasDown;
        private bool _autoLeftComboTriggerWasDown;
        private bool _autoLeftComboStopWasDown;
        private bool _autoRightComboTriggerWasDown;
        private bool _autoRightComboStopWasDown;
        private bool _jablkaBindWasDown;
        private bool _kopacz533BindWasDown;
        private bool _kopacz633BindWasDown;
        private bool _testCaptureBindWasDown;
        private bool _testFastUpExitBindWasDown;
        private bool _testAutoFishingBindWasDown;
        private bool _testAutoFishingCaptureBindWasDown;
        private bool _suppressBindToggleUntilRelease;

        private DateTime _nextHoldLeftClickAtUtc = DateTime.UtcNow;
        private DateTime _nextHoldRightClickAtUtc = DateTime.UtcNow;
        private DateTime _nextAutoLeftClickAtUtc = DateTime.UtcNow;
        private DateTime _nextAutoRightClickAtUtc = DateTime.UtcNow;
        private bool _holdLeftToggleClickingEnabled;
        private bool _holdLeftToggleWasDown;
        private DateTime _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
        private bool _holdRightRuntimePressActive;
        private bool _holdRightInjectedButtonDown;
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
        private readonly List<CheckBox> _inventoryCleanupSlotCheckBoxes = new List<CheckBox>();
        private readonly List<CheckBox> _inventoryCleanupItemTypeCheckBoxes = new List<CheckBox>();
        private readonly List<Button> _autoReconnectHomeSlotButtons = new List<Button>();
        private readonly List<Button> _autoReconnectServerProfileButtons = new List<Button>();
        private readonly List<Drawing.Point> _inventoryCleanupTargets = new List<Drawing.Point>();
        private readonly Dictionary<string, int> _inventoryCleanupInitialItemTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _inventoryCleanupInitialItemTypeStackCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _inventoryCleanupItemTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _inventoryCleanupItemTypeStackCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private InventoryCleanupStage _inventoryCleanupStage = InventoryCleanupStage.None;
        private int _inventoryCleanupGeneration;
        private bool _inventoryCleanupScanInProgress;
        private InventoryCleanupOwner _inventoryCleanupOwner = InventoryCleanupOwner.None;
        private DateTime _nextInventoryCleanupAtUtc = DateTime.MaxValue;
        private DateTime _nextInventoryCleanupStageAtUtc = DateTime.UtcNow;
        private InventoryCleanupOwner _inventoryCleanupCommandPauseOwner = InventoryCleanupOwner.None;
        private DateTime _inventoryCleanupCommandPauseStartedAtUtc = DateTime.MinValue;
        private int _inventoryCleanupTargetIndex;
        private int _inventoryCleanupDetectionAttempts;
        private int _inventoryCleanupDropPass;
        private int _inventoryCleanupInitialMarkedStacks;
        private int _inventoryCleanupRemovedStacks;
        private int _inventoryCleanupRemovedItems;
        private int _inventoryCleanupRemainingMarkedStacks;
        private bool _inventoryCleanupCursorParkedForDetection;
        private Drawing.Rectangle _inventoryCleanupClientArea = Drawing.Rectangle.Empty;
        private bool _inventoryCleanupControlDown;
        private bool _inventoryCleanupDropKeyDown;
        private int _inventoryCleanupDropVirtualKey;
        private bool _inventoryCleanupReturnLeftDown;
        private bool _inventoryCleanupReturnBackwardDown;
        private DateTime _inventoryCleanupReturnLeftUntilUtc = DateTime.MinValue;
        private DateTime _inventoryCleanupReturnBackwardUntilUtc = DateTime.MinValue;
        private bool _inventoryCleanupEatAfterCleanupPending;
        private bool _inventoryCleanupEatingRightButtonDown;
        private bool _inventoryCleanupEatingCompleted;
        private string _inventoryCleanupLastResult = "Brak poprzedniego skanu";
        private bool _inventoryCleanupLastResultWarning;
        private int _inventoryCleanupFullCobblestoneStacks;
        private int _inventoryCleanupLastFullCobblestoneStacks;
        private bool _inventoryCleanupCobbleXCommandPending;
        private bool _inventoryCleanupCobbleXCommandSent;
        private bool _inventoryCleanupCobbleXCommandFailed;
        private string _inventoryCleanupPendingCobbleXCommand = string.Empty;
        private string _inventoryCleanupLogSessionId = string.Empty;
        private string _inventoryCleanupLogStartError = string.Empty;
        private string _kopacz533MiningRunId = string.Empty;
        private string _kopacz633MiningRunId = string.Empty;
        private DateTime _inventoryCleanupOpenedAtUtc = DateTime.MinValue;
        private MiningLogSummary _latestMiningLogSummary;
        private static readonly (string Id, string Label)[] InventoryCleanupItemTypes =
        {
            ("diamond", "Diament"),
            ("gold_ingot", "Sztabka złota"),
            ("gold_block", "Blok złota"),
            ("iron_ingot", "Sztabka żelaza"),
            ("iron_block", "Blok żelaza"),
            ("emerald", "Emerald"),
            ("emerald_block", "Blok emeraldu"),
            ("obsidian", "Obsydian"),
            ("apple", "Jabłko"),
            ("sand", "Piasek"),
            ("gunpowder", "Proch"),
            ("coal", "Węgiel"),
            ("quartz", "Kwarc"),
            ("book", "Książka"),
            ("ender_pearl", "Ender perła"),
            ("redstone", "Redstone")
        };
        private DateTime _nextBindyStageAtUtc = DateTime.UtcNow;
        private BindyCommandStage _bindyCommandStage = BindyCommandStage.None;
        private string _bindyPendingCommand = string.Empty;
        private string _bindyPendingEntryName = string.Empty;
        private string _bindyLastExecutedName = string.Empty;
        private DateTime _bindyLastExecutedAtUtc = DateTime.MinValue;
        private DateTime _nextTestFastUpExitActionAtUtc = DateTime.UtcNow;
        private FastUpExitStage _testFastUpExitStage = FastUpExitStage.LookUp;
        private DateTime _testFastUpExitBreakHoldUntilUtc = DateTime.UtcNow;
        private bool _testFastUpExitBreakHoldActive;
        private DateTime _testFastUpExitPlaceHoldUntilUtc = DateTime.UtcNow;
        private bool _testFastUpExitPlaceHoldActive;
        private DateTime _testFastUpExitPlacePulseAtUtc = DateTime.UtcNow;
        private int _testFastUpExitLookSweepTicksRemaining;
        private int _testFastUpExitLookSweepBurstsPerTick = FastUpLookSweepBurstsPerTick;
        private int _testFastUpExitLookSweepDirectionY;
        private FastUpExitStage _testFastUpExitLookSweepNextStage = FastUpExitStage.LookUp;
        private DateTime _testFastUpExitJumpHoldUntilUtc = DateTime.UtcNow;
        private bool _testFastUpExitJumpHoldActive;
        private DateTime _nextTestAutoFishingScanAtUtc = DateTime.UtcNow;
        private DateTime _nextTestAutoFishingActionAtUtc = DateTime.UtcNow;
        private DateTime _nextTestAutoFishingRepairAtUtc = DateTime.MaxValue;
        private DateTime _nextTestAutoFishingRepairStageAtUtc = DateTime.UtcNow;
        private bool _testAutoFishingAwaitSecondClick;
        private bool _testAutoFishingRecastAfterRepairPending;
        private DateTime _testAutoFishingRecastAfterRepairAtUtc = DateTime.UtcNow;
        private bool _testAutoFishingWaitingForCastRegistration;
        private bool _testAutoFishingBaselineReady;
        private DateTime _testAutoFishingBaselineArmedAtUtc = DateTime.UtcNow;
        private DateTime _testAutoFishingNoBobberSinceAtUtc = DateTime.MinValue;
        private DateTime _nextTestAutoFishingPreviewAtUtc = DateTime.UtcNow;
        private double _testAutoFishingLastDetectedBobberX = double.NaN;
        private double _testAutoFishingBaselineBobberY;
        private double _testAutoFishingLastDetectedBobberY = double.NaN;
        private int _testAutoFishingLastDetectedRedPixels;
        private int _testAutoFishingMissedDetections;
        private int _testAutoFishingBiteConfirmationFrames;
        private int _testAutoFishingCaughtCount;
        private DateTime _testAutoFishingLastCatchAtUtc = DateTime.MinValue;
        private DateTime _testAutoFishingRuntimeStartedAtUtc = DateTime.MinValue;
        private TestAutoFishingRepairStage _testAutoFishingRepairStage;
        private readonly Dictionary<string, bool> _bindyBindWasDownById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private BindyEntry? _bindyCaptureEntry;
        private TextBox? _bindyCaptureTextBox;
        private DateTime _nextRuntimeTileRefreshAtUtc = DateTime.UtcNow;
        private OverlayHudWindow? _overlayHud;
        private Forms.NotifyIcon? _trayIcon;
        private bool _isExitRequested;
        private bool _isMinimizedToTray;
        private bool _isTestCaptureSelectionInProgress;
        private readonly object _mouseHookLifecycleSync = new object();
        private readonly ManualResetEventSlim _mouseHookThreadReady = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _mouseHookStateChanged = new ManualResetEventSlim(false);
        private volatile bool _mouseHookRequested;
        private DateTime _nextMouseHookStartAttemptAtUtc;
        private Thread? _mouseHookThread;
        private volatile uint _mouseHookThreadId;
        private int _mouseHookStartError;
        private IntPtr _mouseHookHandle = IntPtr.Zero;
        private LowLevelMouseProc? _mouseHookProc;
        private volatile bool _physicalLeftButtonDown;
        private volatile bool _physicalRightButtonDown;
        private readonly object _f3TesseractLock = new object();
        private TesseractEngine? _f3TesseractEngine;
        private AutoReconnectStage _autoReconnectStage = AutoReconnectStage.None;
        private int _autoReconnectGeneration;
        private DateTime _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
        private DateTime _nextAutoReconnectHealthCheckAtUtc = DateTime.UtcNow;
        private bool _autoReconnectOcrInProgress;
        private bool _autoReconnectTickInProgress;
        private bool _autoReconnectManualRun;
        private bool _autoReconnectInventoryOnly;
        private bool _autoReconnectResumeKopacz533;
        private bool _autoReconnectResumeKopacz633;
        private string _autoReconnectLogMiningRunId = string.Empty;
        private string _autoReconnectLogOwner = string.Empty;
        private int _autoReconnectAttempt;
        private int _autoReconnectInventoryFailures;
        private int _autoReconnectLastCountdownSecond = -1;
        private AutoReconnectScreenKind _autoReconnectPendingScreenKind = AutoReconnectScreenKind.Unknown;
        private string _autoReconnectLastOcrText = string.Empty;
        private bool _autoReconnectReturningHomeAfterMissingPickaxe;
        private string _autoReconnectActiveHomeCommand = "/home";
        private bool _autoReconnectActiveHomeHasGui = true;
        private int _autoReconnectActiveHomeGuiDelaySeconds = 1;
        private int _autoReconnectActiveHomeGuiRows = 3;
        private int _autoReconnectActiveHomeGuiColumns = 9;
        private int _autoReconnectActiveHomeSlot = 11;
        private int _autoReconnectActiveTeleportDelaySeconds = 10;
        private string _pendingAutoReconnectProfileDeleteId = string.Empty;
        private DateTime _pendingAutoReconnectProfileDeleteUntilUtc = DateTime.MinValue;
        private const double OverlayScreenMargin = 16;
        private const int MinimumCaptureSelectionSize = 24;
        private const int TestAutoFishingScanIntervalMs = 45;
        private const int TestAutoFishingPreviewIntervalMs = 220;
        private const int TestAutoFishingSecondClickDelayMs = 95;
        private const int TestAutoFishingCastRegistrationDelayMs = 2000;
        private const int TestAutoFishingNoBobberRecastMs = 5000;
        private const int TestAutoFishingBiteArmingDelayMs = 420;
        private const int TestAutoFishingDropThresholdPx = 3;
        private const int TestAutoFishingRequiredBiteFrames = 2;
        private const int TestAutoFishingMinRedPixels = 3;
        private const int TestAutoFishingMissTolerance = 6;
        private const int TestAutoFishingLossTriggerMisses = 3;
        private const int TestAutoFishingRepairDelayAfterOpenChatMs = 180;
        private const int TestAutoFishingRepairDelayAfterTypeCommandMs = 110;
        private const int TestAutoFishingRepairRecastDelayMs = 190;
        private const int AutoReconnectTeleportSafetyBufferSeconds = 2;
        private const int TestAutoFishingRepairIntervalMaxSeconds = 3600;
        // BlazingPack can block Back for about 5 s and the next connection for
        // about 6 s. One extra second avoids clicking on the boundary.
        private const int AutoReconnectBlockedButtonWaitSeconds = 7;

        private readonly Random _random = new Random();
        private const string FastUpDefaultPickaxeType = "Diamentowy";
        private static readonly string[] FastUpPickaxeTypes =
        {
            "Kamienny",
            "Żelazny",
            "Diamentowy",
            "Diamentowy 5/3/3"
        };

        private enum BindTarget
        {
            None,
            HoldToggle,
            AutoLeft,
            AutoRight,
            Kopacz533,
            Kopacz633,
            JablkaZLisci,
            FastUpExit,
            TestCaptureArea,
            AutoArmor,
            AutoWater,
            TestAutoFishing,
            TestAutoFishingCaptureArea,
            ChatOpen,
            DropItem
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

        private enum FastUpExitStage
        {
            LookUp,
            SelectPickaxe,
            BreakBlock,
            SelectBlock,
            LookDown,
            PlaceBlock
        }

        private enum TestAutoFishingRepairStage
        {
            None,
            OpenChat,
            TypeCommand,
            SubmitCommand
        }

        private enum AutoReconnectStage
        {
            None,
            EmergencyWaitBeforeReconnect,
            EmergencyOpenInventory,
            EmergencyVerifyInventory,
            HealthOpenInventory,
            HealthVerifyInventory,
            AnalyzeScreen,
            WaitForScreenAnalysis,
            WaitForDisconnectButtonUnlock,
            WaitAfterScreenClick,
            OpenDirectConnect,
            WaitForDirectConnect,
            EnterServerAddress,
            WaitForServerJoin,
            OpenHomeChat,
            TypeHomeCommand,
            SubmitHomeCommand,
            WaitForHomeMenu,
            ClickHomeSlot,
            WaitForTeleport,
            OpenVerificationInventory,
            VerifyAfterTeleport,
            RetryDelay
        }

        private enum AutoReconnectScreenKind
        {
            Unknown,
            Inventory,
            PlayerDead,
            Disconnected,
            ReconnectChoice,
            ServerList,
            DirectConnect,
            Banned,
            AlreadyConnected
        }

        private enum Kopacz633StrafeDirection
        {
            None,
            Forward,
            Right,
            Backward,
            Left
        }

        private enum InventoryCleanupStage
        {
            None,
            ReturnToMiningStart,
            OpenInventory,
            WaitForInventory,
            MoveToSlot,
            PressDropModifier,
            PressDropKey,
            ReleaseDropKeys,
            CloseInventory,
            OpenCobbleXChat,
            TypeCobbleXCommand,
            SubmitCobbleXCommand,
            SelectFoodSlot,
            StartEating,
            StopEatingAndRestoreTool,
            ResumeMining
        }

        private enum InventoryCleanupOwner
        {
            None,
            Kopacz533,
            Kopacz633
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
        private static readonly object CursorAppearanceCacheLock = new object();
        private static readonly Dictionary<IntPtr, bool> CursorBlankAppearanceCache = new Dictionary<IntPtr, bool>();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DrawIconEx(
            IntPtr hdc,
            int xLeft,
            int yTop,
            IntPtr hIcon,
            int cxWidth,
            int cyWidth,
            uint istepIfAniCur,
            IntPtr hbrFlickerFreeDraw,
            uint diFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_XBUTTON1 = 0x05;
        private const int VK_XBUTTON2 = 0x06;
        private const int WH_MOUSE_LL = 14;
        private const uint WM_QUIT = 0x0012;
        private const uint WM_SET_MOUSE_HOOK = 0x8001;
        private const uint PM_NOREMOVE = 0x0000;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int LLMHF_INJECTED = 0x00000001;
        private const int CURSOR_SHOWING = 0x00000001;
        private const int SM_CXCURSOR = 13;
        private const int SM_CYCURSOR = 14;
        private const uint DI_NORMAL = 0x0003;
        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_A = 0x41;
        private const int VK_D = 0x44;
        private const int VK_E = 0x45;
        private const int VK_Q = 0x51;
        private const int VK_W = 0x57;
        private const int VK_S = 0x53;
        private const int VK_O = 0x4F;
        private const int VK_SPACE = 0x20;
        private const int VK_T = 0x54;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_MENU = 0x12;
        private const int VK_RETURN = 0x0D;
        private const int VK_ESCAPE = 0x1B;
        private const int FastUpVerticalStepDelta = 260;
        private const int FastUpLookDurationMinMs = 20;
        private const int FastUpLookDurationMaxMs = 600;
        private const int FastUpBreakDurationDefaultMs = 140;
        private const int FastUpBreakDurationMinMs = 20;
        private const int FastUpBreakDurationMaxMs = 1200;
        private const int FastUpPlaceAfterJumpDefaultMs = 45;
        private const int FastUpPlaceAfterJumpMinMs = 20;
        private const int FastUpPlaceAfterJumpMaxMs = 400;
        private const int FastUpJumpHoldMs = 70;
        private const int FastUpPlaceHoldMs = 120;
        private const int FastUpPlacePulseIntervalMs = 22;
        private const int FastUpSlotSwitchDelayMs = 35;
        private const int FastUpPlaceDelayMs = 130;
        private const int FastUpLookSweepTicks = 14;
        private const int FastUpLookSweepBurstsPerTick = 6;
        private const double FastUpLookDownDurationScale = 0.6;
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
        private const int InventoryCleanupMinimumIntervalSeconds = 10;
        private const int InventoryCleanupMaximumIntervalSeconds = 3600;
        private const int InventoryCleanupReturnSettleMs = 120;
        private const int InventoryCleanupOpenDelayMs = 350;
        private const int InventoryCleanupDetectionRetryMs = 140;
        private const int InventoryCleanupMaximumDetectionAttempts = 4;
        private const int InventoryCleanupMaximumDropPasses = 3;
        private const int InventoryCleanupTooltipClearDelayMs = 240;
        private const int InventoryCleanupCursorSettleMs = 55;
        private const int InventoryCleanupModifierSettleMs = 100;
        private const int InventoryCleanupDropKeyHoldMs = 100;
        private const int InventoryCleanupBetweenDropsMs = 75;
        private const int InventoryCleanupCloseDelayMs = 120;
        private const int InventoryCleanupResumeDelayMs = 180;
        private const int CobbleXDelayAfterCloseInventoryMs = 180;
        private const int CobbleXDelayAfterOpenChatMs = 180;
        private const int CobbleXDelayAfterTypeCommandMs = 110;
        private const int CobbleXDelayAfterSubmitResumeMs = 130;
        private const int InventoryCleanupFoodSlotSettleMs = 140;
        private const int InventoryCleanupEatingHoldMs = 4000;
        private const int InventoryCleanupToolSlotSettleMs = 180;
        private const int CobbleXMinimumRequiredStacks = 1;
        private const int CobbleXMaximumRequiredStacks = 27;

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

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

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
            Closing += MainWindow_Closing;
            InitializeTrayIcon();

            _settingsService = new SettingsService();
            _miningLogService = new MiningLogService();
            _isFirstRun = !_settingsService.SettingsFileExists;
            _settings = _settingsService.Load();
            EnsureSettingsConsistency();
            _currentAppVersion = ReleaseNotesCatalog.CurrentVersion;
            Title = $"Minecraft Helper {_currentAppVersion}";
            _showStartupNotice = _isFirstRun
                || !string.Equals(
                    _settings.LastAcknowledgedVersion,
                    _currentAppVersion,
                    StringComparison.OrdinalIgnoreCase);
            _macroDiagnosticsService = new MacroDiagnosticsService();
            _autoClickScheduler = new AutoClickScheduler(_macroDiagnosticsService);
            _damageSoundDetector = new DamageSoundDetector();
            _damageSoundDetector.ProgressChanged += DamageSoundDetector_ProgressChanged;
            _damageSoundDetector.DamageDetected += DamageSoundDetector_DamageDetected;
            _damageSoundDetector.CaptureFailed += DamageSoundDetector_CaptureFailed;

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
            _focusTimer.Tick += (_, __) =>
            {
                _isMinecraftFocused = CheckGameFocus();
                RefreshTestAutoFishingPreview(DateTime.UtcNow);
            };

            _macroTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(5)
            };
            _macroTimer.Tick += RunMacroTick;

            _autoReconnectTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _autoReconnectTimer.Tick += RunAutoReconnectTick;

            _transientStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _transientStatusTimer.Tick += (_, __) =>
            {
                _transientStatusTimer.Stop();
                UpdateStatusBar("Gotowy", "Green");
            };

            _bindyHudClearTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(BindyHudNotificationMs + 100)
            };
            _bindyHudClearTimer.Tick += (_, __) =>
            {
                _bindyHudClearTimer.Stop();
                RefreshOverlayHud(DateTime.UtcNow);
            };

            DataContext = this;

            _isLoadingUi = true;
            try
            {
                InitializeInventoryCleanupSlotGrid();
                InitializeInventoryCleanupItemTypeGrid();
                InitializeAutoReconnectHomeSlotGrid();
                LoadToUi();
            }
            finally
            {
                _isLoadingUi = false;
            }
            UpdateEnabledStates();
            RefreshTopTiles();
            RefreshMiningLogsSummary();

            _focusTimer.Start();
            _macroTimer.Start();
            _autoReconnectTimer.Start();
            _isMinecraftFocused = CheckGameFocus();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyDarkTitleBar();
            InitializeMouseHookThread();

            // If Windows launches the app minimized (e.g. shortcut setting),
            // do not auto-hide it to tray on startup.
            if (WindowState == WindowState.Minimized && !_isMinimizedToTray)
            {
                WindowState = WindowState.Normal;
                Activate();
            }

            if (_showStartupNotice)
            {
                await Dispatcher.InvokeAsync(
                    ShowStartupNotice,
                    DispatcherPriority.ApplicationIdle);
            }

            await CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            AppUpdateInfo? update = await UpdateCheckService.CheckForUpdateAsync(_currentAppVersion);
            if (update == null || !IsVisible || _isExitRequested)
                return;

            var dialog = new UpdateAvailableWindow(update)
            {
                Owner = this
            };
            dialog.ShowDialog();

            if (IsVisible && !_isExitRequested)
                Activate();
        }

        private void ShowStartupNotice()
        {
            if (!IsVisible || _isExitRequested)
                return;

            var dialog = new FirstRunGuideWindow(
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
                }
                catch (Exception ex)
                {
                    UpdateStatusBar("Nie udało się zapisać potwierdzenia wersji: " + ex.Message, "Red");
                }
            }

            Activate();
        }

        private void StartMouseHook()
        {
            if (GetMouseHookHandle() != IntPtr.Zero || DateTime.UtcNow < _nextMouseHookStartAttemptAtUtc)
                return;

            // Keep the message thread warm, but register the global hook only
            // before a macro sends mouse input. Idle GUI must not intercept moves.
            _nextMouseHookStartAttemptAtUtc = DateTime.UtcNow.AddSeconds(2);
            InitializeMouseHookThread();
            if (ChangeMouseHookRegistration(enabled: true))
                _nextMouseHookStartAttemptAtUtc = DateTime.MinValue;
            else
                UpdateStatusBar($"Nie udało się uruchomić obsługi fizycznego LPM/PPM (Win32: {_mouseHookStartError}).", "Red");
        }

        private bool ChangeMouseHookRegistration(bool enabled)
        {
            _mouseHookRequested = enabled;
            _mouseHookStateChanged.Reset();
            if (_mouseHookThreadId == 0
                || !PostThreadMessage(_mouseHookThreadId, WM_SET_MOUSE_HOOK, UIntPtr.Zero, IntPtr.Zero))
            {
                _mouseHookStartError = Marshal.GetLastWin32Error();
                return false;
            }

            return _mouseHookStateChanged.Wait(TimeSpan.FromSeconds(2))
                && (GetMouseHookHandle() != IntPtr.Zero) == enabled;
        }

        private void SuspendMouseHookWhenIdle()
        {
            // Keep tracking across pauses/commands inside a running macro. In
            // particular, never re-seed physical state from an injected HOLD.
            if (_holdMacroRuntimeEnabled || _autoLeftRuntimeEnabled || _autoRightRuntimeEnabled
                || _jablkaRuntimeEnabled || _kopacz533RuntimeEnabled || _kopacz633RuntimeEnabled
                || _testFastUpExitRuntimeEnabled || _testAutoFishingRuntimeEnabled
                || IsAutoArmorRunning
                || _inventoryCleanupStage != InventoryCleanupStage.None
                || _autoReconnectStage != AutoReconnectStage.None
                || _holdRightInjectedButtonDown || _inventoryCleanupEatingRightButtonDown
                || _kopacz533Holding || _kopacz633HoldingAttack
                || _testFastUpExitBreakHoldActive || _testFastUpExitPlaceHoldActive)
            {
                return;
            }

            if (_mouseHookRequested || GetMouseHookHandle() != IntPtr.Zero)
                ChangeMouseHookRegistration(enabled: false);
        }

        private void InitializeMouseHookThread()
        {
            lock (_mouseHookLifecycleSync)
            {
                if (_mouseHookThread?.IsAlive == true)
                    return;

                _mouseHookProc = MouseHookCallback;
                _mouseHookStartError = 0;
                _mouseHookThreadReady.Reset();
                _mouseHookThread = new Thread(MouseHookThreadMain)
                {
                    IsBackground = true,
                    Name = "Minecraft Helper mouse hook"
                };
                _mouseHookThread.Start();
            }

            if (!_mouseHookThreadReady.Wait(TimeSpan.FromSeconds(2)))
            {
                UpdateStatusBar("Nie udało się uruchomić wątku obsługi myszy w wymaganym czasie.", "Red");
                return;
            }
        }

        private void StopMouseHook()
        {
            _mouseHookRequested = false;
            Thread? hookThread;
            uint hookThreadId;
            lock (_mouseHookLifecycleSync)
            {
                hookThread = _mouseHookThread;
                hookThreadId = _mouseHookThreadId;
            }

            if (hookThread == null)
                return;

            if (hookThreadId != 0)
                _ = PostThreadMessage(hookThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);

            if (hookThread != Thread.CurrentThread && !hookThread.Join(TimeSpan.FromSeconds(2)))
            {
                IntPtr staleHandle = Interlocked.Exchange(ref _mouseHookHandle, IntPtr.Zero);
                if (staleHandle != IntPtr.Zero)
                    _ = UnhookWindowsHookEx(staleHandle);
            }

            lock (_mouseHookLifecycleSync)
            {
                if (_mouseHookThread == hookThread)
                    _mouseHookThread = null;
                _mouseHookThreadId = 0;
                _mouseHookProc = null;
            }
        }

        private void MouseHookThreadMain()
        {
            IntPtr hookHandle = IntPtr.Zero;
            try
            {
                _mouseHookThreadId = GetCurrentThreadId();
                // Create the thread's message queue before shutdown can post WM_QUIT.
                _ = PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);

                string moduleName = Process.GetCurrentProcess().MainModule?.ModuleName ?? string.Empty;
                IntPtr moduleHandle = string.IsNullOrWhiteSpace(moduleName)
                    ? IntPtr.Zero
                    : GetModuleHandle(moduleName);
                _macroDiagnosticsService.RecordMouseHookState(enabled: false);
                _mouseHookThreadReady.Set();
                while (true)
                {
                    int result = GetMessage(out MSG message, IntPtr.Zero, 0, 0);
                    if (result <= 0)
                        break;
                    if (message.message == WM_SET_MOUSE_HOOK)
                    {
                        if (_mouseHookRequested && hookHandle == IntPtr.Zero)
                        {
                            hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc!, moduleHandle, 0);
                            _mouseHookStartError = hookHandle == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
                            // Do this on the hook thread before processing input,
                            // and before the caller is allowed to inject a click.
                            _physicalLeftButtonDown = IsVirtualKeyDown(VK_LBUTTON);
                            _physicalRightButtonDown = IsVirtualKeyDown(VK_RBUTTON);
                            Interlocked.Exchange(ref _mouseHookHandle, hookHandle);
                            if (hookHandle != IntPtr.Zero)
                                _macroDiagnosticsService.RecordMouseHookState(enabled: true);
                        }
                        else if (!_mouseHookRequested && hookHandle != IntPtr.Zero)
                        {
                            if (UnhookWindowsHookEx(hookHandle))
                            {
                                hookHandle = IntPtr.Zero;
                                Interlocked.Exchange(ref _mouseHookHandle, IntPtr.Zero);
                                _macroDiagnosticsService.RecordMouseHookState(enabled: false);
                            }
                        }
                        _mouseHookStateChanged.Set();
                        continue;
                    }
                    _ = TranslateMessage(ref message);
                    _ = DispatchMessage(ref message);
                }
            }
            finally
            {
                _mouseHookThreadReady.Set();
                _mouseHookStateChanged.Set();
                if (hookHandle != IntPtr.Zero)
                    _ = UnhookWindowsHookEx(hookHandle);
                _ = Interlocked.CompareExchange(ref _mouseHookHandle, IntPtr.Zero, hookHandle);
                _mouseHookThreadId = 0;
            }
        }

        private IntPtr GetMouseHookHandle()
        {
            return Interlocked.CompareExchange(ref _mouseHookHandle, IntPtr.Zero, IntPtr.Zero);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = unchecked((int)(long)wParam);
                MSLLHOOKSTRUCT hookData = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                ProcessPhysicalMouseEvent(message, hookData);
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void ProcessPhysicalMouseEvent(int message, MSLLHOOKSTRUCT hookData)
        {
            if ((hookData.flags & LLMHF_INJECTED) != 0)
                return;

            if (message == WM_MOUSEMOVE)
                _macroDiagnosticsService.RecordMouseMove(hookData.time, hookData.pt.X, hookData.pt.Y);
            else if (message == WM_LBUTTONDOWN)
                _physicalLeftButtonDown = true;
            else if (message == WM_LBUTTONUP)
                _physicalLeftButtonDown = false;
            else if (message == WM_RBUTTONDOWN)
                _physicalRightButtonDown = true;
            else if (message == WM_RBUTTONUP)
                _physicalRightButtonDown = false;
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Minecraft Helper",
                Visible = true,
                Icon = TryLoadTrayIcon() ?? System.Drawing.SystemIcons.Application,
                BalloonTipTitle = "Minecraft Helper",
                BalloonTipText = "Aplikacja została zminimalizowana do zasobnika systemowego i nadal działa w tle.",
                BalloonTipIcon = Forms.ToolTipIcon.Info
            };

            var renderer = new DarkTrayMenuRenderer();
            var contextMenu = new Forms.ContextMenuStrip
            {
                BackColor = Drawing.Color.FromArgb(16, 28, 44),
                ForeColor = Drawing.Color.FromArgb(233, 244, 255),
                Renderer = renderer,
                ShowCheckMargin = false,
                ShowImageMargin = false,
                Padding = new Forms.Padding(3),
                Font = new Drawing.Font("Segoe UI", 9F)
            };

            Forms.ToolStripItem showItem = contextMenu.Items.Add("Pokaż aplikację");
            showItem.Padding = new Forms.Padding(8, 3, 8, 3);
            showItem.ForeColor = Drawing.Color.FromArgb(233, 244, 255);
            showItem.Click += (_, __) => RestoreFromTray();

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            Forms.ToolStripItem exitItem = contextMenu.Items.Add("Zakończ aplikację");
            exitItem.Padding = new Forms.Padding(8, 3, 8, 3);
            exitItem.ForeColor = Drawing.Color.FromArgb(233, 244, 255);
            exitItem.Click += (_, __) => ExitFromTray();

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (_, __) => RestoreFromTray();
            _trayIcon.BalloonTipClicked += (_, __) => RestoreFromTray();
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

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExitRequested)
                return;

            e.Cancel = true;
            MinimizeToTray(showNotification: true);
        }

        private void MinimizeToTray(bool showNotification)
        {
            if (_isMinimizedToTray)
                return;

            Hide();
            ShowInTaskbar = false;
            _isMinimizedToTray = true;

            if (showNotification && _trayIcon != null)
                _trayIcon.ShowBalloonTip(3500);
        }

        private void RestoreFromTray()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RestoreFromTray));
                return;
            }

            if (_isMinimizedToTray)
            {
                ShowInTaskbar = true;
                Show();
                _isMinimizedToTray = false;
            }

            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitFromTray()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ExitFromTray));
                return;
            }

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
            _settings.LastAcknowledgedVersion ??= string.Empty;

            _settings.MacroLeftButton ??= new MacroButton();
            _settings.MacroRightButton ??= new MacroButton();
            _settings.HoldLeftButton ??= new MacroButton();
            _settings.HoldRightButton ??= new MacroButton();
            _settings.AutoLeftButton ??= new MacroButton();
            _settings.AutoRightButton ??= new MacroButton();
            if (_settings.AutoLeftHoldBindMode)
                _settings.AutoLeftComboMode = false;
            if (_settings.AutoRightHoldBindMode)
            {
                _settings.AutoRightComboMode = false;
                if (!string.IsNullOrWhiteSpace(_settings.AutoRightButton.Key)
                    && IsSameBindKey(_settings.AutoLeftButton.Key, _settings.AutoRightButton.Key))
                {
                    // AUTO PPM in hold-bind mode requires a separate key. Clear an
                    // old shared assignment so both clickers cannot start together.
                    _settings.AutoRightButton.Key = string.Empty;
                }
            }

            // These legacy modules are intentionally hidden from the streamlined UI.
            // Force them off so settings imported from an older build cannot run invisibly.
            _settings.HoldEnabled = false;
            _settings.TestEntitiesEnabled = false;
            _settings.TestCustomCaptureEnabled = false;
            _settings.TestFastUpExitEnabled = false;

            _settings.Kopacz533Commands ??= new List<MinerCommand>();
            _settings.Kopacz633Commands ??= new List<MinerCommand>();
            _settings.InventoryCleanupSlots ??= Enumerable.Range(0, 27).ToList();
            _settings.InventoryCleanupSlots = _settings.InventoryCleanupSlots
                .Where(slot => slot >= 0 && slot < 27)
                .Distinct()
                .OrderBy(slot => slot)
                .ToList();
            HashSet<string> supportedCleanupItemTypes = InventoryCleanupItemTypes
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _settings.InventoryCleanupItemTypes ??= InventoryCleanupItemTypes.Select(item => item.Id).ToList();
            _settings.InventoryCleanupItemTypes = _settings.InventoryCleanupItemTypes
                .Where(itemId => supportedCleanupItemTypes.Contains(itemId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (_settings.InventoryCleanupIntervalSeconds <= 0)
                _settings.InventoryCleanupIntervalSeconds = 120;
            _settings.InventoryCleanupIntervalSeconds = Math.Clamp(
                _settings.InventoryCleanupIntervalSeconds,
                InventoryCleanupMinimumIntervalSeconds,
                InventoryCleanupMaximumIntervalSeconds);
            _settings.CobbleXCommand ??= "/cx";
            if (_settings.CobbleXRequiredFullStacks <= 0)
                _settings.CobbleXRequiredFullStacks = 9;
            _settings.CobbleXRequiredFullStacks = Math.Clamp(
                _settings.CobbleXRequiredFullStacks,
                CobbleXMinimumRequiredStacks,
                CobbleXMaximumRequiredStacks);
            _settings.BindyCommands ??= new List<MinerCommand>();
            _settings.BindyEntries ??= new List<BindyEntry>();
            _settings.JablkaZLisciCommand ??= string.Empty;
            _settings.TestCustomCaptureBind ??= string.Empty;
            _settings.TestFastUpExitBind ??= string.Empty;
            NormalizeAutoArmorSettings();
            NormalizeAutoWaterSettings();
            _settings.TestAutoFishingBind ??= string.Empty;
            _settings.TestAutoFishingCaptureBind ??= string.Empty;
            _settings.TestAutoFishingRepairCommand ??= string.Empty;
            _settings.EmergencyDamageSoundDeviceId ??= string.Empty;
            _settings.EmergencyDamageSoundSimilarityPercent = Math.Clamp(_settings.EmergencyDamageSoundSimilarityPercent, 70, 99);
            _settings.EmergencyDamageSoundMinimumDb = Math.Clamp(_settings.EmergencyDamageSoundMinimumDb, -70.0, -10.0);
            _settings.EmergencyDamageSoundTemplates ??= new List<List<double>>();
            _settings.EmergencyDamageSoundTemplates = _settings.EmergencyDamageSoundTemplates
                .Where(template => template != null
                    && template.Count == DamageSoundDetector.FingerprintBandCount
                    && template.All(double.IsFinite))
                .Take(24)
                .Select(template => template.ToList())
                .ToList();
            // Starsze ustawienia miały dwa osobne przełączniki. Od tej wersji
            // jeden przełącznik "Awaryjna ochrona Kopacza" steruje całym flow.
            bool emergencyProtectionEnabled = _settings.EmergencyDamageSoundEnabled
                || _settings.EmergencyReconnectEnabled;
            _settings.EmergencyDamageSoundEnabled = emergencyProtectionEnabled;
            _settings.EmergencyReconnectEnabled = emergencyProtectionEnabled;
            _settings.EmergencyReconnectDelaySeconds = Math.Clamp(
                _settings.EmergencyReconnectDelaySeconds <= 0 ? 30 : _settings.EmergencyReconnectDelaySeconds,
                1,
                600);
            _settings.AutoReconnectProfile ??= "Arivi";
            _settings.AutoReconnectServerAddress ??= string.Empty;
            _settings.AutoReconnectHomeCommand ??= "/home";
            if (!string.Equals(_settings.AutoReconnectProfile, "Standard", StringComparison.OrdinalIgnoreCase))
                _settings.AutoReconnectProfile = "Arivi";
            _settings.AutoReconnectHomeSlot = Math.Clamp(_settings.AutoReconnectHomeSlot, 1, 27);
            _settings.AutoReconnectJoinDelaySeconds = Math.Clamp(_settings.AutoReconnectJoinDelaySeconds, 2, 120);
            _settings.AutoReconnectTeleportDelaySeconds = Math.Clamp(_settings.AutoReconnectTeleportDelaySeconds, 1, 120);
            _settings.AutoReconnectWatchdogSeconds = Math.Clamp(_settings.AutoReconnectWatchdogSeconds, 15, 3600);
            _settings.AutoReconnectMaxAttempts = Math.Clamp(_settings.AutoReconnectMaxAttempts, 1, 10);
            EnsureAutoReconnectServerProfiles();
            _settings.TestFastUpExitPickaxeType ??= FastUpDefaultPickaxeType;
            _settings.TestFastUpExitLookDurationByPickaxe ??= new Dictionary<string, int>();
            _settings.TestFastUpExitBreakDurationByPickaxe ??= new Dictionary<string, int>();
            _settings.BindyKey ??= string.Empty;
            _settings.TargetProcessName ??= string.Empty;
            _settings.ChatOpenKey = NormalizeMinecraftControlKey(_settings.ChatOpenKey, "T");
            _settings.DropItemKey = NormalizeMinecraftControlKey(_settings.DropItemKey, "Q");
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
            if (_settings.TestAutoFishingCaptureX < 0)
                _settings.TestAutoFishingCaptureX = 0;
            if (_settings.TestAutoFishingCaptureY < 0)
                _settings.TestAutoFishingCaptureY = 0;
            if (_settings.TestAutoFishingCaptureWidth < 0)
                _settings.TestAutoFishingCaptureWidth = 0;
            if (_settings.TestAutoFishingCaptureHeight < 0)
                _settings.TestAutoFishingCaptureHeight = 0;
            _settings.TestAutoFishingRepairEverySeconds = Math.Clamp(_settings.TestAutoFishingRepairEverySeconds, 0, TestAutoFishingRepairIntervalMaxSeconds);
            if (_settings.TestFastUpExitBlockSlot < 1 || _settings.TestFastUpExitBlockSlot > 9)
                _settings.TestFastUpExitBlockSlot = 2;
            if (_settings.TestFastUpExitPickaxeSlot < 1 || _settings.TestFastUpExitPickaxeSlot > 9)
                _settings.TestFastUpExitPickaxeSlot = 1;

            int legacyLookDurationMs = Math.Clamp(_settings.TestFastUpExitLookDurationMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            int legacyBreakDurationMs = NormalizeFastUpBreakDurationMs(_settings.TestFastUpExitBreakDurationMs);
            var stalePickaxeDurationKeys = new List<string>();
            foreach (string pickaxeKey in _settings.TestFastUpExitLookDurationByPickaxe.Keys)
            {
                if (!IsValidFastUpPickaxeType(pickaxeKey))
                    stalePickaxeDurationKeys.Add(pickaxeKey);
            }
            for (int i = 0; i < stalePickaxeDurationKeys.Count; i++)
                _settings.TestFastUpExitLookDurationByPickaxe.Remove(stalePickaxeDurationKeys[i]);

            var stalePickaxeBreakDurationKeys = new List<string>();
            foreach (string pickaxeKey in _settings.TestFastUpExitBreakDurationByPickaxe.Keys)
            {
                if (!IsValidFastUpPickaxeType(pickaxeKey))
                    stalePickaxeBreakDurationKeys.Add(pickaxeKey);
            }
            for (int i = 0; i < stalePickaxeBreakDurationKeys.Count; i++)
                _settings.TestFastUpExitBreakDurationByPickaxe.Remove(stalePickaxeBreakDurationKeys[i]);

            for (int i = 0; i < FastUpPickaxeTypes.Length; i++)
            {
                string pickaxeType = FastUpPickaxeTypes[i];
                if (!_settings.TestFastUpExitLookDurationByPickaxe.TryGetValue(pickaxeType, out int configuredMs))
                    _settings.TestFastUpExitLookDurationByPickaxe[pickaxeType] = legacyLookDurationMs;
                else
                    _settings.TestFastUpExitLookDurationByPickaxe[pickaxeType] = Math.Clamp(configuredMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);

                if (!_settings.TestFastUpExitBreakDurationByPickaxe.TryGetValue(pickaxeType, out int configuredBreakMs))
                    _settings.TestFastUpExitBreakDurationByPickaxe[pickaxeType] = legacyBreakDurationMs;
                else
                    _settings.TestFastUpExitBreakDurationByPickaxe[pickaxeType] = NormalizeFastUpBreakDurationMs(configuredBreakMs);
            }

            _settings.TestFastUpExitPickaxeType = NormalizeFastUpPickaxeType(_settings.TestFastUpExitPickaxeType);
            if (!_settings.TestFastUpExitLookDurationByPickaxe.TryGetValue(_settings.TestFastUpExitPickaxeType, out int selectedPickaxeLookMs))
            {
                selectedPickaxeLookMs = legacyLookDurationMs;
                _settings.TestFastUpExitLookDurationByPickaxe[_settings.TestFastUpExitPickaxeType] = selectedPickaxeLookMs;
            }
            if (!_settings.TestFastUpExitBreakDurationByPickaxe.TryGetValue(_settings.TestFastUpExitPickaxeType, out int selectedPickaxeBreakMs))
            {
                selectedPickaxeBreakMs = legacyBreakDurationMs;
                _settings.TestFastUpExitBreakDurationByPickaxe[_settings.TestFastUpExitPickaxeType] = selectedPickaxeBreakMs;
            }
            _settings.TestFastUpExitLookDurationMs = Math.Clamp(selectedPickaxeLookMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            _settings.TestFastUpExitBreakDurationMs = NormalizeFastUpBreakDurationMs(selectedPickaxeBreakMs);
            _settings.TestFastUpExitPlaceAfterJumpMs = NormalizeFastUpPlaceAfterJumpMs(_settings.TestFastUpExitPlaceAfterJumpMs);
            if (IsMacroButtonEmpty(_settings.HoldLeftButton) && !IsMacroButtonEmpty(_settings.MacroLeftButton))
                CopyMacroButtonData(_settings.MacroLeftButton, _settings.HoldLeftButton);
            if (IsMacroButtonEmpty(_settings.HoldRightButton) && !IsMacroButtonEmpty(_settings.MacroRightButton))
                CopyMacroButtonData(_settings.MacroRightButton, _settings.HoldRightButton);

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

        private static bool LooksLikeLauncherWindow(ProcessTargetOption option)
        {
            return option.ProcessName.Contains("launcher", StringComparison.OrdinalIgnoreCase)
                || option.WindowTitle.Contains("launcher", StringComparison.OrdinalIgnoreCase);
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

        private bool TryResolveTargetWindow(bool allowPendingSelection, out IntPtr windowHandle)
        {
            windowHandle = IntPtr.Zero;

            int targetProcessId = _settings.TargetProcessId;
            string targetProcessName = (_settings.TargetProcessName ?? string.Empty).Trim();
            string targetWindowTitle = (_settings.TargetWindowTitle ?? string.Empty).Trim();

            if (allowPendingSelection && GetSelectedTargetProcessOption() is ProcessTargetOption selectedProcess)
            {
                targetProcessId = selectedProcess.ProcessId;
                targetProcessName = selectedProcess.ProcessName;
                targetWindowTitle = selectedProcess.WindowTitle;
            }

            if (targetProcessId <= 0
                && string.IsNullOrWhiteSpace(targetProcessName)
                && string.IsNullOrWhiteSpace(targetWindowTitle))
            {
                return false;
            }

            if (targetProcessId > 0)
            {
                try
                {
                    using Process configuredProcess = Process.GetProcessById(targetProcessId);
                    bool processNameMatches = string.IsNullOrWhiteSpace(targetProcessName)
                        || string.Equals(configuredProcess.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase);
                    if (processNameMatches && configuredProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        windowHandle = configuredProcess.MainWindowHandle;
                        return true;
                    }
                }
                catch
                {
                    // The saved PID may expire after the game is restarted; resolve by name/title below.
                }
            }

            List<ProcessTargetOption> options = GetRunningWindowProcesses();
            ProcessTargetOption? match = null;

            if (match == null && !string.IsNullOrWhiteSpace(targetProcessName) && !string.IsNullOrWhiteSpace(targetWindowTitle))
            {
                match = options.FirstOrDefault(option =>
                    string.Equals(option.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase)
                    && (option.WindowTitle.Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase)
                        || targetWindowTitle.Contains(option.WindowTitle, StringComparison.OrdinalIgnoreCase)));
            }

            if (match == null && !string.IsNullOrWhiteSpace(targetProcessName))
            {
                match = options.FirstOrDefault(option =>
                    string.Equals(option.ProcessName, targetProcessName, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null && !string.IsNullOrWhiteSpace(targetWindowTitle))
            {
                match = options.FirstOrDefault(option =>
                    option.WindowTitle.Contains(targetWindowTitle, StringComparison.OrdinalIgnoreCase)
                    || targetWindowTitle.Contains(option.WindowTitle, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null)
                return false;

            try
            {
                using Process process = Process.GetProcessById(match.ProcessId);
                windowHandle = process.MainWindowHandle;
                return windowHandle != IntPtr.Zero;
            }
            catch
            {
                windowHandle = IntPtr.Zero;
                return false;
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

            if (focusedWindow != IntPtr.Zero)
            {
                _targetGameWindowHandle = focusedWindow;
            }
            else if (!TryResolveTargetWindow(allowPendingSelection: false, out _targetGameWindowHandle))
            {
                _targetGameWindowHandle = IntPtr.Zero;
            }

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
            _autoClickScheduler.Stop();
            UpdateStatusBar("Gotowy", "Green");

            _holdMacroRuntimeEnabled = false;
            _autoLeftRuntimeEnabled = false;
            SetAutoLeftDabHold(false);
            _autoRightRuntimeEnabled = false;
            _autoLeftComboTriggerWasDown = false;
            _autoLeftComboStopWasDown = false;
            _autoRightComboTriggerWasDown = false;
            _autoRightComboStopWasDown = false;
            _jablkaRuntimeEnabled = false;
            _kopacz533RuntimeEnabled = false;
            _kopacz633RuntimeEnabled = false;
            _testFastUpExitRuntimeEnabled = false;
            _testAutoFishingRuntimeEnabled = false;
            ResetAutoWaterRuntimeState();
            ResetJablkaRuntimeState();
            ResetKopacz533RuntimeState();
            ResetKopacz633RuntimeState();
            ResetTestFastUpExitRuntimeState();
            ResetTestAutoFishingRuntimeState();
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
            ChkAutoLeftComboMode.IsChecked = _settings.AutoLeftComboMode;
            ChkAutoLeftHoldBindMode.IsChecked = _settings.AutoLeftHoldBindMode;
            ChkAutoLeftDabMode.IsChecked = _settings.AutoLeftDabMode;

            ChkAutoRightEnabled.IsChecked = _settings.AutoRightButton.Enabled;
            TxtAutoRightKey.Text = _settings.AutoRightButton.Key;
            TxtAutoRightMinCps.Text = _settings.AutoRightButton.MinCps.ToString();
            TxtAutoRightMaxCps.Text = _settings.AutoRightButton.MaxCps.ToString();
            ChkAutoRightComboMode.IsChecked = _settings.AutoRightComboMode;
            ChkAutoRightHoldBindMode.IsChecked = _settings.AutoRightHoldBindMode;

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

            ChkInventoryCleanupEnabled.IsChecked = _settings.InventoryCleanupEnabled;
            TxtInventoryCleanupIntervalSeconds.Text = _settings.InventoryCleanupIntervalSeconds.ToString(CultureInfo.InvariantCulture);
            ChkInventoryCleanupAllItemTypes.IsChecked = _settings.InventoryCleanupDiscardAllItemTypes;
            ChkInventoryCleanupEatAfterCleanup.IsChecked = _settings.InventoryCleanupEatAfterCleanup;
            ChkCobbleXEnabled.IsChecked = _settings.CobbleXEnabled;
            TxtCobbleXCommand.Text = _settings.CobbleXCommand;
            TxtCobbleXRequiredStacks.Text = _settings.CobbleXRequiredFullStacks.ToString(CultureInfo.InvariantCulture);
            LoadInventoryCleanupSlotsToUi();
            LoadInventoryCleanupItemTypesToUi();
            UpdateInventoryCleanupStatus("Gotowe. Włącz texturepack Minecraft Helper w grze.", "Default");

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
            TxtTestCustomCaptureBind.Text = _settings.TestCustomCaptureBind;
            ChkTestFastUpExitEnabled.IsChecked = _settings.TestFastUpExitEnabled;
            TxtTestFastUpExitBind.Text = _settings.TestFastUpExitBind;
            LoadAutoArmorToUi();
            LoadAutoWaterToUi();
            ChkTestAutoFishingEnabled.IsChecked = _settings.TestAutoFishingEnabled;
            TxtTestAutoFishingBind.Text = _settings.TestAutoFishingBind;
            TxtTestAutoFishingCaptureBind.Text = _settings.TestAutoFishingCaptureBind;
            TxtTestAutoFishingRepairCommand.Text = _settings.TestAutoFishingRepairCommand;
            TxtTestAutoFishingRepairEverySeconds.Text = _settings.TestAutoFishingRepairEverySeconds.ToString(CultureInfo.InvariantCulture);
            ChkEmergencyDamageSoundEnabled.IsChecked = _settings.EmergencyDamageSoundEnabled;
            ChkEmergencyDamageSoundTestMode.IsChecked = _settings.EmergencyDamageSoundTestMode;
            SlEmergencyDamageSoundSimilarity.Value = _settings.EmergencyDamageSoundSimilarityPercent;
            TxtEmergencyDamageSoundSimilarityValue.Text = $"{_settings.EmergencyDamageSoundSimilarityPercent}%";
            LoadBundledDamageSoundReferences(showStatus: false);
            RefreshEmergencyDamageSoundDevices();
            UpdateEmergencyDamageSoundReferenceInfo();
            TxtEmergencyReconnectDelaySeconds.Text = _settings.EmergencyReconnectDelaySeconds.ToString(CultureInfo.InvariantCulture);
            ChkAutoReconnectEnabled.IsChecked = _settings.AutoReconnectEnabled;
            CbAutoReconnectProfile.SelectedIndex = string.Equals(_settings.AutoReconnectProfile, "Standard", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            TxtAutoReconnectServerAddress.Text = _settings.AutoReconnectServerAddress;
            TxtAutoReconnectHomeCommand.Text = _settings.AutoReconnectHomeCommand;
            TxtAutoReconnectHomeSlot.Text = _settings.AutoReconnectHomeSlot.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectJoinDelay.Text = _settings.AutoReconnectJoinDelaySeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectTeleportDelay.Text = _settings.AutoReconnectTeleportDelaySeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectWatchdogSeconds.Text = _settings.AutoReconnectWatchdogSeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectMaxAttempts.Text = _settings.AutoReconnectMaxAttempts.ToString(CultureInfo.InvariantCulture);
            RefreshAutoReconnectHomeSlotGrid();
            RefreshAutoReconnectServerProfileCards();
            RefreshEmergencyReconnectProfileSummary();
            CbTestFastUpExitBlockSlot.SelectedIndex = Math.Clamp(_settings.TestFastUpExitBlockSlot, 1, 9) - 1;
            CbTestFastUpExitPickaxeSlot.SelectedIndex = Math.Clamp(_settings.TestFastUpExitPickaxeSlot, 1, 9) - 1;
            string selectedPickaxeType = NormalizeFastUpPickaxeType(_settings.TestFastUpExitPickaxeType);
            int selectedPickaxeIndex = Array.IndexOf(FastUpPickaxeTypes, selectedPickaxeType);
            CbTestFastUpExitPickaxeType.SelectedIndex = selectedPickaxeIndex >= 0 ? selectedPickaxeIndex : Array.IndexOf(FastUpPickaxeTypes, FastUpDefaultPickaxeType);
            if (ChkTestFastUpExitLookMsEnabled != null)
                ChkTestFastUpExitLookMsEnabled.IsChecked = _settings.TestFastUpExitLookDurationEnabled;
            if (ChkTestFastUpExitPlaceMsEnabled != null)
                ChkTestFastUpExitPlaceMsEnabled.IsChecked = _settings.TestFastUpExitPlaceAfterJumpEnabled;
            ApplyFastUpLookSliderForPickaxe(selectedPickaxeType);
            ApplyFastUpBreakSliderForPickaxe(selectedPickaxeType);
            if (SlTestFastUpExitPlaceMs != null)
                SlTestFastUpExitPlaceMs.Value = NormalizeFastUpPlaceAfterJumpMs(_settings.TestFastUpExitPlaceAfterJumpMs);
            UpdateTestFastUpExitPlaceDurationLabel(NormalizeFastUpPlaceAfterJumpMs(_settings.TestFastUpExitPlaceAfterJumpMs));
            UpdateTestCustomCaptureAreaInfo();
            UpdateTestAutoFishingAreaInfo();
            UpdateTestAutoFishingStatusLabel();

            // OVERLAY
            ChkAnimatedBackgroundEnabled.IsChecked = _settings.AnimatedBackgroundEnabled;
            CyberBackground.IsAnimationEnabled = _settings.AnimatedBackgroundEnabled;
            ChkOverlayHudEnabled.IsChecked = _settings.OverlayHudEnabled;
            ChkOverlayAnimationsEnabled.IsChecked = _settings.OverlayAnimationsEnabled;
            TxtChatOpenKey.Text = _settings.ChatOpenKey;
            TxtDropItemKey.Text = _settings.DropItemKey;
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

        private void InitializeInventoryCleanupSlotGrid()
        {
            PanelInventoryCleanupSlots.Children.Clear();
            _inventoryCleanupSlotCheckBoxes.Clear();

            for (int slot = 0; slot < 27; slot++)
            {
                var checkBox = new CheckBox
                {
                    Content = (slot + 1).ToString(CultureInfo.InvariantCulture),
                    Tag = slot,
                    Style = (Style)FindResource("SlotGridCheckBoxStyle"),
                    ToolTip = $"Slot {slot + 1} — rząd {(slot / 9) + 1}, kolumna {(slot % 9) + 1}"
                };
                checkBox.Checked += InventoryCleanupSlot_Changed;
                checkBox.Unchecked += InventoryCleanupSlot_Changed;
                _inventoryCleanupSlotCheckBoxes.Add(checkBox);
                PanelInventoryCleanupSlots.Children.Add(checkBox);
                UpdateInventoryCleanupOptionVisual(checkBox);
            }
        }

        private void InitializeInventoryCleanupItemTypeGrid()
        {
            PanelInventoryCleanupItemTypes.Children.Clear();
            _inventoryCleanupItemTypeCheckBoxes.Clear();

            foreach ((string itemId, string label) in InventoryCleanupItemTypes)
            {
                var checkBox = new CheckBox
                {
                    Content = label,
                    Tag = itemId,
                    Margin = new Thickness(7, 0, 4, 0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(216, 226, 240)),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand
                };
                var cell = new Border
                {
                    Width = 146,
                    Height = 27,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(3),
                    BorderThickness = new Thickness(1),
                    Child = checkBox
                };
                checkBox.Checked += InventoryCleanupItemType_Changed;
                checkBox.Unchecked += InventoryCleanupItemType_Changed;
                _inventoryCleanupItemTypeCheckBoxes.Add(checkBox);
                PanelInventoryCleanupItemTypes.Children.Add(cell);
                UpdateInventoryCleanupOptionVisual(checkBox);
            }
        }

        private void InitializeAutoReconnectHomeSlotGrid()
        {
            if (PanelAutoReconnectHomeSlots == null)
                return;

            PanelAutoReconnectHomeSlots.Children.Clear();
            _autoReconnectHomeSlotButtons.Clear();

            for (int slot = 1; slot <= 27; slot++)
            {
                var button = new Button
                {
                    Content = slot.ToString(CultureInfo.InvariantCulture),
                    Tag = slot,
                    Width = 36,
                    Height = 28,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    ToolTip = $"Slot {slot} — rząd {((slot - 1) / 9) + 1}, kolumna {((slot - 1) % 9) + 1}"
                };
                button.Click += AutoReconnectHomeSlot_Click;
                _autoReconnectHomeSlotButtons.Add(button);
                PanelAutoReconnectHomeSlots.Children.Add(button);
            }

            RefreshAutoReconnectHomeSlotGrid();
        }

        private void AutoReconnectHomeSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: int slot })
                return;

            slot = Math.Clamp(slot, 1, 27);
            TxtAutoReconnectHomeSlot.Text = slot.ToString(CultureInfo.InvariantCulture);
            _settings.AutoReconnectHomeSlot = slot;
            RefreshAutoReconnectHomeSlotGrid();

            int row = ((slot - 1) / 9) + 1;
            int column = ((slot - 1) % 9) + 1;
            UpdateAutoReconnectStatus($"Wybrano slot home {slot} (rząd {row}, kolumna {column}).", "Green");

            if (!_isLoadingUi)
                MarkDirty();
        }

        private void RefreshAutoReconnectHomeSlotGrid()
        {
            if (_autoReconnectHomeSlotButtons.Count == 0)
                return;

            int selectedSlot = _settings.AutoReconnectHomeSlot;
            if (TxtAutoReconnectHomeSlot != null &&
                int.TryParse(TxtAutoReconnectHomeSlot.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSlot))
            {
                selectedSlot = parsedSlot;
            }
            selectedSlot = Math.Clamp(selectedSlot, 1, 27);

            foreach (Button button in _autoReconnectHomeSlotButtons)
            {
                bool selected = button.Tag is int slot && slot == selectedSlot;
                button.Background = new SolidColorBrush(selected
                    ? Color.FromRgb(23, 50, 74)
                    : Color.FromRgb(30, 42, 57));
                button.BorderBrush = new SolidColorBrush(selected
                    ? Color.FromRgb(46, 168, 255)
                    : Color.FromRgb(62, 83, 110));
                button.Foreground = new SolidColorBrush(selected
                    ? Color.FromRgb(56, 214, 180)
                    : Color.FromRgb(216, 226, 240));
                button.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }

        private void LoadInventoryCleanupSlotsToUi()
        {
            var selectedSlots = new HashSet<int>(_settings.InventoryCleanupSlots ?? Enumerable.Range(0, 27));
            for (int slot = 0; slot < _inventoryCleanupSlotCheckBoxes.Count; slot++)
                _inventoryCleanupSlotCheckBoxes[slot].IsChecked = selectedSlots.Contains(slot);
        }

        private void LoadInventoryCleanupItemTypesToUi()
        {
            var selectedTypes = new HashSet<string>(
                _settings.InventoryCleanupItemTypes ?? InventoryCleanupItemTypes.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);
            foreach (CheckBox checkBox in _inventoryCleanupItemTypeCheckBoxes)
            {
                string itemId = checkBox.Tag?.ToString() ?? string.Empty;
                checkBox.IsChecked = selectedTypes.Contains(itemId);
            }
        }

        private HashSet<int> GetSelectedInventoryCleanupSlots()
        {
            var slots = new HashSet<int>();
            for (int slot = 0; slot < _inventoryCleanupSlotCheckBoxes.Count; slot++)
            {
                if (_inventoryCleanupSlotCheckBoxes[slot].IsChecked == true)
                    slots.Add(slot);
            }
            return slots;
        }

        private HashSet<string> GetSelectedInventoryCleanupItemTypes()
        {
            var itemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CheckBox checkBox in _inventoryCleanupItemTypeCheckBoxes)
            {
                if (checkBox.IsChecked == true && checkBox.Tag is string itemId)
                    itemTypes.Add(itemId);
            }
            return itemTypes;
        }

        private static string GetInventoryCleanupItemLabel(string itemId)
        {
            if (string.Equals(itemId, "other", StringComparison.OrdinalIgnoreCase))
                return "Pozostałe przedmioty";

            foreach ((string id, string label) in InventoryCleanupItemTypes)
            {
                if (string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase))
                    return label;
            }

            return itemId;
        }

        private int GetConfiguredInventoryCleanupIntervalSeconds()
        {
            int parsed = ParseNonNegativeInt(TxtInventoryCleanupIntervalSeconds.Text);
            if (parsed <= 0)
                parsed = 120;
            return Math.Clamp(parsed, InventoryCleanupMinimumIntervalSeconds, InventoryCleanupMaximumIntervalSeconds);
        }

        private int GetConfiguredCobbleXRequiredStacks()
        {
            int parsed = ParseNonNegativeInt(TxtCobbleXRequiredStacks.Text);
            if (parsed <= 0)
                parsed = 9;
            return Math.Clamp(parsed, CobbleXMinimumRequiredStacks, CobbleXMaximumRequiredStacks);
        }

        private string GetConfiguredCobbleXCommand()
        {
            return TxtCobbleXCommand.Text.Trim();
        }

        private void UpdateInventoryCleanupStatus(string message, string colorName)
        {
            if (TxtInventoryCleanupStatus == null)
                return;

            TxtInventoryCleanupStatus.Text = message;
            TxtInventoryCleanupStatus.Foreground = colorName == "Red"
                ? new SolidColorBrush(Color.FromRgb(255, 107, 107))
                : colorName == "Green"
                    ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                    : colorName == "Orange"
                        ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                        : new SolidColorBrush(Color.FromRgb(216, 226, 240));
        }

        private void InventoryCleanupSlot_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
                UpdateInventoryCleanupOptionVisual(checkBox);

            if (!_isLoadingUi)
                MarkDirty();
        }

        private void InventoryCleanupItemType_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
                UpdateInventoryCleanupOptionVisual(checkBox);

            if (!_isLoadingUi)
            {
                RefreshTopTiles();
                MarkDirty();
            }
        }

        private void ChkInventoryCleanupAllItemTypes_Changed(object sender, RoutedEventArgs e)
        {
            RefreshInventoryCleanupItemTypeMode();
            if (!_isLoadingUi)
            {
                RefreshTopTiles();
                MarkDirty();
            }
        }

        private void RefreshInventoryCleanupItemTypeMode()
        {
            bool customSelectionEnabled = ChkInventoryCleanupAllItemTypes?.IsChecked != true;
            if (PanelInventoryCleanupItemTypes != null)
            {
                PanelInventoryCleanupItemTypes.IsEnabled = customSelectionEnabled;
                PanelInventoryCleanupItemTypes.Opacity = customSelectionEnabled ? 1.0 : 0.72;
            }
            if (BtnInventoryCleanupSelectAllItemTypes != null)
                BtnInventoryCleanupSelectAllItemTypes.IsEnabled = customSelectionEnabled;
            if (BtnInventoryCleanupClearItemTypes != null)
                BtnInventoryCleanupClearItemTypes.IsEnabled = customSelectionEnabled;
        }

        private static void UpdateInventoryCleanupOptionVisual(CheckBox checkBox)
        {
            if (checkBox.Tag is int)
            {
                bool slotSelected = checkBox.IsChecked == true;
                checkBox.Background = new SolidColorBrush(slotSelected
                    ? Color.FromRgb(23, 50, 74)
                    : Color.FromRgb(30, 42, 57));
                checkBox.BorderBrush = new SolidColorBrush(slotSelected
                    ? Color.FromRgb(46, 168, 255)
                    : Color.FromRgb(62, 83, 110));
                checkBox.Foreground = new SolidColorBrush(slotSelected
                    ? Color.FromRgb(56, 214, 180)
                    : Color.FromRgb(216, 226, 240));
                checkBox.BorderThickness = new Thickness(slotSelected ? 2 : 1);
                checkBox.Opacity = 1.0;
                return;
            }

            if (checkBox.Parent is not Border cell)
                return;

            bool selected = checkBox.IsChecked == true;
            cell.Background = new SolidColorBrush(selected
                ? Color.FromRgb(23, 50, 74)
                : Color.FromRgb(17, 26, 37));
            cell.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(46, 168, 255)
                : Color.FromRgb(52, 70, 94));
            cell.Opacity = selected ? 1.0 : 0.72;
        }

        private void BtnInventoryCleanupSelectAll_Click(object sender, RoutedEventArgs e)
        {
            for (int slot = 0; slot < _inventoryCleanupSlotCheckBoxes.Count; slot++)
                _inventoryCleanupSlotCheckBoxes[slot].IsChecked = true;
            MarkDirty();
        }

        private void BtnInventoryCleanupClearSlots_Click(object sender, RoutedEventArgs e)
        {
            for (int slot = 0; slot < _inventoryCleanupSlotCheckBoxes.Count; slot++)
                _inventoryCleanupSlotCheckBoxes[slot].IsChecked = false;
            MarkDirty();
        }

        private void BtnInventoryCleanupSelectAllItemTypes_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox checkBox in _inventoryCleanupItemTypeCheckBoxes)
                checkBox.IsChecked = true;
            MarkDirty();
        }

        private void BtnInventoryCleanupClearItemTypes_Click(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox checkBox in _inventoryCleanupItemTypeCheckBoxes)
                checkBox.IsChecked = false;
            MarkDirty();
        }

        private void BtnInventoryCleanupTest_Click(object sender, RoutedEventArgs e)
        {
            HashSet<int> enabledSlots = GetSelectedInventoryCleanupSlots();
            HashSet<string> enabledItemTypes = GetSelectedInventoryCleanupItemTypes();
            bool discardEverythingExceptCobblestone = ChkInventoryCleanupAllItemTypes.IsChecked == true;
            bool cobbleXEnabled = ChkCobbleXEnabled.IsChecked == true;
            if (!cobbleXEnabled && enabledSlots.Count == 0)
            {
                UpdateInventoryCleanupStatus("Test: zaznacz przynajmniej jeden slot albo włącz CobbleX.", "Orange");
                return;
            }
            if (!cobbleXEnabled && !discardEverythingExceptCobblestone && enabledItemTypes.Count == 0)
            {
                UpdateInventoryCleanupStatus("Test: zaznacz przynajmniej jeden typ przedmiotu albo włącz CobbleX.", "Orange");
                return;
            }

            if (!TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out _))
            {
                UpdateInventoryCleanupStatus("Test: nie udało się przechwycić okna gry.", "Red");
                return;
            }

            using (Drawing.Bitmap capturedBitmap = bitmap!)
            {
                if (!InventoryMarkerDetector.TryDetect(capturedBitmap, enabledSlots, enabledItemTypes, out InventoryMarkerDetection detection))
                {
                    UpdateInventoryCleanupStatus("Test: nie znaleziono znaczników GUI. Otwórz ekwipunek, włącz przygotowany texturepack i pozostaw grę widoczną.", "Orange");
                    return;
                }

                if (discardEverythingExceptCobblestone && !detection.SupportsFullInventoryScan)
                {
                    UpdateInventoryCleanupStatus("Test: tryb 'Wyrzucaj wszystko' wymaga widocznej siatki EQ i aktualnych znaczników GUI z paczki Minecraft Helper.", "Orange");
                    return;
                }

                if (!discardEverythingExceptCobblestone && detection.UnknownMarkerSlots.Count > 0)
                {
                    UpdateInventoryCleanupStatus("Test: wykryto starą wersję znaczników. Włącz nowy texturepack z rozpoznawaniem typów przedmiotów.", "Orange");
                    return;
                }

                IReadOnlyList<DetectedInventoryItem> detectedItems = discardEverythingExceptCobblestone
                    ? detection.AllNonCobblestoneItems
                    : detection.Items;
                string items = detectedItems.Count == 0
                    ? "brak wybranych przedmiotów"
                    : string.Join(", ", detectedItems.Select(item => $"{item.Slot + 1} ({GetInventoryCleanupItemLabel(item.ItemId)} x{item.Quantity})"));
                int requiredCobbleStacks = GetConfiguredCobbleXRequiredStacks();
                string cobbleResult = cobbleXEnabled
                    ? $" Cobble 64: {detection.FullCobblestoneSlots.Count}/{requiredCobbleStacks}."
                    : string.Empty;
                UpdateInventoryCleanupStatus($"Test OK (GUI x{detection.Layout.Scale}). Do wyrzucenia: {items}.{cobbleResult}", "Green");
            }
        }

        private bool TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out Drawing.Rectangle clientArea)
        {
            bitmap = null;
            clientArea = Drawing.Rectangle.Empty;
            if (_targetGameWindowHandle == IntPtr.Zero || !TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
                return false;

            int width = clientRect.Right - clientRect.Left;
            int height = clientRect.Bottom - clientRect.Top;
            if (width < 320 || height < 240)
                return false;

            clientArea = new Drawing.Rectangle(clientRect.Left, clientRect.Top, width, height);
            try
            {
                bitmap = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format32bppArgb);
                using Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(clientArea.Left, clientArea.Top, 0, 0, clientArea.Size, Drawing.CopyPixelOperation.SourceCopy);
                return true;
            }
            catch
            {
                bitmap?.Dispose();
                bitmap = null;
                clientArea = Drawing.Rectangle.Empty;
                return false;
            }
        }

        private void EnsureAutoReconnectServerProfiles()
        {
            _settings.AutoReconnectServerProfiles ??= new List<AutoReconnectServerProfile>();
            if (_settings.AutoReconnectServerProfiles.Count == 0)
            {
                bool legacyHasGui = !string.Equals(_settings.AutoReconnectProfile, "Standard", StringComparison.OrdinalIgnoreCase);
                string legacyName = _settings.AutoReconnectServerAddress.Contains("arivi", StringComparison.OrdinalIgnoreCase)
                    ? "Arivi"
                    : "Domyślny serwer";
                _settings.AutoReconnectServerProfiles.Add(new AutoReconnectServerProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = legacyName,
                    ServerAddress = _settings.AutoReconnectServerAddress,
                    HomeCommand = NormalizeChatCommand(_settings.AutoReconnectHomeCommand),
                    HomeHasGui = legacyHasGui,
                    HomeGuiDelaySeconds = 1,
                    HomeGuiRows = 3,
                    HomeGuiColumns = 9,
                    HomeGuiSlot = Math.Clamp(_settings.AutoReconnectHomeSlot, 1, 27),
                    JoinDelaySeconds = Math.Clamp(_settings.AutoReconnectJoinDelaySeconds, 2, 120),
                    TeleportDelaySeconds = Math.Clamp(_settings.AutoReconnectTeleportDelaySeconds, 1, 120),
                    WatchdogSeconds = Math.Clamp(_settings.AutoReconnectWatchdogSeconds, 15, 3600),
                    MaxAttempts = Math.Clamp(_settings.AutoReconnectMaxAttempts, 1, 10),
                    MissingPickaxeRecoveryEnabled = _settings.AutoReconnectMissingPickaxeRecoveryEnabled
                });
            }

            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (AutoReconnectServerProfile profile in _settings.AutoReconnectServerProfiles)
            {
                profile.Id = string.IsNullOrWhiteSpace(profile.Id) || !usedIds.Add(profile.Id)
                    ? Guid.NewGuid().ToString("N")
                    : profile.Id.Trim();
                usedIds.Add(profile.Id);
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Serwer" : profile.Name.Trim();
                profile.ServerAddress = (profile.ServerAddress ?? string.Empty).Trim();
                NormalizeAutoReconnectHomeSettings(profile);
                profile.JoinDelaySeconds = Math.Clamp(profile.JoinDelaySeconds, 2, 120);
                profile.TeleportDelaySeconds = Math.Clamp(profile.TeleportDelaySeconds, 1, 120);
                profile.WatchdogSeconds = Math.Clamp(profile.WatchdogSeconds, 15, 3600);
                profile.MaxAttempts = Math.Clamp(profile.MaxAttempts, 1, 10);
            }

            AutoReconnectServerProfile? selected = _settings.AutoReconnectServerProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, _settings.AutoReconnectSelectedServerProfileId, StringComparison.OrdinalIgnoreCase));
            selected ??= _settings.AutoReconnectServerProfiles[0];
            _settings.AutoReconnectSelectedServerProfileId = selected.Id;
            ApplyAutoReconnectServerProfile(selected, updateUi: false);
        }

        private AutoReconnectServerProfile? GetSelectedAutoReconnectServerProfile()
        {
            return _settings.AutoReconnectServerProfiles?.FirstOrDefault(profile =>
                string.Equals(profile.Id, _settings.AutoReconnectSelectedServerProfileId, StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyAutoReconnectServerProfile(AutoReconnectServerProfile profile, bool updateUi)
        {
            NormalizeAutoReconnectHomeSettings(profile);
            _settings.AutoReconnectSelectedServerProfileId = profile.Id;
            _settings.AutoReconnectProfile = profile.HomeHasGui ? "Arivi" : "Standard";
            _settings.AutoReconnectServerAddress = profile.ServerAddress.Trim();
            _settings.AutoReconnectHomeCommand = NormalizeChatCommand(profile.HomeCommand);
            _settings.AutoReconnectHomeHasGui = profile.HomeHasGui;
            _settings.AutoReconnectHomeGuiDelaySeconds = Math.Clamp(profile.HomeGuiDelaySeconds, 0, 120);
            _settings.AutoReconnectHomeGuiRows = profile.HomeGuiRows;
            _settings.AutoReconnectHomeGuiColumns = profile.HomeGuiColumns;
            _settings.AutoReconnectHomeSlot = profile.HomeGuiSlot;
            _settings.AutoReconnectJoinDelaySeconds = Math.Clamp(profile.JoinDelaySeconds, 2, 120);
            _settings.AutoReconnectTeleportDelaySeconds = Math.Clamp(profile.TeleportDelaySeconds, 1, 120);
            _settings.AutoReconnectWatchdogSeconds = Math.Clamp(profile.WatchdogSeconds, 15, 3600);
            _settings.AutoReconnectMaxAttempts = Math.Clamp(profile.MaxAttempts, 1, 10);
            _settings.AutoReconnectMissingPickaxeRecoveryEnabled = profile.MissingPickaxeRecoveryEnabled;

            if (!updateUi)
                return;

            CbAutoReconnectProfile.SelectedIndex = profile.HomeHasGui ? 0 : 1;
            TxtAutoReconnectServerAddress.Text = _settings.AutoReconnectServerAddress;
            TxtAutoReconnectHomeCommand.Text = _settings.AutoReconnectHomeCommand;
            TxtAutoReconnectHomeSlot.Text = _settings.AutoReconnectHomeSlot.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectJoinDelay.Text = _settings.AutoReconnectJoinDelaySeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectTeleportDelay.Text = _settings.AutoReconnectTeleportDelaySeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectWatchdogSeconds.Text = _settings.AutoReconnectWatchdogSeconds.ToString(CultureInfo.InvariantCulture);
            TxtAutoReconnectMaxAttempts.Text = _settings.AutoReconnectMaxAttempts.ToString(CultureInfo.InvariantCulture);
            RefreshAutoReconnectHomeSlotGrid();
        }

        private void RefreshAutoReconnectServerProfileCards()
        {
            if (PanelAutoReconnectServerProfiles == null)
                return;

            PanelAutoReconnectServerProfiles.Children.Clear();
            _autoReconnectServerProfileButtons.Clear();
            foreach (AutoReconnectServerProfile profile in _settings.AutoReconnectServerProfiles)
            {
                bool selected = string.Equals(profile.Id, _settings.AutoReconnectSelectedServerProfileId, StringComparison.OrdinalIgnoreCase);
                var content = new StackPanel { Margin = new Thickness(2) };
                content.Children.Add(new TextBlock
                {
                    Text = profile.Name,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(selected ? Color.FromRgb(56, 214, 180) : Color.FromRgb(216, 226, 240)),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                content.Children.Add(new TextBlock
                {
                    Text = profile.ServerAddress,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193)),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                content.Children.Add(new TextBlock
                {
                    Text = profile.HomeHasGui ? $"GUI {profile.HomeGuiRows}×{profile.HomeGuiColumns} • slot {profile.HomeGuiSlot}" : "Komenda bez GUI",
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255)),
                    Margin = new Thickness(0, 2, 0, 0)
                });

                var button = new Button
                {
                    Content = content,
                    Tag = profile.Id,
                    Width = 176,
                    Height = 70,
                    Margin = new Thickness(0, 0, 7, 7),
                    Padding = new Thickness(7, 5, 7, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(selected ? Color.FromRgb(23, 50, 74) : Color.FromRgb(30, 42, 57)),
                    BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(46, 168, 255) : Color.FromRgb(62, 83, 110)),
                    BorderThickness = new Thickness(selected ? 2 : 1)
                };
                button.Click += AutoReconnectServerProfile_Click;
                _autoReconnectServerProfileButtons.Add(button);
                PanelAutoReconnectServerProfiles.Children.Add(button);
            }

            RefreshAutoReconnectSelectedProfileSummary();
            RefreshEmergencyReconnectProfileSummary();
        }

        private void RefreshAutoReconnectSelectedProfileSummary()
        {
            AutoReconnectServerProfile? profile = GetSelectedAutoReconnectServerProfile();
            if (profile == null || TxtAutoReconnectSelectedProfileName == null)
                return;

            TxtAutoReconnectSelectedProfileName.Text = profile.Name;
            TxtAutoReconnectSelectedProfileAddress.Text = profile.ServerAddress;
            TxtAutoReconnectSelectedProfileHome.Text = profile.HomeHasGui
                ? $"{profile.HomeCommand} → GUI {profile.HomeGuiRows}×{profile.HomeGuiColumns}, slot {profile.HomeGuiSlot}, oczekiwanie {profile.HomeGuiDelaySeconds} s"
                : $"{profile.HomeCommand} → bez GUI";
            string missingPickaxeHome = profile.MissingPickaxeHomeHasGui == true
                ? $"{profile.MissingPickaxeHomeCommand} → GUI {profile.MissingPickaxeHomeGuiRows}×{profile.MissingPickaxeHomeGuiColumns}, slot {profile.MissingPickaxeHomeGuiSlot}"
                : $"{profile.MissingPickaxeHomeCommand} → bez GUI";
            TxtAutoReconnectSelectedProfileTiming.Text =
                $"dołączenie {profile.JoinDelaySeconds} s • teleport {profile.TeleportDelaySeconds} s + {AutoReconnectTeleportSafetyBufferSeconds} s buforu • próby {profile.MaxAttempts}" +
                (profile.MissingPickaxeRecoveryEnabled
                    ? $" • brak kilofa: {missingPickaxeHome}"
                    : " • kontrola kilofa wyłączona");
        }

        private void AutoReconnectServerProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string profileId })
                return;
            AutoReconnectServerProfile? profile = _settings.AutoReconnectServerProfiles.FirstOrDefault(item =>
                string.Equals(item.Id, profileId, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                return;

            ApplyAutoReconnectServerProfile(profile, updateUi: true);
            RefreshAutoReconnectServerProfileCards();
            _nextAutoReconnectHealthCheckAtUtc = DateTime.UtcNow.AddSeconds(profile.WatchdogSeconds);
            UpdateAutoReconnectStatus($"Wybrano profil serwera: {profile.Name}.", "Green");
            if (!_isLoadingUi)
                MarkDirty();
        }

        private void BtnAutoReconnectAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AutoReconnectProfileWindow(null) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.ResultProfile == null)
                return;

            _settings.AutoReconnectServerProfiles.Add(dialog.ResultProfile);
            ApplyAutoReconnectServerProfile(dialog.ResultProfile, updateUi: true);
            RefreshAutoReconnectServerProfileCards();
            UpdateAutoReconnectStatus($"Dodano profil serwera: {dialog.ResultProfile.Name}.", "Green");
            MarkDirty();
        }

        private void BtnAutoReconnectEditProfile_Click(object sender, RoutedEventArgs e)
        {
            AutoReconnectServerProfile? selected = GetSelectedAutoReconnectServerProfile();
            if (selected == null)
                return;

            var dialog = new AutoReconnectProfileWindow(selected) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.ResultProfile == null)
                return;

            int index = _settings.AutoReconnectServerProfiles.FindIndex(profile =>
                string.Equals(profile.Id, selected.Id, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                _settings.AutoReconnectServerProfiles[index] = dialog.ResultProfile;
            ApplyAutoReconnectServerProfile(dialog.ResultProfile, updateUi: true);
            RefreshAutoReconnectServerProfileCards();
            UpdateAutoReconnectStatus($"Zapisano profil serwera: {dialog.ResultProfile.Name}.", "Green");
            MarkDirty();
        }

        private void BtnAutoReconnectDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            AutoReconnectServerProfile? selected = GetSelectedAutoReconnectServerProfile();
            if (selected == null)
                return;
            if (_settings.AutoReconnectServerProfiles.Count <= 1)
            {
                UpdateAutoReconnectStatus("Musi pozostać przynajmniej jeden profil serwera.", "Orange");
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (!string.Equals(_pendingAutoReconnectProfileDeleteId, selected.Id, StringComparison.OrdinalIgnoreCase)
                || now > _pendingAutoReconnectProfileDeleteUntilUtc)
            {
                _pendingAutoReconnectProfileDeleteId = selected.Id;
                _pendingAutoReconnectProfileDeleteUntilUtc = now.AddSeconds(5);
                UpdateAutoReconnectStatus($"Kliknij Usuń ponownie w ciągu 5 s, aby usunąć profil {selected.Name}.", "Orange");
                return;
            }

            _settings.AutoReconnectServerProfiles.Remove(selected);
            AutoReconnectServerProfile next = _settings.AutoReconnectServerProfiles[0];
            ApplyAutoReconnectServerProfile(next, updateUi: true);
            RefreshAutoReconnectServerProfileCards();
            _pendingAutoReconnectProfileDeleteId = string.Empty;
            UpdateAutoReconnectStatus($"Usunięto profil: {selected.Name}.", "Green");
            MarkDirty();
        }

        private string GetSelectedAutoReconnectProfile()
        {
            return CbAutoReconnectProfile?.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && string.Equals(tag, "Standard", StringComparison.OrdinalIgnoreCase)
                    ? "Standard"
                    : "Arivi";
        }

        private static string NormalizeChatCommand(string? command)
        {
            string normalized = (command ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "/home";
            return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : "/" + normalized;
        }

        private static void NormalizeAutoReconnectHomeSettings(AutoReconnectServerProfile profile)
        {
            profile.HomeCommand = NormalizeChatCommand(profile.HomeCommand);
            profile.HomeGuiDelaySeconds = Math.Clamp(profile.HomeGuiDelaySeconds, 0, 120);
            profile.HomeGuiRows = Math.Clamp(profile.HomeGuiRows, 1, 6);
            profile.HomeGuiColumns = Math.Clamp(profile.HomeGuiColumns, 1, 9);
            profile.HomeGuiSlot = Math.Clamp(profile.HomeGuiSlot, 1, profile.HomeGuiRows * profile.HomeGuiColumns);

            profile.MissingPickaxeHomeCommand = NormalizeChatCommand(
                string.IsNullOrWhiteSpace(profile.MissingPickaxeHomeCommand)
                    ? profile.HomeCommand
                    : profile.MissingPickaxeHomeCommand);
            profile.MissingPickaxeHomeHasGui ??= profile.HomeHasGui;
            profile.MissingPickaxeHomeGuiDelaySeconds = profile.MissingPickaxeHomeGuiDelaySeconds < 0
                ? profile.HomeGuiDelaySeconds
                : Math.Clamp(profile.MissingPickaxeHomeGuiDelaySeconds, 0, 120);
            profile.MissingPickaxeHomeGuiRows = profile.MissingPickaxeHomeGuiRows <= 0
                ? profile.HomeGuiRows
                : Math.Clamp(profile.MissingPickaxeHomeGuiRows, 1, 6);
            profile.MissingPickaxeHomeGuiColumns = profile.MissingPickaxeHomeGuiColumns <= 0
                ? profile.HomeGuiColumns
                : Math.Clamp(profile.MissingPickaxeHomeGuiColumns, 1, 9);
            int missingPickaxeSlotCount = profile.MissingPickaxeHomeGuiRows * profile.MissingPickaxeHomeGuiColumns;
            profile.MissingPickaxeHomeGuiSlot = profile.MissingPickaxeHomeGuiSlot <= 0
                ? Math.Clamp(profile.HomeGuiSlot, 1, missingPickaxeSlotCount)
                : Math.Clamp(profile.MissingPickaxeHomeGuiSlot, 1, missingPickaxeSlotCount);
        }

        private void ReadAutoReconnectSettingsFromUi()
        {
            AutoReconnectServerProfile? selected = GetSelectedAutoReconnectServerProfile();
            if (selected != null)
                ApplyAutoReconnectServerProfile(selected, updateUi: false);
        }

        private static int CalculateAutoReconnectTeleportWaitSeconds(int configuredSeconds)
        {
            return Math.Clamp(configuredSeconds, 1, 120) + AutoReconnectTeleportSafetyBufferSeconds;
        }

        private void ConfigureActiveAutoReconnectHome(bool missingPickaxeRecovery)
        {
            AutoReconnectServerProfile? profile = GetSelectedAutoReconnectServerProfile();
            if (profile == null)
            {
                _autoReconnectActiveHomeCommand = NormalizeChatCommand(_settings.AutoReconnectHomeCommand);
                _autoReconnectActiveHomeHasGui = _settings.AutoReconnectHomeHasGui;
                _autoReconnectActiveHomeGuiDelaySeconds = Math.Clamp(_settings.AutoReconnectHomeGuiDelaySeconds, 0, 120);
                _autoReconnectActiveHomeGuiRows = Math.Clamp(_settings.AutoReconnectHomeGuiRows, 1, 6);
                _autoReconnectActiveHomeGuiColumns = Math.Clamp(_settings.AutoReconnectHomeGuiColumns, 1, 9);
                _autoReconnectActiveHomeSlot = Math.Clamp(
                    _settings.AutoReconnectHomeSlot,
                    1,
                    _autoReconnectActiveHomeGuiRows * _autoReconnectActiveHomeGuiColumns);
                _autoReconnectActiveTeleportDelaySeconds = Math.Clamp(
                    _settings.AutoReconnectTeleportDelaySeconds,
                    1,
                    120);
                return;
            }

            NormalizeAutoReconnectHomeSettings(profile);
            if (missingPickaxeRecovery)
            {
                _autoReconnectActiveHomeCommand = profile.MissingPickaxeHomeCommand;
                _autoReconnectActiveHomeHasGui = profile.MissingPickaxeHomeHasGui == true;
                _autoReconnectActiveHomeGuiDelaySeconds = profile.MissingPickaxeHomeGuiDelaySeconds;
                _autoReconnectActiveHomeGuiRows = profile.MissingPickaxeHomeGuiRows;
                _autoReconnectActiveHomeGuiColumns = profile.MissingPickaxeHomeGuiColumns;
                _autoReconnectActiveHomeSlot = profile.MissingPickaxeHomeGuiSlot;
                _autoReconnectActiveTeleportDelaySeconds = Math.Clamp(profile.TeleportDelaySeconds, 1, 120);
                return;
            }

            _autoReconnectActiveHomeCommand = profile.HomeCommand;
            _autoReconnectActiveHomeHasGui = profile.HomeHasGui;
            _autoReconnectActiveHomeGuiDelaySeconds = profile.HomeGuiDelaySeconds;
            _autoReconnectActiveHomeGuiRows = profile.HomeGuiRows;
            _autoReconnectActiveHomeGuiColumns = profile.HomeGuiColumns;
            _autoReconnectActiveHomeSlot = profile.HomeGuiSlot;
            _autoReconnectActiveTeleportDelaySeconds = Math.Clamp(profile.TeleportDelaySeconds, 1, 120);
        }

        private void UpdateAutoReconnectStatus(string message, string colorName = "Default")
        {
            if (TxtAutoReconnectStatus != null)
            {
                TxtAutoReconnectStatus.Text = message;
                TxtAutoReconnectStatus.Foreground = colorName == "Red"
                    ? new SolidColorBrush(Color.FromRgb(255, 107, 107))
                    : colorName == "Green"
                        ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                        : colorName == "Orange"
                            ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                            : new SolidColorBrush(Color.FromRgb(146, 166, 193));
            }

            if (_emergencyReconnectActive)
                UpdateEmergencyReconnectStatus(message, colorName);

            UpdateStatusBar(message, colorName);
        }

        private bool CanRunAutoReconnectTest(out string error)
        {
            error = string.Empty;
            if (_inventoryCleanupStage != InventoryCleanupStage.None)
            {
                error = "Poczekaj na zakończenie bieżącego Auto EQ przed uruchomieniem testu reconnectu.";
                return false;
            }
            if (_targetGameWindowHandle == IntPtr.Zero)
            {
                error = "Auto reconnect: najpierw wybierz i zapisz proces Minecrafta.";
                return false;
            }
            return true;
        }

        private void CaptureMiningModeForAutoReconnect()
        {
            _autoReconnectResumeKopacz533 = _kopacz533RuntimeEnabled;
            _autoReconnectResumeKopacz633 = _kopacz633RuntimeEnabled;
            if (_autoReconnectResumeKopacz533)
            {
                _autoReconnectLogMiningRunId = _kopacz533MiningRunId;
                _autoReconnectLogOwner = GetInventoryCleanupOwnerLabel(InventoryCleanupOwner.Kopacz533);
            }
            else if (_autoReconnectResumeKopacz633)
            {
                _autoReconnectLogMiningRunId = _kopacz633MiningRunId;
                _autoReconnectLogOwner = GetInventoryCleanupOwnerLabel(InventoryCleanupOwner.Kopacz633);
            }
            else
            {
                _autoReconnectLogMiningRunId = string.Empty;
                _autoReconnectLogOwner = "Auto reconnect";
            }
        }

        private void PauseMiningForAutoReconnect()
        {
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            _autoClickScheduler.Stop();
        }

        private void BeginEmergencyReconnectAfterDisconnect(DateTime now)
        {
            AutoReconnectServerProfile? profile = GetSelectedAutoReconnectServerProfile();
            if (profile == null)
            {
                ClearEmergencyReconnectRuntimeState(stopSoundMonitoring: true);
                UpdateEmergencyReconnectStatus("Nie uruchomiono reconnectu: brak wybranego profilu serwera.", "Red");
                return;
            }

            ReadAutoReconnectSettingsFromUi();
            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: false);
            _emergencyReconnectActive = true;
            _emergencyReconnectSoundGuardActive = true;
            _emergencyReconnectShutdownAfterHome = false;
            _emergencyReconnectInventoryAttempts = 0;
            _autoReconnectManualRun = false;
            _autoReconnectInventoryOnly = false;
            _autoReconnectAttempt = 0;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectReturningHomeAfterMissingPickaxe = false;
            _autoReconnectResumeKopacz533 = _emergencyReconnectResumeKopacz533;
            _autoReconnectResumeKopacz633 = _emergencyReconnectResumeKopacz633;
            _autoReconnectLogOwner = _emergencyReconnectResumeKopacz533
                ? GetInventoryCleanupOwnerLabel(InventoryCleanupOwner.Kopacz533)
                : _emergencyReconnectResumeKopacz633
                    ? GetInventoryCleanupOwnerLabel(InventoryCleanupOwner.Kopacz633)
                    : "Awaryjny reconnect";
            _autoReconnectStage = AutoReconnectStage.EmergencyWaitBeforeReconnect;
            int delaySeconds = Math.Clamp(_settings.EmergencyReconnectDelaySeconds, 1, 600);
            _nextAutoReconnectActionAtUtc = now.AddSeconds(delaySeconds);
            _autoReconnectLastCountdownSecond = -1;
            RecordAutomationLogEvent(
                MiningLogEventTypes.EmergencyProtectionStarted,
                MiningLogStatuses.Completed,
                $"Alarm obrażeń: wyjście z serwera zakończone. Awaryjny reconnect za {delaySeconds} s; profil: {profile.Name}.");
            UpdateEmergencyReconnectStatus(
                $"Rozłączono. Reconnect za {delaySeconds} s. Nasłuch obrażeń pozostaje aktywny do kontroli kilofa.",
                "Orange");
            UpdateEnabledStates();
        }

        private void ResetAutoReconnectStageForEmergencyRedetection()
        {
            _autoReconnectGeneration++;
            _autoReconnectStage = AutoReconnectStage.None;
            _autoReconnectOcrInProgress = false;
            _autoReconnectManualRun = false;
            _autoReconnectInventoryOnly = false;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectLastCountdownSecond = -1;
            _autoReconnectPendingScreenKind = AutoReconnectScreenKind.Unknown;
            _autoReconnectReturningHomeAfterMissingPickaxe = false;
            _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
        }

        private void ClearEmergencyReconnectRuntimeState(bool stopSoundMonitoring)
        {
            _emergencyReconnectActive = false;
            _emergencyReconnectSoundGuardActive = false;
            _emergencyReconnectResumeKopacz533 = false;
            _emergencyReconnectResumeKopacz633 = false;
            _emergencyReconnectShutdownAfterHome = false;
            _emergencyReconnectInventoryAttempts = 0;
            if (stopSoundMonitoring
                && !_kopacz533RuntimeEnabled
                && !_kopacz633RuntimeEnabled
                && _damageSoundDetector.IsRunning)
            {
                StopEmergencyDamageSoundListening(updateStatus: false);
            }
        }

        private void CompleteEmergencyReconnectAndResumeMining()
        {
            bool resumeKopacz533 = _emergencyReconnectResumeKopacz533;
            bool resumeKopacz633 = _emergencyReconnectResumeKopacz633;
            int teleportWaitSeconds = CalculateAutoReconnectTeleportWaitSeconds(
                _autoReconnectActiveTeleportDelaySeconds);
            string plannedMiner = resumeKopacz533 && ChkKopacz533Enabled?.IsChecked == true
                ? "Kopacz 5/3/3"
                : resumeKopacz633 && ChkKopacz633Enabled?.IsChecked == true && IsKopacz633DirectionSelected()
                    ? "Kopacz 6/3/3"
                    : string.Empty;
            if (!string.IsNullOrEmpty(plannedMiner))
            {
                RecordAutomationLogEvent(
                    MiningLogEventTypes.EmergencyMiningResumed,
                    MiningLogStatuses.Completed,
                    $"Odczekano {teleportWaitSeconds} s od wyboru home ({_autoReconnectActiveTeleportDelaySeconds} s z profilu + {AutoReconnectTeleportSafetyBufferSeconds} s bezpieczeństwa). Wybrano slot 1 i wznowiono {plannedMiner}.");
            }
            StopAutoReconnect(
                "Awaryjny reconnect zakończony: wykonano powrót do wybranego home.",
                resumeMining: false,
                warning: false);

            DateTime now = DateTime.UtcNow;
            string resumedMiner = string.Empty;
            if (resumeKopacz533 && ChkKopacz533Enabled?.IsChecked == true)
            {
                SendKeyTap(VK_1);
                _kopacz533RuntimeEnabled = true;
                StopClickerRuntimesForExclusivePointerMacro();
                StopOtherExclusivePointerMacros(keepKopacz533: true);
                StartKopacz533Runtime(now);
                resumedMiner = "Kopacz 5/3/3";
            }
            else if (resumeKopacz633
                && ChkKopacz633Enabled?.IsChecked == true
                && IsKopacz633DirectionSelected())
            {
                SendKeyTap(VK_1);
                _kopacz633RuntimeEnabled = true;
                StopClickerRuntimesForExclusivePointerMacro();
                StopOtherExclusivePointerMacros(keepKopacz633: true);
                StartKopacz633Runtime(now);
                resumedMiner = "Kopacz 6/3/3";
            }

            if (string.IsNullOrEmpty(resumedMiner))
            {
                UpdateEmergencyReconnectStatus(
                    "Powrót do home zakończony, ale nie wznowiono Kopacza — jego kanał lub kierunek jest obecnie wyłączony.",
                    "Orange");
                UpdateStatusBar("Awaryjny reconnect zakończony bez wznowienia Kopacza.", "Orange");
            }
            else
            {
                UpdateEmergencyReconnectStatus($"Powrót zakończony po {teleportWaitSeconds} s. Wybrano slot 1 i wznowiono {resumedMiner}.", "Green");
                UpdateStatusBar($"Awaryjny reconnect zakończony — slot 1 wybrany, wznowiono {resumedMiner}.", "Green");
            }

            RefreshTopTiles();
            UpdateEnabledStates();
            UpdateEmergencyDamageSoundMonitoring();
        }

        private void CompleteEmergencyReconnectAndShutdown()
        {
            int teleportWaitSeconds = CalculateAutoReconnectTeleportWaitSeconds(
                _autoReconnectActiveTeleportDelaySeconds);
            RecordAutomationLogEvent(
                MiningLogEventTypes.EmergencyShutdown,
                MiningLogStatuses.Completed,
                $"Po wyborze awaryjnego home odczekano {teleportWaitSeconds} s ({_autoReconnectActiveTeleportDelaySeconds} s z profilu + {AutoReconnectTeleportSafetyBufferSeconds} s bezpieczeństwa). Minecraft Helper zostaje zamknięty.");
            StopAutoReconnect(
                "Awaryjny home osiągnięty bez diamentowego kilofa. Kopanie pozostaje wyłączone; zamykam program.",
                resumeMining: false,
                warning: false);
            UpdateEmergencyReconnectStatus(
                "Awaryjny home osiągnięty. Program zostaje zamknięty zgodnie z ustawioną procedurą.",
                "Green");
            ExitFromTray();
        }

        private void BeginAutoReconnectHealthCheck(bool manual, bool inventoryOnly)
        {
            if (_autoReconnectStage != AutoReconnectStage.None)
                return;

            ReadAutoReconnectSettingsFromUi();
            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: false);
            _autoReconnectManualRun = manual;
            _autoReconnectInventoryOnly = inventoryOnly;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectReturningHomeAfterMissingPickaxe = false;
            CaptureMiningModeForAutoReconnect();
            PauseMiningForAutoReconnect();
            RecordAutomationLogEvent(
                MiningLogEventTypes.HealthCheckStarted,
                MiningLogStatuses.Completed,
                $"Rozpoczęto kontrolę EQ ({(manual ? "test ręczny" : "kontrola automatyczna")}). Kopanie zostało chwilowo wstrzymane.");
            _autoReconnectStage = AutoReconnectStage.HealthOpenInventory;
            _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
            UpdateAutoReconnectStatus("Kontrola: zatrzymano kopanie, za chwilę otwieram EQ...", "Orange");
        }

        private void BeginFullAutoReconnect(bool manual)
        {
            if (_autoReconnectStage != AutoReconnectStage.None)
                return;

            ReadAutoReconnectSettingsFromUi();
            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: false);
            _autoReconnectManualRun = manual;
            _autoReconnectInventoryOnly = false;
            _autoReconnectAttempt = 0;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectReturningHomeAfterMissingPickaxe = false;
            CaptureMiningModeForAutoReconnect();
            PauseMiningForAutoReconnect();
            AutoReconnectServerProfile? reconnectProfile = GetSelectedAutoReconnectServerProfile();
            string profileLabel = reconnectProfile?.Name?.Trim() ?? _settings.AutoReconnectProfile;
            RecordAutomationLogEvent(
                MiningLogEventTypes.AutoReconnectStarted,
                MiningLogStatuses.Completed,
                $"Uruchomiono {(manual ? "ręczny" : "automatyczny")} reconnect. Profil: {profileLabel}; adres: {_settings.AutoReconnectServerAddress}; powrót: {_autoReconnectActiveHomeCommand}.");
            _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
            _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
            UpdateAutoReconnectStatus("Reconnect: analizuję aktualny ekran...", "Orange");
        }

        private void StopAutoReconnect(string message, bool resumeMining, bool warning)
        {
            _autoReconnectGeneration++;
            bool wasEmergencyReconnect = _emergencyReconnectActive;
            bool hadActiveStage = _autoReconnectStage != AutoReconnectStage.None;
            bool wasInventoryOnly = _autoReconnectInventoryOnly;
            bool wasMissingPickaxeRecovery = _autoReconnectReturningHomeAfterMissingPickaxe;
            int attempts = _autoReconnectAttempt;
            if (hadActiveStage)
            {
                RecordAutomationLogEvent(
                    wasEmergencyReconnect
                        ? MiningLogEventTypes.EmergencyProtectionFinished
                        : wasMissingPickaxeRecovery
                            ? MiningLogEventTypes.MissingPickaxeRecoveryFinished
                        : wasInventoryOnly
                            ? MiningLogEventTypes.HealthCheckFinished
                            : MiningLogEventTypes.AutoReconnectFinished,
                    warning ? MiningLogStatuses.Aborted : MiningLogStatuses.Completed,
                    $"{message} Próby połączenia: {attempts}; wznowienie kopania: {(resumeMining ? "tak" : "nie")}.");
            }

            _autoReconnectStage = AutoReconnectStage.None;
            _autoReconnectOcrInProgress = false;
            _autoReconnectManualRun = false;
            _autoReconnectInventoryOnly = false;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectLastCountdownSecond = -1;
            _autoReconnectPendingScreenKind = AutoReconnectScreenKind.Unknown;
            _autoReconnectReturningHomeAfterMissingPickaxe = false;
            _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
            _nextAutoReconnectHealthCheckAtUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(_settings.AutoReconnectWatchdogSeconds, 15, 3600));

            bool targetFocused = _targetGameWindowHandle != IntPtr.Zero
                && GetForegroundWindow() == _targetGameWindowHandle;
            if (resumeMining && targetFocused)
            {
                if (_autoReconnectResumeKopacz533 && _kopacz533RuntimeEnabled)
                    SetKopacz533MiningHold(true);
                if (_autoReconnectResumeKopacz633 && _kopacz633RuntimeEnabled)
                    SetKopacz633AttackHold(true);
            }

            _autoReconnectResumeKopacz533 = false;
            _autoReconnectResumeKopacz633 = false;
            _autoReconnectLogMiningRunId = string.Empty;
            _autoReconnectLogOwner = string.Empty;
            if (wasEmergencyReconnect)
            {
                ClearEmergencyReconnectRuntimeState(stopSoundMonitoring: true);
                UpdateEmergencyReconnectStatus(message, warning ? "Red" : "Green");
            }
            UpdateAutoReconnectStatus(message, warning ? "Red" : "Green");
            UpdateEnabledStates();
        }

        private async void RunAutoReconnectTick(object? sender, EventArgs e)
        {
            if (_isLoadingUi || _autoReconnectTickInProgress)
                return;

            DateTime now = DateTime.UtcNow;
            if (_autoReconnectStage == AutoReconnectStage.None)
                return;

            // RunMacroTickCore intentionally stops before refreshing the normal
            // HUD while reconnect owns the input. Keep the reconnect tile and
            // its countdown alive from the slower reconnect timer instead.
            RefreshOverlayHud(now);

            if (_targetGameWindowHandle == IntPtr.Zero || GetForegroundWindow() != _targetGameWindowHandle)
            {
                UpdateAutoReconnectStatus("Reconnect wstrzymany: Minecraft musi być aktywnym oknem.", "Orange");
                return;
            }
            if (_autoReconnectStage == AutoReconnectStage.EmergencyWaitBeforeReconnect
                && now < _nextAutoReconnectActionAtUtc)
            {
                int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_nextAutoReconnectActionAtUtc - now).TotalSeconds));
                if (remainingSeconds != _autoReconnectLastCountdownSecond)
                {
                    _autoReconnectLastCountdownSecond = remainingSeconds;
                    UpdateAutoReconnectStatus(
                        $"Awaryjny reconnect za {remainingSeconds} s. Nasłuch obrażeń nadal działa.",
                        "Orange");
                }
                return;
            }
            if (_autoReconnectStage == AutoReconnectStage.WaitForDisconnectButtonUnlock
                && now < _nextAutoReconnectActionAtUtc)
            {
                int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_nextAutoReconnectActionAtUtc - now).TotalSeconds));
                if (remainingSeconds != _autoReconnectLastCountdownSecond)
                {
                    _autoReconnectLastCountdownSecond = remainingSeconds;
                    string action = _autoReconnectPendingScreenKind == AutoReconnectScreenKind.ReconnectChoice
                        ? "Reconnect"
                        : "Back to Server List";
                    UpdateAutoReconnectStatus($"Przycisk {action} może być zablokowany. Próba za {remainingSeconds} s...", "Orange");
                }
                return;
            }
            if (now < _nextAutoReconnectActionAtUtc)
                return;

            _autoReconnectTickInProgress = true;
            int generation = _autoReconnectGeneration;
            IntPtr targetWindow = _targetGameWindowHandle;
            try
            {
                switch (_autoReconnectStage)
                {
                    case AutoReconnectStage.EmergencyWaitBeforeReconnect:
                        _autoReconnectLastCountdownSecond = -1;
                        _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                        _nextAutoReconnectActionAtUtc = now;
                        UpdateAutoReconnectStatus("Minął czas oczekiwania. Rozpoznaję ekran i rozpoczynam reconnect...", "Orange");
                        break;

                    case AutoReconnectStage.HealthOpenInventory:
                        PauseMiningForAutoReconnect();
                        SendKeyTap(VK_E);
                        _autoReconnectStage = AutoReconnectStage.HealthVerifyInventory;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(650);
                        UpdateAutoReconnectStatus("Kontrola: sprawdzam znacznik otwartego EQ...", "Orange");
                        break;

                    case AutoReconnectStage.HealthVerifyInventory:
                        var healthScan = await InspectOpenMinecraftInventoryAsync();
                        if (!IsAutoReconnectResultCurrent(generation, AutoReconnectStage.HealthVerifyInventory, targetWindow))
                            return;
                        if (healthScan.Found)
                        {
                            int detectedScale = healthScan.Scale;
                            bool diamondPickaxePresent = healthScan.PickaxePresent;
                            SendKeyTap(VK_E);
                            if (_autoReconnectInventoryOnly)
                            {
                                StopAutoReconnect(
                                    diamondPickaxePresent
                                        ? $"Test EQ OK: wykryto EQ (GUI x{detectedScale}) oraz znacznik diamentowego kilofa."
                                        : $"Test EQ: wykryto EQ (GUI x{detectedScale}), ale nie znaleziono znacznika diamentowego kilofa.",
                                    resumeMining: true,
                                    warning: !diamondPickaxePresent);
                                break;
                            }

                            StopAutoReconnect($"Kontrola OK: wykryto EQ (GUI x{detectedScale}). Kopanie wznowione.", resumeMining: true, warning: false);
                            break;
                        }

                        _autoReconnectInventoryFailures++;
                        SendKeyTap(VK_ESCAPE);
                        if (_autoReconnectInventoryOnly)
                        {
                            StopAutoReconnect("Test EQ nieudany: nie wykryto znaczników paczki Minecraft Helper.", resumeMining: true, warning: true);
                            break;
                        }
                        if (_autoReconnectInventoryFailures < 3)
                        {
                            _autoReconnectStage = AutoReconnectStage.HealthOpenInventory;
                            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(450);
                            UpdateAutoReconnectStatus($"Kontrola EQ: próba {_autoReconnectInventoryFailures + 1}/3...", "Orange");
                            break;
                        }

                        _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
                        UpdateAutoReconnectStatus("Brak potwierdzenia EQ. Uruchamiam procedurę odzyskiwania połączenia...", "Orange");
                        break;

                    case AutoReconnectStage.AnalyzeScreen:
                        await AnalyzeAndHandleAutoReconnectScreenAsync();
                        break;

                    case AutoReconnectStage.WaitForDisconnectButtonUnlock:
                        if (!TryClickClientPoint(0.5, 0.59, bottomOffset: null))
                        {
                            StopAutoReconnect("Nie udało się kliknąć przycisku ekranu rozłączenia.", resumeMining: false, warning: true);
                            break;
                        }

                        if (_autoReconnectPendingScreenKind == AutoReconnectScreenKind.ReconnectChoice)
                        {
                            _autoReconnectAttempt++;
                            _autoReconnectStage = AutoReconnectStage.WaitForServerJoin;
                            _nextAutoReconnectActionAtUtc = now.AddSeconds(Math.Max(AutoReconnectBlockedButtonWaitSeconds, _settings.AutoReconnectJoinDelaySeconds));
                            UpdateAutoReconnectStatus($"Kliknięto Reconnect — próba {_autoReconnectAttempt}/{_settings.AutoReconnectMaxAttempts}...", "Orange");
                        }
                        else
                        {
                            bool proxyCooldown = _autoReconnectPendingScreenKind == AutoReconnectScreenKind.AlreadyConnected;
                            _autoReconnectStage = AutoReconnectStage.WaitAfterScreenClick;
                            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(proxyCooldown ? 2500 : 800);
                            UpdateAutoReconnectStatus(proxyCooldown
                                ? "Kliknięto powrót. Proxy nadal może zwalniać poprzednią sesję..."
                                : "Kliknięto Back to Server List. Czekam na listę serwerów...", "Orange");
                        }
                        _autoReconnectPendingScreenKind = AutoReconnectScreenKind.Unknown;
                        _autoReconnectLastCountdownSecond = -1;
                        break;

                    case AutoReconnectStage.WaitAfterScreenClick:
                        _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                        _nextAutoReconnectActionAtUtc = now;
                        break;

                    case AutoReconnectStage.OpenDirectConnect:
                        // Minecraft 1.8.8 uses GUI coordinates here. With required
                        // GUI Scale: Large (x3), the button centre is 42 * 3 px
                        // above the bottom edge of the client.
                        if (!TryClickClientPoint(0.5, null, bottomOffset: 126))
                        {
                            StopAutoReconnect("Nie udało się kliknąć Direct Connect.", resumeMining: false, warning: true);
                            break;
                        }
                        _autoReconnectStage = AutoReconnectStage.WaitForDirectConnect;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(700);
                        UpdateAutoReconnectStatus("Otwieram Direct Connect...", "Orange");
                        break;

                    case AutoReconnectStage.WaitForDirectConnect:
                        _autoReconnectStage = AutoReconnectStage.EnterServerAddress;
                        _nextAutoReconnectActionAtUtc = now;
                        break;

                    case AutoReconnectStage.EnterServerAddress:
                        if (!TryInspectDirectConnectAddressField(out bool addressAlreadyEntered, out string recognizedAddress))
                        {
                            StopAutoReconnect("Nie udało się sprawdzić pola adresu na ekranie Direct Connect.", resumeMining: false, warning: true);
                            break;
                        }

                        if (!addressAlreadyEntered)
                        {
                            if (string.IsNullOrWhiteSpace(_settings.AutoReconnectServerAddress))
                            {
                                StopAutoReconnect("Pole adresu jest puste, a w konfiguracji nie podano adresu serwera.", resumeMining: false, warning: true);
                                break;
                            }
                            if (!TryClickDirectConnectAddressField())
                            {
                                StopAutoReconnect("Nie udało się aktywować pola adresu serwera.", resumeMining: false, warning: true);
                                break;
                            }
                            SendKeyDown(VK_CONTROL);
                            SendKeyTap(VK_A);
                            SendKeyUp(VK_CONTROL);
                            if (!SendTextByKeyboard(_settings.AutoReconnectServerAddress))
                            {
                                StopAutoReconnect("Nie udało się wpisać adresu serwera.", resumeMining: false, warning: true);
                                break;
                            }
                        }

                        SendKeyTap(VK_RETURN);
                        _autoReconnectAttempt++;
                        _autoReconnectStage = AutoReconnectStage.WaitForServerJoin;
                        _nextAutoReconnectActionAtUtc = now.AddSeconds(_settings.AutoReconnectJoinDelaySeconds);
                        string addressAction = addressAlreadyEntered
                            ? string.IsNullOrWhiteSpace(recognizedAddress)
                                ? "Adres był już wpisany"
                                : $"Adres był już wpisany ({recognizedAddress})"
                            : $"Wpisano {_settings.AutoReconnectServerAddress}";
                        UpdateAutoReconnectStatus($"{addressAction}; zatwierdzono Enterem — próba {_autoReconnectAttempt}/{_settings.AutoReconnectMaxAttempts}...", "Orange");
                        break;

                    case AutoReconnectStage.WaitForServerJoin:
                        if (IsInventoryCursorVisible())
                        {
                            if (_autoReconnectAttempt >= _settings.AutoReconnectMaxAttempts)
                            {
                                StopAutoReconnect($"Nie potwierdzono wejścia do gry po {_autoReconnectAttempt} próbach.", resumeMining: false, warning: true);
                                break;
                            }
                            _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                            _nextAutoReconnectActionAtUtc = now;
                            UpdateAutoReconnectStatus("Po czasie oczekiwania nadal widać ekran GUI. Sprawdzam jego typ...", "Orange");
                            break;
                        }
                        if (_emergencyReconnectActive)
                        {
                            RecordAutomationLogEvent(
                                MiningLogEventTypes.EmergencyReconnectJoined,
                                MiningLogStatuses.Completed,
                                $"Dołączono do serwera po {_autoReconnectAttempt} próbach. Otwieram EQ i rozpoczynam kontrolę diamentowego kilofa.");
                            _autoReconnectStage = AutoReconnectStage.EmergencyOpenInventory;
                            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
                            UpdateAutoReconnectStatus("Dołączono do gry. Otwieram EQ i sprawdzam diamentowy kilof...", "Orange");
                        }
                        else
                        {
                            _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
                            _nextAutoReconnectActionAtUtc = now;
                        }
                        break;

                    case AutoReconnectStage.EmergencyOpenInventory:
                        SendKeyTap(VK_E);
                        _autoReconnectStage = AutoReconnectStage.EmergencyVerifyInventory;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(700);
                        UpdateAutoReconnectStatus("Sprawdzam EQ i obecność diamentowego kilofa...", "Orange");
                        break;

                    case AutoReconnectStage.EmergencyVerifyInventory:
                        var emergencyScan = await InspectOpenMinecraftInventoryAsync();
                        if (!IsAutoReconnectResultCurrent(generation, AutoReconnectStage.EmergencyVerifyInventory, targetWindow))
                            return;

                        if (!emergencyScan.Found)
                        {
                            SendKeyTap(VK_ESCAPE);
                            _emergencyReconnectInventoryAttempts++;
                            if (_emergencyReconnectInventoryAttempts < 3)
                            {
                                _autoReconnectStage = AutoReconnectStage.EmergencyOpenInventory;
                                _nextAutoReconnectActionAtUtc = now.AddMilliseconds(500);
                                UpdateAutoReconnectStatus(
                                    $"Nie potwierdzono otwartego EQ. Ponawiam próbę {_emergencyReconnectInventoryAttempts + 1}/3...",
                                    "Orange");
                                break;
                            }

                            StopAutoReconnect(
                                "Awaryjny reconnect zatrzymany: po 3 próbach nie wykryto znaczników EQ. Nie wysłano żadnej komendy /home.",
                                resumeMining: false,
                                warning: true);
                            break;
                        }

                        if (emergencyScan.PickaxePresent)
                        {
                            SendKeyTap(VK_E);
                            _emergencyReconnectInventoryAttempts = 0;
                            _emergencyReconnectSoundGuardActive = false;
                            StopEmergencyDamageSoundListening(updateStatus: false);
                            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: false);
                            RecordAutomationLogEvent(
                                MiningLogEventTypes.EmergencyPickaxeDetected,
                                MiningLogStatuses.Completed,
                                $"Wykryto diamentowy kilof po {_emergencyReconnectInventoryAttempts + 1} odczytach EQ (GUI x{emergencyScan.Scale}). Wybrano podstawowy home: {_autoReconnectActiveHomeCommand}.");
                            _emergencyReconnectShutdownAfterHome = false;
                            _autoReconnectReturningHomeAfterMissingPickaxe = false;
                            _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
                            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
                            UpdateAutoReconnectStatus(
                                $"Wykryto diamentowy kilof (GUI x{emergencyScan.Scale}). Wysyłam {_autoReconnectActiveHomeCommand}, a po teleporcie wznowię Kopacza.",
                                "Green");
                            break;
                        }

                        _emergencyReconnectInventoryAttempts++;
                        const int emergencyPickaxeScanLimit = 5;
                        if (_emergencyReconnectInventoryAttempts < emergencyPickaxeScanLimit)
                        {
                            _autoReconnectStage = AutoReconnectStage.EmergencyVerifyInventory;
                            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(320);
                            UpdateAutoReconnectStatus(
                                $"Nie odczytano jeszcze znacznika kilofa. Potwierdzam wynik {_emergencyReconnectInventoryAttempts + 1}/{emergencyPickaxeScanLimit}...",
                                "Orange");
                            break;
                        }

                        SendKeyTap(VK_E);
                        _emergencyReconnectSoundGuardActive = false;
                        StopEmergencyDamageSoundListening(updateStatus: false);
                        if (!emergencyScan.HasGuiMarkers)
                        {
                            StopAutoReconnect(
                                "Nie potwierdzono znaczników aktualnej paczki Minecraft Helper. Nie uznaję tego za brak kilofa i nie wykonuję awaryjnego /home.",
                                resumeMining: false,
                                warning: true);
                            break;
                        }

                        AutoReconnectServerProfile? emergencyProfile = GetSelectedAutoReconnectServerProfile();
                        if (emergencyProfile?.MissingPickaxeRecoveryEnabled != true)
                        {
                            StopAutoReconnect(
                                "Nie wykryto diamentowego kilofa, ale w wybranym profilu nie włączono awaryjnego home. Kopacz pozostaje wyłączony.",
                                resumeMining: false,
                                warning: true);
                            break;
                        }

                        ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: true);
                        _emergencyReconnectShutdownAfterHome = true;
                        _autoReconnectReturningHomeAfterMissingPickaxe = true;
                        RecordAutomationLogEvent(
                            MiningLogEventTypes.EmergencyPickaxeMissing,
                            MiningLogStatuses.Completed,
                            $"Po {emergencyPickaxeScanLimit} odczytach EQ nie wykryto diamentowego kilofa. Wybrano awaryjny home: {_autoReconnectActiveHomeCommand}; po teleporcie aplikacja zostanie zamknięta.");
                        _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
                        UpdateAutoReconnectStatus(
                            $"Brak diamentowego kilofa. Wysyłam awaryjne {_autoReconnectActiveHomeCommand}; po teleporcie program zostanie zamknięty.",
                            "Red");
                        break;

                    case AutoReconnectStage.OpenHomeChat:
                        SendChatOpenKeyTap();
                        _autoReconnectStage = AutoReconnectStage.TypeHomeCommand;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(220);
                        UpdateAutoReconnectStatus($"Wysyłam {_autoReconnectActiveHomeCommand}...", "Orange");
                        break;

                    case AutoReconnectStage.TypeHomeCommand:
                        if (!SendTextByKeyboard(_autoReconnectActiveHomeCommand))
                        {
                            StopAutoReconnect("Nie udało się wpisać komendy domu.", resumeMining: false, warning: true);
                            break;
                        }
                        _autoReconnectStage = AutoReconnectStage.SubmitHomeCommand;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(120);
                        break;

                    case AutoReconnectStage.SubmitHomeCommand:
                        SendKeyTap(VK_RETURN);
                        if (_emergencyReconnectActive)
                        {
                            RecordAutomationLogEvent(
                                MiningLogEventTypes.EmergencyHomeCommandSent,
                                MiningLogStatuses.Completed,
                                $"Wysłano {_autoReconnectActiveHomeCommand}. Tryb: {(_emergencyReconnectShutdownAfterHome ? "awaryjny home bez kilofa" : "podstawowy home i wznowienie kopania")}. Po zatwierdzeniu home program odczeka {CalculateAutoReconnectTeleportWaitSeconds(_autoReconnectActiveTeleportDelaySeconds)} s przed następną akcją.");
                        }
                        if (_autoReconnectActiveHomeHasGui)
                        {
                            _autoReconnectStage = AutoReconnectStage.WaitForHomeMenu;
                            _nextAutoReconnectActionAtUtc = now.AddSeconds(_autoReconnectActiveHomeGuiDelaySeconds);
                            UpdateAutoReconnectStatus($"Czekam {_autoReconnectActiveHomeGuiDelaySeconds} s na menu wyboru home...", "Orange");
                        }
                        else
                        {
                            int teleportWaitSeconds = CalculateAutoReconnectTeleportWaitSeconds(
                                _autoReconnectActiveTeleportDelaySeconds);
                            _autoReconnectStage = AutoReconnectStage.WaitForTeleport;
                            _nextAutoReconnectActionAtUtc = now.AddSeconds(teleportWaitSeconds);
                            UpdateAutoReconnectStatus(
                                $"Czekam {teleportWaitSeconds} s na teleport ({_autoReconnectActiveTeleportDelaySeconds} s z profilu + {AutoReconnectTeleportSafetyBufferSeconds} s bezpieczeństwa)...",
                                "Orange");
                        }
                        break;

                    case AutoReconnectStage.WaitForHomeMenu:
                        _autoReconnectStage = AutoReconnectStage.ClickHomeSlot;
                        _nextAutoReconnectActionAtUtc = now;
                        break;

                    case AutoReconnectStage.ClickHomeSlot:
                        if (!IsInventoryCursorVisible())
                        {
                            StopAutoReconnect("Nie wykryto otwartego menu wyboru home. Zatrzymano bez klikania.", resumeMining: false, warning: true);
                            break;
                        }
                        if (!TryClickHomeMenuSlot(
                                _autoReconnectActiveHomeSlot,
                                _autoReconnectActiveHomeGuiRows,
                                _autoReconnectActiveHomeGuiColumns))
                        {
                            StopAutoReconnect("Nie udało się wyznaczyć pozycji slotu home.", resumeMining: false, warning: true);
                            break;
                        }
                        int teleportWaitSecondsAfterClick = CalculateAutoReconnectTeleportWaitSeconds(
                            _autoReconnectActiveTeleportDelaySeconds);
                        _autoReconnectStage = AutoReconnectStage.WaitForTeleport;
                        _nextAutoReconnectActionAtUtc = now.AddSeconds(teleportWaitSecondsAfterClick);
                        UpdateAutoReconnectStatus(
                            $"Kliknięto home w slocie {_autoReconnectActiveHomeSlot} układu {_autoReconnectActiveHomeGuiRows}×{_autoReconnectActiveHomeGuiColumns}. Czekam {teleportWaitSecondsAfterClick} s na teleport ({_autoReconnectActiveTeleportDelaySeconds} s + {AutoReconnectTeleportSafetyBufferSeconds} s bezpieczeństwa)...",
                            "Orange");
                        break;

                    case AutoReconnectStage.WaitForTeleport:
                        if (_emergencyReconnectActive)
                        {
                            if (_emergencyReconnectShutdownAfterHome)
                                CompleteEmergencyReconnectAndShutdown();
                            else
                                CompleteEmergencyReconnectAndResumeMining();
                            break;
                        }
                        _autoReconnectStage = AutoReconnectStage.OpenVerificationInventory;
                        _nextAutoReconnectActionAtUtc = now.AddSeconds(1);
                        UpdateAutoReconnectStatus("Teleport zakończony. Czekam dodatkową 1 s przed otwarciem EQ...", "Orange");
                        break;

                    case AutoReconnectStage.OpenVerificationInventory:
                        SendKeyTap(VK_E);
                        _autoReconnectStage = AutoReconnectStage.VerifyAfterTeleport;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(700);
                        UpdateAutoReconnectStatus("Weryfikuję EQ po teleportacji...", "Orange");
                        break;

                    case AutoReconnectStage.VerifyAfterTeleport:
                        var verificationScan = await InspectOpenMinecraftInventoryAsync();
                        if (!IsAutoReconnectResultCurrent(generation, AutoReconnectStage.VerifyAfterTeleport, targetWindow))
                            return;
                        if (verificationScan.Found)
                        {
                            int verificationScale = verificationScan.Scale;
                            SendKeyTap(VK_E);
                            if (_autoReconnectReturningHomeAfterMissingPickaxe)
                            {
                                StopAutoReconnect(
                                    $"Powrót do home zakończony: EQ potwierdzone (GUI x{verificationScale}). Kopanie pozostaje wyłączone.",
                                    resumeMining: false,
                                    warning: true);
                            }
                            else
                            {
                                StopAutoReconnect($"Reconnect zakończony: EQ potwierdzone (GUI x{verificationScale}), kopanie wznowione.", resumeMining: true, warning: false);
                            }
                            break;
                        }

                        SendKeyTap(VK_ESCAPE);
                        if (_autoReconnectAttempt >= _settings.AutoReconnectMaxAttempts)
                        {
                            StopAutoReconnect($"Reconnect zatrzymany po {_autoReconnectAttempt} próbach: brak potwierdzenia EQ.", resumeMining: false, warning: true);
                            break;
                        }
                        _autoReconnectStage = AutoReconnectStage.RetryDelay;
                        _nextAutoReconnectActionAtUtc = now.AddSeconds(3);
                        UpdateAutoReconnectStatus("Nie potwierdzono powrotu do gry. Ponawiam analizę za 3 s...", "Orange");
                        break;

                    case AutoReconnectStage.RetryDelay:
                        _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                        _nextAutoReconnectActionAtUtc = now;
                        break;
                }
            }
            catch (Exception ex)
            {
                if (generation == _autoReconnectGeneration)
                    StopAutoReconnect("Błąd auto reconnectu: " + ex.Message, resumeMining: false, warning: true);
            }
            finally
            {
                _autoReconnectTickInProgress = false;
            }
        }

        private bool IsAutoReconnectResultCurrent(int generation, AutoReconnectStage expectedStage, IntPtr targetWindow)
        {
            return generation == _autoReconnectGeneration
                && _autoReconnectStage == expectedStage
                && targetWindow != IntPtr.Zero
                && targetWindow == _targetGameWindowHandle
                && GetForegroundWindow() == targetWindow;
        }

        private async Task<(bool Found, int Scale, bool PickaxePresent, bool HasGuiMarkers)> InspectOpenMinecraftInventoryAsync()
        {
            if (!TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out _))
                return (false, 0, false, false);

            using (bitmap)
            {
                return await Task.Run(() =>
                {
                    if (!InventoryMarkerDetector.TryDetect(bitmap!, null, null, out InventoryMarkerDetection detection))
                        return (false, 0, false, false);
                    bool pickaxePresent = InventoryMarkerDetector.ContainsMarkedItem(
                        bitmap!, detection.Layout, "diamond_pickaxe", includeHotbar: true);
                    return (true, detection.Layout.Scale, pickaxePresent, detection.HasGuiMarkers);
                });
            }
        }

        private void BeginMissingPickaxeHomeRecovery(InventoryCleanupOwner owner, DateTime now)
        {
            if (_autoReconnectStage != AutoReconnectStage.None)
                return;

            ReadAutoReconnectSettingsFromUi();
            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: true);
            _autoReconnectLogMiningRunId = GetMiningRunId(owner);
            _autoReconnectLogOwner = GetInventoryCleanupOwnerLabel(owner);
            RecordAutomationLogEvent(
                MiningLogEventTypes.MissingPickaxeRecovery,
                MiningLogStatuses.Completed,
                $"Brak znacznika diamentowego kilofa. Wysyłam {_autoReconnectActiveHomeCommand}; po powrocie kopanie pozostanie zakończone.");
            StopMinerRuntimeAfterMissingPickaxe(owner);
            _autoReconnectReturningHomeAfterMissingPickaxe = true;
            _autoReconnectManualRun = false;
            _autoReconnectInventoryOnly = false;
            _autoReconnectAttempt = 0;
            _autoReconnectInventoryFailures = 0;
            _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
            _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
            UpdateAutoReconnectStatus(
                $"Auto EQ nie wykrył diamentowego kilofa. Wykonuję {_autoReconnectActiveHomeCommand} i kończę {GetInventoryCleanupOwnerLabel(owner)}...",
                "Red");
        }

        private void StopMinerRuntimeAfterMissingPickaxe(InventoryCleanupOwner owner)
        {
            if (owner == InventoryCleanupOwner.Kopacz533)
            {
                _kopacz533RuntimeEnabled = false;
                SetKopacz533MiningHold(false);
                ResetKopacz533RuntimeState();
                EndMiningLogRun(owner, "Kopanie zakończone: nie wykryto diamentowego kilofa.", MiningLogStatuses.Aborted);
            }
            else if (owner == InventoryCleanupOwner.Kopacz633)
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
                EndMiningLogRun(owner, "Kopanie zakończone: nie wykryto diamentowego kilofa.", MiningLogStatuses.Aborted);
            }

            _autoReconnectResumeKopacz533 = false;
            _autoReconnectResumeKopacz633 = false;
            RefreshTopTiles();
        }

        private bool TryInspectDirectConnectAddressField(out bool hasAddress, out string recognizedAddress)
        {
            hasAddress = false;
            recognizedAddress = string.Empty;
            if (!TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out _))
                return false;

            using (bitmap)
            {
                const int guiScale = 3; // reconnect wymaga GUI Scale: Large
                int fieldLeft = bitmap!.Width / 2 - 100 * guiScale;
                int fieldTop = 116 * guiScale;
                var textArea = new Drawing.Rectangle(
                    fieldLeft + 4 * guiScale,
                    fieldTop + 3 * guiScale,
                    192 * guiScale,
                    14 * guiScale);

                if (textArea.Left < 0 || textArea.Top < 0 ||
                    textArea.Right > bitmap.Width || textArea.Bottom > bitmap.Height)
                {
                    return false;
                }

                using Drawing.Bitmap addressCrop = bitmap.Clone(textArea, DrawingImaging.PixelFormat.Format32bppArgb);
                if (EnsureF3TesseractEngine())
                {
                    string rawText = RunOcrOnBitmap(addressCrop, TesseractPageSegMode.SingleLine);
                    recognizedAddress = Regex.Replace(rawText ?? string.Empty, @"\s+", string.Empty).Trim();
                    int recognizedCharacters = recognizedAddress.Count(char.IsLetterOrDigit);
                    if (recognizedCharacters >= 2)
                    {
                        hasAddress = true;
                        return true;
                    }
                }

                int brightPixels = 0;
                int firstBrightColumn = addressCrop.Width;
                int lastBrightColumn = -1;
                for (int x = 0; x < addressCrop.Width; x++)
                {
                    int columnPixels = 0;
                    for (int y = 0; y < addressCrop.Height; y++)
                    {
                        Drawing.Color pixel = addressCrop.GetPixel(x, y);
                        int brightest = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                        int darkest = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                        if (brightest >= 120 && brightest - darkest <= 55)
                            columnPixels++;
                    }

                    if (columnPixels >= 2)
                    {
                        brightPixels += columnPixels;
                        firstBrightColumn = Math.Min(firstBrightColumn, x);
                        lastBrightColumn = x;
                    }
                }

                int occupiedWidth = lastBrightColumn >= firstBrightColumn
                    ? lastBrightColumn - firstBrightColumn + 1
                    : 0;
                // Pojedynczy migający kursor zajmuje najwyżej szerokość jednego znaku.
                hasAddress = brightPixels >= 120 && occupiedWidth >= 24;
                return true;
            }
        }

        private bool TryClickDirectConnectAddressField()
        {
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT rect))
                return false;

            const int guiScale = 3;
            int clientWidth = rect.Right - rect.Left;
            int clientHeight = rect.Bottom - rect.Top;
            int x = rect.Left + clientWidth / 2;
            int y = rect.Top + (116 + 10) * guiScale;
            if (clientWidth <= 0 || clientHeight <= 0 || y < rect.Top || y >= rect.Bottom)
                return false;

            NativeInput.SetCursorPosition(x, y);
            SendMouseClick(leftButton: true, holdPulseMode: false);
            return true;
        }

        private async Task AnalyzeAndHandleAutoReconnectScreenAsync()
        {
            if (_autoReconnectOcrInProgress)
                return;
            if (!EnsureF3TesseractEngine())
            {
                StopAutoReconnect("Brak OCR (eng.traineddata). Nie można rozpoznać ekranu reconnectu.", resumeMining: false, warning: true);
                return;
            }
            if (!TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out Drawing.Rectangle clientArea))
            {
                StopAutoReconnect("Nie udało się przechwycić obrazu okna Minecrafta.", resumeMining: false, warning: true);
                return;
            }

            _autoReconnectOcrInProgress = true;
            _autoReconnectStage = AutoReconnectStage.WaitForScreenAnalysis;
            int generation = _autoReconnectGeneration;
            IntPtr targetWindow = _targetGameWindowHandle;
            try
            {
                using (bitmap)
                {
                    bool inventoryFound = await Task.Run(() => InventoryMarkerDetector.TryDetect(bitmap!, null, null, out _));
                    if (!IsAutoReconnectResultCurrent(generation, AutoReconnectStage.WaitForScreenAnalysis, targetWindow))
                        return;
                    if (inventoryFound)
                    {
                        HandleAutoReconnectScreen(AutoReconnectScreenKind.Inventory, clientArea);
                        return;
                    }

                    string text = await Task.Run(() =>
                    {
                        using Drawing.Bitmap prepared = PrepareBitmapForOcr(bitmap!);
                        string raw = RunOcrOnBitmap(bitmap!, TesseractPageSegMode.SparseText);
                        string enhanced = RunOcrOnBitmap(prepared, TesseractPageSegMode.SparseText);
                        return raw + Environment.NewLine + enhanced;
                    });
                    // A mining STOP can be handled while OCR is running. Never
                    // let a late result click a menu or restart the cancelled flow.
                    if (!IsAutoReconnectResultCurrent(generation, AutoReconnectStage.WaitForScreenAnalysis, targetWindow))
                        return;
                    _autoReconnectLastOcrText = text;
                    HandleAutoReconnectScreen(ClassifyAutoReconnectScreen(text), clientArea);
                }
            }
            finally
            {
                if (generation == _autoReconnectGeneration)
                {
                    _autoReconnectOcrInProgress = false;
                    // If focus/target changed during the await, retry only when
                    // the game is active again instead of staying in WAIT forever.
                    if (_autoReconnectStage == AutoReconnectStage.WaitForScreenAnalysis)
                    {
                        _autoReconnectStage = AutoReconnectStage.AnalyzeScreen;
                        _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
                    }
                }
            }
        }

        private static AutoReconnectScreenKind ClassifyAutoReconnectScreen(string? text)
        {
            // OCR często zostawia nawiasy/tagi serwera (np. "[MH] DIRECT CONNECT")
            // albo rozdziela wyrazy znakami. Do klasyfikacji liczą się same tokeny.
            string normalized = Regex.Replace((text ?? string.Empty).ToUpperInvariant(), @"\s+", " ").Trim();
            string tokens = Regex.Replace(normalized, @"[^A-Z0-9]+", " ").Trim();
            if (normalized.Contains("BANNED") || normalized.Contains(" BAN ") || normalized.Contains("ZABLOKOW") || normalized.Contains("BAN ENDS"))
                return AutoReconnectScreenKind.Banned;
            if (normalized.Contains("ALREADY CONNECTED") || normalized.Contains("POLACZENIE Z PROXY") || normalized.Contains("POŁĄCZENIE Z PROXY"))
                return AutoReconnectScreenKind.AlreadyConnected;
            if (normalized.Contains("PLAYER DEAD") || normalized.Contains("GAME OVER") || normalized.Contains("RESPAWN"))
                return AutoReconnectScreenKind.PlayerDead;
            if (normalized.Contains("RECONNECT"))
                return AutoReconnectScreenKind.ReconnectChoice;
            if (normalized.Contains("CONNECTION FAILED") || normalized.Contains("CONNECTION LOST") || normalized.Contains("KICKED") || normalized.Contains("LOGIN FAILED") || normalized.Contains("FAILED TO CONNECT") || normalized.Contains("DISCONNECTED"))
                return AutoReconnectScreenKind.Disconnected;
            if (normalized.Contains("SERVER ADDRESS"))
                return AutoReconnectScreenKind.DirectConnect;
            bool hasMultiplayerScreen = tokens.Contains("MULTIPLAYER")
                || (tokens.Contains("PLAY") && tokens.Contains("MULTI"));
            bool hasDirectConnectButton = tokens.Contains("DIRECT") && tokens.Contains("CONNECT");
            if (hasMultiplayerScreen
                || hasDirectConnectButton
                || normalized.Contains("SERVER LIST")
                || normalized.Contains("JOIN SERVER"))
                return AutoReconnectScreenKind.ServerList;
            return AutoReconnectScreenKind.Unknown;
        }

        private void HandleAutoReconnectScreen(AutoReconnectScreenKind kind, Drawing.Rectangle clientArea)
        {
            DateTime now = DateTime.UtcNow;
            switch (kind)
            {
                case AutoReconnectScreenKind.Inventory:
                    if (_emergencyReconnectActive)
                    {
                        _autoReconnectStage = AutoReconnectStage.EmergencyVerifyInventory;
                        _nextAutoReconnectActionAtUtc = now;
                        UpdateAutoReconnectStatus("Wykryto otwarte EQ. Sprawdzam diamentowy kilof...", "Orange");
                    }
                    else
                    {
                        SendKeyTap(VK_E);
                        _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
                        _nextAutoReconnectActionAtUtc = now.AddMilliseconds(350);
                        UpdateAutoReconnectStatus("Wykryto otwarte EQ. Zamykam je i przechodzę do /home...", "Orange");
                    }
                    return;

                case AutoReconnectScreenKind.PlayerDead:
                    double respawnY = Math.Clamp((clientArea.Height / 4.0 + 82 * 3) / Math.Max(1, clientArea.Height), 0.35, 0.58);
                    ClickClientPoint(clientArea, 0.5, respawnY);
                    _autoReconnectStage = AutoReconnectStage.WaitForServerJoin;
                    _nextAutoReconnectActionAtUtc = now.AddSeconds(Math.Max(3, _settings.AutoReconnectJoinDelaySeconds));
                    UpdateAutoReconnectStatus("Wykryto śmierć gracza. Kliknięto Respawn.", "Orange");
                    return;

                case AutoReconnectScreenKind.ReconnectChoice:
                    ScheduleBlockedDisconnectButtonClick(kind, now);
                    return;

                case AutoReconnectScreenKind.Disconnected:
                case AutoReconnectScreenKind.AlreadyConnected:
                    if (kind == AutoReconnectScreenKind.AlreadyConnected && _autoReconnectAttempt >= _settings.AutoReconnectMaxAttempts)
                    {
                        StopAutoReconnect("Serwer nadal zgłasza aktywne połączenie z proxy. Limit prób osiągnięty.", resumeMining: false, warning: true);
                        return;
                    }
                    ScheduleBlockedDisconnectButtonClick(kind, now);
                    return;

                case AutoReconnectScreenKind.ServerList:
                    _autoReconnectStage = AutoReconnectStage.OpenDirectConnect;
                    _nextAutoReconnectActionAtUtc = now.AddMilliseconds(250);
                    UpdateAutoReconnectStatus("Wykryto listę serwerów. Otwieram Direct Connect...", "Orange");
                    return;

                case AutoReconnectScreenKind.DirectConnect:
                    _autoReconnectStage = AutoReconnectStage.EnterServerAddress;
                    _nextAutoReconnectActionAtUtc = now;
                    return;

                case AutoReconnectScreenKind.Banned:
                    StopAutoReconnect("Wykryto komunikat o banie/blokadzie. Automat nie będzie ponawiał połączenia.", resumeMining: false, warning: true);
                    return;

                default:
                    string preview = Regex.Replace(_autoReconnectLastOcrText ?? string.Empty, @"\s+", " ").Trim();
                    if (preview.Length > 150)
                        preview = preview.Substring(0, 150) + "...";
                    StopAutoReconnect("Nieznany ekran — zatrzymano bez klikania." + (string.IsNullOrWhiteSpace(preview) ? string.Empty : " OCR: " + preview), resumeMining: false, warning: true);
                    return;
            }
        }

        private void ScheduleBlockedDisconnectButtonClick(AutoReconnectScreenKind kind, DateTime now)
        {
            _autoReconnectPendingScreenKind = kind;
            _autoReconnectLastCountdownSecond = AutoReconnectBlockedButtonWaitSeconds;
            _autoReconnectStage = AutoReconnectStage.WaitForDisconnectButtonUnlock;
            _nextAutoReconnectActionAtUtc = now.AddSeconds(AutoReconnectBlockedButtonWaitSeconds);
            string action = kind == AutoReconnectScreenKind.ReconnectChoice
                ? "Reconnect"
                : "Back to Server List";
            UpdateAutoReconnectStatus(
                $"Wykryto ekran rozłączenia. Czekam {AutoReconnectBlockedButtonWaitSeconds} s na odblokowanie przycisku {action}...",
                "Orange");
        }

        private bool TryClickClientPoint(double xRatio, double? yRatio, int? bottomOffset)
        {
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT rect))
                return false;
            var clientArea = new Drawing.Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            double y = yRatio ?? Math.Clamp((clientArea.Height - (bottomOffset ?? 0)) / (double)Math.Max(1, clientArea.Height), 0.0, 1.0);
            ClickClientPoint(clientArea, xRatio, y);
            return true;
        }

        private void ClickClientPoint(Drawing.Rectangle clientArea, double xRatio, double yRatio)
        {
            int x = clientArea.Left + (int)Math.Round(clientArea.Width * Math.Clamp(xRatio, 0.0, 1.0));
            int y = clientArea.Top + (int)Math.Round(clientArea.Height * Math.Clamp(yRatio, 0.0, 1.0));
            NativeInput.SetCursorPosition(x, y);
            SendMouseClick(leftButton: true, holdPulseMode: false);
        }

        private bool TryClickHomeMenuSlot(int oneBasedSlot, int rows, int columns)
        {
            if (!TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT rect))
                return false;

            const int guiScale = 3; // wymagane przez moduł GUI Scale: Large
            const int guiWidth = 176;
            rows = Math.Clamp(rows, 1, 6);
            columns = Math.Clamp(columns, 1, 9);
            int guiHeight = 114 + rows * 18;
            oneBasedSlot = Math.Clamp(oneBasedSlot, 1, rows * columns);
            int slot = oneBasedSlot - 1;
            int column = slot % columns;
            int row = slot / columns;
            int clientWidth = rect.Right - rect.Left;
            int clientHeight = rect.Bottom - rect.Top;
            int guiLeft = (clientWidth - guiWidth * guiScale) / 2;
            int guiTop = (clientHeight - guiHeight * guiScale) / 2;
            if (guiLeft < 0 || guiTop < 0)
                return false;

            int gridLeft = columns == 9 ? 8 : Math.Max(8, (guiWidth - columns * 18) / 2);
            int x = rect.Left + guiLeft + (gridLeft + column * 18 + 8) * guiScale;
            int y = rect.Top + guiTop + (18 + row * 18 + 8) * guiScale;
            NativeInput.SetCursorPosition(x, y);
            SendMouseClick(leftButton: true, holdPulseMode: false);
            return true;
        }

        private async void BtnAutoReconnectRecognize_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRunAutoReconnectTest(out string error))
            {
                UpdateAutoReconnectStatus(error, "Red");
                return;
            }
            UpdateAutoReconnectStatus("Rozpoznanie za 3 sekundy — przełącz fokus na Minecrafta.", "Orange");
            await Task.Delay(3000);
            if (_targetGameWindowHandle == IntPtr.Zero || GetForegroundWindow() != _targetGameWindowHandle)
            {
                UpdateAutoReconnectStatus("Test anulowany: Minecraft nie był aktywnym oknem po odliczaniu.", "Red");
                return;
            }
            if (!EnsureF3TesseractEngine() || !TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out _))
            {
                UpdateAutoReconnectStatus("Nie udało się uruchomić OCR lub przechwycić obrazu gry.", "Red");
                return;
            }

            using (bitmap)
            {
                if (InventoryMarkerDetector.TryDetect(bitmap!, null, null, out InventoryMarkerDetection inventory))
                {
                    UpdateAutoReconnectStatus($"Rozpoznano: otwarte EQ (GUI x{inventory.Layout.Scale}).", "Green");
                    return;
                }
                string text = await Task.Run(() => RunOcrOnBitmap(bitmap!, TesseractPageSegMode.SparseText));
                AutoReconnectScreenKind kind = ClassifyAutoReconnectScreen(text);
                string preview = Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
                if (preview.Length > 180)
                    preview = preview.Substring(0, 180) + "...";
                UpdateAutoReconnectStatus($"Rozpoznano: {GetAutoReconnectScreenLabel(kind)}. OCR: {preview}", kind == AutoReconnectScreenKind.Unknown ? "Orange" : "Green");
            }
        }

        private static string GetAutoReconnectScreenLabel(AutoReconnectScreenKind kind)
        {
            return kind switch
            {
                AutoReconnectScreenKind.Inventory => "ekwipunek",
                AutoReconnectScreenKind.PlayerDead => "śmierć / Respawn",
                AutoReconnectScreenKind.Disconnected => "rozłączenie",
                AutoReconnectScreenKind.ReconnectChoice => "przycisk Reconnect",
                AutoReconnectScreenKind.ServerList => "lista serwerów",
                AutoReconnectScreenKind.DirectConnect => "Direct Connect",
                AutoReconnectScreenKind.Banned => "ban / blokada",
                AutoReconnectScreenKind.AlreadyConnected => "stare połączenie proxy",
                _ => "nieznany ekran"
            };
        }

        private void BtnAutoReconnectCheckInventory_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRunAutoReconnectTest(out string error))
            {
                UpdateAutoReconnectStatus(error, "Red");
                return;
            }
            BeginAutoReconnectHealthCheck(manual: true, inventoryOnly: true);
        }

        private void BtnAutoReconnectTestHome_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRunAutoReconnectTest(out string error))
            {
                UpdateAutoReconnectStatus(error, "Red");
                return;
            }
            if (_autoReconnectStage != AutoReconnectStage.None)
                return;
            ReadAutoReconnectSettingsFromUi();
            ConfigureActiveAutoReconnectHome(missingPickaxeRecovery: false);
            _autoReconnectManualRun = true;
            CaptureMiningModeForAutoReconnect();
            PauseMiningForAutoReconnect();
            RecordAutomationLogEvent(
                MiningLogEventTypes.AutoReconnectStarted,
                MiningLogStatuses.Completed,
                $"Uruchomiono ręczny test powrotu do home. Komenda: {_autoReconnectActiveHomeCommand}; GUI: {(_autoReconnectActiveHomeHasGui ? "tak" : "nie")}.");
            _autoReconnectStage = AutoReconnectStage.OpenHomeChat;
            _nextAutoReconnectActionAtUtc = DateTime.UtcNow;
            UpdateAutoReconnectStatus("Test /home uruchomiony.", "Orange");
        }

        private void BtnAutoReconnectStart_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRunAutoReconnectTest(out string error))
            {
                UpdateAutoReconnectStatus(error, "Red");
                return;
            }
            BeginFullAutoReconnect(manual: true);
        }

        private void BtnAutoReconnectStop_Click(object sender, RoutedEventArgs e)
        {
            StopAutoReconnect("Auto reconnect zatrzymany ręcznie.", resumeMining: true, warning: false);
        }

        private void ChkAutoReconnectEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;
            if (ChkAutoReconnectEnabled.IsChecked != true
                && !_emergencyReconnectActive
                && _autoReconnectStage != AutoReconnectStage.None)
                StopAutoReconnect("Auto reconnect wyłączony.", resumeMining: true, warning: false);
            _nextAutoReconnectHealthCheckAtUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(ParseNonNegativeInt(TxtAutoReconnectWatchdogSeconds?.Text ?? string.Empty), 15, 3600));
            UpdateEnabledStates();
            MarkDirty();
        }

        private bool IsPeriodicAutoReconnectEnabled()
        {
            return ChkEmergencyDamageSoundEnabled?.IsChecked == true
                && ChkAutoReconnectEnabled?.IsChecked == true;
        }

        private void AutoReconnectSetting_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;
            MarkDirty();
        }

        private bool HasCustomCaptureAreaConfigured()
        {
            return _settings.TestCustomCaptureWidth >= MinimumCaptureSelectionSize && _settings.TestCustomCaptureHeight >= MinimumCaptureSelectionSize;
        }

        private void UpdateTestCustomCaptureAreaInfo()
        {
            if (TxtTestCustomCaptureAreaInfo == null)
                return;

            bool hasArea = HasCustomCaptureAreaConfigured();
            TxtTestCustomCaptureAreaInfo.Text = hasArea
                ? $"Obszar OCR: x={_settings.TestCustomCaptureX}, y={_settings.TestCustomCaptureY}, {_settings.TestCustomCaptureWidth}x{_settings.TestCustomCaptureHeight} (względem okna gry)."
                : "Brak obszaru OCR. Kliknij \"Zaznacz obszar\".";
        }

        private bool HasTestAutoFishingAreaConfigured()
        {
            return _settings.TestAutoFishingCaptureWidth >= MinimumCaptureSelectionSize
                && _settings.TestAutoFishingCaptureHeight >= MinimumCaptureSelectionSize;
        }

        private void UpdateTestAutoFishingAreaInfo()
        {
            if (TxtTestAutoFishingAreaInfo == null)
                return;

            TxtTestAutoFishingAreaInfo.Text = HasTestAutoFishingAreaConfigured()
                ? $"Obszar spławika: x={_settings.TestAutoFishingCaptureX}, y={_settings.TestAutoFishingCaptureY}, {_settings.TestAutoFishingCaptureWidth}x{_settings.TestAutoFishingCaptureHeight} (względem okna gry)."
                : "Brak obszaru monitoringu spławika. Kliknij \"Zaznacz obszar\".";
            RefreshTestAutoFishingPreview(DateTime.UtcNow, force: true);
        }

        private void RefreshTestAutoFishingPreview(DateTime now, bool force = false)
        {
            if (ImgTestAutoFishingPreview == null || TxtTestAutoFishingPreviewInfo == null)
                return;
            // Do not capture and convert preview frames while the app is hidden.
            // That work runs on the UI thread and used to stall dispatcher-based input.
            if (!IsVisible || _isMinimizedToTray || MainTabControl?.SelectedIndex != 3)
                return;
            if (!HasTestAutoFishingAreaConfigured())
            {
                ImgTestAutoFishingPreview.Source = null;
                TxtTestAutoFishingPreviewInfo.Text = "Brak zaznaczonego obszaru.";
                return;
            }
            if (_targetGameWindowHandle == IntPtr.Zero || !TryGetTestAutoFishingCaptureArea(_targetGameWindowHandle, out Drawing.Rectangle captureArea))
            {
                TxtTestAutoFishingPreviewInfo.Text = "Podgląd niedostępny. Uruchom i wybierz okno gry.";
                return;
            }
            if (!force && now < _nextTestAutoFishingPreviewAtUtc)
                return;

            _nextTestAutoFishingPreviewAtUtc = now.AddMilliseconds(TestAutoFishingPreviewIntervalMs);
            TxtTestAutoFishingPreviewInfo.Text = $"x={_settings.TestAutoFishingCaptureX}, y={_settings.TestAutoFishingCaptureY}, {_settings.TestAutoFishingCaptureWidth}x{_settings.TestAutoFishingCaptureHeight}";
            if (TryCaptureScreenRectBitmapSource(captureArea, out BitmapSource? bitmapSource))
                ImgTestAutoFishingPreview.Source = bitmapSource;
        }

        private static bool TryCaptureScreenRectBitmapSource(Drawing.Rectangle screenRect, out BitmapSource? bitmapSource)
        {
            bitmapSource = null;
            if (screenRect.Width < 2 || screenRect.Height < 2)
                return false;

            try
            {
                using Drawing.Bitmap bitmap = new Drawing.Bitmap(screenRect.Width, screenRect.Height, DrawingImaging.PixelFormat.Format32bppArgb);
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0, screenRect.Size, Drawing.CopyPixelOperation.SourceCopy);

                IntPtr hbitmap = bitmap.GetHbitmap();
                try
                {
                    BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    bitmapSource = source;
                    return true;
                }
                finally
                {
                    DeleteObject(hbitmap);
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateTestAutoFishingStatusLabel()
        {
            if (TxtTestAutoFishingState == null)
                return;
            if (!_testAutoFishingRuntimeEnabled)
            {
                TxtTestAutoFishingState.Text = "-";
                return;
            }
            if (_isPausedByCursorVisibility)
            {
                TxtTestAutoFishingState.Text = "pauza kursora: skan i PPM zatrzymane";
                return;
            }
            if (_testAutoFishingRepairStage != TestAutoFishingRepairStage.None)
            {
                TxtTestAutoFishingState.Text = "komenda: " + GetTestAutoFishingRepairStageLabel(_testAutoFishingRepairStage);
                return;
            }
            if (_testAutoFishingRecastAfterRepairPending)
            {
                TxtTestAutoFishingState.Text = "komenda: ponowne zarzucenie...";
                return;
            }
            if (_testAutoFishingAwaitSecondClick)
            {
                TxtTestAutoFishingState.Text = "PPM #2 (ponowne zarzucenie)";
                return;
            }
            if (_testAutoFishingWaitingForCastRegistration)
            {
                int remainingMs = Math.Max(0, (int)Math.Ceiling((_nextTestAutoFishingActionAtUtc - DateTime.UtcNow).TotalMilliseconds));
                TxtTestAutoFishingState.Text = $"po rzucie: rejestracja spławika za {remainingMs}ms";
                return;
            }
            if (_testAutoFishingBaselineReady && DateTime.UtcNow < _testAutoFishingBaselineArmedAtUtc)
            {
                int remainingMs = Math.Max(0, (int)Math.Ceiling((_testAutoFishingBaselineArmedAtUtc - DateTime.UtcNow).TotalMilliseconds));
                TxtTestAutoFishingState.Text = $"stabilizacja po rzucie... {remainingMs}ms";
                return;
            }
            if (_testAutoFishingBaselineReady && !double.IsNaN(_testAutoFishingLastDetectedBobberY))
            {
                TxtTestAutoFishingState.Text = $"monitoring (Y={(int)Math.Round(_testAutoFishingLastDetectedBobberY)}, baza={(int)Math.Round(_testAutoFishingBaselineBobberY)})";
                return;
            }
            if (_testAutoFishingMissedDetections > 0)
            {
                if (_testAutoFishingNoBobberSinceAtUtc != DateTime.MinValue)
                {
                    double remainingMs = TestAutoFishingNoBobberRecastMs - (DateTime.UtcNow - _testAutoFishingNoBobberSinceAtUtc).TotalMilliseconds;
                    TxtTestAutoFishingState.Text = $"szukanie spławika... ({_testAutoFishingMissedDetections}) | PPM za {Math.Max(0, (int)Math.Ceiling(remainingMs / 1000.0))}s";
                }
                else
                {
                    TxtTestAutoFishingState.Text = $"szukanie spławika... ({_testAutoFishingMissedDetections})";
                }
                return;
            }
            if (_nextTestAutoFishingRepairAtUtc != DateTime.MaxValue)
            {
                int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextTestAutoFishingRepairAtUtc - DateTime.UtcNow).TotalSeconds));
                if (remainingSeconds > 0)
                {
                    TxtTestAutoFishingState.Text = $"szukanie spławika... | /repair za {remainingSeconds}s";
                    return;
                }
            }
            TxtTestAutoFishingState.Text = "szukanie spławika...";
        }

        private string GetConfiguredTestAutoFishingRepairCommand()
        {
            string command = TxtTestAutoFishingRepairCommand?.Text?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(command) ? command : (_settings.TestAutoFishingRepairCommand ?? string.Empty).Trim();
        }

        private int GetConfiguredTestAutoFishingRepairIntervalSeconds()
        {
            if (TxtTestAutoFishingRepairEverySeconds != null)
                return Math.Clamp(ParseNonNegativeInt(TxtTestAutoFishingRepairEverySeconds.Text), 0, TestAutoFishingRepairIntervalMaxSeconds);
            return Math.Clamp(_settings.TestAutoFishingRepairEverySeconds, 0, TestAutoFishingRepairIntervalMaxSeconds);
        }

        private bool HasConfiguredTestAutoFishingRepair()
        {
            return GetConfiguredTestAutoFishingRepairIntervalSeconds() > 0
                && !string.IsNullOrWhiteSpace(GetConfiguredTestAutoFishingRepairCommand());
        }

        private void ResetTestAutoFishingCatchStats()
        {
            _testAutoFishingCaughtCount = 0;
            _testAutoFishingLastCatchAtUtc = DateTime.MinValue;
        }

        private void RegisterTestAutoFishingCatch(DateTime now)
        {
            _testAutoFishingCaughtCount++;
            _testAutoFishingLastCatchAtUtc = now;
        }

        private static string GetTestAutoFishingRepairStageLabel(TestAutoFishingRepairStage stage)
        {
            return stage switch
            {
                TestAutoFishingRepairStage.OpenChat => "otwieranie chatu",
                TestAutoFishingRepairStage.TypeCommand => "wpisywanie komendy",
                TestAutoFishingRepairStage.SubmitCommand => "wysyłanie komendy",
                _ => "oczekiwanie"
            };
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
                BindTarget.FastUpExit => "Szybkie wyjście do góry",
                BindTarget.TestCaptureArea => "Experimental OCR (obszar)",
                BindTarget.AutoArmor => "Auto zbroja",
                BindTarget.AutoWater => "AutoWater",
                BindTarget.TestAutoFishing => "Auto łowienie wędką",
                BindTarget.TestAutoFishingCaptureArea => "Auto łowienie (zaznaczanie obszaru)",
                BindTarget.ChatOpen => "otwieranie chatu",
                BindTarget.DropItem => "wyrzucanie przedmiotu",
                _ => "bind"
            };
        }

        private static string GetSaveButtonBaseContent(BindTarget target)
        {
            return target is BindTarget.Kopacz533 or BindTarget.Kopacz633 ? "Zapisz klawisz" : "Zapisz";
        }

        private static bool IsMinecraftControlKeyTarget(BindTarget target)
        {
            return target is BindTarget.ChatOpen or BindTarget.DropItem;
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
                BindTarget.FastUpExit => BtnTestFastUpExitBind,
                BindTarget.TestCaptureArea => BtnTestCustomCaptureBind,
                BindTarget.AutoArmor => BtnAutoArmorBind,
                BindTarget.AutoWater => BtnAutoWaterBind,
                BindTarget.TestAutoFishing => BtnTestAutoFishingBind,
                BindTarget.TestAutoFishingCaptureArea => BtnTestAutoFishingCaptureBind,
                BindTarget.ChatOpen => BtnChatOpenKeySave,
                BindTarget.DropItem => BtnDropItemKeySave,
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
                BindTarget.FastUpExit => TxtTestFastUpExitBind,
                BindTarget.TestCaptureArea => TxtTestCustomCaptureBind,
                BindTarget.AutoArmor => TxtAutoArmorBind,
                BindTarget.AutoWater => TxtAutoWaterBind,
                BindTarget.TestAutoFishing => TxtTestAutoFishingBind,
                BindTarget.TestAutoFishingCaptureArea => TxtTestAutoFishingCaptureBind,
                BindTarget.ChatOpen => TxtChatOpenKey,
                BindTarget.DropItem => TxtDropItemKey,
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
            yield return BindTarget.FastUpExit;
            yield return BindTarget.TestCaptureArea;
            yield return BindTarget.AutoArmor;
            yield return BindTarget.AutoWater;
            yield return BindTarget.TestAutoFishing;
            yield return BindTarget.TestAutoFishingCaptureArea;
            yield return BindTarget.ChatOpen;
            yield return BindTarget.DropItem;
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
                BindTarget.FastUpExit => "core:fast-up-exit",
                BindTarget.TestCaptureArea => "core:test-capture",
                BindTarget.AutoArmor => "core:auto-armor",
                BindTarget.AutoWater => "core:auto-water",
                BindTarget.TestAutoFishing => "core:test-auto-fishing",
                BindTarget.TestAutoFishingCaptureArea => "core:test-auto-fishing-capture",
                BindTarget.ChatOpen => "minecraft-control:chat-open",
                BindTarget.DropItem => "minecraft-control:drop-item",
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

        private bool CanShareAutoClickerBind(string requestedOwnerId, BindTarget candidateTarget)
        {
            bool isAutoLeftAndRightPair =
                string.Equals(requestedOwnerId, GetBindOwnerId(BindTarget.AutoLeft), StringComparison.OrdinalIgnoreCase)
                    && candidateTarget == BindTarget.AutoRight;
            bool isAutoRightAndLeftPair =
                string.Equals(requestedOwnerId, GetBindOwnerId(BindTarget.AutoRight), StringComparison.OrdinalIgnoreCase)
                    && candidateTarget == BindTarget.AutoLeft;

            bool autoLeftUsesHoldBasedActivation =
                ChkAutoLeftComboMode.IsChecked == true
                || ChkAutoLeftHoldBindMode.IsChecked == true;
            bool autoRightUsesShareableActivation = ChkAutoRightComboMode.IsChecked == true;

            return (isAutoLeftAndRightPair || isAutoRightAndLeftPair)
                && autoLeftUsesHoldBasedActivation
                && autoRightUsesShareableActivation;
        }

        private bool AutoClickersUseSharedBind()
        {
            return !string.IsNullOrWhiteSpace(TxtAutoLeftKey.Text)
                && IsSameBindKey(TxtAutoLeftKey.Text, TxtAutoRightKey.Text);
        }

        private void ShowSharedAutoClickerBindModeConflict()
        {
            const string message = "AUTO LPM i AUTO PPM używają wspólnego bindu. Najpierw usuń albo zmień bind w jednym z makr.";
            UpdateStatusBar(message, "Orange");

            var dialog = BindConflictDialogWindow.CreateSharedAutoClickerModeConflict(TxtAutoLeftKey.Text);
            dialog.Owner = this;
            dialog.ShowDialog();
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
                BindTarget.FastUpExit,
                BindTarget.TestCaptureArea,
                BindTarget.AutoArmor,
                BindTarget.AutoWater,
                BindTarget.TestAutoFishing,
                BindTarget.TestAutoFishingCaptureArea
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

                if (CanShareAutoClickerBind(ownerId, target))
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
            string normalizedKey = string.IsNullOrWhiteSpace(keyText) ? "?" : keyText.Trim();
            string conflictOwner = string.IsNullOrWhiteSpace(conflictOwnerLabel) ? "innej funkcji" : conflictOwnerLabel.Trim();
            string requestedOwner = string.IsNullOrWhiteSpace(requestedOwnerLabel) ? "tej funkcji" : requestedOwnerLabel.Trim();

            string shortMessage = $"Klawisz {normalizedKey} jest już przypisany do: {conflictOwner}.";
            UpdateStatusBar(shortMessage, "Orange");
            var dialog = new BindConflictDialogWindow(requestedOwner, normalizedKey, conflictOwner)
            {
                Owner = this
            };
            dialog.ShowDialog();
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
            RefreshBindSaveButton(BindTarget.FastUpExit);
            RefreshBindSaveButton(BindTarget.TestCaptureArea);
            RefreshBindSaveButton(BindTarget.AutoArmor);
            RefreshBindSaveButton(BindTarget.AutoWater);
            RefreshBindSaveButton(BindTarget.TestAutoFishing);
            RefreshBindSaveButton(BindTarget.TestAutoFishingCaptureArea);
            RefreshBindSaveButton(BindTarget.ChatOpen);
            RefreshBindSaveButton(BindTarget.DropItem);
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
                int remainingSeconds = GetKopaczCommandRemainingSeconds(InventoryCleanupOwner.Kopacz533, now);
                AppendInline(TxtKopacz533Status, GetStatusCommandPreview(nextCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz533Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz533Status, $"{remainingSeconds}s", TileTimeBrush, FontWeights.SemiBold);
                if (IsKopaczCommandCountdownPaused(InventoryCleanupOwner.Kopacz533))
                    AppendInline(TxtKopacz533Status, " (pauza Auto EQ)", TilePauseBrush, FontWeights.SemiBold);
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
                int remainingSeconds = GetKopaczCommandRemainingSeconds(InventoryCleanupOwner.Kopacz633, now);
                AppendInline(TxtKopacz633Status, GetStatusCommandPreview(nextCommand), TileValueBrush, FontWeights.SemiBold);
                AppendInline(TxtKopacz633Status, " za ", TileLabelBrush);
                AppendInline(TxtKopacz633Status, $"{remainingSeconds}s", TileTimeBrush, FontWeights.SemiBold);
                if (IsKopaczCommandCountdownPaused(InventoryCleanupOwner.Kopacz633))
                    AppendInline(TxtKopacz633Status, " (pauza Auto EQ)", TilePauseBrush, FontWeights.SemiBold);
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

            if (_overlayHud == null)
            {
                var overlay = new OverlayHudWindow();
                overlay.Closed += (_, __) =>
                {
                    if (ReferenceEquals(_overlayHud, overlay))
                        _overlayHud = null;
                };
                _overlayHud = overlay;
            }

            _overlayHud.SetOwnerWindowHandle(_targetGameWindowHandle);
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
            bool inventoryCleanupSelected = ChkInventoryCleanupEnabled.IsChecked == true;
            bool testEntitiesModeSelected = ChkTestEntitiesEnabled.IsChecked == true;
            bool fastUpModeSelected = ChkTestFastUpExitEnabled.IsChecked == true;
            bool autoArmorModeSelected = ChkAutoArmorEnabled.IsChecked == true;
            bool autoWaterModeSelected = ChkAutoWaterEnabled.IsChecked == true;
            bool autoFishingModeSelected = ChkTestAutoFishingEnabled.IsChecked == true;
            bool emergencyProtectionSelected = ChkEmergencyDamageSoundEnabled?.IsChecked == true;

            if (_isPausedByCursorVisibility)
            {
                entries.Add(new OverlayHudEntry(
                    "PAUZA (KURSOR)",
                    "Makra klikające są tymczasowo wstrzymane.",
                    OverlayHudTone.Warning));
            }

            bool emergencyProtectionArmedForActiveMiner = emergencyProtectionSelected
                && (_kopacz533RuntimeEnabled || _kopacz633RuntimeEnabled);
            if (emergencyProtectionArmedForActiveMiner
                || _emergencyReconnectActive
                || _emergencyDamageSoundManualTestActive
                || _autoReconnectStage != AutoReconnectStage.None)
            {
                entries.Add(BuildEmergencyProtectionOverlayEntry(now));
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

            if (inventoryCleanupSelected
                && (kop533Visible || kop633Visible || _inventoryCleanupStage != InventoryCleanupStage.None))
            {
                entries.Add(BuildInventoryCleanupOverlayEntry(now));
            }

            if (fastUpModeSelected && _testFastUpExitRuntimeEnabled)
                entries.Add(BuildFastUpExitOverlayEntry());

            if (autoArmorModeSelected && (IsAutoArmorRunning || _autoArmorCalibrationPending))
                entries.Add(BuildAutoArmorOverlayEntry());

            if (autoWaterModeSelected
                && (_autoWaterStage != AutoWaterStage.None || _autoWaterCalibrationPending || _autoWaterRecognitionTestPending))
            {
                entries.Add(BuildAutoWaterOverlayEntry(now));
            }

            if (autoFishingModeSelected && _testAutoFishingRuntimeEnabled)
                entries.Add(BuildTestAutoFishingOverlayEntry(now));

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

        private OverlayHudEntry BuildEmergencyProtectionOverlayEntry(DateTime now)
        {
            AutoReconnectServerProfile? profile = GetSelectedAutoReconnectServerProfile();
            string profileName = profile?.Name?.Trim() ?? "brak profilu";
            string owner = _emergencyReconnectResumeKopacz533 || _kopacz533RuntimeEnabled
                ? "Kopacz 5/3/3"
                : _emergencyReconnectResumeKopacz633 || _kopacz633RuntimeEnabled
                    ? "Kopacz 6/3/3"
                    : "oczekiwanie na Kopacza";

            string state;
            string nextAction;
            string detail;
            OverlayHudTone tone;
            if (_emergencyDamageSoundHandlingAlarm)
            {
                state = "ALARM — WYJŚCIE Z SERWERA";
                nextAction = "Odliczanie do reconnectu";
                detail = "Zatrzymywanie Kopacza i wykonywanie ESC → Disconnect";
                tone = OverlayHudTone.Warning;
            }
            else if (_emergencyDamageSoundManualTestActive)
            {
                int seconds = Math.Max(0, (int)Math.Ceiling((_emergencyDamageSoundManualTestUntilUtc - now).TotalSeconds));
                state = "TEST NASŁUCHU";
                nextAction = "Po alarmie tylko wpis w logach";
                detail = $"Pozostało: {seconds} s • bez wychodzenia z serwera";
                tone = OverlayHudTone.Warning;
            }
            else if (_autoReconnectStage != AutoReconnectStage.None)
            {
                state = GetAutoReconnectOverlayStageLabel(_autoReconnectStage);
                nextAction = GetAutoReconnectOverlayNextAction(
                    _autoReconnectStage,
                    _emergencyReconnectShutdownAfterHome);
                if (_autoReconnectStage == AutoReconnectStage.EmergencyWaitBeforeReconnect)
                {
                    int seconds = Math.Max(0, (int)Math.Ceiling((_nextAutoReconnectActionAtUtc - now).TotalSeconds));
                    detail = $"Reconnect za: {seconds} s • nasłuch nadal aktywny";
                }
                else if (_autoReconnectStage == AutoReconnectStage.WaitForDisconnectButtonUnlock)
                {
                    int seconds = Math.Max(0, (int)Math.Ceiling((_nextAutoReconnectActionAtUtc - now).TotalSeconds));
                    detail = $"Kliknięcie możliwe za: {seconds} s";
                }
                else if (_autoReconnectStage == AutoReconnectStage.WaitForTeleport)
                {
                    int seconds = Math.Max(0, (int)Math.Ceiling((_nextAutoReconnectActionAtUtc - now).TotalSeconds));
                    detail = _emergencyReconnectShutdownAfterHome
                        ? $"Teleport i bezpieczne zamknięcie za: {seconds} s"
                        : $"Teleport i wznowienie Kopacza za: {seconds} s";
                }
                else if (_emergencyReconnectShutdownAfterHome)
                {
                    detail = "Brak kilofa → awaryjny home → zamknięcie programu";
                }
                else
                {
                    detail = _emergencyReconnectActive
                        ? "Kilof → wybrany home → slot 1 → wznowienie Kopacza"
                        : "Trwa operacja Auto Reconnect";
                }
                tone = OverlayHudTone.Warning;
            }
            else if (_damageSoundDetector.IsRunning)
            {
                bool testMode = _settings.EmergencyDamageSoundTestMode;
                state = testMode ? "NASŁUCH TESTOWY" : "OCHRONA UZBROJONA";
                nextAction = testMode
                    ? "Po alarmie tylko komunikat i log"
                    : "Po alarmie zatrzymanie Kopacza i Disconnect";
                detail = testMode
                    ? "Alarm zostanie zapisany bez wyjścia z serwera"
                    : "Oczekiwanie na dźwięk obrażeń";
                tone = testMode ? OverlayHudTone.Warning : OverlayHudTone.Active;
            }
            else
            {
                state = "GOTOWA";
                nextAction = owner == "oczekiwanie na Kopacza"
                    ? "Uruchom Kopacza, aby uzbroić nasłuch"
                    : "Uruchomienie nasłuchu obrażeń";
                detail = owner == "oczekiwanie na Kopacza"
                    ? "Nasłuch uruchomi się razem z Kopaczem"
                    : "Uruchamianie nasłuchu obrażeń";
                tone = OverlayHudTone.Active;
            }

            string body =
                $"Teraz: {state}\n" +
                $"Następnie: {nextAction}\n" +
                $"Tryb: {owner} • Profil: {profileName}\n" +
                detail;
            string title = _emergencyReconnectActive || ChkEmergencyDamageSoundEnabled?.IsChecked == true
                ? "AWARYJNA OCHRONA KOPACZA"
                : "AUTO RECONNECT";
            return new OverlayHudEntry(title, body, tone, Emphasize: _emergencyReconnectActive);
        }

        private static string GetAutoReconnectOverlayStageLabel(AutoReconnectStage stage)
        {
            return stage switch
            {
                AutoReconnectStage.EmergencyWaitBeforeReconnect => "OCZEKIWANIE NA RECONNECT",
                AutoReconnectStage.AnalyzeScreen or AutoReconnectStage.WaitForScreenAnalysis => "ROZPOZNAWANIE EKRANU",
                AutoReconnectStage.WaitForDisconnectButtonUnlock => "OCZEKIWANIE NA PRZYCISK",
                AutoReconnectStage.WaitAfterScreenClick => "POWRÓT DO LISTY SERWERÓW",
                AutoReconnectStage.OpenDirectConnect or AutoReconnectStage.WaitForDirectConnect => "OTWIERANIE DIRECT CONNECT",
                AutoReconnectStage.EnterServerAddress => "WPISYWANIE ADRESU SERWERA",
                AutoReconnectStage.WaitForServerJoin => "DOŁĄCZANIE DO SERWERA",
                AutoReconnectStage.EmergencyOpenInventory => "OTWIERANIE EQ",
                AutoReconnectStage.EmergencyVerifyInventory => "KONTROLA DIAMENTOWEGO KILOFA",
                AutoReconnectStage.OpenHomeChat or AutoReconnectStage.TypeHomeCommand or AutoReconnectStage.SubmitHomeCommand => "WYSYŁANIE KOMENDY HOME",
                AutoReconnectStage.WaitForHomeMenu => "OCZEKIWANIE NA MENU HOME",
                AutoReconnectStage.ClickHomeSlot => "WYBÓR HOME",
                AutoReconnectStage.WaitForTeleport => "TELEPORT NA HOME",
                AutoReconnectStage.HealthOpenInventory or AutoReconnectStage.HealthVerifyInventory => "KONTROLA POŁĄCZENIA I EQ",
                AutoReconnectStage.OpenVerificationInventory or AutoReconnectStage.VerifyAfterTeleport => "WERYFIKACJA EQ PO TELEPORCIE",
                AutoReconnectStage.RetryDelay => "PONOWIENIE RECONNECTU",
                _ => "AUTO RECONNECT"
            };
        }

        private static string GetAutoReconnectOverlayNextAction(
            AutoReconnectStage stage,
            bool shutdownAfterHome)
        {
            return stage switch
            {
                AutoReconnectStage.EmergencyWaitBeforeReconnect => "Rozpoznanie ekranu i wejście przez Direct Connect",
                AutoReconnectStage.AnalyzeScreen or AutoReconnectStage.WaitForScreenAnalysis => "Kliknięcie właściwego przycisku reconnectu",
                AutoReconnectStage.WaitForDisconnectButtonUnlock => "Kliknięcie Reconnect lub Back to Server List",
                AutoReconnectStage.WaitAfterScreenClick => "Otwarcie Direct Connect",
                AutoReconnectStage.OpenDirectConnect or AutoReconnectStage.WaitForDirectConnect => "Wpisanie adresu serwera",
                AutoReconnectStage.EnterServerAddress => "Dołączenie do serwera",
                AutoReconnectStage.WaitForServerJoin => "Otwarcie EQ i kontrola diamentowego kilofa",
                AutoReconnectStage.EmergencyOpenInventory => "Odczyt znaczników EQ",
                AutoReconnectStage.EmergencyVerifyInventory => "Wybór właściwego home na podstawie kilofa",
                AutoReconnectStage.OpenHomeChat or AutoReconnectStage.TypeHomeCommand or AutoReconnectStage.SubmitHomeCommand => "Otwarcie menu home lub rozpoczęcie oczekiwania",
                AutoReconnectStage.WaitForHomeMenu => "Kliknięcie zapisanego slotu home",
                AutoReconnectStage.ClickHomeSlot => "Odliczanie czasu teleportacji",
                AutoReconnectStage.WaitForTeleport => shutdownAfterHome
                    ? "Bezpieczne zamknięcie Minecraft Helper"
                    : "Slot 1 i wznowienie Kopacza",
                AutoReconnectStage.HealthOpenInventory or AutoReconnectStage.HealthVerifyInventory => "Decyzja: kontynuacja albo odzyskanie połączenia",
                AutoReconnectStage.OpenVerificationInventory or AutoReconnectStage.VerifyAfterTeleport => "Zakończenie reconnectu albo ponowienie próby",
                AutoReconnectStage.RetryDelay => "Ponowne rozpoznanie ekranu",
                _ => "Oczekiwanie na kolejny krok"
            };
        }

        private OverlayHudEntry BuildTestEntitiesOverlayEntry()
        {
            string rawValue = TxtTestLiveEntities?.Text?.Trim() ?? string.Empty;
            bool hasValue = !string.IsNullOrWhiteSpace(rawValue) && !string.Equals(rawValue, "-", StringComparison.Ordinal);
            string value = hasValue ? rawValue : "Brak danych";
            string body = $"Encje (E): {value}";

            return new OverlayHudEntry(
                "WYKRYWANIE ENCJI F3",
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
            string activationMode = ChkAutoLeftHoldBindMode.IsChecked == true
                ? "Trzymanie bindu"
                : ChkAutoLeftComboMode.IsChecked == true
                    ? "Bind + LPM"
                    : "Przełącznik";
            string dabState = ChkAutoLeftDabMode.IsChecked != true
                ? "OFF"
                : _autoLeftDabHolding
                    ? "ON (trzymane O)"
                    : "PAUZA";
            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"Tryb: {activationMode}\n" +
                $"CPS: {min}-{max}\n" +
                $"DAB (O): {dabState}";

            return new OverlayHudEntry("AUTO LPM", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildAutoRightOverlayEntry()
        {
            int min = ParseNonNegativeInt(TxtAutoRightMinCps.Text);
            int max = ParseNonNegativeInt(TxtAutoRightMaxCps.Text);
            string bindLabel = GetConfiguredBindLabel(TxtAutoRightKey.Text);
            string runtimeState = GetRuntimeStateLabel(_autoRightRuntimeEnabled);
            string activationMode = ChkAutoRightHoldBindMode.IsChecked == true
                ? "Trzymanie bindu"
                : ChkAutoRightComboMode.IsChecked == true
                    ? "Bind + PPM"
                    : "Przełącznik";
            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"Tryb: {activationMode}\n" +
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
            string startedLine = _kopacz533RuntimeStartedAtUtc == DateTime.MinValue
                ? "Start: -"
                : $"Start: {_kopacz533RuntimeStartedAtUtc.ToLocalTime():HH:mm:ss} | Czas: {FormatRuntimeDuration(elapsedSeconds)}";

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState} | Etap: {stageLabel}\n" +
                $"{startedLine}\n" +
                $"Historia: EQ {_latestMiningLogSummary.InventorySessions:N0} | CX {_latestMiningLogSummary.CobbleXCreated:N0} | {_latestMiningLogSummary.DiscardedItems:N0} wyrzuconych\n" +
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
            int elapsedSeconds = Math.Max(0, (int)Math.Floor((now - _kopacz633RuntimeStartedAtUtc).TotalSeconds));
            string startedLine = _kopacz633RuntimeStartedAtUtc == DateTime.MinValue
                ? "Start: -"
                : $"Start: {_kopacz633RuntimeStartedAtUtc.ToLocalTime():HH:mm:ss} | Czas: {FormatRuntimeDuration(elapsedSeconds)}";

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
                $"{startedLine}\n" +
                $"{movementLabel}\n" +
                $"Historia: EQ {_latestMiningLogSummary.InventorySessions:N0} | CX {_latestMiningLogSummary.CobbleXCreated:N0} | {_latestMiningLogSummary.DiscardedItems:N0} wyrzuconych\n" +
                commandLine;

            return new OverlayHudEntry("KOPACZ 6/3/3", body, OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildInventoryCleanupOverlayEntry(DateTime now)
        {
            int intervalSeconds = GetConfiguredInventoryCleanupIntervalSeconds();
            int selectedSlots = GetSelectedInventoryCleanupSlots().Count;
            int selectedItemTypes = GetSelectedInventoryCleanupItemTypes().Count;
            bool discardEverythingExceptCobblestone = ChkInventoryCleanupAllItemTypes.IsChecked == true;
            string state;
            string progressLine;

            switch (_inventoryCleanupStage)
            {
                case InventoryCleanupStage.ReturnToMiningStart:
                    state = "Powrót na start przed Auto EQ";
                    progressLine = CbKopacz633Direction.SelectedIndex == 2
                        ? "Trzymanie pełnego A + S"
                        : "Trzymanie pełnego A";
                    break;

                case InventoryCleanupStage.OpenInventory:
                    state = "Otwieranie EQ";
                    progressLine = _inventoryCleanupOwner == InventoryCleanupOwner.Kopacz633
                        ? "Pozycja startowa przywrócona"
                        : "Przygotowanie skanowania";
                    break;

                case InventoryCleanupStage.WaitForInventory:
                    state = "Otwieranie i skanowanie EQ";
                    progressLine = $"Próba wykrywania: {Math.Max(1, _inventoryCleanupDetectionAttempts + 1)}/{InventoryCleanupMaximumDetectionAttempts}";
                    break;

                case InventoryCleanupStage.MoveToSlot:
                case InventoryCleanupStage.PressDropModifier:
                case InventoryCleanupStage.PressDropKey:
                case InventoryCleanupStage.ReleaseDropKeys:
                    state = "Wyrzucanie pełnych stosów";
                    int currentTarget = _inventoryCleanupTargets.Count == 0
                        ? 0
                        : Math.Min(_inventoryCleanupTargetIndex + 1, _inventoryCleanupTargets.Count);
                    progressLine = $"Postęp: {currentTarget}/{_inventoryCleanupTargets.Count}";
                    break;

                case InventoryCleanupStage.CloseInventory:
                    state = "Zamykanie EQ";
                    progressLine = $"Wykryte stosy: {_inventoryCleanupTargets.Count}";
                    break;

                case InventoryCleanupStage.OpenCobbleXChat:
                case InventoryCleanupStage.TypeCobbleXCommand:
                case InventoryCleanupStage.SubmitCobbleXCommand:
                    state = "Tworzenie CobbleX";
                    progressLine = $"Cobble 64: {_inventoryCleanupFullCobblestoneStacks}/{GetConfiguredCobbleXRequiredStacks()} | komenda: {_inventoryCleanupPendingCobbleXCommand}";
                    break;

                case InventoryCleanupStage.SelectFoodSlot:
                case InventoryCleanupStage.StartEating:
                case InventoryCleanupStage.StopEatingAndRestoreTool:
                    state = "Jedzenie po Auto EQ";
                    progressLine = "Slot 2 | PPM 4 s | powrót na slot 1";
                    break;

                case InventoryCleanupStage.ResumeMining:
                    state = "Wznawianie kopania";
                    progressLine = $"Wyrzucone stosy: {_inventoryCleanupTargets.Count}";
                    break;

                default:
                    state = "Czekanie";
                    if (_nextInventoryCleanupAtUtc == DateTime.MaxValue)
                    {
                        progressLine = "Następny skan: po uruchomieniu kopacza";
                    }
                    else
                    {
                        int remainingSeconds = Math.Max(0, (int)Math.Ceiling((_nextInventoryCleanupAtUtc - now).TotalSeconds));
                        progressLine = $"Następny skan: za {remainingSeconds}s";
                    }
                    break;
            }

            string cobbleXLine = ChkCobbleXEnabled.IsChecked == true
                ? $"CobbleX: ON | Cobble 64: {_inventoryCleanupLastFullCobblestoneStacks}/{GetConfiguredCobbleXRequiredStacks()} | Komenda: {GetConfiguredCobbleXCommand()}"
                : "CobbleX: OFF";
            string eatingLine = ChkInventoryCleanupEatAfterCleanup.IsChecked == true
                ? "Jedzenie: ON | slot 2 | PPM 4 s | powrót na slot 1"
                : "Jedzenie: OFF";
            string timingLine;
            if (_inventoryCleanupStage != InventoryCleanupStage.None && _inventoryCleanupOpenedAtUtc != DateTime.MinValue)
            {
                int cycleSeconds = Math.Max(0, (int)Math.Floor((now - _inventoryCleanupOpenedAtUtc).TotalSeconds));
                timingLine = $"Bieżące EQ: {_inventoryCleanupOpenedAtUtc.ToLocalTime():HH:mm:ss} | Czas: {FormatRuntimeDuration(cycleSeconds)}";
            }
            else if (_latestMiningLogSummary.LastActivity.HasValue)
            {
                int agoSeconds = Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - _latestMiningLogSummary.LastActivity.Value.ToUniversalTime()).TotalSeconds));
                timingLine = $"Ostatnie EQ: {_latestMiningLogSummary.LastActivity.Value.LocalDateTime:HH:mm:ss} | {FormatRuntimeDuration(agoSeconds)} temu";
            }
            else
            {
                timingLine = "Ostatnie EQ: brak";
            }
            string body =
                $"Stan: {state}\n" +
                $"{progressLine}\n" +
                $"{timingLine}\n" +
                $"Interwał: {intervalSeconds}s | Sloty: {selectedSlots}/27 | " +
                (discardEverythingExceptCobblestone
                    ? "Tryb: wszystko poza cobblestone\n"
                    : $"Typy: {selectedItemTypes}/{InventoryCleanupItemTypes.Length}\n") +
                $"{cobbleXLine}\n" +
                $"{eatingLine}\n" +
                $"Historia EQ: {_latestMiningLogSummary.InventorySessions:N0} skanów | {_latestMiningLogSummary.DiscardedItems:N0} szt. / {_latestMiningLogSummary.DiscardedStacks:N0} stos.\n" +
                $"Ostatni wynik: {_inventoryCleanupLastResult}";

            bool warning = _inventoryCleanupStage == InventoryCleanupStage.None && _inventoryCleanupLastResultWarning;
            return new OverlayHudEntry(
                ChkCobbleXEnabled.IsChecked == true ? "AUTO EQ / COBBLEX" : "AUTO WYRZUCANIE",
                body,
                warning ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildFastUpExitOverlayEntry()
        {
            string bindLabel = GetConfiguredBindLabel(TxtTestFastUpExitBind.Text);
            string runtimeState = GetRuntimeStateLabel(_testFastUpExitRuntimeEnabled);
            int pickaxeSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitPickaxeSlot, _settings.TestFastUpExitPickaxeSlot);
            int blockSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitBlockSlot, _settings.TestFastUpExitBlockSlot);
            string pickaxeType = GetSelectedTestFastUpPickaxeType();
            int breakMs = GetConfiguredFastUpBreakDurationMs();

            string body =
                $"Bind: {bindLabel}\n" +
                $"Stan: {runtimeState}\n" +
                $"Kilof: {pickaxeType} | Slot: {pickaxeSlot}\n" +
                $"Blok: slot {blockSlot} | LPM: {breakMs} ms";

            return new OverlayHudEntry(
                "SZYBKIE WYJŚCIE DO GÓRY",
                body,
                _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private OverlayHudEntry BuildTestAutoFishingOverlayEntry(DateTime now)
        {
            string bindLabel = GetConfiguredBindLabel(TxtTestAutoFishingBind.Text);
            string captureBindLabel = GetConfiguredBindLabel(TxtTestAutoFishingCaptureBind.Text);
            string runtimeStateLabel = GetRuntimeStateLabel(_testAutoFishingRuntimeEnabled);
            string detectionStateLabel = TxtTestAutoFishingState?.Text?.Trim() ?? "-";
            string areaLine = HasTestAutoFishingAreaConfigured()
                ? $"Obszar: x={_settings.TestAutoFishingCaptureX}, y={_settings.TestAutoFishingCaptureY}, {_settings.TestAutoFishingCaptureWidth}x{_settings.TestAutoFishingCaptureHeight}"
                : "Obszar: brak";
            string bobberLine = !double.IsNaN(_testAutoFishingLastDetectedBobberX) && !double.IsNaN(_testAutoFishingLastDetectedBobberY)
                ? $"Spławik: x={_settings.TestAutoFishingCaptureX + (int)Math.Round(_testAutoFishingLastDetectedBobberX)}, y={_settings.TestAutoFishingCaptureY + (int)Math.Round(_testAutoFishingLastDetectedBobberY)} (okno gry) | czerwone px={_testAutoFishingLastDetectedRedPixels}"
                : "Spławik: brak";
            if (_testAutoFishingBaselineReady)
                bobberLine += $" | baza y={_settings.TestAutoFishingCaptureY + (int)Math.Round(_testAutoFishingBaselineBobberY)}";
            string catchLine = _testAutoFishingCaughtCount > 0 && _testAutoFishingLastCatchAtUtc != DateTime.MinValue
                ? $"Złowione: {_testAutoFishingCaughtCount} | ostatnie {Math.Max(0, (int)Math.Floor((now - _testAutoFishingLastCatchAtUtc).TotalSeconds))}s temu"
                : "Złowione: brak";
            int fishingElapsedSeconds = _testAutoFishingRuntimeStartedAtUtc == DateTime.MinValue
                ? 0
                : Math.Max(0, (int)Math.Floor((now - _testAutoFishingRuntimeStartedAtUtc).TotalSeconds));
            string fishingTimeLine = _testAutoFishingRuntimeStartedAtUtc == DateTime.MinValue
                ? "Start: - | Czas: 00:00"
                : $"Start: {_testAutoFishingRuntimeStartedAtUtc.ToLocalTime():HH:mm:ss} | Czas: {FormatRuntimeDuration(fishingElapsedSeconds)}";

            string command = GetConfiguredTestAutoFishingRepairCommand();
            string commandLine;
            if (string.IsNullOrWhiteSpace(command) || GetConfiguredTestAutoFishingRepairIntervalSeconds() <= 0)
                commandLine = "Komenda: OFF";
            else if (_testAutoFishingRepairStage != TestAutoFishingRepairStage.None)
                commandLine = $"Komenda: {GetStatusCommandPreview(command)} ({GetTestAutoFishingRepairStageLabel(_testAutoFishingRepairStage)})";
            else if (_testAutoFishingRecastAfterRepairPending)
                commandLine = $"Komenda: ponowne zarzucenie za {Math.Max(0, (int)Math.Ceiling((_testAutoFishingRecastAfterRepairAtUtc - now).TotalMilliseconds))}ms";
            else
                commandLine = $"Komenda: {GetStatusCommandPreview(command)} za {(_nextTestAutoFishingRepairAtUtc == DateTime.MaxValue ? 0 : Math.Max(0, (int)Math.Ceiling((_nextTestAutoFishingRepairAtUtc - now).TotalSeconds)))}s";

            string body = $"Bind: {bindLabel}\nZaznaczanie: {captureBindLabel}\nStan: {runtimeStateLabel}\n{fishingTimeLine}\nDetekcja: {detectionStateLabel}\n{areaLine}\n{bobberLine}\n{catchLine}\n{commandLine}";
            return new OverlayHudEntry("AUTO ŁOWIENIE", body, _isPausedByCursorVisibility ? OverlayHudTone.Warning : OverlayHudTone.Active);
        }

        private static string FormatRuntimeDuration(int totalSeconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
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
                int remainingSeconds = GetKopaczCommandRemainingSeconds(InventoryCleanupOwner.Kopacz533, now);
                string pauseLabel = IsKopaczCommandCountdownPaused(InventoryCleanupOwner.Kopacz533) ? " (pauza Auto EQ)" : string.Empty;
                return $"Następna #{commandIndex + 1}: {GetStatusCommandPreview(command)} za {remainingSeconds}s{pauseLabel}";
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
                int remainingSeconds = GetKopaczCommandRemainingSeconds(InventoryCleanupOwner.Kopacz633, now);
                string pauseLabel = IsKopaczCommandCountdownPaused(InventoryCleanupOwner.Kopacz633) ? " (pauza Auto EQ)" : string.Empty;
                return $"Następna #{commandIndex + 1}: {GetStatusCommandPreview(command)} za {remainingSeconds}s{pauseLabel}";
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
            bool runtimeDataLive =
                _kopacz533RuntimeEnabled ||
                _kopacz533CommandStage != Kopacz533CommandStage.None ||
                _kopacz633RuntimeEnabled ||
                _kopacz633CommandStage != Kopacz633CommandStage.None ||
                _inventoryCleanupStage != InventoryCleanupStage.None ||
                _testAutoFishingRuntimeEnabled;
            if (!runtimeDataLive)
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

        private void SetExpandableSectionState(
            FrameworkElement section,
            bool expanded)
        {
            if (_expandableSectionStates.TryGetValue(section, out bool previousState)
                && previousState == expanded)
                return;

            _expandableSectionStates[section] = expanded;

            section.BeginAnimation(FrameworkElement.HeightProperty, null);
            section.BeginAnimation(UIElement.OpacityProperty, null);

            if (_isLoadingUi || !IsLoaded)
            {
                section.Height = double.NaN;
                section.Opacity = 1.0;
                section.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            TimeSpan duration = TimeSpan.FromMilliseconds(expanded ? 180 : 140);

            if (expanded)
            {
                section.Visibility = Visibility.Visible;
                section.Height = double.NaN;
                section.UpdateLayout();
                double targetHeight = Math.Max(1.0, section.ActualHeight);

                section.Height = 0;
                section.Opacity = 0;

                var heightAnimation = new DoubleAnimation(0, targetHeight, duration)
                {
                    EasingFunction = easing
                };
                heightAnimation.Completed += (_, _) =>
                {
                    if (!_expandableSectionStates.TryGetValue(section, out bool currentState)
                        || !currentState)
                        return;

                    section.BeginAnimation(FrameworkElement.HeightProperty, null);
                    section.BeginAnimation(UIElement.OpacityProperty, null);
                    section.Height = double.NaN;
                    section.Opacity = 1.0;
                };

                section.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);
                section.BeginAnimation(
                    UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
                return;
            }

            if (section.Visibility != Visibility.Visible)
            {
                section.Height = double.NaN;
                section.Opacity = 1.0;
                section.Visibility = Visibility.Collapsed;
                return;
            }

            double currentHeight = Math.Max(1.0, section.ActualHeight);
            double currentOpacity = section.Opacity;
            section.Height = currentHeight;

            var collapseAnimation = new DoubleAnimation(currentHeight, 0, duration)
            {
                EasingFunction = easing
            };
            collapseAnimation.Completed += (_, _) =>
            {
                if (!_expandableSectionStates.TryGetValue(section, out bool currentState)
                    || currentState)
                    return;

                section.BeginAnimation(FrameworkElement.HeightProperty, null);
                section.BeginAnimation(UIElement.OpacityProperty, null);
                section.Height = double.NaN;
                section.Opacity = 1.0;
                section.Visibility = Visibility.Collapsed;
            };

            section.BeginAnimation(FrameworkElement.HeightProperty, collapseAnimation);
            section.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(currentOpacity, 0, duration) { EasingFunction = easing });
        }

        private void UpdateEnabledStates(object? sender = null, RoutedEventArgs? e = null)
        {
            bool manualOn = ChkMacroManualEnabled.IsChecked ?? false;
            bool holdLeftOn = manualOn && ChkHoldLeftEnabled.IsChecked == true;
            bool holdRightOn = manualOn && ChkHoldRightEnabled.IsChecked == true;
            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;

            SetExpandableSectionState(PanelAutoLeftContent, autoLeftOn);
            SetExpandableSectionState(PanelAutoRightContent, autoRightOn);

            TxtMacroManualKey.IsEnabled = manualOn;
            BtnMacroManualCapture.IsEnabled = manualOn;
            BtnMacroManualClear.IsEnabled = manualOn;
            ChkHoldLeftEnabled.IsEnabled = manualOn;
            ChkHoldRightEnabled.IsEnabled = manualOn;
            TxtManualLeftMinCps.IsEnabled = holdLeftOn;
            TxtManualLeftMaxCps.IsEnabled = holdLeftOn;
            TxtManualRightMinCps.IsEnabled = holdRightOn;
            TxtManualRightMaxCps.IsEnabled = holdRightOn;

            TxtAutoLeftKey.IsEnabled = autoLeftOn;
            BtnAutoLeftCapture.IsEnabled = autoLeftOn;
            BtnAutoLeftClear.IsEnabled = autoLeftOn;
            TxtAutoLeftMinCps.IsEnabled = autoLeftOn;
            TxtAutoLeftMaxCps.IsEnabled = autoLeftOn;
            ChkAutoLeftComboMode.IsEnabled = autoLeftOn;
            ChkAutoLeftHoldBindMode.IsEnabled = autoLeftOn;
            ChkAutoLeftDabMode.IsEnabled = autoLeftOn;

            TxtAutoRightKey.IsEnabled = autoRightOn;
            BtnAutoRightCapture.IsEnabled = autoRightOn;
            BtnAutoRightClear.IsEnabled = autoRightOn;
            TxtAutoRightMinCps.IsEnabled = autoRightOn;
            TxtAutoRightMaxCps.IsEnabled = autoRightOn;
            ChkAutoRightComboMode.IsEnabled = autoRightOn;
            ChkAutoRightHoldBindMode.IsEnabled = autoRightOn;

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
                ReleaseHoldRightInjectedButton();
                _holdRightRuntimePressActive = false;
                _nextHoldRightClickAtUtc = DateTime.UtcNow;
            }
            if (!autoLeftOn)
            {
                _autoLeftRuntimeEnabled = false;
                SetAutoLeftDabHold(false);
                _autoLeftComboTriggerWasDown = false;
                _autoLeftComboStopWasDown = false;
            }
            if (!autoRightOn)
            {
                _autoRightRuntimeEnabled = false;
                _autoRightComboTriggerWasDown = false;
                _autoRightComboStopWasDown = false;
            }

            bool kop533On = ChkKopacz533Enabled.IsChecked ?? false;
            SetExpandableSectionState(PanelKopacz533Content, kop533On);
            TxtKopacz533Key.IsEnabled = kop533On;
            BtnKopacz533Capture.IsEnabled = kop533On;
            BtnKopacz533Clear.IsEnabled = kop533On;
            PanelKopacz533Commands.IsEnabled = kop533On;
            BtnKopacz533AddCommand.IsEnabled = kop533On;
            if (!kop533On)
            {
                _kopacz533RuntimeEnabled = false;
                ResetKopacz533RuntimeState();
                SetKopacz533MiningHold(false);
                EndMiningLogRun(InventoryCleanupOwner.Kopacz533, "Kanał Kopacz 5/3/3 został wyłączony.");
            }

            bool kop633On = ChkKopacz633Enabled.IsChecked ?? false;
            SetExpandableSectionState(PanelKopacz633Content, kop633On);
            TxtKopacz633Key.IsEnabled = kop633On;
            BtnKopacz633Capture.IsEnabled = kop633On;
            BtnKopacz633Clear.IsEnabled = kop633On;
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
                EndMiningLogRun(InventoryCleanupOwner.Kopacz633, "Kanał Kopacz 6/3/3 został wyłączony.");
            }
            UpdateKopaczUpwardInfoVisibility();

            bool inventoryCleanupOn = ChkInventoryCleanupEnabled.IsChecked == true;
            SetExpandableSectionState(PanelInventoryCleanupExpandableContent, inventoryCleanupOn);
            PanelInventoryCleanupContent.IsEnabled = inventoryCleanupOn;
            RefreshInventoryCleanupItemTypeMode();
            bool cobbleXSettingsOn = inventoryCleanupOn && ChkCobbleXEnabled.IsChecked == true;
            SetExpandableSectionState(PanelCobbleXSettings, cobbleXSettingsOn);
            PanelCobbleXSettings.IsEnabled = cobbleXSettingsOn;
            if (!inventoryCleanupOn)
                ResetInventoryCleanupState(scheduleNext: false);

            // JABŁKA Z LIŚCI
            bool jablkaOn = ChkJablkaZLisciEnabled.IsChecked ?? false;
            SetExpandableSectionState(PanelJablkaContent, jablkaOn);
            TxtJablkaZLisciKey.IsEnabled = jablkaOn;
            BtnJablkaZLisciCapture.IsEnabled = jablkaOn;
            BtnJablkaZLisciClear.IsEnabled = jablkaOn;
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
                SetExpandableSectionState(PanelBindyContent, bindyOn);
                PanelBindyContent.IsEnabled = bindyOn;
            }
            if (PanelBindyCommands != null)
                PanelBindyCommands.IsEnabled = bindyOn;
            if (BtnBindyAddCommand != null)
                BtnBindyAddCommand.IsEnabled = bindyOn;
            if (!bindyOn)
                ResetBindyRuntimeState();

            bool cursorPauseOn = ChkPauseWhenCursorVisible.IsChecked == true;
            SetExpandableSectionState(PanelCursorPauseContent, cursorPauseOn);
            bool testEntitiesOn = ChkTestEntitiesEnabled?.IsChecked == true;
            bool testCustomOn = testEntitiesOn;
            bool testFastUpOn = ChkTestFastUpExitEnabled?.IsChecked == true;
            UpdateAutoArmorEnabledState();
            UpdateAutoWaterEnabledState();
            bool testAutoFishingOn = ChkTestAutoFishingEnabled?.IsChecked == true;
            bool emergencyDamageSoundOn = ChkEmergencyDamageSoundEnabled?.IsChecked == true;
            bool emergencyReconnectOn = emergencyDamageSoundOn;
            bool autoReconnectOn = IsPeriodicAutoReconnectEnabled();
            if (PanelTestEntitiesContent != null)
                PanelTestEntitiesContent.Visibility = testEntitiesOn ? Visibility.Visible : Visibility.Collapsed;
            if (TxtTestCustomCaptureBind != null)
                TxtTestCustomCaptureBind.IsEnabled = testCustomOn;
            if (BtnTestCustomCaptureBind != null)
                BtnTestCustomCaptureBind.IsEnabled = testCustomOn;
            if (BtnTestCustomCaptureBindClear != null)
                BtnTestCustomCaptureBindClear.IsEnabled = testCustomOn;
            if (BtnTestSelectCaptureArea != null)
                BtnTestSelectCaptureArea.IsEnabled = testCustomOn;
            if (BtnTestResetCaptureData != null)
                BtnTestResetCaptureData.IsEnabled = testEntitiesOn;
            if (BorderTestFastUpExitContent != null)
                BorderTestFastUpExitContent.Visibility = testFastUpOn ? Visibility.Visible : Visibility.Collapsed;
            if (PanelTestFastUpExitContent != null)
                PanelTestFastUpExitContent.IsEnabled = testFastUpOn;
            if (TxtTestFastUpExitBind != null)
                TxtTestFastUpExitBind.IsEnabled = testFastUpOn;
            if (BtnTestFastUpExitBind != null)
                BtnTestFastUpExitBind.IsEnabled = testFastUpOn;
            if (BtnTestFastUpExitBindClear != null)
                BtnTestFastUpExitBindClear.IsEnabled = testFastUpOn;
            if (CbTestFastUpExitBlockSlot != null)
                CbTestFastUpExitBlockSlot.IsEnabled = testFastUpOn;
            if (CbTestFastUpExitPickaxeSlot != null)
                CbTestFastUpExitPickaxeSlot.IsEnabled = testFastUpOn;
            if (CbTestFastUpExitPickaxeType != null)
                CbTestFastUpExitPickaxeType.IsEnabled = testFastUpOn;
            if (ChkTestFastUpExitLookMsEnabled != null)
                ChkTestFastUpExitLookMsEnabled.IsEnabled = testFastUpOn;
            if (ChkTestFastUpExitPlaceMsEnabled != null)
                ChkTestFastUpExitPlaceMsEnabled.IsEnabled = testFastUpOn;
            bool lookMsEnabled = testFastUpOn && (ChkTestFastUpExitLookMsEnabled?.IsChecked == true);
            bool breakMsEnabled = testFastUpOn;
            bool placeMsEnabled = testFastUpOn && (ChkTestFastUpExitPlaceMsEnabled?.IsChecked == true);
            if (SlTestFastUpExitLookMs != null)
                SlTestFastUpExitLookMs.IsEnabled = lookMsEnabled;
            if (SlTestFastUpExitBreakMs != null)
                SlTestFastUpExitBreakMs.IsEnabled = breakMsEnabled;
            if (SlTestFastUpExitPlaceMs != null)
                SlTestFastUpExitPlaceMs.IsEnabled = placeMsEnabled;
            if (TxtTestFastUpExitLookMsValue != null)
                TxtTestFastUpExitLookMsValue.IsEnabled = lookMsEnabled;
            if (TxtTestFastUpExitBreakMsValue != null)
                TxtTestFastUpExitBreakMsValue.IsEnabled = breakMsEnabled;
            if (TxtTestFastUpExitPlaceMsValue != null)
                TxtTestFastUpExitPlaceMsValue.IsEnabled = placeMsEnabled;
            if (!testFastUpOn)
            {
                _testFastUpExitRuntimeEnabled = false;
                ResetTestFastUpExitRuntimeState();
            }
            if (PanelTestAutoFishingExpandableContent != null)
                SetExpandableSectionState(PanelTestAutoFishingExpandableContent, testAutoFishingOn);
            if (PanelTestAutoFishingContent != null)
                PanelTestAutoFishingContent.IsEnabled = testAutoFishingOn;
            if (TxtTestAutoFishingBind != null)
                TxtTestAutoFishingBind.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingBind != null)
                BtnTestAutoFishingBind.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingBindClear != null)
                BtnTestAutoFishingBindClear.IsEnabled = testAutoFishingOn;
            if (TxtTestAutoFishingCaptureBind != null)
                TxtTestAutoFishingCaptureBind.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingCaptureBind != null)
                BtnTestAutoFishingCaptureBind.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingCaptureBindClear != null)
                BtnTestAutoFishingCaptureBindClear.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingSelectArea != null)
                BtnTestAutoFishingSelectArea.IsEnabled = testAutoFishingOn;
            if (BtnTestAutoFishingResetArea != null)
                BtnTestAutoFishingResetArea.IsEnabled = testAutoFishingOn;
            if (TxtTestAutoFishingRepairCommand != null)
                TxtTestAutoFishingRepairCommand.IsEnabled = testAutoFishingOn;
            if (TxtTestAutoFishingRepairEverySeconds != null)
                TxtTestAutoFishingRepairEverySeconds.IsEnabled = testAutoFishingOn;
            if (!testAutoFishingOn)
            {
                _testAutoFishingRuntimeEnabled = false;
                ResetTestAutoFishingRuntimeState();
            }
            UpdateTestAutoFishingStatusLabel();
            if (PanelEmergencyDamageSoundContent != null)
                SetExpandableSectionState(PanelEmergencyDamageSoundContent, emergencyDamageSoundOn);
            if (CbEmergencyDamageSoundDevice != null)
                CbEmergencyDamageSoundDevice.IsEnabled = emergencyDamageSoundOn;
            if (BtnEmergencyDamageSoundRefreshDevices != null)
                BtnEmergencyDamageSoundRefreshDevices.IsEnabled = emergencyDamageSoundOn;
            if (ChkEmergencyDamageSoundTestMode != null)
                ChkEmergencyDamageSoundTestMode.IsEnabled = emergencyDamageSoundOn;
            if (SlEmergencyDamageSoundSimilarity != null)
                SlEmergencyDamageSoundSimilarity.IsEnabled = emergencyDamageSoundOn;
            if (BtnEmergencyDamageSoundReloadReferences != null)
                BtnEmergencyDamageSoundReloadReferences.IsEnabled = emergencyDamageSoundOn && !_damageSoundDetector.IsRunning;
            if (BtnEmergencyDamageSoundTest != null)
                BtnEmergencyDamageSoundTest.IsEnabled = emergencyDamageSoundOn
                    && _settings.EmergencyDamageSoundTemplates.Count > 0;
            if (BtnEmergencyDamageSoundStop != null)
                BtnEmergencyDamageSoundStop.IsEnabled = emergencyDamageSoundOn && _damageSoundDetector.IsRunning;
            if (!emergencyDamageSoundOn)
                StopEmergencyDamageSoundListening(updateStatus: true);
            if (PanelEmergencyReconnectContent != null)
                SetExpandableSectionState(PanelEmergencyReconnectContent, emergencyReconnectOn);
            if (TxtEmergencyReconnectDelaySeconds != null)
                TxtEmergencyReconnectDelaySeconds.IsEnabled = emergencyReconnectOn && !_emergencyReconnectActive;
            if (BtnEmergencyReconnectStop != null)
                BtnEmergencyReconnectStop.IsEnabled = _emergencyReconnectActive;
            if (PanelAutoReconnectContent != null)
                SetExpandableSectionState(PanelAutoReconnectContent, emergencyDamageSoundOn);
            if (ChkAutoReconnectEnabled != null)
                ChkAutoReconnectEnabled.IsEnabled = emergencyDamageSoundOn;
            if (!autoReconnectOn
                && !_emergencyReconnectActive
                && _autoReconnectStage != AutoReconnectStage.None)
                StopAutoReconnect("Auto reconnect wyłączony.", resumeMining: true, warning: false);

            bool overlayHudOn = ChkOverlayHudEnabled.IsChecked == true;
            SetExpandableSectionState(PanelOverlayHudContent, overlayHudOn);
            PanelOverlayHudContent.IsEnabled = overlayHudOn;

            SetSectionVisualState(BorderManualLeftSection, holdLeftOn);
            SetSectionVisualState(BorderManualRightSection, holdRightOn);
            SetSectionVisualState(BorderManualBindSection, manualOn);
            SetSectionVisualState(BorderTestCustomCaptureSection, testCustomOn);
            SetSectionVisualState(BorderTestFastUpExitSection, true);

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

        private void ClearBindyEntryBind(BindyEntry entry, TextBox keyTextBox)
        {
            string entryId = EnsureBindyEntryId(entry);
            bool hadBind =
                !string.IsNullOrWhiteSpace(entry.Key)
                || !string.IsNullOrWhiteSpace(keyTextBox.Text)
                || _pendingBindyBindValuesById.ContainsKey(entryId)
                || (_bindyCaptureEntry != null && ReferenceEquals(_bindyCaptureEntry, entry));

            if (_bindyCaptureEntry != null && ReferenceEquals(_bindyCaptureEntry, entry))
                CancelBindyRowCapture(showStatus: false);

            entry.Key = string.Empty;
            keyTextBox.Text = string.Empty;
            _pendingBindyBindValuesById.Remove(entryId);
            _bindyBindWasDownById[entryId] = false;
            RefreshBindySaveButton(entryId);

            if (hadBind)
            {
                MarkDirty();
                UpdateStatusBar($"Usunięto bind dla {GetBindyEntryLabel(entry)}", "Orange");
            }
            else
            {
                UpdateStatusBar($"{GetBindyEntryLabel(entry)}: bind jest już pusty", "Orange");
            }
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

            Button clearBindBtn = new Button
            {
                Content = "Usuń bind",
                Width = 86,
                Margin = new Thickness(0, 0, 12, 0)
            };
            clearBindBtn.Click += (_, __) => ClearBindyEntryBind(entry, keyBox);

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
            row.Children.Add(clearBindBtn);
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
                EndMiningLogRun(InventoryCleanupOwner.Kopacz633, "Kopanie zatrzymane: usunięto wybrany kierunek.", MiningLogStatuses.Aborted);
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

            string keyText = key == Key.Return ? "Enter" : key.ToString();
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

            if (_bindyCaptureEntry == null && IsMinecraftControlKeyTarget(_bindCaptureTarget))
            {
                UpdateStatusBar("Sterowanie Minecrafta: wybierz klawisz klawiatury, nie przycisk myszy.", "Orange");
                e.Handled = true;
                return;
            }

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

            if (IsMinecraftControlKeyTarget(target) && !IsSupportedMinecraftControlKey(keyText))
            {
                UpdateStatusBar($"{GetBindTargetLabel(target)}: wybierz prawidłowy klawisz klawiatury.", "Orange");
                return;
            }

            string ownerId = GetBindOwnerId(target);
            if (!IsMinecraftControlKeyTarget(target)
                && TryFindBindConflict(keyText, ownerId, out string conflictOwnerLabel))
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

        private void BtnClearBind_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (sender is not Button button)
                return;
            if (!Enum.TryParse(button.Tag?.ToString(), out BindTarget target) || target == BindTarget.None)
                return;

            ClearFixedBind(target);
        }

        private void ClearFixedBind(BindTarget target)
        {
            TextBox? textBox = GetBindTextBox(target);
            if (textBox == null)
                return;

            bool hadBind = !string.IsNullOrWhiteSpace(textBox.Text) || _pendingBindValues.ContainsKey(target) || _bindCaptureTarget == target;
            textBox.Text = string.Empty;
            _pendingBindValues.Remove(target);

            if (_bindCaptureTarget == target)
            {
                _bindCaptureTarget = BindTarget.None;
                UpdateBindCaptureVisuals();
            }

            switch (target)
            {
                case BindTarget.HoldToggle:
                    _holdBindWasDown = false;
                    _holdMacroRuntimeEnabled = false;
                    ResetHoldLeftToggleState(clearToggleEnabled: true);
                    break;

                case BindTarget.AutoLeft:
                    _autoLeftBindWasDown = false;
                    _autoLeftRuntimeEnabled = false;
                    SetAutoLeftDabHold(false);
                    _autoLeftComboTriggerWasDown = false;
                    _autoLeftComboStopWasDown = false;
                    _nextAutoLeftClickAtUtc = DateTime.UtcNow;
                    break;

                case BindTarget.AutoRight:
                    _autoRightBindWasDown = false;
                    _autoRightRuntimeEnabled = false;
                    _autoRightComboTriggerWasDown = false;
                    _autoRightComboStopWasDown = false;
                    _nextAutoRightClickAtUtc = DateTime.UtcNow;
                    break;

                case BindTarget.JablkaZLisci:
                    _jablkaBindWasDown = false;
                    _jablkaRuntimeEnabled = false;
                    ResetJablkaRuntimeState();
                    break;

                case BindTarget.Kopacz533:
                    _kopacz533BindWasDown = false;
                    _kopacz533RuntimeEnabled = false;
                    SetKopacz533MiningHold(false);
                    ResetKopacz533RuntimeState();
                    EndMiningLogRun(InventoryCleanupOwner.Kopacz533, "Kopanie zatrzymane: usunięto bind Kopacza.");
                    break;

                case BindTarget.Kopacz633:
                    _kopacz633BindWasDown = false;
                    _kopacz633RuntimeEnabled = false;
                    SetKopacz633AttackHold(false);
                    SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                    ResetKopacz633RuntimeState();
                    EndMiningLogRun(InventoryCleanupOwner.Kopacz633, "Kopanie zatrzymane: usunięto bind Kopacza.");
                    break;

                case BindTarget.FastUpExit:
                    _testFastUpExitBindWasDown = false;
                    _testFastUpExitRuntimeEnabled = false;
                    ResetTestFastUpExitRuntimeState();
                    break;

                case BindTarget.TestCaptureArea:
                    _testCaptureBindWasDown = false;
                    break;
                case BindTarget.AutoArmor:
                    _autoArmorBindWasDown = false;
                    if (IsAutoArmorRunning)
                        RequestCancelAutoArmor("Auto zbroja: usunięto bind — kończę bezpiecznie bieżące przełożenie.");
                    break;
                case BindTarget.AutoWater:
                    _autoWaterBindWasDown = false;
                    CancelAutoWater("AutoWater: usunięto bind.", Brushes.Orange, restoreSlot: false);
                    break;
                case BindTarget.TestAutoFishing:
                    _testAutoFishingBindWasDown = false;
                    _testAutoFishingRuntimeEnabled = false;
                    ResetTestAutoFishingRuntimeState();
                    UpdateTestAutoFishingStatusLabel();
                    break;
                case BindTarget.TestAutoFishingCaptureArea:
                    _testAutoFishingCaptureBindWasDown = false;
                    break;
            }

            RefreshBindSaveButton(target);
            RefreshTopTiles();

            if (hadBind)
            {
                MarkDirty();
                UpdateStatusBar($"Usunięto bind dla {GetBindTargetLabel(target)}", "Orange");
            }
            else
            {
                UpdateStatusBar($"{GetBindTargetLabel(target)}: bind jest już pusty", "Orange");
            }
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

        private void BtnTestFastUpExitBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.FastUpExit);
        }

        private void BtnTestCustomCaptureBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.TestCaptureArea);
        }

        private void BtnTestAutoFishingBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.TestAutoFishing);
        }

        private void BtnTestAutoFishingCaptureBind_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.TestAutoFishingCaptureArea);
        }

        private void BtnChatOpenKeySave_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.ChatOpen);
        }

        private void BtnDropItemKeySave_Click(object sender, RoutedEventArgs e)
        {
            ConfirmPendingBind(BindTarget.DropItem);
        }

        private void BtnResetMinecraftControlKey_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi || sender is not Button button)
                return;
            if (!Enum.TryParse(button.Tag?.ToString(), out BindTarget target)
                || !IsMinecraftControlKeyTarget(target))
            {
                return;
            }

            string defaultKey = target == BindTarget.ChatOpen ? "T" : "Q";
            TextBox? textBox = GetBindTextBox(target);
            if (textBox == null)
                return;

            textBox.Text = defaultKey;
            _pendingBindValues.Remove(target);
            if (_bindCaptureTarget == target)
                _bindCaptureTarget = BindTarget.None;
            RefreshBindSaveButton(target);
            UpdateBindCaptureVisuals();
            MarkDirty();
            UpdateStatusBar($"Przywrócono {defaultKey} dla: {GetBindTargetLabel(target)}.", "Green");
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

        private async void BtnTestAutoFishingSelectArea_Click(object sender, RoutedEventArgs e)
        {
            await BeginTestAutoFishingAreaSelectionAsync(triggeredByBind: false);
        }

        private void BtnTestAutoFishingResetArea_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;
            if (_isTestCaptureSelectionInProgress)
            {
                UpdateStatusBar("Najpierw zakończ zaznaczanie obszaru", "Orange");
                return;
            }

            _settings.TestAutoFishingCaptureX = 0;
            _settings.TestAutoFishingCaptureY = 0;
            _settings.TestAutoFishingCaptureWidth = 0;
            _settings.TestAutoFishingCaptureHeight = 0;
            _testAutoFishingRuntimeEnabled = false;
            ResetTestAutoFishingRuntimeState();
            UpdateTestAutoFishingAreaInfo();
            UpdateTestAutoFishingStatusLabel();
            MarkDirty();
            RefreshTopTiles();
            UpdateStatusBar("Zresetowano obszar auto-łowienia.", "Green");
        }

        private void BeginTestCaptureAreaSelectionFromBind()
        {
            if (_isTestCaptureSelectionInProgress)
                return;

            _ = BeginTestCaptureAreaSelectionAsync(triggeredByBind: true);
        }

        private void BeginTestAutoFishingAreaSelectionFromBind()
        {
            if (_isTestCaptureSelectionInProgress)
                return;

            _ = BeginTestAutoFishingAreaSelectionAsync(triggeredByBind: true);
        }

        private async Task BeginTestCaptureAreaSelectionAsync(bool triggeredByBind)
        {
            if (_isTestCaptureSelectionInProgress || _isLoadingUi)
                return;

            if (triggeredByBind && !_isMinecraftFocused)
            {
                UpdateStatusBar("Najpierw ustaw fokus na okno Minecrafta", "Orange");
                return;
            }

            if (!TryResolveTargetWindow(allowPendingSelection: !triggeredByBind, out IntPtr targetWindowHandle))
            {
                UpdateStatusBar("Wybierz okno gry Minecraft (nie launcher) w Ustawieniach i zapisz program", "Orange");
                return;
            }

            if (!TryGetWindowClientRectOnScreen(targetWindowHandle, out RECT clientRect))
            {
                UpdateStatusBar("Nie mogę odczytać rozmiaru okna gry", "Orange");
                return;
            }

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < MinimumCaptureSelectionSize || clientHeight < MinimumCaptureSelectionSize)
            {
                UpdateStatusBar($"Okno gry musi mieć co najmniej {MinimumCaptureSelectionSize}x{MinimumCaptureSelectionSize} px do zaznaczania", "Orange");
                return;
            }

            _isTestCaptureSelectionInProgress = true;
            bool restoreWindowAfterSelection = !triggeredByBind && IsVisible;
            try
            {
                if (restoreWindowAfterSelection)
                {
                    Hide();
                    await Task.Delay(120);
                }

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
                int relativeX = Math.Clamp(selectedRect.Left - clientRect.Left, 0, clientWidth - MinimumCaptureSelectionSize);
                int relativeY = Math.Clamp(selectedRect.Top - clientRect.Top, 0, clientHeight - MinimumCaptureSelectionSize);
                int relativeWidth = Math.Clamp(selectedRect.Width, MinimumCaptureSelectionSize, clientWidth - relativeX);
                int relativeHeight = Math.Clamp(selectedRect.Height, MinimumCaptureSelectionSize, clientHeight - relativeY);

                _settings.TestCustomCaptureX = relativeX;
                _settings.TestCustomCaptureY = relativeY;
                _settings.TestCustomCaptureWidth = relativeWidth;
                _settings.TestCustomCaptureHeight = relativeHeight;

                UpdateTestCustomCaptureAreaInfo();

                MarkDirty();
                UpdateStatusBar($"Zapisano obszar OCR: x={relativeX}, y={relativeY}, {relativeWidth}x{relativeHeight}", "Green");
            }
            finally
            {
                if (restoreWindowAfterSelection)
                {
                    Show();
                    Activate();
                }

                _isTestCaptureSelectionInProgress = false;
            }
        }

        private async Task BeginTestAutoFishingAreaSelectionAsync(bool triggeredByBind)
        {
            if (_isTestCaptureSelectionInProgress || _isLoadingUi)
                return;
            if (triggeredByBind && !_isMinecraftFocused)
            {
                UpdateStatusBar("Najpierw ustaw fokus na okno Minecrafta", "Orange");
                return;
            }
            if (!TryResolveTargetWindow(allowPendingSelection: !triggeredByBind, out IntPtr targetWindowHandle))
            {
                UpdateStatusBar("Wybierz okno gry Minecraft (nie launcher) w Ustawieniach i zapisz program", "Orange");
                return;
            }
            if (!TryGetWindowClientRectOnScreen(targetWindowHandle, out RECT clientRect))
            {
                UpdateStatusBar("Nie mogę odczytać rozmiaru okna gry", "Orange");
                return;
            }

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < MinimumCaptureSelectionSize || clientHeight < MinimumCaptureSelectionSize)
            {
                UpdateStatusBar($"Okno gry musi mieć co najmniej {MinimumCaptureSelectionSize}x{MinimumCaptureSelectionSize} px do zaznaczania", "Orange");
                return;
            }

            _isTestCaptureSelectionInProgress = true;
            bool restoreWindowAfterSelection = !triggeredByBind && IsVisible;
            try
            {
                if (restoreWindowAfterSelection)
                {
                    Hide();
                    await Task.Delay(120);
                }

                UpdateStatusBar("Zaznacz obszar spławika: przytrzymaj LPM i przeciągnij (Esc anuluje)", "Orange");
                Drawing.Rectangle clientBounds = new Drawing.Rectangle(clientRect.Left, clientRect.Top, clientWidth, clientHeight);
                Drawing.Rectangle? screenSelection = await Task.Run(() => CaptureScreenSelectionWithinBounds(clientBounds));
                if (!screenSelection.HasValue)
                {
                    UpdateStatusBar(triggeredByBind
                        ? "Bind auto-łowienia: anulowano zaznaczanie obszaru"
                        : "Anulowano zaznaczanie obszaru auto-łowienia", "Orange");
                    return;
                }

                Drawing.Rectangle selectedRect = screenSelection.Value;
                int relativeX = Math.Clamp(selectedRect.Left - clientRect.Left, 0, clientWidth - MinimumCaptureSelectionSize);
                int relativeY = Math.Clamp(selectedRect.Top - clientRect.Top, 0, clientHeight - MinimumCaptureSelectionSize);
                int relativeWidth = Math.Clamp(selectedRect.Width, MinimumCaptureSelectionSize, clientWidth - relativeX);
                int relativeHeight = Math.Clamp(selectedRect.Height, MinimumCaptureSelectionSize, clientHeight - relativeY);
                _settings.TestAutoFishingCaptureX = relativeX;
                _settings.TestAutoFishingCaptureY = relativeY;
                _settings.TestAutoFishingCaptureWidth = relativeWidth;
                _settings.TestAutoFishingCaptureHeight = relativeHeight;
                ResetTestAutoFishingRuntimeState();
                UpdateTestAutoFishingAreaInfo();
                UpdateTestAutoFishingStatusLabel();
                MarkDirty();
                UpdateStatusBar($"Zapisano obszar spławika: x={relativeX}, y={relativeY}, {relativeWidth}x{relativeHeight}", "Green");
            }
            finally
            {
                if (restoreWindowAfterSelection)
                {
                    Show();
                    Activate();
                }

                _isTestCaptureSelectionInProgress = false;
                RefreshTestAutoFishingPreview(DateTime.UtcNow, force: true);
            }
        }

        private static Drawing.Rectangle? CaptureScreenSelectionWithinBounds(Drawing.Rectangle bounds)
        {
            if (bounds.Width < MinimumCaptureSelectionSize || bounds.Height < MinimumCaptureSelectionSize)
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

        private void BtnMiningLogs_Click(object sender, RoutedEventArgs e)
        {
            var window = new MiningLogsWindow(_miningLogService)
            {
                Owner = this
            };
            window.ShowDialog();
            RefreshMiningLogsSummary();
        }

        private void RefreshMiningLogsSummary()
        {
            MiningLogSummary summary = _miningLogService.GetSummary();
            _latestMiningLogSummary = summary;

            if (TxtMiningLogsSummary == null)
                return;

            int exactStacks = Math.Max(0, summary.DiscardedStacks - summary.LegacyDiscardedStacks);
            string legacyInfo = summary.LegacyDiscardedStacks > 0
                ? $"  •  Stare logi: {summary.LegacyDiscardedStacks:N0} stosów bez liczby sztuk"
                : string.Empty;
            TxtMiningLogsSummary.Text = $"Skanów EQ: {summary.InventorySessions:N0}  •  CobbleX: {summary.CobbleXCreated:N0}  •  Wyrzucone: {summary.DiscardedItems:N0} szt. / {exactStacks:N0} stosów{legacyInfo}";
        }

        private void TxtTestAutoFishingRepairConfig_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            MarkDirty();
            if (_testAutoFishingRuntimeEnabled)
                ResetTestAutoFishingRuntimeState(DateTime.UtcNow);
            UpdateTestAutoFishingStatusLabel();
            RefreshTopTiles();
        }

        private void TxtTestAutoFishingMinutesToSecondsInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtTestAutoFishingMinutesToSecondsOutput == null)
                return;

            string raw = TxtTestAutoFishingMinutesToSecondsInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                TxtTestAutoFishingMinutesToSecondsOutput.Text = "Wpisz minuty, aby dostać sekundy dla pola \"Co X sek\".";
                TxtTestAutoFishingMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193));
                return;
            }

            string normalized = raw.Replace(',', '.');
            if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) || minutes < 0)
            {
                TxtTestAutoFishingMinutesToSecondsOutput.Text = "Nieprawidłowa wartość. Przykład: 3 lub 30,5";
                TxtTestAutoFishingMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                return;
            }

            double seconds = minutes * 60.0;
            CultureInfo pl = CultureInfo.GetCultureInfo("pl-PL");
            TxtTestAutoFishingMinutesToSecondsOutput.Text = $"{minutes.ToString("0.##", pl)} min = {seconds.ToString("0.##", pl)} s";
            TxtTestAutoFishingMinutesToSecondsOutput.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
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
            _settings.HoldLeftEnabled = ChkHoldLeftEnabled.IsChecked == true;
            _settings.HoldRightEnabled = ChkHoldRightEnabled.IsChecked == true;
            _settings.HoldLeftButton.MinCps = ParseNonNegativeInt(TxtManualLeftMinCps.Text);
            _settings.HoldLeftButton.MaxCps = ParseNonNegativeInt(TxtManualLeftMaxCps.Text);
            _settings.HoldRightButton.MinCps = ParseNonNegativeInt(TxtManualRightMinCps.Text);
            _settings.HoldRightButton.MaxCps = ParseNonNegativeInt(TxtManualRightMaxCps.Text);

            // AUTO
            _settings.AutoLeftButton.Enabled = ChkAutoLeftEnabled.IsChecked ?? false;
            _settings.AutoLeftButton.Key = TxtAutoLeftKey.Text.Trim();
            _settings.AutoLeftButton.MinCps = ParseNonNegativeInt(TxtAutoLeftMinCps.Text);
            _settings.AutoLeftButton.MaxCps = ParseNonNegativeInt(TxtAutoLeftMaxCps.Text);
            _settings.AutoLeftComboMode = ChkAutoLeftComboMode.IsChecked ?? false;
            _settings.AutoLeftHoldBindMode = ChkAutoLeftHoldBindMode.IsChecked == true;
            _settings.AutoLeftDabMode = ChkAutoLeftDabMode.IsChecked == true;

            _settings.AutoRightButton.Enabled = ChkAutoRightEnabled.IsChecked ?? false;
            _settings.AutoRightButton.Key = TxtAutoRightKey.Text.Trim();
            _settings.AutoRightButton.MinCps = ParseNonNegativeInt(TxtAutoRightMinCps.Text);
            _settings.AutoRightButton.MaxCps = ParseNonNegativeInt(TxtAutoRightMaxCps.Text);
            _settings.AutoRightComboMode = ChkAutoRightComboMode.IsChecked ?? false;
            _settings.AutoRightHoldBindMode = ChkAutoRightHoldBindMode.IsChecked == true;

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

            _settings.InventoryCleanupEnabled = ChkInventoryCleanupEnabled.IsChecked == true;
            _settings.InventoryCleanupIntervalSeconds = GetConfiguredInventoryCleanupIntervalSeconds();
            _settings.InventoryCleanupDiscardAllItemTypes = ChkInventoryCleanupAllItemTypes.IsChecked == true;
            _settings.InventoryCleanupEatAfterCleanup = ChkInventoryCleanupEatAfterCleanup.IsChecked == true;
            _settings.InventoryCleanupSlots = GetSelectedInventoryCleanupSlots().OrderBy(slot => slot).ToList();
            _settings.InventoryCleanupItemTypes = GetSelectedInventoryCleanupItemTypes()
                .OrderBy(itemId => itemId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _settings.CobbleXEnabled = ChkCobbleXEnabled.IsChecked == true;
            _settings.CobbleXCommand = GetConfiguredCobbleXCommand();
            _settings.CobbleXRequiredFullStacks = GetConfiguredCobbleXRequiredStacks();

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
            _settings.TestEntitiesEnabled = ChkTestEntitiesEnabled.IsChecked == true;
            _settings.TestCustomCaptureEnabled = _settings.TestEntitiesEnabled;
            _settings.TestCustomCaptureBind = TxtTestCustomCaptureBind.Text.Trim();
            _settings.TestFastUpExitEnabled = ChkTestFastUpExitEnabled.IsChecked ?? false;
            _settings.TestFastUpExitBind = TxtTestFastUpExitBind.Text.Trim();
            ReadAutoArmorFromUi();
            ReadAutoWaterFromUi();
            _settings.TestAutoFishingEnabled = ChkTestAutoFishingEnabled.IsChecked ?? false;
            _settings.TestAutoFishingBind = TxtTestAutoFishingBind.Text.Trim();
            _settings.TestAutoFishingCaptureBind = TxtTestAutoFishingCaptureBind.Text.Trim();
            _settings.TestAutoFishingRepairCommand = TxtTestAutoFishingRepairCommand.Text.Trim();
            _settings.TestAutoFishingRepairEverySeconds = Math.Clamp(ParseNonNegativeInt(TxtTestAutoFishingRepairEverySeconds.Text), 0, TestAutoFishingRepairIntervalMaxSeconds);
            _settings.EmergencyDamageSoundEnabled = ChkEmergencyDamageSoundEnabled.IsChecked == true;
            _settings.EmergencyDamageSoundTestMode = ChkEmergencyDamageSoundTestMode.IsChecked != false;
            _settings.EmergencyDamageSoundDeviceId = GetSelectedEmergencyDamageSoundDeviceId();
            _settings.EmergencyDamageSoundSimilarityPercent = Math.Clamp(
                (int)Math.Round(SlEmergencyDamageSoundSimilarity.Value),
                70,
                99);
            _settings.EmergencyReconnectEnabled = _settings.EmergencyDamageSoundEnabled;
            _settings.EmergencyReconnectDelaySeconds = Math.Clamp(
                ParseNonNegativeInt(TxtEmergencyReconnectDelaySeconds.Text),
                1,
                600);
            _settings.AutoReconnectEnabled = ChkAutoReconnectEnabled.IsChecked == true;
            AutoReconnectServerProfile? selectedAutoReconnectProfile = GetSelectedAutoReconnectServerProfile();
            if (selectedAutoReconnectProfile != null)
                ApplyAutoReconnectServerProfile(selectedAutoReconnectProfile, updateUi: false);
            _settings.TestFastUpExitBlockSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitBlockSlot, 2);
            _settings.TestFastUpExitPickaxeSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitPickaxeSlot, 1);
            string selectedPickaxeType = GetSelectedTestFastUpPickaxeType();
            _settings.TestFastUpExitPickaxeType = selectedPickaxeType;
            _settings.TestFastUpExitLookDurationEnabled = ChkTestFastUpExitLookMsEnabled?.IsChecked == true;
            _settings.TestFastUpExitBreakDurationEnabled = true;
            _settings.TestFastUpExitPlaceAfterJumpEnabled = ChkTestFastUpExitPlaceMsEnabled?.IsChecked == true;
            int selectedLookDurationMs = SlTestFastUpExitLookMs == null
                ? GetFastUpLookDurationForPickaxe(selectedPickaxeType)
                : Math.Clamp((int)Math.Round(SlTestFastUpExitLookMs.Value), FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            SetFastUpLookDurationForPickaxe(selectedPickaxeType, selectedLookDurationMs);
            int selectedBreakDurationMs = SlTestFastUpExitBreakMs == null
                ? GetFastUpBreakDurationForPickaxe(selectedPickaxeType)
                : NormalizeFastUpBreakDurationMs((int)Math.Round(SlTestFastUpExitBreakMs.Value));
            SetFastUpBreakDurationForPickaxe(selectedPickaxeType, selectedBreakDurationMs);
            _settings.TestFastUpExitPlaceAfterJumpMs = SlTestFastUpExitPlaceMs == null
                ? NormalizeFastUpPlaceAfterJumpMs(_settings.TestFastUpExitPlaceAfterJumpMs)
                : NormalizeFastUpPlaceAfterJumpMs((int)Math.Round(SlTestFastUpExitPlaceMs.Value));
            _settings.OverlayHudEnabled = ChkOverlayHudEnabled.IsChecked == true;
            _settings.OverlayAnimationsEnabled = ChkOverlayAnimationsEnabled.IsChecked == true;
            _settings.AnimatedBackgroundEnabled = ChkAnimatedBackgroundEnabled.IsChecked != false;
            _settings.ChatOpenKey = NormalizeMinecraftControlKey(TxtChatOpenKey.Text, "T");
            _settings.DropItemKey = NormalizeMinecraftControlKey(TxtDropItemKey.Text, "Q");
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

        private static int GetSelectedTestFastUpSlot(ComboBox? slotComboBox, int fallbackSlot)
        {
            if (slotComboBox == null)
                return Math.Clamp(fallbackSlot, 1, 9);

            int index = slotComboBox.SelectedIndex;
            if (index < 0 || index > 8)
                return Math.Clamp(fallbackSlot, 1, 9);

            return index + 1;
        }

        private string GetSelectedTestFastUpPickaxeType()
        {
            if (CbTestFastUpExitPickaxeType?.SelectedItem is ComboBoxItem item && item.Content is string content)
            {
                return NormalizeFastUpPickaxeType(content);
            }

            return NormalizeFastUpPickaxeType(_settings.TestFastUpExitPickaxeType);
        }

        private bool IsFastUpLookDurationEnabled()
        {
            if (ChkTestFastUpExitLookMsEnabled != null)
                return ChkTestFastUpExitLookMsEnabled.IsChecked == true;

            return _settings.TestFastUpExitLookDurationEnabled;
        }

        private bool IsFastUpBreakDurationEnabled()
        {
            return true;
        }

        private bool IsFastUpPlaceDurationEnabled()
        {
            if (ChkTestFastUpExitPlaceMsEnabled != null)
                return ChkTestFastUpExitPlaceMsEnabled.IsChecked == true;

            return _settings.TestFastUpExitPlaceAfterJumpEnabled;
        }

        private int GetConfiguredFastUpLookDurationMs()
        {
            if (!IsFastUpLookDurationEnabled())
                return FastUpLookDurationMinMs;

            if (SlTestFastUpExitLookMs == null)
                return GetFastUpLookDurationForPickaxe(GetSelectedTestFastUpPickaxeType());

            int value = (int)Math.Round(SlTestFastUpExitLookMs.Value);
            return Math.Clamp(value, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
        }

        private static int NormalizeFastUpBreakDurationMs(int valueMs)
        {
            if (valueMs <= 0)
                return FastUpBreakDurationDefaultMs;

            return Math.Clamp(valueMs, FastUpBreakDurationMinMs, FastUpBreakDurationMaxMs);
        }

        private static int NormalizeFastUpPlaceAfterJumpMs(int valueMs)
        {
            if (valueMs <= 0)
                return FastUpPlaceAfterJumpDefaultMs;

            return Math.Clamp(valueMs, FastUpPlaceAfterJumpMinMs, FastUpPlaceAfterJumpMaxMs);
        }

        private int GetConfiguredFastUpBreakDurationMs()
        {
            if (!IsFastUpBreakDurationEnabled())
                return FastUpBreakDurationMinMs;

            if (SlTestFastUpExitBreakMs == null)
                return GetFastUpBreakDurationForPickaxe(GetSelectedTestFastUpPickaxeType());

            int value = (int)Math.Round(SlTestFastUpExitBreakMs.Value);
            return NormalizeFastUpBreakDurationMs(value);
        }

        private int GetConfiguredFastUpPlaceAfterJumpMs()
        {
            if (!IsFastUpPlaceDurationEnabled())
                return FastUpPlaceAfterJumpMinMs;

            if (SlTestFastUpExitPlaceMs == null)
                return NormalizeFastUpPlaceAfterJumpMs(_settings.TestFastUpExitPlaceAfterJumpMs);

            int value = (int)Math.Round(SlTestFastUpExitPlaceMs.Value);
            return NormalizeFastUpPlaceAfterJumpMs(value);
        }

        private static bool IsValidFastUpPickaxeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return false;

            string trimmed = type.Trim();
            for (int i = 0; i < FastUpPickaxeTypes.Length; i++)
            {
                if (string.Equals(FastUpPickaxeTypes[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeFastUpPickaxeType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return FastUpDefaultPickaxeType;

            string trimmed = type.Trim();
            for (int i = 0; i < FastUpPickaxeTypes.Length; i++)
            {
                string candidate = FastUpPickaxeTypes[i];
                if (string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return FastUpDefaultPickaxeType;
        }

        private int GetFastUpLookDurationForPickaxe(string pickaxeType)
        {
            string normalizedType = NormalizeFastUpPickaxeType(pickaxeType);
            _settings.TestFastUpExitLookDurationByPickaxe ??= new Dictionary<string, int>();
            if (_settings.TestFastUpExitLookDurationByPickaxe.TryGetValue(normalizedType, out int configuredMs))
                return Math.Clamp(configuredMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);

            int fallbackMs = Math.Clamp(_settings.TestFastUpExitLookDurationMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            _settings.TestFastUpExitLookDurationByPickaxe[normalizedType] = fallbackMs;
            return fallbackMs;
        }

        private void SetFastUpLookDurationForPickaxe(string pickaxeType, int durationMs)
        {
            string normalizedType = NormalizeFastUpPickaxeType(pickaxeType);
            int clampedMs = Math.Clamp(durationMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            _settings.TestFastUpExitLookDurationByPickaxe ??= new Dictionary<string, int>();
            _settings.TestFastUpExitLookDurationByPickaxe[normalizedType] = clampedMs;
            _settings.TestFastUpExitLookDurationMs = clampedMs;
        }

        private int GetFastUpBreakDurationForPickaxe(string pickaxeType)
        {
            string normalizedType = NormalizeFastUpPickaxeType(pickaxeType);
            _settings.TestFastUpExitBreakDurationByPickaxe ??= new Dictionary<string, int>();
            if (_settings.TestFastUpExitBreakDurationByPickaxe.TryGetValue(normalizedType, out int configuredMs))
                return NormalizeFastUpBreakDurationMs(configuredMs);

            int fallbackMs = NormalizeFastUpBreakDurationMs(_settings.TestFastUpExitBreakDurationMs);
            _settings.TestFastUpExitBreakDurationByPickaxe[normalizedType] = fallbackMs;
            return fallbackMs;
        }

        private void SetFastUpBreakDurationForPickaxe(string pickaxeType, int durationMs)
        {
            string normalizedType = NormalizeFastUpPickaxeType(pickaxeType);
            int normalizedMs = NormalizeFastUpBreakDurationMs(durationMs);
            _settings.TestFastUpExitBreakDurationByPickaxe ??= new Dictionary<string, int>();
            _settings.TestFastUpExitBreakDurationByPickaxe[normalizedType] = normalizedMs;
            _settings.TestFastUpExitBreakDurationMs = normalizedMs;
        }

        private void ApplyFastUpLookSliderForPickaxe(string pickaxeType)
        {
            int lookDurationMs = GetFastUpLookDurationForPickaxe(pickaxeType);
            if (SlTestFastUpExitLookMs != null)
                SlTestFastUpExitLookMs.Value = lookDurationMs;
            UpdateTestFastUpExitLookDurationLabel(lookDurationMs);
        }

        private void ApplyFastUpBreakSliderForPickaxe(string pickaxeType)
        {
            int breakDurationMs = GetFastUpBreakDurationForPickaxe(pickaxeType);
            if (SlTestFastUpExitBreakMs != null)
                SlTestFastUpExitBreakMs.Value = breakDurationMs;
            UpdateTestFastUpExitBreakDurationLabel(breakDurationMs);
        }

        private void UpdateTestFastUpExitLookDurationLabel(int valueMs)
        {
            if (TxtTestFastUpExitLookMsValue == null)
                return;

            TxtTestFastUpExitLookMsValue.Text = $"{Math.Clamp(valueMs, FastUpLookDurationMinMs, FastUpLookDurationMaxMs)} ms";
        }

        private void UpdateTestFastUpExitBreakDurationLabel(int valueMs)
        {
            if (TxtTestFastUpExitBreakMsValue == null)
                return;

            TxtTestFastUpExitBreakMsValue.Text = $"{NormalizeFastUpBreakDurationMs(valueMs)} ms";
        }

        private void UpdateTestFastUpExitPlaceDurationLabel(int valueMs)
        {
            if (TxtTestFastUpExitPlaceMsValue == null)
                return;

            TxtTestFastUpExitPlaceMsValue.Text = $"{NormalizeFastUpPlaceAfterJumpMs(valueMs)} ms";
        }

        private void ClearTestF3LiveReadings()
        {
            if (TxtTestLiveEntities != null)
                TxtTestLiveEntities.Text = "-";
        }

        private bool TryGetTestAutoFishingCaptureArea(IntPtr windowHandle, out Drawing.Rectangle captureArea)
        {
            captureArea = Drawing.Rectangle.Empty;
            if (!HasTestAutoFishingAreaConfigured())
                return false;
            if (!TryGetWindowClientRectOnScreen(windowHandle, out RECT clientRect))
                return false;

            int clientWidth = Math.Max(0, clientRect.Right - clientRect.Left);
            int clientHeight = Math.Max(0, clientRect.Bottom - clientRect.Top);
            if (clientWidth < MinimumCaptureSelectionSize || clientHeight < MinimumCaptureSelectionSize)
                return false;

            int offsetX = Math.Clamp(_settings.TestAutoFishingCaptureX, 0, Math.Max(0, clientWidth - MinimumCaptureSelectionSize));
            int offsetY = Math.Clamp(_settings.TestAutoFishingCaptureY, 0, Math.Max(0, clientHeight - MinimumCaptureSelectionSize));
            int width = Math.Clamp(_settings.TestAutoFishingCaptureWidth, MinimumCaptureSelectionSize, Math.Max(MinimumCaptureSelectionSize, clientWidth - offsetX));
            int height = Math.Clamp(_settings.TestAutoFishingCaptureHeight, MinimumCaptureSelectionSize, Math.Max(MinimumCaptureSelectionSize, clientHeight - offsetY));
            if (offsetX + width > clientWidth)
                width = clientWidth - offsetX;
            if (offsetY + height > clientHeight)
                height = clientHeight - offsetY;
            if (width < MinimumCaptureSelectionSize || height < MinimumCaptureSelectionSize)
                return false;

            captureArea = new Drawing.Rectangle(clientRect.Left + offsetX, clientRect.Top + offsetY, width, height);
            return true;
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
                _f3TesseractEngine.SetVariable("tessedit_char_whitelist", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:/;|\\-()[] ");
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

        private static Drawing.Bitmap PrepareBitmapForOcr(Drawing.Bitmap source)
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

        private static bool TryGetVisibleCursorInfo(out CURSORINFO cursorInfo)
        {
            cursorInfo = new CURSORINFO
            {
                cbSize = Marshal.SizeOf<CURSORINFO>()
            };

            if (!GetCursorInfo(out cursorInfo))
                return false;

            return (cursorInfo.flags & CURSOR_SHOWING) == CURSOR_SHOWING;
        }

        private bool IsInventoryCursorVisible()
        {
            if (!TryGetVisibleCursorInfo(out CURSORINFO cursorInfo))
                return false;

            // LWJGL 2 keeps the Windows cursor technically "visible" while gameplay
            // is active, but replaces it with a fully transparent cursor. This is
            // especially noticeable in fullscreen and must not be treated as GUI.
            if (IsCursorVisuallyBlank(cursorInfo.hCursor))
                return false;

            return true;
        }

        private static bool IsCursorVisuallyBlank(IntPtr cursorHandle)
        {
            if (cursorHandle == IntPtr.Zero)
                return false;

            lock (CursorAppearanceCacheLock)
            {
                if (CursorBlankAppearanceCache.TryGetValue(cursorHandle, out bool cached))
                    return cached;
            }

            bool isBlank = DetectBlankCursor(cursorHandle);
            lock (CursorAppearanceCacheLock)
            {
                if (CursorBlankAppearanceCache.Count >= 32)
                    CursorBlankAppearanceCache.Clear();
                CursorBlankAppearanceCache[cursorHandle] = isBlank;
            }

            return isBlank;
        }

        private static bool DetectBlankCursor(IntPtr cursorHandle)
        {
            int width = Math.Clamp(GetSystemMetrics(SM_CXCURSOR), 1, 128);
            int height = Math.Clamp(GetSystemMetrics(SM_CYCURSOR), 1, 128);

            return !CursorChangesBackground(cursorHandle, width, height, Drawing.Color.Black)
                && !CursorChangesBackground(cursorHandle, width, height, Drawing.Color.White);
        }

        private static bool CursorChangesBackground(
            IntPtr cursorHandle,
            int width,
            int height,
            Drawing.Color background)
        {
            using var bitmap = new Drawing.Bitmap(width, height, DrawingImaging.PixelFormat.Format32bppArgb);
            using Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap);
            graphics.Clear(background);

            IntPtr hdc = graphics.GetHdc();
            bool drawn;
            try
            {
                drawn = DrawIconEx(
                    hdc,
                    0,
                    0,
                    cursorHandle,
                    width,
                    height,
                    0,
                    IntPtr.Zero,
                    DI_NORMAL);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            // If Windows refuses to render the cursor, keep the safety pause enabled.
            if (!drawn)
                return true;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Drawing.Color pixel = bitmap.GetPixel(x, y);
                    if (pixel.R != background.R || pixel.G != background.G || pixel.B != background.B)
                        return true;
                }
            }

            return false;
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

            UpdateTestAutoFishingStatusLabel();
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
                case "ENTER":
                case "RETURN":
                    virtualKey = VK_RETURN;
                    return true;
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

        private static bool IsSupportedMinecraftControlKey(string keyText)
        {
            if (!TryGetVirtualKey(keyText, out int virtualKey))
                return false;

            return virtualKey is not VK_LBUTTON
                and not VK_RBUTTON
                and not VK_MBUTTON
                and not VK_XBUTTON1
                and not VK_XBUTTON2;
        }

        private static string NormalizeMinecraftControlKey(string? keyText, string fallback)
        {
            string normalized = string.IsNullOrWhiteSpace(keyText) ? fallback : keyText.Trim();
            if (string.Equals(normalized, "Return", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Enter", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "Enter";
            }
            return IsSupportedMinecraftControlKey(normalized) ? normalized : fallback;
        }

        private int GetConfiguredChatOpenVirtualKey()
        {
            string keyText = NormalizeMinecraftControlKey(TxtChatOpenKey?.Text, "T");
            return TryGetVirtualKey(keyText, out int virtualKey) ? virtualKey : VK_T;
        }

        private int GetConfiguredDropItemVirtualKey()
        {
            string keyText = NormalizeMinecraftControlKey(TxtDropItemKey?.Text, "Q");
            return TryGetVirtualKey(keyText, out int virtualKey) ? virtualKey : VK_Q;
        }

        private void SendChatOpenKeyTap()
        {
            SendKeyTap(GetConfiguredChatOpenVirtualKey());
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

        private bool IsPhysicalMouseButtonDown(int mouseVirtualKey)
        {
            // At rest we inject no mouse buttons, so polling is sufficient to
            // recognize the first bind+mouse press without a global hook.
            if (mouseVirtualKey == VK_LBUTTON)
                return GetMouseHookHandle() != IntPtr.Zero ? _physicalLeftButtonDown : IsVirtualKeyDown(VK_LBUTTON);

            if (mouseVirtualKey == VK_RBUTTON)
                return GetMouseHookHandle() != IntPtr.Zero ? _physicalRightButtonDown : IsVirtualKeyDown(VK_RBUTTON);

            return IsVirtualKeyDown(mouseVirtualKey);
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

        private void SyncAutoComboState(string bindKeyText, int mouseVirtualKey, ref bool comboWasDown, ref bool stopWasDown)
        {
            bool bindDown = IsConfiguredBindKeyDown(bindKeyText);
            bool mouseDown = IsPhysicalMouseButtonDown(mouseVirtualKey);
            comboWasDown = bindDown && mouseDown;
            stopWasDown = !mouseDown;
        }

        private void SyncAutoComboStates()
        {
            SyncAutoComboState(TxtAutoLeftKey.Text, VK_LBUTTON, ref _autoLeftComboTriggerWasDown, ref _autoLeftComboStopWasDown);
            SyncAutoComboState(TxtAutoRightKey.Text, VK_RBUTTON, ref _autoRightComboTriggerWasDown, ref _autoRightComboStopWasDown);
        }

        private bool TryHandleAutoComboToggle(
            string bindKeyText,
            int mouseVirtualKey,
            ref bool comboWasDown,
            ref bool stopWasDown,
            ref bool runtimeEnabled,
            string enabledMessage,
            string disabledMessage,
            bool allowEnable = true,
            string? blockedMessage = null)
        {
            bool bindDown = IsConfiguredBindKeyDown(bindKeyText);
            bool mouseDown = IsPhysicalMouseButtonDown(mouseVirtualKey);
            bool comboDown = bindDown && mouseDown;
            bool stopDown = !mouseDown;
            bool changed = false;

            // Start on fresh combo press (bind + mouse).
            if (comboDown && !comboWasDown && !runtimeEnabled)
            {
                if (allowEnable)
                {
                    runtimeEnabled = true;
                    UpdateStatusBar(enabledMessage, "Orange");
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(blockedMessage))
                {
                    UpdateStatusBar(blockedMessage, "Red");
                }
            }

            // Stop when mouse button is released.
            if (stopDown && !stopWasDown && runtimeEnabled)
            {
                runtimeEnabled = false;
                UpdateStatusBar(disabledMessage, "Orange");
                changed = true;
            }

            comboWasDown = comboDown;
            stopWasDown = stopDown;
            return changed;
        }

        private bool TryHandleAutoHoldBind(
            string bindKeyText,
            ref bool bindWasDown,
            ref bool runtimeEnabled,
            string enabledMessage,
            string disabledMessage,
            bool allowEnable,
            string blockedMessage)
        {
            bool bindDown = IsConfiguredBindKeyDown(bindKeyText);
            bool changed = false;

            if (bindDown && !bindWasDown && !runtimeEnabled)
            {
                if (allowEnable)
                {
                    runtimeEnabled = true;
                    UpdateStatusBar(enabledMessage, "Orange");
                    changed = true;
                }
                else
                {
                    UpdateStatusBar(blockedMessage, "Red");
                }
            }
            else if (!bindDown && runtimeEnabled)
            {
                runtimeEnabled = false;
                UpdateStatusBar(disabledMessage, "Orange");
                changed = true;
            }

            // A press made while a GUI is open is deliberately consumed here.
            // Closing the GUI while the key is still held must not start clicking.
            bindWasDown = bindDown;
            return changed;
        }

        private bool TryToggleHoldLeftClicking(DateTime now)
        {
            // GetAsyncKeyState also sees our injected clicks. Use the low-level hook
            // state so the HOLD toggle can only react to a real mouse press.
            bool leftDown = IsPhysicalMouseButtonDown(VK_LBUTTON);

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

            ReleaseHoldRightInjectedButton();
            _holdLeftToggleWasDown = false;
            _holdLeftToggleDownStartedAtUtc = DateTime.MinValue;
            _nextHoldLeftClickAtUtc = DateTime.UtcNow;
            _holdRightRuntimePressActive = false;
        }

        private void ReleaseHoldRightInjectedButton()
        {
            if (!_holdRightInjectedButtonDown)
                return;

            SendMouseButton(leftButton: false, down: false);
            _holdRightInjectedButtonDown = false;
        }

        private void RunMacroTick(object? sender, EventArgs e)
        {
            try
            {
                RunMacroTickCore();
            }
            finally
            {
                // Also runs after early returns (focus loss, bind capture, etc.).
                SuspendMouseHookWhenIdle();
                UpdateEmergencyDamageSoundMonitoring();
            }
        }

        private void RunMacroTickCore()
        {
            _macroDiagnosticsService.RecordUiTick();

            if (_isLoadingUi)
            {
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                SetInventoryCleanupEatingHold(false);
                return;
            }

            if (_bindCaptureTarget != BindTarget.None || _bindyCaptureEntry != null)
            {
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                ReleaseHoldRightInjectedButton();
                SetInventoryCleanupEatingHold(false);
                return;
            }

            if (_suppressBindToggleUntilRelease)
            {
                bool holdDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                bool autoLeftDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                bool autoRightDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                bool jablkaDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                bool kop533Down = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                bool kop633Down = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);
                bool testCaptureDown = IsConfiguredBindKeyDown(TxtTestCustomCaptureBind.Text);
                bool fastUpDown = IsConfiguredBindKeyDown(TxtTestFastUpExitBind.Text);
                bool autoArmorDown = IsConfiguredBindKeyDown(TxtAutoArmorBind.Text);
                bool autoWaterDown = IsConfiguredBindKeyDown(TxtAutoWaterBind.Text);
                bool autoFishingDown = IsConfiguredBindKeyDown(TxtTestAutoFishingBind.Text);
                bool autoFishingCaptureDown = IsConfiguredBindKeyDown(TxtTestAutoFishingCaptureBind.Text);
                bool bindyDown = IsAnyBindyKeyDown();

                _holdBindWasDown = holdDown;
                _autoLeftBindWasDown = autoLeftDown;
                _autoRightBindWasDown = autoRightDown;
                _jablkaBindWasDown = jablkaDown;
                _kopacz533BindWasDown = kop533Down;
                _kopacz633BindWasDown = kop633Down;
                _testCaptureBindWasDown = testCaptureDown;
                _testFastUpExitBindWasDown = fastUpDown;
                _autoArmorBindWasDown = autoArmorDown;
                _autoWaterBindWasDown = autoWaterDown;
                _testAutoFishingBindWasDown = autoFishingDown;
                _testAutoFishingCaptureBindWasDown = autoFishingCaptureDown;
                SyncAutoComboStates();

                if (holdDown || autoLeftDown || autoRightDown || jablkaDown || kop533Down || kop633Down || testCaptureDown || fastUpDown || autoArmorDown || autoWaterDown || autoFishingDown || autoFishingCaptureDown || bindyDown)
                {
                    _autoClickScheduler.Stop();
                    SetAutoLeftDabHold(false);
                    ReleaseHoldRightInjectedButton();
                    SetInventoryCleanupEatingHold(false);
                    return;
                }

                _suppressBindToggleUntilRelease = false;
            }

            // Do an immediate handle check as well as the slower focus timer. This
            // prevents even a short burst of input from leaking after Alt+Tab.
            bool targetWindowFocusedNow = _targetGameWindowHandle != IntPtr.Zero
                && GetForegroundWindow() == _targetGameWindowHandle;

            // Bind toggles can be changed only while Minecraft window has focus.
            if (!_isMinecraftFocused || !targetWindowFocusedNow)
            {
                if (_inventoryCleanupStage == InventoryCleanupStage.ReturnToMiningStart)
                {
                    DateTime focusLostAtUtc = DateTime.UtcNow;
                    ResetInventoryCleanupState(scheduleNext: false, focusLostAtUtc);
                    _nextInventoryCleanupAtUtc = focusLostAtUtc;
                    UpdateInventoryCleanupStatus("Powrót przed Auto EQ przerwany po utracie fokusu. Próba zostanie ponowiona po powrocie do gry.", "Orange");
                }

                // Keep key state in sync to avoid accidental toggle right after refocus.
                _holdBindWasDown = IsConfiguredBindKeyDown(TxtMacroManualKey.Text);
                _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                _jablkaBindWasDown = IsConfiguredBindKeyDown(TxtJablkaZLisciKey.Text);
                _kopacz533BindWasDown = IsConfiguredBindKeyDown(TxtKopacz533Key.Text);
                _kopacz633BindWasDown = IsConfiguredBindKeyDown(TxtKopacz633Key.Text);
                _testCaptureBindWasDown = IsConfiguredBindKeyDown(TxtTestCustomCaptureBind.Text);
                _testFastUpExitBindWasDown = IsConfiguredBindKeyDown(TxtTestFastUpExitBind.Text);
                _autoArmorBindWasDown = IsConfiguredBindKeyDown(TxtAutoArmorBind.Text);
                _autoWaterBindWasDown = IsConfiguredBindKeyDown(TxtAutoWaterBind.Text);
                _testAutoFishingBindWasDown = IsConfiguredBindKeyDown(TxtTestAutoFishingBind.Text);
                _testAutoFishingCaptureBindWasDown = IsConfiguredBindKeyDown(TxtTestAutoFishingCaptureBind.Text);
                SyncBindyKeyStates();
                SyncAutoComboStates();

                SetCursorPauseState(false);
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                SetKopacz533MiningHold(false);
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetTestFastUpExitRuntimeState();
                ResetTestAutoFishingRuntimeState();
                if (IsAutoArmorRunning)
                    CancelAutoArmor("Auto zbroja przerwana po utracie fokusu. Sprawdź EQ przed kolejną próbą.", Brushes.OrangeRed, closeInventory: false);
                if (_autoWaterStage != AutoWaterStage.None)
                    CancelAutoWater("AutoWater przerwany po utracie fokusu Minecrafta.", Brushes.OrangeRed, restoreSlot: false);
                ResetHoldLeftToggleState(clearToggleEnabled: false);
                ResetBindyRuntimeState();
                SetInventoryCleanupEatingHold(false);
                return;
            }

            // STOP has priority over every mining stage, including open inventory,
            // command entry and reconnect. Starting a macro still uses the guards
            // below; this path can only stop work that is already active.
            if (TryStopMiningFromBinds())
                return;

            if (_autoReconnectStage != AutoReconnectStage.None)
            {
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                SetKopacz533MiningHold(false);
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                SetInventoryCleanupEatingHold(false);
                return;
            }

            DateTime autoWaterNow = DateTime.UtcNow;
            if (TryHandleAutoArmorCalibration(autoWaterNow))
                return;
            if (TryHandleAutoWaterCalibrationOrTest(autoWaterNow))
                return;

            bool changed = false;

            bool holdModeSelected = ChkMacroManualEnabled.IsChecked == true;
            bool autoLeftModeSelected = ChkAutoLeftEnabled.IsChecked == true;
            bool autoRightModeSelected = ChkAutoRightEnabled.IsChecked == true;
            bool jablkaModeSelected = ChkJablkaZLisciEnabled.IsChecked == true;
            bool kop533ModeSelected = ChkKopacz533Enabled.IsChecked == true;
            bool kop633ModeSelected = ChkKopacz633Enabled.IsChecked == true;
            bool bindyModeSelected = ChkBindyEnabled.IsChecked == true;
            bool testCaptureModeSelected = ChkTestEntitiesEnabled.IsChecked == true;
            bool fastUpModeSelected = ChkTestFastUpExitEnabled.IsChecked == true;
            bool autoArmorModeSelected = ChkAutoArmorEnabled.IsChecked == true;
            bool autoWaterModeSelected = ChkAutoWaterEnabled.IsChecked == true;
            bool autoFishingModeSelected = ChkTestAutoFishingEnabled.IsChecked == true;
            bool internalCommandTyping =
                _jablkaCommandStage != JablkaCommandStage.None ||
                _kopacz533CommandStage != Kopacz533CommandStage.None ||
                _kopacz633CommandStage != Kopacz633CommandStage.None ||
                _bindyCommandStage != BindyCommandStage.None ||
                _testAutoFishingRepairStage != TestAutoFishingRepairStage.None ||
                _inventoryCleanupStage != InventoryCleanupStage.None;
            bool cursorVisibleForActivation = IsInventoryCursorVisible();

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
                _testFastUpExitBindWasDown = IsConfiguredBindKeyDown(TxtTestFastUpExitBind.Text);
                _autoArmorBindWasDown = IsConfiguredBindKeyDown(TxtAutoArmorBind.Text);
                _autoWaterBindWasDown = IsConfiguredBindKeyDown(TxtAutoWaterBind.Text);
                _testAutoFishingBindWasDown = IsConfiguredBindKeyDown(TxtTestAutoFishingBind.Text);
                _testAutoFishingCaptureBindWasDown = IsConfiguredBindKeyDown(TxtTestAutoFishingCaptureBind.Text);
                SyncBindyKeyStates();
                SyncAutoComboStates();
            }

            if (!internalCommandTyping && testCaptureModeSelected && IsBindPressed(TxtTestCustomCaptureBind.Text, ref _testCaptureBindWasDown))
                BeginTestCaptureAreaSelectionFromBind();

            if (!internalCommandTyping && autoFishingModeSelected && IsBindPressed(TxtTestAutoFishingCaptureBind.Text, ref _testAutoFishingCaptureBindWasDown))
                BeginTestAutoFishingAreaSelectionFromBind();

            if (!internalCommandTyping
                && autoArmorModeSelected
                && IsBindPressed(TxtAutoArmorBind.Text, ref _autoArmorBindWasDown))
            {
                if (IsAutoArmorRunning)
                    RequestCancelAutoArmor("Anulowanie bindem — kończę bezpiecznie bieżące przełożenie.");
                else
                    TryStartAutoArmor(DateTime.UtcNow);
                changed = true;
            }

            if (IsAutoArmorRunning)
            {
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                ReleaseHoldRightInjectedButton();
                RunAutoArmorTick(DateTime.UtcNow);
                RefreshLiveTopTiles(DateTime.UtcNow);
                RefreshOverlayHud(DateTime.UtcNow);
                return;
            }

            if (!internalCommandTyping
                && autoWaterModeSelected
                && IsBindPressed(TxtAutoWaterBind.Text, ref _autoWaterBindWasDown))
            {
                if (_autoWaterStage != AutoWaterStage.None)
                {
                    CancelAutoWater("AutoWater anulowany bindem.", Brushes.Orange, restoreSlot: true);
                    UpdateStatusBar("AutoWater anulowany", "Orange");
                }
                else
                {
                    TryStartAutoWater(DateTime.UtcNow);
                }
                changed = true;
            }

            if (_autoWaterStage != AutoWaterStage.None)
            {
                _autoClickScheduler.Stop();
                SetAutoLeftDabHold(false);
                RunAutoWaterTick(DateTime.UtcNow);
                RefreshLiveTopTiles(DateTime.UtcNow);
                RefreshOverlayHud(DateTime.UtcNow);
                return;
            }

            if (!internalCommandTyping && IsBindPressed(TxtTestFastUpExitBind.Text, ref _testFastUpExitBindWasDown) && fastUpModeSelected)
            {
                _testFastUpExitRuntimeEnabled = !_testFastUpExitRuntimeEnabled;
                if (_testFastUpExitRuntimeEnabled)
                {
                    StopClickerRuntimesForExclusivePointerMacro();
                    StopOtherExclusivePointerMacros(keepFastUp: true);
                }
                ResetTestFastUpExitRuntimeState(DateTime.UtcNow);
                UpdateStatusBar(_testFastUpExitRuntimeEnabled ? "Szybkie wyjście do góry aktywowane" : "Szybkie wyjście do góry wyłączone", "Orange");
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtTestAutoFishingBind.Text, ref _testAutoFishingBindWasDown) && autoFishingModeSelected)
            {
                if (!HasTestAutoFishingAreaConfigured())
                {
                    _testAutoFishingRuntimeEnabled = false;
                    ResetTestAutoFishingRuntimeState(DateTime.UtcNow);
                    UpdateStatusBar("Auto łowienie: najpierw zaznacz obszar spławika", "Orange");
                }
                else
                {
                    bool enabling = !_testAutoFishingRuntimeEnabled;
                    _testAutoFishingRuntimeEnabled = enabling;
                    DateTime toggledAtUtc = DateTime.UtcNow;
                    if (enabling)
                    {
                        _testAutoFishingRuntimeStartedAtUtc = toggledAtUtc;
                        ResetTestAutoFishingCatchStats();
                    }
                    else
                    {
                        _testAutoFishingRuntimeStartedAtUtc = DateTime.MinValue;
                    }
                    ResetTestAutoFishingRuntimeState(toggledAtUtc);
                    if (enabling)
                        StartTestAutoFishingCastRegistrationDelay(toggledAtUtc);
                    UpdateStatusBar(enabling ? "Auto łowienie aktywowane" : "Auto łowienie wyłączone", "Orange");
                }
                UpdateTestAutoFishingStatusLabel();
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtMacroManualKey.Text, ref _holdBindWasDown) && holdModeSelected)
            {
                bool enabling = !_holdMacroRuntimeEnabled;
                bool holdMacroBlocked = enabling && cursorVisibleForActivation;
                if (holdMacroBlocked)
                {
                    UpdateStatusBar("Nie uruchomiono HOLD LPM/PPM: zamknij ekwipunek, chat lub inne GUI.", "Red");
                    ResetHoldLeftToggleState(clearToggleEnabled: true);
                }
                else
                {
                    _holdMacroRuntimeEnabled = enabling;
                    if (_holdMacroRuntimeEnabled)
                        StopExclusivePointerMacrosForClicker();
                    ResetHoldLeftToggleState(clearToggleEnabled: true);
                    UpdateStatusBar(_holdMacroRuntimeEnabled ? "HOLD aktywowane" : "HOLD wyłączone", "Orange");
                    changed = true;
                }
            }

            if (!internalCommandTyping && autoLeftModeSelected)
            {
                bool autoLeftHoldBindMode = ChkAutoLeftHoldBindMode.IsChecked == true;
                bool autoLeftComboMode = ChkAutoLeftComboMode.IsChecked == true;
                if (autoLeftHoldBindMode)
                {
                    _autoLeftComboTriggerWasDown = false;
                    _autoLeftComboStopWasDown = false;
                    if (TryHandleAutoHoldBind(
                        TxtAutoLeftKey.Text,
                        ref _autoLeftBindWasDown,
                        ref _autoLeftRuntimeEnabled,
                        "AUTO LPM aktywowane (trzymanie bindu)",
                        "AUTO LPM wyłączone (puszczono bind)",
                        allowEnable: !cursorVisibleForActivation,
                        blockedMessage: "Nie uruchomiono AUTO LPM: zamknij ekwipunek, chat lub inne GUI."))
                    {
                        if (_autoLeftRuntimeEnabled)
                            StopExclusivePointerMacrosForClicker();
                        else
                            _autoClickScheduler.StopLeftImmediately();
                        changed = true;
                    }
                }
                else if (autoLeftComboMode)
                {
                    _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
                    if (TryHandleAutoComboToggle(
                        TxtAutoLeftKey.Text,
                        VK_LBUTTON,
                        ref _autoLeftComboTriggerWasDown,
                        ref _autoLeftComboStopWasDown,
                        ref _autoLeftRuntimeEnabled,
                        "AUTO LPM aktywowane (bind + LPM)",
                        "AUTO LPM wyłączone (puszczono LPM)",
                        allowEnable: !cursorVisibleForActivation,
                        blockedMessage: "Nie uruchomiono AUTO LPM: zamknij ekwipunek, chat lub inne GUI."))
                    {
                        if (_autoLeftRuntimeEnabled)
                            StopExclusivePointerMacrosForClicker();
                        else
                            _autoClickScheduler.StopLeftImmediately();
                        changed = true;
                    }
                }
                else
                {
                    _autoLeftComboTriggerWasDown = false;
                    _autoLeftComboStopWasDown = false;
                    if (IsBindPressed(TxtAutoLeftKey.Text, ref _autoLeftBindWasDown))
                    {
                        bool enabling = !_autoLeftRuntimeEnabled;
                        if (enabling && cursorVisibleForActivation)
                        {
                            UpdateStatusBar("Nie uruchomiono AUTO LPM: zamknij ekwipunek, chat lub inne GUI.", "Red");
                        }
                        else
                        {
                            _autoLeftRuntimeEnabled = enabling;
                            if (_autoLeftRuntimeEnabled)
                                StopExclusivePointerMacrosForClicker();
                            else
                                _autoClickScheduler.StopLeftImmediately();
                            UpdateStatusBar(_autoLeftRuntimeEnabled ? "AUTO LPM aktywowane" : "AUTO LPM wyłączone", "Orange");
                            changed = true;
                        }
                    }
                }
            }
            else
            {
                _autoLeftComboTriggerWasDown = false;
                _autoLeftComboStopWasDown = false;
            }

            if (!internalCommandTyping && autoRightModeSelected)
            {
                bool autoRightHoldBindMode = ChkAutoRightHoldBindMode.IsChecked == true;
                bool autoRightComboMode = ChkAutoRightComboMode.IsChecked == true;
                if (autoRightHoldBindMode)
                {
                    _autoRightComboTriggerWasDown = false;
                    _autoRightComboStopWasDown = false;
                    if (TryHandleAutoHoldBind(
                        TxtAutoRightKey.Text,
                        ref _autoRightBindWasDown,
                        ref _autoRightRuntimeEnabled,
                        "AUTO PPM aktywowane (trzymanie bindu)",
                        "AUTO PPM wyłączone (puszczono bind)",
                        allowEnable: !cursorVisibleForActivation,
                        blockedMessage: "Nie uruchomiono AUTO PPM: zamknij ekwipunek, chat lub inne GUI."))
                    {
                        if (_autoRightRuntimeEnabled)
                            StopExclusivePointerMacrosForClicker();
                        else
                            _autoClickScheduler.StopRightImmediately();
                        changed = true;
                    }
                }
                else if (autoRightComboMode)
                {
                    _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
                    if (TryHandleAutoComboToggle(
                        TxtAutoRightKey.Text,
                        VK_RBUTTON,
                        ref _autoRightComboTriggerWasDown,
                        ref _autoRightComboStopWasDown,
                        ref _autoRightRuntimeEnabled,
                        "AUTO PPM aktywowane (bind + PPM)",
                        "AUTO PPM wyłączone (puszczono PPM)",
                        allowEnable: !cursorVisibleForActivation,
                        blockedMessage: "Nie uruchomiono AUTO PPM: zamknij ekwipunek, chat lub inne GUI."))
                    {
                        if (_autoRightRuntimeEnabled)
                            StopExclusivePointerMacrosForClicker();
                        else
                            _autoClickScheduler.StopRightImmediately();
                        changed = true;
                    }
                }
                else
                {
                    _autoRightComboTriggerWasDown = false;
                    _autoRightComboStopWasDown = false;
                    if (IsBindPressed(TxtAutoRightKey.Text, ref _autoRightBindWasDown))
                    {
                        bool enabling = !_autoRightRuntimeEnabled;
                        if (enabling && cursorVisibleForActivation)
                        {
                            UpdateStatusBar("Nie uruchomiono AUTO PPM: zamknij ekwipunek, chat lub inne GUI.", "Red");
                        }
                        else
                        {
                            _autoRightRuntimeEnabled = enabling;
                            if (_autoRightRuntimeEnabled)
                                StopExclusivePointerMacrosForClicker();
                            else
                                _autoClickScheduler.StopRightImmediately();
                            UpdateStatusBar(_autoRightRuntimeEnabled ? "AUTO PPM aktywowane" : "AUTO PPM wyłączone", "Orange");
                            changed = true;
                        }
                    }
                }
            }
            else
            {
                _autoRightComboTriggerWasDown = false;
                _autoRightComboStopWasDown = false;
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
                ToggleKopacz533Runtime();
                changed = true;
            }

            if (!internalCommandTyping && IsBindPressed(TxtKopacz633Key.Text, ref _kopacz633BindWasDown) && kop633ModeSelected)
            {
                ToggleKopacz633Runtime();
                changed = true;
            }

            if (!internalCommandTyping
                && bindyModeSelected
                && _bindyCommandStage == BindyCommandStage.None
                && TryGetPressedBindyEntry(allowActivation: !cursorVisibleForActivation, out BindyEntry bindyEntry))
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
                _autoClickScheduler.StopLeftImmediately();
                changed = true;
            }
            if (!autoRightModeSelected && _autoRightRuntimeEnabled)
            {
                _autoRightRuntimeEnabled = false;
                _autoClickScheduler.StopRightImmediately();
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
                EndMiningLogRun(InventoryCleanupOwner.Kopacz533, "Kopanie zatrzymane: kanał nie jest już aktywny.");
                changed = true;
            }
            if (!kop633ModeSelected && _kopacz633RuntimeEnabled)
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
                EndMiningLogRun(InventoryCleanupOwner.Kopacz633, "Kopanie zatrzymane: kanał nie jest już aktywny.");
                changed = true;
            }
            if (!fastUpModeSelected && _testFastUpExitRuntimeEnabled)
            {
                _testFastUpExitRuntimeEnabled = false;
                ResetTestFastUpExitRuntimeState();
                changed = true;
            }
            if (!autoFishingModeSelected && _testAutoFishingRuntimeEnabled)
            {
                _testAutoFishingRuntimeEnabled = false;
                ResetTestAutoFishingRuntimeState();
                UpdateTestAutoFishingStatusLabel();
                changed = true;
            }
            if (!autoWaterModeSelected && _autoWaterStage != AutoWaterStage.None)
            {
                CancelAutoWater("AutoWater zatrzymany: moduł został wyłączony.", Brushes.Orange, restoreSlot: false);
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
            {
                RefreshTopTiles();
            }

            DateTime now = DateTime.UtcNow;
            bool pauseWhenCursorVisible = ChkPauseWhenCursorVisible.IsChecked == true;
            bool anyCursorPauseClickerRuntimeActive =
                (holdModeSelected && _holdMacroRuntimeEnabled) ||
                (autoLeftModeSelected && _autoLeftRuntimeEnabled) ||
                (autoRightModeSelected && _autoRightRuntimeEnabled);

            // A visible cursor blocks only LPM/PPM clickers. Kopacz, Jabłka,
            // fishing and experimental modules must not react to cursor visibility.
            bool shouldPauseForCursor = pauseWhenCursorVisible
                && anyCursorPauseClickerRuntimeActive
                && _inventoryCleanupStage == InventoryCleanupStage.None
                && IsInventoryCursorVisible();

            SetCursorPauseState(shouldPauseForCursor);
            if (shouldPauseForCursor)
            {
                _autoClickScheduler.Stop();
                if (SetAutoLeftDabHold(false))
                    RefreshTopTiles();
                _nextHoldLeftClickAtUtc = now;
                _nextHoldRightClickAtUtc = now;
                ResetHoldLeftToggleState(clearToggleEnabled: false);
                _nextAutoLeftClickAtUtc = now;
                _nextAutoRightClickAtUtc = now;
                return;
            }

            if (SetAutoLeftDabHold(autoLeftModeSelected && _autoLeftRuntimeEnabled && ChkAutoLeftDabMode.IsChecked == true && !internalCommandTyping))
                RefreshTopTiles();

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

                    if (!_holdLeftToggleClickingEnabled)
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
                bool rightHoldActive = !internalCommandTyping && holdRightEnabled && IsPhysicalMouseButtonDown(VK_RBUTTON);
                if (rightHoldActive != rightHoldWasActive)
                {
                    _holdRightRuntimePressActive = rightHoldActive;
                    RefreshTopTiles();
                }

                if (holdRightEnabled && rightHoldActive)
                {
                    // HOLD PPM is emitted by AutoClickScheduler together with the
                    // regular clickers. The UI timer is not precise enough for 20 CPS.
                    _holdRightInjectedButtonDown = true;
                }
                else
                {
                    _nextHoldRightClickAtUtc = now;
                    if (rightHoldWasActive || _holdRightInjectedButtonDown)
                        ReleaseHoldRightInjectedButton();
                }
            }
            else
            {
                _nextHoldLeftClickAtUtc = now;
                _nextHoldRightClickAtUtc = now;
                ResetHoldLeftToggleState(clearToggleEnabled: true);
            }

            UpdateAutoClickScheduler(holdModeSelected, autoLeftModeSelected, autoRightModeSelected, internalCommandTyping);

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

            if (_inventoryCleanupStage == InventoryCleanupStage.None)
            {
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

                if (fastUpModeSelected && _testFastUpExitRuntimeEnabled && !internalCommandTyping)
                    RunTestFastUpExitTick(now);
                else
                    ResetTestFastUpExitRuntimeState(now);

                if (autoFishingModeSelected && _testAutoFishingRuntimeEnabled)
                    RunTestAutoFishingTick(now);
                else
                    ResetTestAutoFishingRuntimeState(now);
            }

            RefreshLiveTopTiles(now);
        }

        private void UpdateAutoClickScheduler(
            bool holdModeSelected,
            bool autoLeftModeSelected,
            bool autoRightModeSelected,
            bool internalCommandTyping)
        {
            bool leftEnabled = false;
            int leftMinCps = 1;
            int leftMaxCps = 1;
            bool rightEnabled = false;
            int rightMinCps = 1;
            int rightMaxCps = 1;
            bool rightHoldPulseMode = false;
            int leftRequiredHoldVirtualKey = 0;
            int rightRequiredHoldVirtualKey = 0;

            if (!internalCommandTyping)
            {
                if (holdModeSelected
                    && _holdMacroRuntimeEnabled
                    && ChkHoldLeftEnabled.IsChecked == true
                    && _holdLeftToggleClickingEnabled)
                {
                    leftEnabled = TryGetCpsRange(
                        TxtManualLeftMinCps.Text,
                        TxtManualLeftMaxCps.Text,
                        out leftMinCps,
                        out leftMaxCps);
                }
                else if (autoLeftModeSelected && _autoLeftRuntimeEnabled)
                {
                    leftEnabled = TryGetCpsRange(
                        TxtAutoLeftMinCps.Text,
                        TxtAutoLeftMaxCps.Text,
                        out leftMinCps,
                        out leftMaxCps);
                    if (leftEnabled
                        && ChkAutoLeftHoldBindMode.IsChecked == true
                        && TryGetVirtualKey(TxtAutoLeftKey.Text, out int leftHoldKey))
                    {
                        leftRequiredHoldVirtualKey = leftHoldKey;
                    }
                }

                if (holdModeSelected
                    && _holdMacroRuntimeEnabled
                    && ChkHoldRightEnabled.IsChecked == true
                    && _holdRightRuntimePressActive)
                {
                    rightEnabled = TryGetCpsRange(
                        TxtManualRightMinCps.Text,
                        TxtManualRightMaxCps.Text,
                        out rightMinCps,
                        out rightMaxCps);
                    rightHoldPulseMode = true;
                }
                else if (autoRightModeSelected && _autoRightRuntimeEnabled)
                {
                    rightEnabled = TryGetCpsRange(
                        TxtAutoRightMinCps.Text,
                        TxtAutoRightMaxCps.Text,
                        out rightMinCps,
                        out rightMaxCps);
                    if (rightEnabled
                        && ChkAutoRightHoldBindMode.IsChecked == true
                        && TryGetVirtualKey(TxtAutoRightKey.Text, out int rightHoldKey))
                    {
                        rightRequiredHoldVirtualKey = rightHoldKey;
                    }
                }
            }

            if (leftEnabled || rightEnabled)
                StartMouseHook();

            _autoClickScheduler.Update(
                leftEnabled,
                leftMinCps,
                leftMaxCps,
                rightEnabled,
                rightMinCps,
                rightMaxCps,
                rightHoldPulseMode,
                leftRequiredHoldVirtualKey,
                rightRequiredHoldVirtualKey,
                _targetGameWindowHandle);
        }

        private void StopExclusivePointerMacrosForClicker()
        {
            StopOtherExclusivePointerMacros();
        }

        private void StopClickerRuntimesForExclusivePointerMacro()
        {
            _autoClickScheduler.Stop();
            _holdMacroRuntimeEnabled = false;
            ResetHoldLeftToggleState(clearToggleEnabled: true);
            _autoLeftRuntimeEnabled = false;
            _autoRightRuntimeEnabled = false;
            SetAutoLeftDabHold(false);
        }

        private void StopOtherExclusivePointerMacros(
            bool keepKopacz533 = false,
            bool keepKopacz633 = false,
            bool keepFastUp = false)
        {
            if (!keepKopacz533 && _kopacz533RuntimeEnabled)
            {
                _kopacz533RuntimeEnabled = false;
                SetKopacz533MiningHold(false);
                ResetKopacz533RuntimeState();
                EndMiningLogRun(InventoryCleanupOwner.Kopacz533, "Kopanie zatrzymane przez uruchomienie innego makra.");
            }

            if (!keepKopacz633 && _kopacz633RuntimeEnabled)
            {
                _kopacz633RuntimeEnabled = false;
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                ResetKopacz633RuntimeState();
                EndMiningLogRun(InventoryCleanupOwner.Kopacz633, "Kopanie zatrzymane przez uruchomienie innego makra.");
            }

            if (!keepFastUp && _testFastUpExitRuntimeEnabled)
            {
                _testFastUpExitRuntimeEnabled = false;
                ResetTestFastUpExitRuntimeState();
            }
        }

        private void RunTestFastUpExitTick(DateTime now)
        {
            if (_testFastUpExitJumpHoldActive && now >= _testFastUpExitJumpHoldUntilUtc)
                SetTestFastUpExitJumpHold(false);

            if (_testFastUpExitPlaceHoldActive)
            {
                if (now < _testFastUpExitPlaceHoldUntilUtc)
                {
                    if (now >= _testFastUpExitPlacePulseAtUtc)
                    {
                        SendMouseClick(leftButton: false, holdPulseMode: true);
                        _testFastUpExitPlacePulseAtUtc = now.AddMilliseconds(FastUpPlacePulseIntervalMs);
                    }

                    return;
                }

                SetTestFastUpExitPlaceHold(false);
                _testFastUpExitStage = FastUpExitStage.LookUp;
                _nextTestFastUpExitActionAtUtc = now.AddMilliseconds(FastUpPlaceDelayMs);
                return;
            }

            if (_testFastUpExitLookSweepTicksRemaining > 0)
            {
                for (int i = 0; i < _testFastUpExitLookSweepBurstsPerTick; i++)
                    SendMouseMoveRelative(0, _testFastUpExitLookSweepDirectionY);

                _testFastUpExitLookSweepTicksRemaining--;
                if (_testFastUpExitLookSweepTicksRemaining > 0)
                    return;

                _testFastUpExitStage = _testFastUpExitLookSweepNextStage;
                if (_testFastUpExitStage == FastUpExitStage.PlaceBlock)
                {
                    StartTestFastUpExitJumpPulse(now);
                    int placeDelayAfterJumpMs = Math.Max(40, GetConfiguredFastUpPlaceAfterJumpMs());
                    _nextTestFastUpExitActionAtUtc = now.AddMilliseconds(placeDelayAfterJumpMs);
                }
                else
                {
                    _nextTestFastUpExitActionAtUtc = now.AddMilliseconds(FastUpSlotSwitchDelayMs);
                }
                return;
            }

            if (_testFastUpExitBreakHoldActive)
            {
                if (now < _testFastUpExitBreakHoldUntilUtc)
                    return;

                SetTestFastUpExitBreakHold(false);
                if (_testFastUpExitStage == FastUpExitStage.BreakBlock)
                    _testFastUpExitStage = FastUpExitStage.SelectBlock;
                _nextTestFastUpExitActionAtUtc = now;
            }

            if (now < _nextTestFastUpExitActionAtUtc)
                return;

            int pickaxeSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitPickaxeSlot, _settings.TestFastUpExitPickaxeSlot);
            int blockSlot = GetSelectedTestFastUpSlot(CbTestFastUpExitBlockSlot, _settings.TestFastUpExitBlockSlot);
            switch (_testFastUpExitStage)
            {
                case FastUpExitStage.LookUp:
                    StartFastUpLookSweep(maxUp: true, FastUpExitStage.SelectPickaxe);
                    return;

                case FastUpExitStage.SelectPickaxe:
                    SendKeyTap(GetSlotVirtualKey(pickaxeSlot));
                    _testFastUpExitStage = FastUpExitStage.BreakBlock;
                    _nextTestFastUpExitActionAtUtc = now.AddMilliseconds(FastUpSlotSwitchDelayMs);
                    return;

                case FastUpExitStage.BreakBlock:
                    SetTestFastUpExitBreakHold(true);
                    _testFastUpExitBreakHoldUntilUtc = now.AddMilliseconds(GetConfiguredFastUpBreakDurationMs());
                    _nextTestFastUpExitActionAtUtc = _testFastUpExitBreakHoldUntilUtc;
                    return;

                case FastUpExitStage.SelectBlock:
                    SendKeyTap(GetSlotVirtualKey(blockSlot));
                    _testFastUpExitStage = FastUpExitStage.LookDown;
                    _nextTestFastUpExitActionAtUtc = now.AddMilliseconds(FastUpSlotSwitchDelayMs);
                    return;

                case FastUpExitStage.LookDown:
                    StartFastUpLookSweep(maxUp: false, FastUpExitStage.PlaceBlock);
                    return;

                case FastUpExitStage.PlaceBlock:
                    // Re-select block slot to avoid desync between hotbar switch and PPM place.
                    SendKeyTap(GetSlotVirtualKey(blockSlot));
                    SetTestFastUpExitPlaceHold(true);
                    _testFastUpExitPlacePulseAtUtc = now;
                    _testFastUpExitPlaceHoldUntilUtc = now.AddMilliseconds(FastUpPlaceHoldMs);
                    _nextTestFastUpExitActionAtUtc = _testFastUpExitPlaceHoldUntilUtc;
                    return;

                default:
                    _testFastUpExitStage = FastUpExitStage.LookUp;
                    _nextTestFastUpExitActionAtUtc = now;
                    return;
            }
        }

        private void RunTestAutoFishingTick(DateTime now)
        {
            if (_jablkaCommandStage != JablkaCommandStage.None
                || _kopacz533CommandStage != Kopacz533CommandStage.None
                || _kopacz633CommandStage != Kopacz633CommandStage.None
                || _bindyCommandStage != BindyCommandStage.None)
                return;

            if (_testAutoFishingRepairStage != TestAutoFishingRepairStage.None)
            {
                if (!TryProcessTestAutoFishingRepairCommand(now))
                    ResetTestAutoFishingRuntimeState(now);
                UpdateTestAutoFishingStatusLabel();
                return;
            }

            if (HasConfiguredTestAutoFishingRepair() && now >= _nextTestAutoFishingRepairAtUtc)
            {
                StartTestAutoFishingRepairCommand(now);
                UpdateTestAutoFishingStatusLabel();
                return;
            }

            if (_testAutoFishingRecastAfterRepairPending)
            {
                if (now < _testAutoFishingRecastAfterRepairAtUtc)
                    return;

                SendMouseClick(leftButton: false, holdPulseMode: false);
                _testAutoFishingRecastAfterRepairPending = false;
                _testAutoFishingAwaitSecondClick = true;
                _testAutoFishingWaitingForCastRegistration = false;
                _nextTestAutoFishingActionAtUtc = now.AddMilliseconds(TestAutoFishingSecondClickDelayMs);
                ResetTestAutoFishingDetection(now);
                UpdateTestAutoFishingStatusLabel();
                return;
            }

            if (_testAutoFishingAwaitSecondClick)
            {
                if (now < _nextTestAutoFishingActionAtUtc)
                    return;

                SendMouseClick(leftButton: false, holdPulseMode: false);
                _testAutoFishingAwaitSecondClick = false;
                StartTestAutoFishingCastRegistrationDelay(now);
                UpdateTestAutoFishingStatusLabel();
                return;
            }

            if (_testAutoFishingWaitingForCastRegistration)
            {
                if (now < _nextTestAutoFishingActionAtUtc)
                {
                    UpdateTestAutoFishingStatusLabel();
                    return;
                }

                _testAutoFishingWaitingForCastRegistration = false;
                _nextTestAutoFishingActionAtUtc = now;
                _nextTestAutoFishingScanAtUtc = now;
            }

            if (now < _nextTestAutoFishingActionAtUtc || now < _nextTestAutoFishingScanAtUtc)
                return;
            if (!TryGetTestAutoFishingCaptureArea(_targetGameWindowHandle, out Drawing.Rectangle captureArea))
            {
                _testAutoFishingRuntimeEnabled = false;
                ResetTestAutoFishingRuntimeState(now);
                UpdateTestAutoFishingStatusLabel();
                UpdateStatusBar("Auto łowienie: obszar spławika jest nieprawidłowy", "Orange");
                RefreshTopTiles();
                return;
            }

            _nextTestAutoFishingScanAtUtc = now.AddMilliseconds(TestAutoFishingScanIntervalMs);
            try
            {
                using Drawing.Bitmap bitmap = new Drawing.Bitmap(captureArea.Width, captureArea.Height, DrawingImaging.PixelFormat.Format32bppArgb);
                using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, captureArea.Size, Drawing.CopyPixelOperation.SourceCopy);

                if (!TryGetFishingBobberPosition(bitmap, out double bobberX, out double bobberY, out int redPixels))
                {
                    HandleMissingFishingBobber(now);
                    return;
                }

                _testAutoFishingMissedDetections = 0;
                _testAutoFishingNoBobberSinceAtUtc = DateTime.MinValue;
                _testAutoFishingLastDetectedBobberX = bobberX;
                _testAutoFishingLastDetectedBobberY = bobberY;
                _testAutoFishingLastDetectedRedPixels = redPixels;
                if (!_testAutoFishingBaselineReady)
                {
                    _testAutoFishingBaselineBobberY = bobberY;
                    _testAutoFishingBaselineReady = true;
                    _testAutoFishingBiteConfirmationFrames = 0;
                    _testAutoFishingBaselineArmedAtUtc = now.AddMilliseconds(TestAutoFishingBiteArmingDelayMs);
                    UpdateTestAutoFishingStatusLabel();
                    return;
                }

                bool armed = now >= _testAutoFishingBaselineArmedAtUtc;
                double drop = bobberY - _testAutoFishingBaselineBobberY;
                if (armed && drop >= TestAutoFishingDropThresholdPx)
                {
                    _testAutoFishingBiteConfirmationFrames++;
                    if (_testAutoFishingBiteConfirmationFrames >= TestAutoFishingRequiredBiteFrames)
                    {
                        TriggerTestAutoFishingCatch(now, "Auto łowienie: branie! PPM -> ponowne zarzucenie");
                        return;
                    }
                }
                else
                {
                    _testAutoFishingBiteConfirmationFrames = 0;
                }

                if (_testAutoFishingBiteConfirmationFrames == 0)
                {
                    double updateWeight = armed ? (drop > 0 ? 0.01 : 0.04) : 0.55;
                    _testAutoFishingBaselineBobberY = _testAutoFishingBaselineBobberY * (1.0 - updateWeight) + bobberY * updateWeight;
                }
                UpdateTestAutoFishingStatusLabel();
            }
            catch
            {
                HandleMissingFishingBobber(now);
            }
        }

        private void HandleMissingFishingBobber(DateTime now)
        {
            _testAutoFishingLastDetectedBobberX = double.NaN;
            _testAutoFishingLastDetectedBobberY = double.NaN;
            _testAutoFishingLastDetectedRedPixels = 0;
            _testAutoFishingBiteConfirmationFrames = 0;
            _testAutoFishingMissedDetections++;
            if (_testAutoFishingNoBobberSinceAtUtc == DateTime.MinValue)
                _testAutoFishingNoBobberSinceAtUtc = now;

            if (_testAutoFishingBaselineReady
                && now >= _testAutoFishingBaselineArmedAtUtc
                && _testAutoFishingMissedDetections >= TestAutoFishingLossTriggerMisses)
            {
                TriggerTestAutoFishingCatch(now, "Auto łowienie: branie (zanik spławika)");
                return;
            }

            if ((now - _testAutoFishingNoBobberSinceAtUtc).TotalMilliseconds >= TestAutoFishingNoBobberRecastMs)
            {
                SendMouseClick(leftButton: false, holdPulseMode: false);
                _testAutoFishingAwaitSecondClick = false;
                StartTestAutoFishingCastRegistrationDelay(now);
                UpdateTestAutoFishingStatusLabel();
                UpdateStatusBar("Auto łowienie: brak spławika przez 5s -> ponowny rzut (PPM)", "Orange");
                return;
            }

            if (_testAutoFishingMissedDetections >= TestAutoFishingMissTolerance)
                _testAutoFishingBaselineReady = false;
            UpdateTestAutoFishingStatusLabel();
        }

        private void TriggerTestAutoFishingCatch(DateTime now, string status)
        {
            RegisterTestAutoFishingCatch(now);
            SendMouseClick(leftButton: false, holdPulseMode: false);
            _testAutoFishingAwaitSecondClick = true;
            _testAutoFishingWaitingForCastRegistration = false;
            _nextTestAutoFishingActionAtUtc = now.AddMilliseconds(TestAutoFishingSecondClickDelayMs);
            ResetTestAutoFishingDetection(now);
            UpdateTestAutoFishingStatusLabel();
            UpdateStatusBar(status, "Orange");
        }

        private void StartTestAutoFishingCastRegistrationDelay(DateTime now)
        {
            ResetTestAutoFishingDetection(now);
            _testAutoFishingNoBobberSinceAtUtc = now;
            _testAutoFishingWaitingForCastRegistration = true;
            _nextTestAutoFishingActionAtUtc = now.AddMilliseconds(TestAutoFishingCastRegistrationDelayMs);
            _nextTestAutoFishingScanAtUtc = _nextTestAutoFishingActionAtUtc;
        }

        private void ResetTestAutoFishingDetection(DateTime now)
        {
            _testAutoFishingBaselineReady = false;
            _testAutoFishingBaselineBobberY = 0;
            _testAutoFishingBaselineArmedAtUtc = now;
            _testAutoFishingNoBobberSinceAtUtc = DateTime.MinValue;
            _testAutoFishingLastDetectedBobberX = double.NaN;
            _testAutoFishingLastDetectedBobberY = double.NaN;
            _testAutoFishingLastDetectedRedPixels = 0;
            _testAutoFishingMissedDetections = 0;
            _testAutoFishingBiteConfirmationFrames = 0;
        }

        private void StartTestAutoFishingRepairCommand(DateTime now)
        {
            if (!HasConfiguredTestAutoFishingRepair())
            {
                _nextTestAutoFishingRepairAtUtc = DateTime.MaxValue;
                return;
            }

            _testAutoFishingRepairStage = TestAutoFishingRepairStage.OpenChat;
            _nextTestAutoFishingRepairStageAtUtc = now;
            _testAutoFishingAwaitSecondClick = false;
            _testAutoFishingRecastAfterRepairPending = false;
            _testAutoFishingWaitingForCastRegistration = false;
            ResetTestAutoFishingDetection(now);
            _nextTestAutoFishingActionAtUtc = now;
            _nextTestAutoFishingScanAtUtc = now;
            UpdateStatusBar("Auto łowienie: wykonywanie komendy naprawy", "Orange");
        }

        private bool TryProcessTestAutoFishingRepairCommand(DateTime now)
        {
            if (_testAutoFishingRepairStage == TestAutoFishingRepairStage.None)
                return false;
            if (now < _nextTestAutoFishingRepairStageAtUtc)
                return true;

            switch (_testAutoFishingRepairStage)
            {
                case TestAutoFishingRepairStage.OpenChat:
                    SendChatOpenKeyTap();
                    _testAutoFishingRepairStage = TestAutoFishingRepairStage.TypeCommand;
                    _nextTestAutoFishingRepairStageAtUtc = now.AddMilliseconds(TestAutoFishingRepairDelayAfterOpenChatMs);
                    return true;
                case TestAutoFishingRepairStage.TypeCommand:
                    string command = GetConfiguredTestAutoFishingRepairCommand();
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        _testAutoFishingRepairStage = TestAutoFishingRepairStage.None;
                        _nextTestAutoFishingRepairAtUtc = DateTime.MaxValue;
                        return true;
                    }
                    if (!SendTextByKeyboard(command))
                    {
                        UpdateStatusBar("Auto łowienie: błąd wpisywania komendy naprawy", "Red");
                        _testAutoFishingRepairStage = TestAutoFishingRepairStage.None;
                        _nextTestAutoFishingRepairAtUtc = now.AddSeconds(3);
                        return true;
                    }
                    _testAutoFishingRepairStage = TestAutoFishingRepairStage.SubmitCommand;
                    _nextTestAutoFishingRepairStageAtUtc = now.AddMilliseconds(TestAutoFishingRepairDelayAfterTypeCommandMs);
                    return true;
                case TestAutoFishingRepairStage.SubmitCommand:
                    SendKeyTap(VK_RETURN);
                    _testAutoFishingRepairStage = TestAutoFishingRepairStage.None;
                    int interval = GetConfiguredTestAutoFishingRepairIntervalSeconds();
                    _nextTestAutoFishingRepairAtUtc = interval > 0 ? now.AddSeconds(interval) : DateTime.MaxValue;
                    _testAutoFishingRecastAfterRepairPending = true;
                    _testAutoFishingRecastAfterRepairAtUtc = now.AddMilliseconds(TestAutoFishingRepairRecastDelayMs);
                    _nextTestAutoFishingActionAtUtc = _testAutoFishingRecastAfterRepairAtUtc;
                    _nextTestAutoFishingScanAtUtc = _testAutoFishingRecastAfterRepairAtUtc;
                    ResetTestAutoFishingDetection(now);
                    UpdateStatusBar("Auto łowienie: komenda naprawy wykonana", "Green");
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFishingBobberPosition(Drawing.Bitmap screenshot, out double bobberX, out double bobberY, out int matchedPixelCount)
        {
            bobberX = 0;
            bobberY = 0;
            matchedPixelCount = 0;
            if (screenshot.Width < 2 || screenshot.Height < 2)
                return false;

            Drawing.Rectangle rect = new Drawing.Rectangle(0, 0, screenshot.Width, screenshot.Height);
            DrawingImaging.BitmapData bitmapData = screenshot.LockBits(rect, DrawingImaging.ImageLockMode.ReadOnly, DrawingImaging.PixelFormat.Format32bppArgb);
            try
            {
                int stride = bitmapData.Stride;
                int absoluteStride = Math.Abs(stride);
                byte[] pixels = new byte[absoluteStride * bitmapData.Height];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                int width = bitmapData.Width;
                int height = bitmapData.Height;
                bool[] redMask = new bool[width * height];
                for (int y = 0; y < bitmapData.Height; y++)
                {
                    int row = stride >= 0 ? y * stride : (bitmapData.Height - 1 - y) * absoluteStride;
                    for (int x = 0; x < bitmapData.Width; x++)
                    {
                        int index = row + x * 4;
                        byte blue = pixels[index];
                        byte green = pixels[index + 1];
                        byte red = pixels[index + 2];
                        if (red >= 130 && green <= 145 && blue <= 145 && red - green >= 15 && red - blue >= 10)
                            redMask[y * width + x] = true;
                    }
                }

                // The red tip of a bobber is one compact mark. Ignore unrelated scattered red pixels.
                bool[] visited = new bool[redMask.Length];
                var pending = new Queue<int>();
                double bestXTotal = 0;
                double bestYTotal = 0;
                for (int start = 0; start < redMask.Length; start++)
                {
                    if (!redMask[start] || visited[start])
                        continue;

                    int clusterPixels = 0;
                    double clusterXTotal = 0;
                    double clusterYTotal = 0;
                    visited[start] = true;
                    pending.Enqueue(start);

                    while (pending.Count > 0)
                    {
                        int point = pending.Dequeue();
                        int pointX = point % width;
                        int pointY = point / width;
                        clusterPixels++;
                        clusterXTotal += pointX;
                        clusterYTotal += pointY;

                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            int neighbourY = pointY + offsetY;
                            if (neighbourY < 0 || neighbourY >= height)
                                continue;

                            for (int offsetX = -1; offsetX <= 1; offsetX++)
                            {
                                if (offsetX == 0 && offsetY == 0)
                                    continue;

                                int neighbourX = pointX + offsetX;
                                if (neighbourX < 0 || neighbourX >= width)
                                    continue;

                                int neighbour = neighbourY * width + neighbourX;
                                if (!redMask[neighbour] || visited[neighbour])
                                    continue;

                                visited[neighbour] = true;
                                pending.Enqueue(neighbour);
                            }
                        }
                    }

                    if (clusterPixels > matchedPixelCount)
                    {
                        matchedPixelCount = clusterPixels;
                        bestXTotal = clusterXTotal;
                        bestYTotal = clusterYTotal;
                    }
                }

                if (matchedPixelCount < TestAutoFishingMinRedPixels)
                    return false;
                bobberX = bestXTotal / matchedPixelCount;
                bobberY = bestYTotal / matchedPixelCount;
                return true;
            }
            finally
            {
                screenshot.UnlockBits(bitmapData);
            }
        }

        private static int GetSlotVirtualKey(int slot)
        {
            int normalizedSlot = Math.Clamp(slot, 1, 9);
            return 0x30 + normalizedSlot;
        }

        private void StartFastUpLookSweep(bool maxUp, FastUpExitStage nextStage)
        {
            _testFastUpExitLookSweepDirectionY = maxUp ? -FastUpVerticalStepDelta : FastUpVerticalStepDelta;
            int lookDurationMs = GetConfiguredFastUpLookDurationMs();
            if (!maxUp)
                lookDurationMs = Math.Max(FastUpLookDurationMinMs, (int)Math.Round(lookDurationMs * FastUpLookDownDurationScale));

            double tickMs = _macroTimer.Interval.TotalMilliseconds;
            if (tickMs < 1.0)
                tickMs = 5.0;

            int sweepTicks = Math.Max(1, (int)Math.Ceiling(lookDurationMs / tickMs));
            int targetBursts = FastUpLookSweepTicks * FastUpLookSweepBurstsPerTick;
            _testFastUpExitLookSweepBurstsPerTick = Math.Max(1, (int)Math.Ceiling(targetBursts / (double)sweepTicks));
            _testFastUpExitLookSweepTicksRemaining = sweepTicks;
            _testFastUpExitLookSweepNextStage = nextStage;
        }

        private void StartKopacz533Runtime(DateTime now)
        {
            ResetKopacz533RuntimeState(now);
            _kopacz533RuntimeStartedAtUtc = now;
            ScheduleNextInventoryCleanup(now);

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
            StartMiningLogRun(InventoryCleanupOwner.Kopacz533);
        }

        private bool HasMiningWork(InventoryCleanupOwner owner)
        {
            bool runtimeActive = owner == InventoryCleanupOwner.Kopacz533
                ? _kopacz533RuntimeEnabled || _kopacz533ResumeMiningPending
                : _kopacz633RuntimeEnabled || _kopacz633ResumeMiningPending;
            return runtimeActive
                || (_inventoryCleanupStage != InventoryCleanupStage.None && _inventoryCleanupOwner == owner)
                || IsReconnectForMiner(owner);
        }

        private bool IsReconnectForMiner(InventoryCleanupOwner owner)
        {
            return _autoReconnectStage != AutoReconnectStage.None
                && ((owner == InventoryCleanupOwner.Kopacz533 && _autoReconnectResumeKopacz533)
                    || (owner == InventoryCleanupOwner.Kopacz633 && _autoReconnectResumeKopacz633)
                    || _autoReconnectLogOwner == GetInventoryCleanupOwnerLabel(owner));
        }

        private bool TryStopMiningFromBinds()
        {
            bool stop533 = HasMiningWork(InventoryCleanupOwner.Kopacz533)
                && IsMiningStopBindPressed(TxtKopacz533Key.Text, ref _kopacz533BindWasDown);
            bool stop633 = HasMiningWork(InventoryCleanupOwner.Kopacz633)
                && IsMiningStopBindPressed(TxtKopacz633Key.Text, ref _kopacz633BindWasDown);
            if (stop533)
                StopMiningFromBind(InventoryCleanupOwner.Kopacz533);
            if (stop633)
                StopMiningFromBind(InventoryCleanupOwner.Kopacz633);
            return stop533 || stop633;
        }

        private bool IsMiningStopBindPressed(string keyText, ref bool wasDown)
        {
            if (!TryGetVirtualKey(keyText, out int key))
                return false;

            // Auto EQ holds Ctrl/drop and movement keys across timer ticks. Their
            // injected DOWN must not be mistaken for another press of a bind.
            bool injectedDown = (_inventoryCleanupDropKeyDown && key == _inventoryCleanupDropVirtualKey)
                || (_inventoryCleanupControlDown && key is VK_CONTROL or VK_LCONTROL)
                || (_inventoryCleanupReturnLeftDown && key == VK_A)
                || (_inventoryCleanupReturnBackwardDown && key == VK_S)
                || (_kopacz533Holding && key is VK_SHIFT or 0xA0) // Shift / LeftShift
                || (_kopacz633StrafeDirection == Kopacz633StrafeDirection.Left && key == VK_A)
                || (_kopacz633StrafeDirection == Kopacz633StrafeDirection.Right && key == VK_D)
                || (_kopacz633StrafeDirection == Kopacz633StrafeDirection.Forward && key == VK_W)
                || (_kopacz633StrafeDirection == Kopacz633StrafeDirection.Backward && key == VK_S)
                || ((_kopacz533Holding || _kopacz633HoldingAttack) && key == VK_LBUTTON)
                || (_inventoryCleanupEatingRightButtonDown && key == VK_RBUTTON);
            return ConsumeMiningStopPress(IsVirtualKeyDown(key), injectedDown, ref wasDown);
        }

        private static bool ConsumeMiningStopPress(bool isDown, bool injectedDown, ref bool wasDown)
        {
            bool pressed = isDown && !wasDown && !injectedDown;
            wasDown = isDown;
            return pressed;
        }

        private void StopMiningFromBind(InventoryCleanupOwner owner)
        {
            string label = GetInventoryCleanupOwnerLabel(owner);
            // Release held inputs before any disk logging or UI refresh.
            ReleaseMiningInputs(owner);
            if (IsReconnectForMiner(owner))
                StopAutoReconnect($"{label}: przerwano bieżącą akcję bindem.", resumeMining: false, warning: true);

            if (_inventoryCleanupOwner == owner && _inventoryCleanupStage != InventoryCleanupStage.None)
            {
                RecordAbortedInventoryCleanup("Auto EQ przerwane bindem kopacza.");
                _inventoryCleanupLastResult = "Przerwano bindem kopacza";
                _inventoryCleanupLastResultWarning = true;
                UpdateInventoryCleanupStatus("Auto EQ zatrzymane bindem. Pozostałe akcje anulowano.", "Orange");
            }

            StopMiningRuntime(owner);
            EndMiningLogRun(owner, "Kopanie i bieżąca akcja zatrzymane bindem użytkownika.");
            UpdateStatusBar($"{label} wyłączony — bieżąca akcja anulowana.", "Orange");
            RefreshTopTiles();
        }

        private void StopMiningRuntime(InventoryCleanupOwner owner)
        {
            ReleaseMiningInputs(owner);
            if (owner == InventoryCleanupOwner.Kopacz533)
            {
                _kopacz533RuntimeEnabled = false;
                ResetKopacz533RuntimeState();
            }
            else
            {
                _kopacz633RuntimeEnabled = false;
                ResetKopacz633RuntimeState();
            }
        }

        private void ReleaseMiningInputs(InventoryCleanupOwner owner)
        {
            if (owner == InventoryCleanupOwner.Kopacz533)
                SetKopacz533MiningHold(false);
            else
            {
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            }
            if (_inventoryCleanupOwner == owner)
            {
                ReleaseInventoryCleanupDropKeys();
                ReleaseInventoryCleanupReturnKeys();
                SetInventoryCleanupEatingHold(false);
            }
        }

        private void ToggleKopacz533Runtime()
        {
            if (ChkKopacz533Enabled?.IsChecked != true)
            {
                UpdateStatusBar("Najpierw zaznacz kanał Kopacz 5/3/3.", "Red");
                return;
            }

            _kopacz533RuntimeEnabled = !_kopacz533RuntimeEnabled;
            if (_kopacz533RuntimeEnabled)
            {
                StopClickerRuntimesForExclusivePointerMacro();
                StopOtherExclusivePointerMacros(keepKopacz533: true);
                StartKopacz533Runtime(DateTime.UtcNow);
            }
            else
            {
                StopMiningFromBind(InventoryCleanupOwner.Kopacz533);
                return;
            }

            UpdateStatusBar(_kopacz533RuntimeEnabled ? "Kopacz 5/3/3 aktywowany" : "Kopacz 5/3/3 wyłączony", "Orange");
            RefreshTopTiles();
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
            ScheduleNextInventoryCleanup(now);

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
            StartMiningLogRun(InventoryCleanupOwner.Kopacz633);
        }

        private void ToggleKopacz633Runtime()
        {
            if (ChkKopacz633Enabled?.IsChecked != true)
            {
                UpdateStatusBar("Najpierw zaznacz kanał Kopacz 6/3/3.", "Red");
                return;
            }

            if (!_kopacz633RuntimeEnabled && !IsKopacz633DirectionSelected())
            {
                UpdateStatusBar("Kopacz 6/3/3: wybierz kierunek 'Na wprost' lub 'Do góry'", "Orange");
                return;
            }

            _kopacz633RuntimeEnabled = !_kopacz633RuntimeEnabled;
            if (_kopacz633RuntimeEnabled)
            {
                StopClickerRuntimesForExclusivePointerMacro();
                StopOtherExclusivePointerMacros(keepKopacz633: true);
                StartKopacz633Runtime(DateTime.UtcNow);
            }
            else
            {
                StopMiningFromBind(InventoryCleanupOwner.Kopacz633);
                return;
            }

            UpdateStatusBar(_kopacz633RuntimeEnabled ? "Kopacz 6/3/3 aktywowany" : "Kopacz 6/3/3 wyłączony", "Orange");
            RefreshTopTiles();
        }

        private void ScheduleNextInventoryCleanup(DateTime now)
        {
            _nextInventoryCleanupAtUtc = ChkInventoryCleanupEnabled.IsChecked == true
                ? now.AddSeconds(GetConfiguredInventoryCleanupIntervalSeconds())
                : DateTime.MaxValue;
        }

        private bool TryRunInventoryCleanup(InventoryCleanupOwner owner, DateTime now)
        {
            if (_inventoryCleanupStage != InventoryCleanupStage.None)
            {
                if (_inventoryCleanupOwner != owner)
                    return false;

                ProcessInventoryCleanupStage(now);
                return true;
            }

            if (ChkInventoryCleanupEnabled.IsChecked != true || now < _nextInventoryCleanupAtUtc)
                return false;
            if (_kopacz533CommandStage != Kopacz533CommandStage.None
                || _kopacz633CommandStage != Kopacz633CommandStage.None
                || _jablkaCommandStage != JablkaCommandStage.None
                || _bindyCommandStage != BindyCommandStage.None
                || _testAutoFishingRepairStage != TestAutoFishingRepairStage.None)
                return false;

            bool cobbleXEnabled = ChkCobbleXEnabled.IsChecked == true;
            bool discardEverythingExceptCobblestone = ChkInventoryCleanupAllItemTypes.IsChecked == true;
            HashSet<int> enabledSlots = GetSelectedInventoryCleanupSlots();
            if (!cobbleXEnabled && enabledSlots.Count == 0)
            {
                ScheduleNextInventoryCleanup(now);
                _inventoryCleanupLastResult = "Pominięto: nie wybrano slotów";
                _inventoryCleanupLastResultWarning = true;
                UpdateInventoryCleanupStatus("Pominięto: nie wybrano żadnego slotu.", "Orange");
                return false;
            }
            if (!cobbleXEnabled
                && !discardEverythingExceptCobblestone
                && GetSelectedInventoryCleanupItemTypes().Count == 0)
            {
                ScheduleNextInventoryCleanup(now);
                _inventoryCleanupLastResult = "Pominięto: nie wybrano typów przedmiotów";
                _inventoryCleanupLastResultWarning = true;
                UpdateInventoryCleanupStatus("Pominięto: nie wybrano żadnego typu przedmiotu do wyrzucenia.", "Orange");
                return false;
            }

            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            ResetJablkaRuntimeState(now);
            ResetBindyRuntimeState(now);
            ResetTestFastUpExitRuntimeState(now);
            ResetTestAutoFishingRuntimeState(now);

            _inventoryCleanupOwner = owner;
            PauseKopaczCommandCountdown(owner, now);
            _inventoryCleanupStage = owner == InventoryCleanupOwner.Kopacz633
                ? InventoryCleanupStage.ReturnToMiningStart
                : InventoryCleanupStage.OpenInventory;
            _inventoryCleanupDetectionAttempts = 0;
            _inventoryCleanupDropPass = 0;
            _inventoryCleanupInitialMarkedStacks = 0;
            _inventoryCleanupRemovedStacks = 0;
            _inventoryCleanupRemovedItems = 0;
            _inventoryCleanupRemainingMarkedStacks = 0;
            _inventoryCleanupCursorParkedForDetection = false;
            _inventoryCleanupTargetIndex = 0;
            _inventoryCleanupTargets.Clear();
            _inventoryCleanupInitialItemTypeCounts.Clear();
            _inventoryCleanupInitialItemTypeStackCounts.Clear();
            _inventoryCleanupItemTypeCounts.Clear();
            _inventoryCleanupItemTypeStackCounts.Clear();
            _inventoryCleanupClientArea = Drawing.Rectangle.Empty;
            _inventoryCleanupFullCobblestoneStacks = 0;
            _inventoryCleanupCobbleXCommandPending = false;
            _inventoryCleanupCobbleXCommandSent = false;
            _inventoryCleanupCobbleXCommandFailed = false;
            _inventoryCleanupPendingCobbleXCommand = string.Empty;
            _inventoryCleanupEatAfterCleanupPending = ChkInventoryCleanupEatAfterCleanup.IsChecked == true;
            _inventoryCleanupEatingCompleted = false;
            _inventoryCleanupOpenedAtUtc = now;
            _inventoryCleanupLogStartError = string.Empty;
            if (owner == InventoryCleanupOwner.Kopacz633)
            {
                StartInventoryCleanupReturnToMiningStart(now);
            }
            else
            {
                _nextInventoryCleanupStageAtUtc = now;
                UpdateInventoryCleanupStatus("Otwieranie ekwipunku...", "Orange");
            }
            return true;
        }

        private void StartInventoryCleanupLogSession()
        {
            if (!string.IsNullOrWhiteSpace(_inventoryCleanupLogSessionId))
                return;

            bool discardEverythingExceptCobblestone = ChkInventoryCleanupAllItemTypes.IsChecked == true;
            IReadOnlyList<string> selectedItemTypes = discardEverythingExceptCobblestone
                ? Array.Empty<string>()
                : GetSelectedInventoryCleanupItemTypes()
                    .Select(GetInventoryCleanupItemLabel)
                    .OrderBy(label => label, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            if (!_miningLogService.StartInventorySession(
                    GetInventoryCleanupOwnerLabel(_inventoryCleanupOwner),
                    GetMiningRunId(_inventoryCleanupOwner),
                    discardEverythingExceptCobblestone ? "all-except-cobblestone" : "selected-items",
                    ChkCobbleXEnabled.IsChecked == true,
                    _inventoryCleanupEatAfterCleanupPending,
                    GetSelectedInventoryCleanupSlots().OrderBy(slot => slot).ToList(),
                    selectedItemTypes,
                    out _inventoryCleanupLogSessionId,
                    out string logStartError))
            {
                _inventoryCleanupLogStartError = logStartError;
            }

            RefreshMiningLogsSummary();
        }

        private void StartInventoryCleanupReturnToMiningStart(DateTime now)
        {
            ReleaseInventoryCleanupReturnKeys();

            bool upwardMode = CbKopacz633Direction.SelectedIndex == 2;
            int leftBlocks = upwardMode
                ? GetConfiguredKopacz633UpwardWidth()
                : GetConfiguredKopacz633ForwardWidth();
            int backwardBlocks = upwardMode
                ? GetConfiguredKopacz633UpwardLength()
                : 0;

            _inventoryCleanupReturnLeftUntilUtc = now.AddMilliseconds(leftBlocks * Kopacz633MsPerBlock);
            _inventoryCleanupReturnBackwardUntilUtc = backwardBlocks > 0
                ? now.AddMilliseconds(backwardBlocks * Kopacz633MsPerBlock)
                : DateTime.MinValue;

            SetInventoryCleanupReturnLeft(down: true);
            if (backwardBlocks > 0)
                SetInventoryCleanupReturnBackward(down: true);

            _nextInventoryCleanupStageAtUtc = backwardBlocks > 0
                ? (_inventoryCleanupReturnLeftUntilUtc <= _inventoryCleanupReturnBackwardUntilUtc
                    ? _inventoryCleanupReturnLeftUntilUtc
                    : _inventoryCleanupReturnBackwardUntilUtc)
                : _inventoryCleanupReturnLeftUntilUtc;

            UpdateInventoryCleanupStatus(
                upwardMode
                    ? $"Auto EQ gotowe do startu. Wracam na początek: A ({leftBlocks} bl.) + S ({backwardBlocks} bl.)..."
                    : $"Auto EQ gotowe do startu. Wracam na początek: A ({leftBlocks} bl.)...",
                "Orange");
        }

        private void ProcessInventoryCleanupReturnToMiningStart(DateTime now)
        {
            if (_inventoryCleanupReturnLeftDown && now >= _inventoryCleanupReturnLeftUntilUtc)
                SetInventoryCleanupReturnLeft(down: false);
            if (_inventoryCleanupReturnBackwardDown && now >= _inventoryCleanupReturnBackwardUntilUtc)
                SetInventoryCleanupReturnBackward(down: false);

            if (_inventoryCleanupReturnLeftDown || _inventoryCleanupReturnBackwardDown)
            {
                DateTime nextReleaseAtUtc = DateTime.MaxValue;
                if (_inventoryCleanupReturnLeftDown)
                    nextReleaseAtUtc = _inventoryCleanupReturnLeftUntilUtc;
                if (_inventoryCleanupReturnBackwardDown && _inventoryCleanupReturnBackwardUntilUtc < nextReleaseAtUtc)
                    nextReleaseAtUtc = _inventoryCleanupReturnBackwardUntilUtc;

                _nextInventoryCleanupStageAtUtc = nextReleaseAtUtc;
                return;
            }

            // The upward route always restarts from its first (W) leg after A+S
            // has pushed the player back into the starting corner.
            _kopacz633UpwardLegIndex = 0;
            _kopacz633MovementLegEndAtUtc = now;
            _inventoryCleanupStage = InventoryCleanupStage.OpenInventory;
            _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupReturnSettleMs);
            UpdateInventoryCleanupStatus("Pozycja startowa przywrócona. Za chwilę otwieram EQ...", "Orange");
        }

        private void ProcessInventoryCleanupStage(DateTime now)
        {
            if (now < _nextInventoryCleanupStageAtUtc)
                return;

            switch (_inventoryCleanupStage)
            {
                case InventoryCleanupStage.ReturnToMiningStart:
                    ProcessInventoryCleanupReturnToMiningStart(now);
                    return;

                case InventoryCleanupStage.OpenInventory:
                    ReleaseInventoryCleanupReturnKeys();
                    StartInventoryCleanupLogSession();
                    SendKeyTap(VK_E);
                    _inventoryCleanupStage = InventoryCleanupStage.WaitForInventory;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupOpenDelayMs);
                    UpdateInventoryCleanupStatus(
                        _inventoryCleanupOwner == InventoryCleanupOwner.Kopacz633
                            ? "Pozycja startowa przywrócona. Otwieranie ekwipunku..."
                            : "Otwieranie ekwipunku...",
                        "Orange");
                    return;

                case InventoryCleanupStage.WaitForInventory:
                    DetectInventoryCleanupTargets(now);
                    return;

                case InventoryCleanupStage.MoveToSlot:
                    if (_inventoryCleanupTargetIndex >= _inventoryCleanupTargets.Count)
                    {
                        _inventoryCleanupStage = InventoryCleanupStage.WaitForInventory;
                        _inventoryCleanupCursorParkedForDetection = false;
                        _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupBetweenDropsMs);
                        return;
                    }

                    Drawing.Point target = _inventoryCleanupTargets[_inventoryCleanupTargetIndex];
                    NativeInput.SetCursorPosition(target.X, target.Y);
                    _inventoryCleanupStage = InventoryCleanupStage.PressDropModifier;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupCursorSettleMs);
                    return;

                case InventoryCleanupStage.PressDropModifier:
                    if (!SetInventoryCleanupControl(down: true))
                    {
                        AbortInventoryCleanup(now, "Nie udało się nacisnąć lewego Ctrl.");
                        return;
                    }
                    _inventoryCleanupStage = InventoryCleanupStage.PressDropKey;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupModifierSettleMs);
                    return;

                case InventoryCleanupStage.PressDropKey:
                    if (!SetInventoryCleanupDropKey(down: true))
                    {
                        string dropKey = NormalizeMinecraftControlKey(TxtDropItemKey.Text, "Q");
                        AbortInventoryCleanup(now, $"Nie udało się nacisnąć klawisza wyrzucania ({dropKey}).");
                        return;
                    }
                    _inventoryCleanupStage = InventoryCleanupStage.ReleaseDropKeys;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupDropKeyHoldMs);
                    return;

                case InventoryCleanupStage.ReleaseDropKeys:
                    ReleaseInventoryCleanupDropKeys();
                    _inventoryCleanupTargetIndex++;
                    bool hasMoreTargets = _inventoryCleanupTargetIndex < _inventoryCleanupTargets.Count;
                    _inventoryCleanupStage = hasMoreTargets
                        ? InventoryCleanupStage.MoveToSlot
                        : InventoryCleanupStage.WaitForInventory;
                    if (!hasMoreTargets)
                    {
                        _inventoryCleanupCursorParkedForDetection = false;
                        UpdateInventoryCleanupStatus(
                            $"Przebieg {_inventoryCleanupDropPass}/{InventoryCleanupMaximumDropPasses} zakończony. Odsuwam kursor i skanuję EQ ponownie...",
                            "Orange");
                    }
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupBetweenDropsMs);
                    return;

                case InventoryCleanupStage.CloseInventory:
                    ReleaseInventoryCleanupDropKeys();
                    if (!_inventoryCleanupClientArea.IsEmpty)
                    {
                        NativeInput.SetCursorPosition(
                            _inventoryCleanupClientArea.Left + _inventoryCleanupClientArea.Width / 2,
                            _inventoryCleanupClientArea.Top + _inventoryCleanupClientArea.Height / 2);
                    }
                    SendKeyTap(VK_E);
                    _inventoryCleanupStage = _inventoryCleanupCobbleXCommandPending
                        ? InventoryCleanupStage.OpenCobbleXChat
                        : GetInventoryCleanupPostCommandStage();
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(
                        _inventoryCleanupCobbleXCommandPending
                            ? CobbleXDelayAfterCloseInventoryMs
                            : InventoryCleanupResumeDelayMs);
                    return;

                case InventoryCleanupStage.OpenCobbleXChat:
                    SendChatOpenKeyTap();
                    _inventoryCleanupStage = InventoryCleanupStage.TypeCobbleXCommand;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(CobbleXDelayAfterOpenChatMs);
                    return;

                case InventoryCleanupStage.TypeCobbleXCommand:
                    if (!SendTextByKeyboard(_inventoryCleanupPendingCobbleXCommand))
                    {
                        _inventoryCleanupCobbleXCommandFailed = true;
                        _inventoryCleanupCobbleXCommandPending = false;
                        _inventoryCleanupStage = GetInventoryCleanupPostCommandStage();
                        _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupResumeDelayMs);
                        return;
                    }

                    _inventoryCleanupStage = InventoryCleanupStage.SubmitCobbleXCommand;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(CobbleXDelayAfterTypeCommandMs);
                    return;

                case InventoryCleanupStage.SubmitCobbleXCommand:
                    SendKeyTap(VK_RETURN);
                    _inventoryCleanupCobbleXCommandSent = true;
                    _inventoryCleanupCobbleXCommandPending = false;
                    _inventoryCleanupStage = GetInventoryCleanupPostCommandStage();
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(CobbleXDelayAfterSubmitResumeMs);
                    return;

                case InventoryCleanupStage.SelectFoodSlot:
                    SendKeyTap(VK_2);
                    _inventoryCleanupStage = InventoryCleanupStage.StartEating;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupFoodSlotSettleMs);
                    UpdateInventoryCleanupStatus("Auto EQ zakończone. Wybrano slot 2 — rozpoczynam jedzenie...", "Orange");
                    return;

                case InventoryCleanupStage.StartEating:
                    SetInventoryCleanupEatingHold(true);
                    _inventoryCleanupStage = InventoryCleanupStage.StopEatingAndRestoreTool;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupEatingHoldMs);
                    UpdateInventoryCleanupStatus("Jedzenie ze slotu 2: trzymam PPM przez 4 sekundy...", "Orange");
                    return;

                case InventoryCleanupStage.StopEatingAndRestoreTool:
                    SetInventoryCleanupEatingHold(false);
                    SendKeyTap(VK_1);
                    _inventoryCleanupEatingCompleted = true;
                    _inventoryCleanupStage = InventoryCleanupStage.ResumeMining;
                    _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupToolSlotSettleMs);
                    UpdateInventoryCleanupStatus("Jedzenie zakończone. Wrócono na slot 1 — wznawiam kopanie...", "Green");
                    return;

                case InventoryCleanupStage.ResumeMining:
                    int removedStacks = _inventoryCleanupRemovedStacks;
                    int removedItems = _inventoryCleanupRemovedItems;
                    int cobbleStacks = _inventoryCleanupFullCobblestoneStacks;
                    int requiredCobbleStacks = GetConfiguredCobbleXRequiredStacks();
                    bool cobbleXSent = _inventoryCleanupCobbleXCommandSent;
                    bool cobbleXFailed = _inventoryCleanupCobbleXCommandFailed;
                    string cobbleXCommand = _inventoryCleanupPendingCobbleXCommand;
                    InventoryCleanupOwner cleanupOwner = _inventoryCleanupOwner;
                    string cleanupResult = removedStacks == 0
                        ? "Brak przedmiotów do wyrzucenia"
                        : $"Wyrzucono {removedItems} szt. z {removedStacks} stosów";
                    if (_inventoryCleanupRemainingMarkedStacks > 0)
                        cleanupResult += $"; pozostało {_inventoryCleanupRemainingMarkedStacks} po {InventoryCleanupMaximumDropPasses} przebiegach";
                    string cobbleResult = cobbleXSent
                        ? $"wysłano CobbleX ({_inventoryCleanupPendingCobbleXCommand})"
                        : cobbleXFailed
                            ? "CobbleX niewysłany: uzupełnij poprawną komendę"
                            : $"Cobble 64: {cobbleStacks}/{requiredCobbleStacks}";
                    string miningLogError = RecordCompletedInventoryCleanup(
                        cleanupOwner,
                        removedItems,
                        removedStacks,
                        cobbleXSent,
                        cobbleXCommand,
                        cobbleStacks,
                        requiredCobbleStacks);
                    _inventoryCleanupLastResult = $"{cleanupResult}; {cobbleResult}";
                    if (_inventoryCleanupEatingCompleted)
                        _inventoryCleanupLastResult += "; jedzenie zakończone, slot 1 przywrócony";
                    bool cleanupWarning = _inventoryCleanupRemainingMarkedStacks > 0;
                    _inventoryCleanupLastResultWarning = cobbleXFailed || cleanupWarning || !string.IsNullOrWhiteSpace(miningLogError);
                    ResetInventoryCleanupState(scheduleNext: true, now);
                    UpdateInventoryCleanupStatus(
                        string.IsNullOrWhiteSpace(miningLogError)
                            ? $"Gotowe: {cleanupResult}; {cobbleResult}. Następny skan za {GetConfiguredInventoryCleanupIntervalSeconds()} s."
                            : $"Gotowe: {cleanupResult}; {cobbleResult}. Nie zapisano części logów: {miningLogError}",
                        cobbleXFailed || cleanupWarning || !string.IsNullOrWhiteSpace(miningLogError) ? "Orange" : "Green");
                    return;
            }
        }

        private bool IsInventoryCleanupScanCurrent(int generation, InventoryCleanupOwner owner)
        {
            return generation == _inventoryCleanupGeneration
                && _inventoryCleanupStage == InventoryCleanupStage.WaitForInventory
                && _inventoryCleanupOwner == owner
                && (owner == InventoryCleanupOwner.Kopacz533
                    ? _kopacz533RuntimeEnabled
                    : owner == InventoryCleanupOwner.Kopacz633 && _kopacz633RuntimeEnabled);
        }

        private async void DetectInventoryCleanupTargets(DateTime now)
        {
            if (_inventoryCleanupScanInProgress)
                return;

            if (!_inventoryCleanupCursorParkedForDetection)
            {
                if (TryGetWindowClientRectOnScreen(_targetGameWindowHandle, out RECT clientRect))
                {
                    // Keep the cursor outside the inventory GUI so an item tooltip
                    // cannot cover markers in neighbouring slots during capture.
                    NativeInput.SetCursorPosition(
                        Math.Max(clientRect.Left + 8, clientRect.Right - 16),
                        Math.Max(clientRect.Top + 8, clientRect.Bottom - 16));
                }

                _inventoryCleanupCursorParkedForDetection = true;
                _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupTooltipClearDelayMs);
                UpdateInventoryCleanupStatus("Odsuwanie kursora poza EQ i oczekiwanie na zniknięcie podpowiedzi...", "Orange");
                return;
            }

            _inventoryCleanupCursorParkedForDetection = false;
            _inventoryCleanupDetectionAttempts++;
            if (!IsInventoryCursorVisible())
            {
                RetryOrAbortInventoryCleanup(now, "Gra nie pokazała ekwipunku.", startReconnectIfEnabled: true);
                return;
            }

            if (!TryCaptureTargetClient(out Drawing.Bitmap? bitmap, out Drawing.Rectangle clientArea))
            {
                RetryOrAbortInventoryCleanup(now, "Nie udało się przechwycić okna gry.");
                return;
            }

            using (Drawing.Bitmap capturedBitmap = bitmap!)
            {
                int generation = _inventoryCleanupGeneration;
                InventoryCleanupOwner owner = _inventoryCleanupOwner;
                IntPtr targetWindow = _targetGameWindowHandle;
                _inventoryCleanupScanInProgress = true;
                try
                {
                    HashSet<int> enabledSlots = GetSelectedInventoryCleanupSlots();
                    HashSet<string> enabledItemTypes = GetSelectedInventoryCleanupItemTypes();
                    bool discardEverythingExceptCobblestone = ChkInventoryCleanupAllItemTypes.IsChecked == true;
                    var scan = await Task.Run(() =>
                    {
                        bool found = InventoryMarkerDetector.TryDetect(capturedBitmap, enabledSlots, enabledItemTypes, out InventoryMarkerDetection result);
                        return (Found: found, Detection: result);
                    });
                    if (!IsInventoryCleanupScanCurrent(generation, owner)
                        || targetWindow != _targetGameWindowHandle
                        || GetForegroundWindow() != targetWindow)
                        return;

                    now = DateTime.UtcNow;
                    InventoryMarkerDetection detection = scan.Detection;
                    if (!scan.Found)
                    {
                        RetryOrAbortInventoryCleanup(now, "Nie znaleziono znaczników texturepacka.", startReconnectIfEnabled: true);
                        return;
                    }
                    if (discardEverythingExceptCobblestone && !detection.SupportsFullInventoryScan)
                    {
                        AbortInventoryCleanup(now, "Tryb 'Wyrzucaj wszystko' nie potwierdził widocznej siatki EQ. Włącz aktualną paczkę Minecraft Helper.");
                        return;
                    }
                    if (!discardEverythingExceptCobblestone && detection.UnknownMarkerSlots.Count > 0)
                    {
                        AbortInventoryCleanup(now, "Wykryto starą wersję texturepacka bez rozpoznawania typów. Włącz nową paczkę Minecraft Helper.");
                        return;
                    }
                    if (TryHandleMissingPickaxeDuringInventoryCleanup(capturedBitmap, detection, now))
                        return;

                    _inventoryCleanupDetectionAttempts = 0;
                    _inventoryCleanupClientArea = clientArea;
                    _inventoryCleanupFullCobblestoneStacks = detection.FullCobblestoneSlots.Count;
                    _inventoryCleanupLastFullCobblestoneStacks = _inventoryCleanupFullCobblestoneStacks;
                    IReadOnlyList<DetectedInventoryItem> detectedItems = discardEverythingExceptCobblestone
                        ? detection.AllNonCobblestoneItems
                        : detection.Items;
                    IReadOnlyList<int> detectedSlots = discardEverythingExceptCobblestone
                        ? detection.AllNonCobblestoneSlots
                        : detection.MarkedSlots;
                    UpdateConfirmedInventoryCleanupCounts(detectedItems);
                    int requiredCobbleStacks = GetConfiguredCobbleXRequiredStacks();
                    _inventoryCleanupPendingCobbleXCommand = GetConfiguredCobbleXCommand();
                    _inventoryCleanupCobbleXCommandPending = ChkCobbleXEnabled.IsChecked == true
                        && _inventoryCleanupFullCobblestoneStacks >= requiredCobbleStacks
                        && !string.IsNullOrWhiteSpace(_inventoryCleanupPendingCobbleXCommand);
                    _inventoryCleanupCobbleXCommandFailed = ChkCobbleXEnabled.IsChecked == true
                        && _inventoryCleanupFullCobblestoneStacks >= requiredCobbleStacks
                        && string.IsNullOrWhiteSpace(_inventoryCleanupPendingCobbleXCommand);
                    _inventoryCleanupTargets.Clear();
                    foreach (int slot in detectedSlots)
                    {
                        Drawing.Point relativeCenter = detection.Layout.GetSlotCenter(slot);
                        _inventoryCleanupTargets.Add(new Drawing.Point(
                            clientArea.Left + relativeCenter.X,
                            clientArea.Top + relativeCenter.Y));
                    }

                    _inventoryCleanupTargetIndex = 0;
                    if (_inventoryCleanupTargets.Count == 0)
                    {
                        string scanSummary = _inventoryCleanupDropPass == 0
                            ? "brak przedmiotów do wyrzucenia"
                            : $"EQ czyste po {_inventoryCleanupDropPass} przebiegach; wyrzucono {_inventoryCleanupRemovedItems} szt. z {_inventoryCleanupRemovedStacks} stosów";
                        UpdateInventoryCleanupStatus($"Skan OK (GUI x{detection.Layout.Scale}): {scanSummary}; Cobble 64: {_inventoryCleanupFullCobblestoneStacks}/{requiredCobbleStacks}.", "Green");
                        _inventoryCleanupStage = InventoryCleanupStage.CloseInventory;
                        _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupCloseDelayMs);
                    }
                    else if (_inventoryCleanupDropPass >= InventoryCleanupMaximumDropPasses)
                    {
                        _inventoryCleanupRemainingMarkedStacks = _inventoryCleanupTargets.Count;
                        _inventoryCleanupTargets.Clear();
                        UpdateInventoryCleanupStatus(
                            $"Zatrzymano po {InventoryCleanupMaximumDropPasses} przebiegach: nadal wykryto {_inventoryCleanupRemainingMarkedStacks} stosów. Zamykam EQ, aby uniknąć pętli.",
                            "Orange");
                        _inventoryCleanupStage = InventoryCleanupStage.CloseInventory;
                        _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupCloseDelayMs);
                    }
                    else
                    {
                        _inventoryCleanupDropPass++;
                        UpdateInventoryCleanupStatus(
                            $"Skan {_inventoryCleanupDropPass}/{InventoryCleanupMaximumDropPasses} (GUI x{detection.Layout.Scale}): {_inventoryCleanupTargets.Count} stosów do wyrzucenia; Cobble 64: {_inventoryCleanupFullCobblestoneStacks}/{requiredCobbleStacks}.",
                            "Green");
                        _inventoryCleanupStage = InventoryCleanupStage.MoveToSlot;
                        _nextInventoryCleanupStageAtUtc = now;
                    }
                }
                catch (Exception ex)
                {
                    if (IsInventoryCleanupScanCurrent(generation, owner))
                        RetryOrAbortInventoryCleanup(DateTime.UtcNow, "Błąd skanowania EQ: " + ex.Message);
                }
                finally
                {
                    _inventoryCleanupScanInProgress = false;
                }
            }
        }

        private bool TryHandleMissingPickaxeDuringInventoryCleanup(
            Drawing.Bitmap capturedBitmap,
            InventoryMarkerDetection detection,
            DateTime now)
        {
            if (!IsPeriodicAutoReconnectEnabled()
                || !_settings.AutoReconnectMissingPickaxeRecoveryEnabled)
            {
                return false;
            }

            if (!detection.HasGuiMarkers)
            {
                AbortInventoryCleanup(
                    now,
                    "Nie potwierdzono aktualnej paczki Minecraft Helper. Pomijam kontrolę kilofa, aby nie wykonać fałszywego /home.");
                return true;
            }

            if (InventoryMarkerDetector.ContainsMarkedItem(
                    capturedBitmap,
                    detection.Layout,
                    "diamond_pickaxe",
                    includeHotbar: true))
            {
                return false;
            }

            InventoryCleanupOwner owner = _inventoryCleanupOwner;
            string ownerLabel = GetInventoryCleanupOwnerLabel(owner);
            const string reason = "Auto EQ nie wykrył znacznika diamentowego kilofa w EQ ani na hotbarze.";
            SendKeyTap(VK_E);
            RecordAbortedInventoryCleanup(reason);
            _inventoryCleanupLastResult = "Przerwano: brak diamentowego kilofa";
            _inventoryCleanupLastResultWarning = true;
            ResetInventoryCleanupState(scheduleNext: false, now);
            UpdateInventoryCleanupStatus($"{reason} Uruchamiam powrót do home dla {ownerLabel}.", "Red");
            BeginMissingPickaxeHomeRecovery(owner, now);
            return true;
        }

        private string RecordCompletedInventoryCleanup(
            InventoryCleanupOwner owner,
            int removedItems,
            int removedStacks,
            bool cobbleXSent,
            string cobbleXCommand,
            int detectedCobblestoneStacks,
            int requiredCobblestoneStacks)
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(_inventoryCleanupLogStartError))
                errors.Add(_inventoryCleanupLogStartError);

            string ownerLabel = GetInventoryCleanupOwnerLabel(owner);
            List<MiningLogItemDetail> itemDetails = BuildInventoryCleanupItemDetails();

            string details = removedStacks == 0
                ? "Skan zakończony — nie znaleziono przedmiotów do wyrzucenia."
                : $"Skan zakończony — wyrzucono {removedItems} szt. z {removedStacks} stosów.";
            if (_inventoryCleanupRemainingMarkedStacks > 0)
                details += $" Pozostało {_inventoryCleanupRemainingMarkedStacks} stosów po {InventoryCleanupMaximumDropPasses} przebiegach.";

            bool saved = _miningLogService.CompleteInventorySession(
                _inventoryCleanupLogSessionId,
                ownerLabel,
                removedItems,
                removedStacks,
                itemDetails,
                _inventoryCleanupDropPass,
                _inventoryCleanupRemainingMarkedStacks,
                cobbleXSent,
                cobbleXCommand,
                detectedCobblestoneStacks,
                requiredCobblestoneStacks,
                _inventoryCleanupEatingCompleted,
                details,
                out string completionError);
            if (!saved)
                errors.Add(completionError);

            _inventoryCleanupLogSessionId = string.Empty;
            _inventoryCleanupLogStartError = string.Empty;

            RefreshMiningLogsSummary();
            return string.Join(" | ", errors.Where(error => !string.IsNullOrWhiteSpace(error)).Distinct());
        }

        private List<MiningLogItemDetail> BuildInventoryCleanupItemDetails()
        {
            var itemDetails = new List<MiningLogItemDetail>();
            foreach ((string itemId, int itemCount) in _inventoryCleanupItemTypeCounts
                .OrderBy(pair => string.Equals(pair.Key, "other", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(pair => GetInventoryCleanupItemLabel(pair.Key), StringComparer.CurrentCultureIgnoreCase))
            {
                _inventoryCleanupItemTypeStackCounts.TryGetValue(itemId, out int stackCount);
                if (itemCount <= 0 && stackCount <= 0)
                    continue;

                itemDetails.Add(new MiningLogItemDetail
                {
                    ItemId = itemId,
                    Label = GetInventoryCleanupItemLabel(itemId),
                    ItemCount = Math.Max(0, itemCount),
                    StackCount = Math.Max(0, stackCount)
                });
            }
            return itemDetails;
        }

        private static string GetInventoryCleanupOwnerLabel(InventoryCleanupOwner owner)
        {
            return owner switch
            {
                InventoryCleanupOwner.Kopacz533 => "Kopacz 5/3/3",
                InventoryCleanupOwner.Kopacz633 => "Kopacz 6/3/3",
                _ => "Kopacz"
            };
        }

        private string GetMiningRunId(InventoryCleanupOwner owner)
        {
            return owner switch
            {
                InventoryCleanupOwner.Kopacz533 => _kopacz533MiningRunId,
                InventoryCleanupOwner.Kopacz633 => _kopacz633MiningRunId,
                _ => string.Empty
            };
        }

        private void SetMiningRunId(InventoryCleanupOwner owner, string runId)
        {
            if (owner == InventoryCleanupOwner.Kopacz533)
                _kopacz533MiningRunId = runId;
            else if (owner == InventoryCleanupOwner.Kopacz633)
                _kopacz633MiningRunId = runId;
        }

        private void StartMiningLogRun(InventoryCleanupOwner owner)
        {
            string previousRunId = GetMiningRunId(owner);
            if (!string.IsNullOrWhiteSpace(previousRunId))
            {
                _ = _miningLogService.CompleteMiningRun(
                    previousRunId,
                    "Poprzednia sesja została zamknięta podczas uruchamiania nowego kopania.",
                    MiningLogStatuses.Interrupted,
                    out _);
            }

            string cleanupMode = ChkInventoryCleanupEnabled.IsChecked != true
                ? "Auto EQ wyłączone"
                : ChkInventoryCleanupAllItemTypes.IsChecked == true
                    ? "Auto EQ: wszystko poza cobblestone"
                    : "Auto EQ: wybrane przedmioty";
            string direction = owner == InventoryCleanupOwner.Kopacz633
                ? CbKopacz633Direction.SelectedIndex == 2
                    ? $"do góry, {GetConfiguredKopacz633UpwardWidth()}×{GetConfiguredKopacz633UpwardLength()}"
                    : $"na wprost, szerokość {GetConfiguredKopacz633ForwardWidth()}"
                : "tryb standardowy";
            string details = $"Uruchomiono {GetInventoryCleanupOwnerLabel(owner)} ({direction}). {cleanupMode}; "
                + $"CobbleX: {(ChkCobbleXEnabled.IsChecked == true ? "ON" : "OFF")}; "
                + $"jedzenie po EQ: {(ChkInventoryCleanupEatAfterCleanup.IsChecked == true ? "ON" : "OFF")}.";

            if (_miningLogService.StartMiningRun(
                    GetInventoryCleanupOwnerLabel(owner),
                    details,
                    out string runId,
                    out _))
            {
                SetMiningRunId(owner, runId);
            }
            else
            {
                SetMiningRunId(owner, string.Empty);
            }

            RefreshMiningLogsSummary();
        }

        private void EndMiningLogRun(
            InventoryCleanupOwner owner,
            string reason,
            string status = MiningLogStatuses.Completed)
        {
            string runId = GetMiningRunId(owner);
            if (string.IsNullOrWhiteSpace(runId))
                return;

            _ = _miningLogService.CompleteMiningRun(runId, reason, status, out _);
            SetMiningRunId(owner, string.Empty);
            RefreshMiningLogsSummary();
        }

        private void RecordAutomationLogEvent(string eventType, string status, string details)
        {
            string owner = string.IsNullOrWhiteSpace(_autoReconnectLogOwner)
                ? "Auto reconnect"
                : _autoReconnectLogOwner;
            _ = _miningLogService.RecordAutomationEvent(
                _autoReconnectLogMiningRunId,
                owner,
                eventType,
                status,
                details,
                out _);
            RefreshMiningLogsSummary();
        }

        private void UpdateConfirmedInventoryCleanupCounts(IReadOnlyList<DetectedInventoryItem> currentItems)
        {
            if (_inventoryCleanupDropPass == 0)
            {
                _inventoryCleanupInitialMarkedStacks = currentItems.Count;
                _inventoryCleanupInitialItemTypeCounts.Clear();
                _inventoryCleanupInitialItemTypeStackCounts.Clear();
                foreach (DetectedInventoryItem item in currentItems)
                {
                    _inventoryCleanupInitialItemTypeCounts.TryGetValue(item.ItemId, out int count);
                    _inventoryCleanupInitialItemTypeCounts[item.ItemId] = count + Math.Max(1, item.Quantity);
                    _inventoryCleanupInitialItemTypeStackCounts.TryGetValue(item.ItemId, out int stacks);
                    _inventoryCleanupInitialItemTypeStackCounts[item.ItemId] = stacks + 1;
                }
            }

            var currentItemTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var currentItemTypeStackCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (DetectedInventoryItem item in currentItems)
            {
                currentItemTypeCounts.TryGetValue(item.ItemId, out int count);
                currentItemTypeCounts[item.ItemId] = count + Math.Max(1, item.Quantity);
                currentItemTypeStackCounts.TryGetValue(item.ItemId, out int stacks);
                currentItemTypeStackCounts[item.ItemId] = stacks + 1;
            }

            _inventoryCleanupRemovedStacks = Math.Max(0, _inventoryCleanupInitialMarkedStacks - currentItems.Count);
            _inventoryCleanupRemovedItems = 0;
            _inventoryCleanupItemTypeCounts.Clear();
            _inventoryCleanupItemTypeStackCounts.Clear();
            foreach ((string itemId, int initialCount) in _inventoryCleanupInitialItemTypeCounts)
            {
                currentItemTypeCounts.TryGetValue(itemId, out int currentCount);
                int removedCount = Math.Max(0, initialCount - currentCount);
                if (removedCount == 0)
                    continue;

                _inventoryCleanupItemTypeCounts[itemId] = removedCount;
                _inventoryCleanupRemovedItems += removedCount;

                _inventoryCleanupInitialItemTypeStackCounts.TryGetValue(itemId, out int initialStacks);
                currentItemTypeStackCounts.TryGetValue(itemId, out int currentStacks);
                int removedStacks = Math.Max(0, initialStacks - currentStacks);
                if (removedStacks > 0)
                    _inventoryCleanupItemTypeStackCounts[itemId] = removedStacks;
            }
        }

        private void RetryOrAbortInventoryCleanup(
            DateTime now,
            string reason,
            bool startReconnectIfEnabled = false)
        {
            if (_inventoryCleanupDetectionAttempts < InventoryCleanupMaximumDetectionAttempts)
            {
                _nextInventoryCleanupStageAtUtc = now.AddMilliseconds(InventoryCleanupDetectionRetryMs);
                return;
            }

            InventoryCleanupOwner owner = _inventoryCleanupOwner;
            bool ownerStillRunning = owner == InventoryCleanupOwner.Kopacz533
                ? _kopacz533RuntimeEnabled
                : owner == InventoryCleanupOwner.Kopacz633 && _kopacz633RuntimeEnabled;
            if (startReconnectIfEnabled
                && IsPeriodicAutoReconnectEnabled()
                && ownerStillRunning
                && _autoReconnectStage == AutoReconnectStage.None)
            {
                string reconnectReason = reason + " Po trzech próbach uruchomiono flow reconnectu.";
                RecordAbortedInventoryCleanup(reconnectReason);
                _inventoryCleanupLastResult = "Reconnect: " + reason;
                _inventoryCleanupLastResultWarning = true;
                ResetInventoryCleanupState(scheduleNext: false, now);
                UpdateInventoryCleanupStatus(reconnectReason, "Orange");
                BeginFullAutoReconnect(manual: false);
                return;
            }

            AbortInventoryCleanup(now, reason + " Sprawdź aktywny texturepack.");
        }

        private void AbortInventoryCleanup(DateTime now, string reason)
        {
            ReleaseInventoryCleanupDropKeys();
            if (IsInventoryCursorVisible())
                SendKeyTap(VK_ESCAPE);

            RecordAbortedInventoryCleanup(reason);
            _inventoryCleanupLastResult = "Przerwano: " + reason;
            _inventoryCleanupLastResultWarning = true;
            ResetInventoryCleanupState(scheduleNext: true, now);
            UpdateInventoryCleanupStatus($"Czyszczenie przerwane: {reason}", "Red");
        }

        private void RecordAbortedInventoryCleanup(string reason)
        {
            if (string.IsNullOrWhiteSpace(_inventoryCleanupLogSessionId))
                return;

            _ = _miningLogService.AbortInventorySession(
                _inventoryCleanupLogSessionId,
                GetInventoryCleanupOwnerLabel(_inventoryCleanupOwner),
                reason,
                _inventoryCleanupRemovedItems,
                _inventoryCleanupRemovedStacks,
                BuildInventoryCleanupItemDetails(),
                _inventoryCleanupDropPass,
                _inventoryCleanupRemainingMarkedStacks,
                _inventoryCleanupFullCobblestoneStacks,
                GetConfiguredCobbleXRequiredStacks(),
                out _);
            _inventoryCleanupLogSessionId = string.Empty;
            _inventoryCleanupLogStartError = string.Empty;
            RefreshMiningLogsSummary();
        }

        private bool SetInventoryCleanupControl(bool down)
        {
            if (_inventoryCleanupControlDown == down)
                return true;

            if (!NativeInput.SendKey(VK_LCONTROL, down))
                return false;

            _inventoryCleanupControlDown = down;
            return true;
        }

        private bool SetInventoryCleanupDropKey(bool down)
        {
            if (_inventoryCleanupDropKeyDown == down)
                return true;

            if (down)
            {
                int virtualKey = GetConfiguredDropItemVirtualKey();
                if (!NativeInput.SendKey(virtualKey, down: true))
                    return false;

                _inventoryCleanupDropVirtualKey = virtualKey;
                _inventoryCleanupDropKeyDown = true;
                return true;
            }

            int pressedVirtualKey = _inventoryCleanupDropVirtualKey != 0
                ? _inventoryCleanupDropVirtualKey
                : GetConfiguredDropItemVirtualKey();
            if (!NativeInput.SendKey(pressedVirtualKey, down: false))
                return false;

            _inventoryCleanupDropKeyDown = false;
            _inventoryCleanupDropVirtualKey = 0;
            return true;
        }

        private void ReleaseInventoryCleanupDropKeys()
        {
            if (_inventoryCleanupDropKeyDown)
            {
                int virtualKey = _inventoryCleanupDropVirtualKey != 0
                    ? _inventoryCleanupDropVirtualKey
                    : GetConfiguredDropItemVirtualKey();
                NativeInput.SendKey(virtualKey, down: false);
                _inventoryCleanupDropKeyDown = false;
                _inventoryCleanupDropVirtualKey = 0;
            }

            if (_inventoryCleanupControlDown)
            {
                NativeInput.SendKey(VK_LCONTROL, down: false);
                _inventoryCleanupControlDown = false;
            }
        }

        private void SetInventoryCleanupReturnLeft(bool down)
        {
            if (_inventoryCleanupReturnLeftDown == down)
                return;

            if (down)
                SendKeyDown(VK_A);
            else
                SendKeyUp(VK_A);
            _inventoryCleanupReturnLeftDown = down;
        }

        private void SetInventoryCleanupReturnBackward(bool down)
        {
            if (_inventoryCleanupReturnBackwardDown == down)
                return;

            if (down)
                SendKeyDown(VK_S);
            else
                SendKeyUp(VK_S);
            _inventoryCleanupReturnBackwardDown = down;
        }

        private void ReleaseInventoryCleanupReturnKeys()
        {
            SetInventoryCleanupReturnLeft(down: false);
            SetInventoryCleanupReturnBackward(down: false);
            _inventoryCleanupReturnLeftUntilUtc = DateTime.MinValue;
            _inventoryCleanupReturnBackwardUntilUtc = DateTime.MinValue;
        }

        private InventoryCleanupStage GetInventoryCleanupPostCommandStage()
        {
            return _inventoryCleanupEatAfterCleanupPending
                ? InventoryCleanupStage.SelectFoodSlot
                : InventoryCleanupStage.ResumeMining;
        }

        private void SetInventoryCleanupEatingHold(bool enabled)
        {
            if (_inventoryCleanupEatingRightButtonDown == enabled)
                return;

            SendMouseButton(leftButton: false, down: enabled);
            _inventoryCleanupEatingRightButtonDown = enabled;
        }

        private bool IsKopaczCommandCountdownPaused(InventoryCleanupOwner owner)
        {
            return owner != InventoryCleanupOwner.None
                && _inventoryCleanupCommandPauseOwner == owner
                && _inventoryCleanupCommandPauseStartedAtUtc != DateTime.MinValue;
        }

        private void PauseKopaczCommandCountdown(InventoryCleanupOwner owner, DateTime now)
        {
            if (owner == InventoryCleanupOwner.None || IsKopaczCommandCountdownPaused(owner))
                return;

            _inventoryCleanupCommandPauseOwner = owner;
            _inventoryCleanupCommandPauseStartedAtUtc = now;
        }

        private int GetKopaczCommandRemainingSeconds(InventoryCleanupOwner owner, DateTime now)
        {
            DateTime deadline = owner == InventoryCleanupOwner.Kopacz533
                ? _nextKopacz533CommandAtUtc
                : _nextKopacz633CommandAtUtc;
            if (deadline == DateTime.MaxValue)
                return 0;

            DateTime countdownNow = IsKopaczCommandCountdownPaused(owner)
                ? _inventoryCleanupCommandPauseStartedAtUtc
                : now;
            return Math.Max(0, (int)Math.Ceiling((deadline - countdownNow).TotalSeconds));
        }

        private void ResumeKopaczCommandCountdown(DateTime now)
        {
            InventoryCleanupOwner owner = _inventoryCleanupCommandPauseOwner;
            DateTime pauseStartedAtUtc = _inventoryCleanupCommandPauseStartedAtUtc;
            _inventoryCleanupCommandPauseOwner = InventoryCleanupOwner.None;
            _inventoryCleanupCommandPauseStartedAtUtc = DateTime.MinValue;
            if (owner == InventoryCleanupOwner.None || pauseStartedAtUtc == DateTime.MinValue)
                return;

            TimeSpan pausedFor = now > pauseStartedAtUtc ? now - pauseStartedAtUtc : TimeSpan.Zero;
            if (owner == InventoryCleanupOwner.Kopacz533
                && _kopacz533RuntimeEnabled
                && _nextKopacz533CommandAtUtc != DateTime.MaxValue)
            {
                _nextKopacz533CommandAtUtc = AddWithoutOverflow(_nextKopacz533CommandAtUtc, pausedFor);
            }
            else if (owner == InventoryCleanupOwner.Kopacz633
                && _kopacz633RuntimeEnabled
                && _nextKopacz633CommandAtUtc != DateTime.MaxValue)
            {
                _nextKopacz633CommandAtUtc = AddWithoutOverflow(_nextKopacz633CommandAtUtc, pausedFor);
            }
        }

        private static DateTime AddWithoutOverflow(DateTime value, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                return value;
            long remainingTicks = DateTime.MaxValue.Ticks - value.Ticks;
            return duration.Ticks >= remainingTicks ? DateTime.MaxValue : value.Add(duration);
        }

        private void ResetInventoryCleanupState(bool scheduleNext, DateTime? now = null)
        {
            // Invalidate results still being calculated on the worker thread.
            _inventoryCleanupGeneration++;
            DateTime resetAtUtc = now ?? DateTime.UtcNow;
            ResumeKopaczCommandCountdown(resetAtUtc);
            if (_inventoryCleanupStage != InventoryCleanupStage.None
                && !string.IsNullOrWhiteSpace(_inventoryCleanupLogSessionId))
            {
                RecordAbortedInventoryCleanup("Sesja EQ została zatrzymana przed zakończeniem skanowania.");
            }

            ReleaseInventoryCleanupDropKeys();
            ReleaseInventoryCleanupReturnKeys();
            SetInventoryCleanupEatingHold(false);
            _inventoryCleanupStage = InventoryCleanupStage.None;
            _inventoryCleanupOwner = InventoryCleanupOwner.None;
            _inventoryCleanupTargets.Clear();
            _inventoryCleanupInitialItemTypeCounts.Clear();
            _inventoryCleanupInitialItemTypeStackCounts.Clear();
            _inventoryCleanupItemTypeCounts.Clear();
            _inventoryCleanupItemTypeStackCounts.Clear();
            _inventoryCleanupTargetIndex = 0;
            _inventoryCleanupDetectionAttempts = 0;
            _inventoryCleanupDropPass = 0;
            _inventoryCleanupInitialMarkedStacks = 0;
            _inventoryCleanupRemovedStacks = 0;
            _inventoryCleanupRemovedItems = 0;
            _inventoryCleanupRemainingMarkedStacks = 0;
            _inventoryCleanupCursorParkedForDetection = false;
            _inventoryCleanupClientArea = Drawing.Rectangle.Empty;
            _inventoryCleanupFullCobblestoneStacks = 0;
            _inventoryCleanupCobbleXCommandPending = false;
            _inventoryCleanupCobbleXCommandSent = false;
            _inventoryCleanupCobbleXCommandFailed = false;
            _inventoryCleanupPendingCobbleXCommand = string.Empty;
            _inventoryCleanupEatAfterCleanupPending = false;
            _inventoryCleanupEatingCompleted = false;
            _inventoryCleanupLogSessionId = string.Empty;
            _inventoryCleanupLogStartError = string.Empty;
            _inventoryCleanupOpenedAtUtc = DateTime.MinValue;
            _nextInventoryCleanupStageAtUtc = resetAtUtc;

            if (scheduleNext && ChkInventoryCleanupEnabled.IsChecked == true)
                ScheduleNextInventoryCleanup(resetAtUtc);
            else
                _nextInventoryCleanupAtUtc = DateTime.MaxValue;
        }

        private void ResetInventoryCleanupForOwner(InventoryCleanupOwner owner)
        {
            if (_inventoryCleanupOwner == owner)
                ResetInventoryCleanupState(scheduleNext: false);
        }

        private void RunKopacz633Tick(DateTime now)
        {
            if (!IsKopacz633DirectionSelected())
            {
                SetKopacz633AttackHold(false);
                SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
                return;
            }

            if (TryRunInventoryCleanup(InventoryCleanupOwner.Kopacz633, now))
                return;

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
            if (TryRunInventoryCleanup(InventoryCleanupOwner.Kopacz533, now))
                return;

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

        private bool TryGetPressedBindyEntry(bool allowActivation, out BindyEntry entry)
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

                // Keep the edge state synchronized while chat, EQ or another GUI
                // is open. The same held key cannot fire later when the GUI closes.
                if (!allowActivation)
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
            UpdateTemporaryStatusBar($"BINDY: uruchomiono \"{_bindyPendingEntryName}\"", "Orange");
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
                    SendChatOpenKeyTap();
                    _bindyCommandStage = BindyCommandStage.TypeCommand;
                    _nextBindyStageAtUtc = now.AddMilliseconds(BindyDelayAfterOpenChatMs);
                    return true;

                case BindyCommandStage.TypeCommand:
                    if (!SendTextByKeyboard(_bindyPendingCommand))
                    {
                        UpdateTemporaryStatusBar("BINDY: błąd wpisywania komendy", "Red", 5);
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
                    UpdateTemporaryStatusBar($"BINDY: {executedName} zostało wykonane", "Green");
                    RefreshOverlayHud(now);
                    _bindyHudClearTimer.Stop();
                    _bindyHudClearTimer.Start();
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
                    SendChatOpenKeyTap();
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
                    SendChatOpenKeyTap();
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
                SendMouseButton(leftButton: true, down: true);
            }
            else
            {
                SendMouseButton(leftButton: true, down: false);
                SendKeyUp(VK_SHIFT);
            }

            _kopacz533Holding = enabled;
        }

        private bool SetAutoLeftDabHold(bool enabled)
        {
            if (enabled && (_targetGameWindowHandle == IntPtr.Zero || GetForegroundWindow() != _targetGameWindowHandle))
                enabled = false;

            if (_autoLeftDabHolding == enabled)
                return false;

            if (enabled)
                SendKeyDown(VK_O);
            else
                SendKeyUp(VK_O);

            _autoLeftDabHolding = enabled;
            return true;
        }

        private void SetKopacz633AttackHold(bool enabled)
        {
            if (_kopacz633HoldingAttack == enabled)
                return;

            if (enabled)
                SendMouseButton(leftButton: true, down: true);
            else
                SendMouseButton(leftButton: true, down: false);

            _kopacz633HoldingAttack = enabled;
        }

        private void SetTestFastUpExitBreakHold(bool enabled)
        {
            if (_testFastUpExitBreakHoldActive == enabled)
                return;

            if (enabled)
                SendMouseButton(leftButton: true, down: true);
            else
                SendMouseButton(leftButton: true, down: false);

            _testFastUpExitBreakHoldActive = enabled;
        }

        private void SetTestFastUpExitPlaceHold(bool enabled)
        {
            if (_testFastUpExitPlaceHoldActive == enabled)
                return;

            if (enabled)
                SendMouseButton(leftButton: false, down: true);
            else
                SendMouseButton(leftButton: false, down: false);

            _testFastUpExitPlaceHoldActive = enabled;
        }

        private void SetTestFastUpExitJumpHold(bool enabled)
        {
            if (_testFastUpExitJumpHoldActive == enabled)
                return;

            if (enabled)
                SendKeyDown(VK_SPACE);
            else
                SendKeyUp(VK_SPACE);

            _testFastUpExitJumpHoldActive = enabled;
        }

        private void StartTestFastUpExitJumpPulse(DateTime now)
        {
            _testFastUpExitJumpHoldUntilUtc = now.AddMilliseconds(FastUpJumpHoldMs);
            SetTestFastUpExitJumpHold(true);
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
                    SendChatOpenKeyTap();
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

        private void ResetTestFastUpExitRuntimeState()
        {
            ResetTestFastUpExitRuntimeState(DateTime.UtcNow);
        }

        private void ResetTestFastUpExitRuntimeState(DateTime now)
        {
            SetTestFastUpExitBreakHold(false);
            SetTestFastUpExitPlaceHold(false);
            SetTestFastUpExitJumpHold(false);
            _nextTestFastUpExitActionAtUtc = now;
            _testFastUpExitBreakHoldUntilUtc = now;
            _testFastUpExitPlaceHoldUntilUtc = now;
            _testFastUpExitPlacePulseAtUtc = now;
            _testFastUpExitJumpHoldUntilUtc = now;
            _testFastUpExitStage = FastUpExitStage.LookUp;
            _testFastUpExitLookSweepTicksRemaining = 0;
            _testFastUpExitLookSweepBurstsPerTick = FastUpLookSweepBurstsPerTick;
            _testFastUpExitLookSweepDirectionY = 0;
            _testFastUpExitLookSweepNextStage = FastUpExitStage.LookUp;
        }

        private void ResetTestAutoFishingRuntimeState()
        {
            ResetTestAutoFishingRuntimeState(DateTime.UtcNow);
        }

        private void ResetTestAutoFishingRuntimeState(DateTime now)
        {
            _nextTestAutoFishingScanAtUtc = now;
            _nextTestAutoFishingActionAtUtc = now;
            _nextTestAutoFishingRepairAtUtc = DateTime.MaxValue;
            _nextTestAutoFishingRepairStageAtUtc = now;
            _testAutoFishingAwaitSecondClick = false;
            _testAutoFishingRecastAfterRepairPending = false;
            _testAutoFishingRecastAfterRepairAtUtc = now;
            _testAutoFishingWaitingForCastRegistration = false;
            _testAutoFishingRepairStage = TestAutoFishingRepairStage.None;
            ResetTestAutoFishingDetection(now);
            if (_testAutoFishingRuntimeEnabled && HasConfiguredTestAutoFishingRepair())
                _nextTestAutoFishingRepairAtUtc = now.AddSeconds(GetConfiguredTestAutoFishingRepairIntervalSeconds());
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
            ResetInventoryCleanupForOwner(InventoryCleanupOwner.Kopacz533);
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
            ResetInventoryCleanupForOwner(InventoryCleanupOwner.Kopacz633);
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

            minCps = Math.Clamp(min, 1, AutoClickScheduler.MaximumCps);
            maxCps = Math.Clamp(max, 1, AutoClickScheduler.MaximumCps);
            return true;
        }

        private static void SendMouseMoveRelative(int deltaX, int deltaY)
        {
            NativeInput.SendMouseMoveRelative(deltaX, deltaY);
        }

        private void SendMouseClick(bool leftButton, bool holdPulseMode)
        {
            StartMouseHook();
            NativeInput.SendMouseClick(leftButton, holdPulseMode);
        }

        private void SendMouseButton(bool leftButton, bool down)
        {
            if (down)
                StartMouseHook();
            NativeInput.SendMouseButton(leftButton, down);
        }

        private static void SendKeyTap(int virtualKey)
        {
            SendKeyDown(virtualKey);
            SendKeyUp(virtualKey);
        }

        private static void SendKeyDown(int virtualKey)
        {
            NativeInput.SendKey(virtualKey, down: true);
        }

        private static void SendKeyUp(int virtualKey)
        {
            NativeInput.SendKey(virtualKey, down: false);
        }

        private void UpdateStatusBar(string message, string colorName)
        {
            if (TxtStatusBar == null) return;

            _transientStatusTimer?.Stop();

            TxtStatusBar.Text = message;
            TxtStatusBar.Foreground =
                colorName == "Red" ? new SolidColorBrush(Color.FromRgb(255, 107, 107)) :
                colorName == "Green" ? new SolidColorBrush(Color.FromRgb(56, 214, 180)) :
                colorName == "Orange" ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) :
                new SolidColorBrush(Color.FromRgb(207, 219, 235));
        }

        private void UpdateTemporaryStatusBar(string message, string colorName, int seconds = 4)
        {
            UpdateStatusBar(message, colorName);
            _transientStatusTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
            _transientStatusTimer.Start();
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
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkOverlaySettings_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            UpdateOverlayLayout();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
        }

        private void ChkAnimatedBackgroundEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (CyberBackground != null)
                CyberBackground.IsAnimationEnabled = ChkAnimatedBackgroundEnabled.IsChecked != false;

            if (_isLoadingUi)
                return;

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

        private void ChkAutoLeftComboMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoLeftComboMode.IsChecked != true
                && ChkAutoLeftHoldBindMode.IsChecked != true
                && AutoClickersUseSharedBind())
            {
                ChkAutoLeftComboMode.IsChecked = true;
                ShowSharedAutoClickerBindModeConflict();
                return;
            }

            if (ChkAutoLeftComboMode.IsChecked == true)
                ChkAutoLeftHoldBindMode.IsChecked = false;

            _autoLeftRuntimeEnabled = false;
            SetAutoLeftDabHold(false);
            _autoLeftComboTriggerWasDown = false;
            _autoLeftComboStopWasDown = false;
            _nextAutoLeftClickAtUtc = DateTime.UtcNow;
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkAutoLeftHoldBindMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoLeftHoldBindMode.IsChecked != true
                && ChkAutoLeftComboMode.IsChecked != true
                && AutoClickersUseSharedBind())
            {
                ChkAutoLeftHoldBindMode.IsChecked = true;
                ShowSharedAutoClickerBindModeConflict();
                return;
            }

            if (ChkAutoLeftHoldBindMode.IsChecked == true)
                ChkAutoLeftComboMode.IsChecked = false;

            _autoLeftRuntimeEnabled = false;
            SetAutoLeftDabHold(false);
            _autoLeftBindWasDown = IsConfiguredBindKeyDown(TxtAutoLeftKey.Text);
            _autoLeftComboTriggerWasDown = false;
            _autoLeftComboStopWasDown = false;
            _nextAutoLeftClickAtUtc = DateTime.UtcNow;
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkAutoLeftDabMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            bool shouldHold = ChkAutoLeftDabMode.IsChecked == true
                && ChkAutoLeftEnabled.IsChecked == true
                && _autoLeftRuntimeEnabled
                && _isMinecraftFocused
                && !_isPausedByCursorVisibility;
            SetAutoLeftDabHold(shouldHold);
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkAutoRightComboMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoRightComboMode.IsChecked != true
                && ChkAutoRightHoldBindMode.IsChecked != true
                && AutoClickersUseSharedBind())
            {
                ChkAutoRightComboMode.IsChecked = true;
                ShowSharedAutoClickerBindModeConflict();
                return;
            }

            if (ChkAutoRightComboMode.IsChecked == true)
                ChkAutoRightHoldBindMode.IsChecked = false;

            _autoRightRuntimeEnabled = false;
            _autoRightComboTriggerWasDown = false;
            _autoRightComboStopWasDown = false;
            _nextAutoRightClickAtUtc = DateTime.UtcNow;
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkAutoRightHoldBindMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkAutoRightHoldBindMode.IsChecked == true && AutoClickersUseSharedBind())
            {
                ChkAutoRightHoldBindMode.IsChecked = false;
                UpdateStatusBar("AUTO PPM — Trzymanie bindu wymaga osobnego klawisza.", "Orange");
                var dialog = new BindConflictDialogWindow(
                    "AUTO PPM — Trzymanie bindu",
                    TxtAutoRightKey.Text,
                    "AUTO LPM");
                dialog.Owner = this;
                dialog.ShowDialog();
                return;
            }

            if (ChkAutoRightHoldBindMode.IsChecked == true)
                ChkAutoRightComboMode.IsChecked = false;

            _autoRightRuntimeEnabled = false;
            _autoRightBindWasDown = IsConfiguredBindKeyDown(TxtAutoRightKey.Text);
            _autoRightComboTriggerWasDown = false;
            _autoRightComboStopWasDown = false;
            _nextAutoRightClickAtUtc = DateTime.UtcNow;
            RefreshTopTiles();
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

        private void RefreshEmergencyDamageSoundDevices()
        {
            if (CbEmergencyDamageSoundDevice == null)
                return;

            string preferredId = GetSelectedEmergencyDamageSoundDeviceId();
            if (string.IsNullOrWhiteSpace(preferredId))
                preferredId = _settings.EmergencyDamageSoundDeviceId;

            bool previousLoading = _isLoadingUi;
            _isLoadingUi = true;
            try
            {
                CbEmergencyDamageSoundDevice.Items.Clear();
                IReadOnlyList<AudioOutputDeviceInfo> devices = DamageSoundDetector.GetOutputDevices();
                foreach (AudioOutputDeviceInfo device in devices)
                {
                    CbEmergencyDamageSoundDevice.Items.Add(new ComboBoxItem
                    {
                        Content = device.IsDefault ? $"{device.Name} (domyślne)" : device.Name,
                        Tag = device.Id
                    });
                }

                int selectedIndex = -1;
                for (int i = 0; i < CbEmergencyDamageSoundDevice.Items.Count; i++)
                {
                    if (CbEmergencyDamageSoundDevice.Items[i] is ComboBoxItem item
                        && string.Equals(item.Tag as string, preferredId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                CbEmergencyDamageSoundDevice.SelectedIndex = selectedIndex >= 0
                    ? selectedIndex
                    : (CbEmergencyDamageSoundDevice.Items.Count > 0 ? 0 : -1);
            }
            catch (Exception ex)
            {
                CbEmergencyDamageSoundDevice.Items.Add(new ComboBoxItem
                {
                    Content = "Nie udało się odczytać urządzeń audio",
                    IsEnabled = false
                });
                CbEmergencyDamageSoundDevice.SelectedIndex = 0;
                UpdateEmergencyDamageSoundStatus("Błąd urządzeń audio: " + ex.Message, "Red");
            }
            finally
            {
                _isLoadingUi = previousLoading;
            }

            UpdateEmergencyDamageSoundSourcePreview();
        }

        private string GetSelectedEmergencyDamageSoundDeviceId()
        {
            return CbEmergencyDamageSoundDevice?.SelectedItem is ComboBoxItem item
                ? item.Tag as string ?? string.Empty
                : string.Empty;
        }

        private string ResolveEmergencyDamageSoundDeviceId(out string sourceDescription, out bool matchedMinecraft)
        {
            matchedMinecraft = false;
            if (TryGetCurrentMinecraftProcessId(out int minecraftProcessId)
                && DamageSoundDetector.TryGetOutputDeviceForProcess(minecraftProcessId, out AudioOutputDeviceInfo? minecraftDevice)
                && minecraftDevice != null)
            {
                matchedMinecraft = true;
                sourceDescription = $"Minecraft [{minecraftProcessId}] → {minecraftDevice.Name}";
                return minecraftDevice.Id;
            }

            string fallbackDeviceId = GetSelectedEmergencyDamageSoundDeviceId();
            string fallbackName = CbEmergencyDamageSoundDevice?.SelectedItem is ComboBoxItem fallbackItem
                ? fallbackItem.Content?.ToString() ?? "domyślne urządzenie Windows"
                : "domyślne urządzenie Windows";
            sourceDescription = TryGetCurrentMinecraftProcessId(out int unresolvedProcessId)
                ? $"Nie znaleziono sesji Minecraft [{unresolvedProcessId}] — awaryjnie: {fallbackName}"
                : $"Brak zapisanego procesu Minecraft — awaryjnie: {fallbackName}";
            return fallbackDeviceId;
        }

        private bool TryGetCurrentMinecraftProcessId(out int processId)
        {
            processId = 0;
            IntPtr targetWindow = _targetGameWindowHandle;
            if (targetWindow == IntPtr.Zero && TryResolveTargetWindow(allowPendingSelection: false, out IntPtr resolvedWindow))
            {
                targetWindow = resolvedWindow;
                _targetGameWindowHandle = resolvedWindow;
            }

            if (targetWindow != IntPtr.Zero)
            {
                _ = GetWindowThreadProcessId(targetWindow, out uint processIdRaw);
                if (processIdRaw > 0 && processIdRaw <= int.MaxValue)
                {
                    processId = (int)processIdRaw;
                    return true;
                }
            }

            int configuredProcessId = _settings.TargetProcessId;
            if (configuredProcessId <= 0)
                return false;
            try
            {
                using Process configuredProcess = Process.GetProcessById(configuredProcessId);
                string configuredName = (_settings.TargetProcessName ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(configuredName)
                    && !string.Equals(configuredProcess.ProcessName, configuredName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                processId = configuredProcessId;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateEmergencyDamageSoundSourcePreview()
        {
            if (TxtEmergencyDamageSoundSource == null)
                return;

            try
            {
                _ = ResolveEmergencyDamageSoundDeviceId(out string sourceDescription, out bool matchedMinecraft);
                TxtEmergencyDamageSoundSource.Text = matchedMinecraft
                    ? "Automatycznie: " + sourceDescription
                    : sourceDescription;
                TxtEmergencyDamageSoundSource.Foreground = matchedMinecraft
                    ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                    : new SolidColorBrush(Color.FromRgb(251, 191, 36));
            }
            catch (Exception ex)
            {
                TxtEmergencyDamageSoundSource.Text = "Nie udało się sprawdzić sesji Minecrafta: " + ex.Message;
                TxtEmergencyDamageSoundSource.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
            }
        }

        private void UpdateEmergencyDamageSoundReferenceInfo()
        {
            if (TxtEmergencyDamageSoundReferenceInfo == null)
                return;
            int count = _settings.EmergencyDamageSoundTemplates?.Count ?? 0;
            TxtEmergencyDamageSoundReferenceInfo.Text = count > 0
                ? $"Stały wzorzec gotowy: hit1–hit4 • {count} profili widma • minimalny poziom {_settings.EmergencyDamageSoundMinimumDb:0} dB."
                : "Nie udało się wczytać stałych wzorców hit1–hit4 z plików aplikacji.";
            TxtEmergencyDamageSoundReferenceInfo.Foreground = count > 0
                ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                : new SolidColorBrush(Color.FromRgb(146, 166, 193));
        }

        private void UpdateEmergencyDamageSoundStatus(string message, string colorName = "Default")
        {
            if (TxtEmergencyDamageSoundStatus == null)
                return;
            TxtEmergencyDamageSoundStatus.Text = message;
            TxtEmergencyDamageSoundStatus.Foreground = colorName == "Red"
                ? new SolidColorBrush(Color.FromRgb(255, 107, 107))
                : colorName == "Green"
                    ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                    : colorName == "Orange"
                        ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                        : new SolidColorBrush(Color.FromRgb(146, 166, 193));
        }

        private void UpdateEmergencyReconnectStatus(string message, string colorName = "Default")
        {
            if (TxtEmergencyReconnectStatus == null)
                return;
            TxtEmergencyReconnectStatus.Text = message;
            TxtEmergencyReconnectStatus.Foreground = colorName == "Red"
                ? new SolidColorBrush(Color.FromRgb(255, 107, 107))
                : colorName == "Green"
                    ? new SolidColorBrush(Color.FromRgb(56, 214, 180))
                    : colorName == "Orange"
                        ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                        : new SolidColorBrush(Color.FromRgb(146, 166, 193));
        }

        private void RefreshEmergencyReconnectProfileSummary()
        {
            if (TxtEmergencyReconnectProfileSummary == null)
                return;

            AutoReconnectServerProfile? profile = GetSelectedAutoReconnectServerProfile();
            if (profile == null)
            {
                TxtEmergencyReconnectProfileSummary.Text = "Brak profilu Auto reconnect. Dodaj i wybierz profil serwera.";
                TxtEmergencyReconnectProfileSummary.Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 107));
                return;
            }

            NormalizeAutoReconnectHomeSettings(profile);
            string normalHome = profile.HomeHasGui
                ? $"{profile.HomeCommand} → GUI {profile.HomeGuiRows}×{profile.HomeGuiColumns}, slot {profile.HomeGuiSlot}"
                : $"{profile.HomeCommand} → bez GUI";
            string missingHome = profile.MissingPickaxeHomeHasGui == true
                ? $"{profile.MissingPickaxeHomeCommand} → GUI {profile.MissingPickaxeHomeGuiRows}×{profile.MissingPickaxeHomeGuiColumns}, slot {profile.MissingPickaxeHomeGuiSlot}"
                : $"{profile.MissingPickaxeHomeCommand} → bez GUI";
            TxtEmergencyReconnectProfileSummary.Text =
                $"Profil: {profile.Name} • serwer: {profile.ServerAddress}\n" +
                $"Kilof wykryty: {normalHome} → wznowienie Kopacza\n" +
                $"Brak kilofa: {missingHome} → teleport i zamknięcie programu";
            TxtEmergencyReconnectProfileSummary.Foreground = profile.MissingPickaxeRecoveryEnabled
                ? new SolidColorBrush(Color.FromRgb(146, 166, 193))
                : new SolidColorBrush(Color.FromRgb(251, 191, 36));
            if (!profile.MissingPickaxeRecoveryEnabled)
                TxtEmergencyReconnectProfileSummary.Text += "\nUWAGA: w profilu jest wyłączona obsługa braku kilofa.";
        }

        private void EmergencyReconnectSetting_Changed(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;
            _settings.EmergencyReconnectDelaySeconds = Math.Clamp(
                ParseNonNegativeInt(TxtEmergencyReconnectDelaySeconds.Text),
                1,
                600);
            MarkDirty();
        }

        private void BtnEmergencyReconnectStop_Click(object sender, RoutedEventArgs e)
        {
            if (_autoReconnectStage != AutoReconnectStage.None)
                StopAutoReconnect("Awaryjna procedura zatrzymana ręcznie.", resumeMining: false, warning: true);
            else
                ClearEmergencyReconnectRuntimeState(stopSoundMonitoring: true);
            UpdateEmergencyReconnectStatus("Awaryjna procedura zatrzymana ręcznie.", "Orange");
            UpdateEnabledStates();
        }

        private void ChkEmergencyDamageSoundEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            _settings.EmergencyDamageSoundEnabled = ChkEmergencyDamageSoundEnabled.IsChecked == true;
            _settings.EmergencyReconnectEnabled = _settings.EmergencyDamageSoundEnabled;
            if (!_settings.EmergencyDamageSoundEnabled)
            {
                if (_emergencyReconnectActive)
                    StopAutoReconnect("Awaryjna ochrona wyłączona przez użytkownika.", resumeMining: false, warning: true);
                StopEmergencyDamageSoundListening(updateStatus: true);
                UpdateEmergencyReconnectStatus("Ochrona awaryjna jest wyłączona.");
            }
            else if (_settings.EmergencyDamageSoundTemplates.Count == 0)
            {
                UpdateEmergencyDamageSoundStatus("Włączono, ale brakuje stałych wzorców hit1–hit4.", "Orange");
                UpdateEmergencyReconnectStatus("Brakuje wzorców hit1–hit4 — procedura nie jest jeszcze uzbrojona.", "Orange");
            }
            else
            {
                UpdateEmergencyDamageSoundStatus("Gotowy. Nasłuch uruchomi się razem z Kopaczem.", "Green");
                UpdateEmergencyReconnectStatus(
                    ChkEmergencyDamageSoundTestMode.IsChecked == false
                        ? "Gotowe. Pełna ochrona uruchomi się po alarmie podczas pracy Kopacza."
                        : "Tryb testowy jest włączony — alarm nie wyjdzie z serwera.",
                    ChkEmergencyDamageSoundTestMode.IsChecked == false ? "Green" : "Orange");
            }
            RefreshEmergencyReconnectProfileSummary();
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkEmergencyDamageSoundSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkEmergencyDamageSoundTestMode.IsChecked == false)
            {
                MessageBoxResult confirmation = MessageBox.Show(
                    "Wyłączenie trybu testowego uzbraja prawdziwą reakcję. Po wykryciu wzorca podczas pracy Kopacza program natychmiast zatrzyma automatyzację, otworzy menu ESC i kliknie Disconnect.\n\nKontynuować?",
                    "Uzbrojenie awaryjnego wyjścia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (confirmation != MessageBoxResult.Yes)
                {
                    _isLoadingUi = true;
                    ChkEmergencyDamageSoundTestMode.IsChecked = true;
                    _isLoadingUi = false;
                }
            }

            _settings.EmergencyDamageSoundTestMode = ChkEmergencyDamageSoundTestMode.IsChecked != false;
            UpdateEmergencyDamageSoundStatus(
                _settings.EmergencyDamageSoundTestMode
                    ? "Tryb testowy: alarm nie zatrzyma kopania ani nie wyjdzie z serwera."
                    : "TRYB REALNY UZBROJONY: wykrycie uruchomi ESC → Disconnect.",
                _settings.EmergencyDamageSoundTestMode ? "Green" : "Orange");
            UpdateEmergencyReconnectStatus(
                _settings.EmergencyDamageSoundTestMode
                    ? "Tryb testowy jest włączony — alarm nie wyjdzie z serwera."
                    : "Gotowe. Pełna ochrona uruchomi się po alarmie podczas pracy Kopacza.",
                _settings.EmergencyDamageSoundTestMode ? "Orange" : "Green");
            MarkDirty();
        }

        private void SlEmergencyDamageSoundSimilarity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int value = Math.Clamp((int)Math.Round(e.NewValue), 70, 99);
            if (TxtEmergencyDamageSoundSimilarityValue != null)
                TxtEmergencyDamageSoundSimilarityValue.Text = $"{value}%";
            if (_isLoadingUi)
                return;

            _settings.EmergencyDamageSoundSimilarityPercent = value;
            if (_damageSoundDetector.IsRunning)
            {
                _damageSoundDetector.Stop();
                _emergencyDamageSoundMonitoringForMiner = false;
                _emergencyDamageSoundManualTestActive = false;
            }
            MarkDirty();
        }

        private void CbEmergencyDamageSoundDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            _settings.EmergencyDamageSoundDeviceId = GetSelectedEmergencyDamageSoundDeviceId();
            StopEmergencyDamageSoundListening(updateStatus: false);
            UpdateEmergencyDamageSoundSourcePreview();
            UpdateEmergencyDamageSoundStatus("Zmieniono urządzenie awaryjne. Minecraft nadal jest wybierany automatycznie.", "Green");
            MarkDirty();
        }

        private void BtnEmergencyDamageSoundRefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            StopEmergencyDamageSoundListening(updateStatus: false);
            RefreshEmergencyDamageSoundDevices();
            _settings.EmergencyDamageSoundDeviceId = GetSelectedEmergencyDamageSoundDeviceId();
            UpdateEmergencyDamageSoundSourcePreview();
            UpdateEmergencyDamageSoundStatus("Sprawdzono sesję audio Minecrafta i odświeżono urządzenia.", "Green");
            MarkDirty();
        }

        private void BtnEmergencyDamageSoundReloadReferences_Click(object sender, RoutedEventArgs e)
        {
            StopEmergencyDamageSoundListening(updateStatus: false);
            LoadBundledDamageSoundReferences(showStatus: true);
            UpdateEnabledStates();
        }

        private bool LoadBundledDamageSoundReferences(bool showStatus)
        {
            string soundDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "Damage");
            string[] soundFiles = Enumerable.Range(1, 4)
                .Select(index => Path.Combine(soundDirectory, $"hit{index}.ogg"))
                .ToArray();
            try
            {
                DamageSoundReferenceSet referenceSet = DamageSoundDetector.LoadReferenceFiles(soundFiles);
                _settings.EmergencyDamageSoundTemplates = referenceSet.Templates
                    .Select(template => template.ToList())
                    .ToList();
                _settings.EmergencyDamageSoundMinimumDb = referenceSet.SuggestedMinimumDb;
                UpdateEmergencyDamageSoundReferenceInfo();
                if (showStatus)
                {
                    UpdateEmergencyDamageSoundStatus(
                        $"Wczytano {referenceSet.SourceFileCount} stałe dźwięki obrażeń i {_settings.EmergencyDamageSoundTemplates.Count} profili widma.",
                        "Green");
                    MarkDirty();
                }
                return true;
            }
            catch (Exception ex)
            {
                _settings.EmergencyDamageSoundTemplates.Clear();
                UpdateEmergencyDamageSoundReferenceInfo();
                if (showStatus || TxtEmergencyDamageSoundStatus != null)
                    UpdateEmergencyDamageSoundStatus("Błąd stałych wzorców dźwięku: " + ex.Message, "Red");
                return false;
            }
        }

        private void BtnEmergencyDamageSoundTest_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.EmergencyDamageSoundTemplates.Count == 0)
            {
                UpdateEmergencyDamageSoundStatus("Nie wczytano stałych wzorców hit1–hit4.", "Red");
                return;
            }

            _emergencyDamageSoundManualTestActive = true;
            _emergencyDamageSoundManualTestUntilUtc = DateTime.UtcNow.AddSeconds(15);
            StartEmergencyDamageSoundMonitoring(manualTest: true);
        }

        private void BtnEmergencyDamageSoundStop_Click(object sender, RoutedEventArgs e)
        {
            StopEmergencyDamageSoundListening(updateStatus: false);
            UpdateEmergencyDamageSoundStatus("Test/nasłuch zatrzymany. Automatycznie wróci przy następnym uruchomieniu Kopacza.", "Orange");
            UpdateEnabledStates();
        }

        private void UpdateEmergencyDamageSoundMonitoring()
        {
            if (_isLoadingUi || _emergencyDamageSoundHandlingAlarm)
                return;

            DateTime now = DateTime.UtcNow;
            if (_emergencyDamageSoundManualTestActive && now >= _emergencyDamageSoundManualTestUntilUtc)
            {
                StopEmergencyDamageSoundListening(updateStatus: false);
                UpdateEmergencyDamageSoundStatus("Test zakończony. Nie wykryto wzorca w czasie 15 sekund.", "Green");
                UpdateEnabledStates();
            }

            bool minerActive = _kopacz533RuntimeEnabled || _kopacz633RuntimeEnabled;
            bool emergencyGuardActive = _emergencyReconnectSoundGuardActive;
            bool enabled = ChkEmergencyDamageSoundEnabled?.IsChecked == true;
            bool hasTemplates = _settings.EmergencyDamageSoundTemplates.Count > 0;
            bool shouldMonitorForMiner = ShouldMonitorEmergencyDamageSound(
                enabled,
                hasTemplates,
                minerActive || emergencyGuardActive,
                manualTest: false);
            bool shouldMonitor = ShouldMonitorEmergencyDamageSound(
                enabled,
                hasTemplates,
                minerActive || emergencyGuardActive,
                _emergencyDamageSoundManualTestActive);

            if (!shouldMonitor)
            {
                if (_damageSoundDetector.IsRunning)
                    StopEmergencyDamageSoundListening(updateStatus: false);
                _emergencyDamageSoundMonitoringForMiner = false;
                return;
            }

            if (_damageSoundDetector.IsRunning)
            {
                _emergencyDamageSoundMonitoringForMiner = shouldMonitorForMiner && !_emergencyDamageSoundManualTestActive;
                return;
            }

            StartEmergencyDamageSoundMonitoring(_emergencyDamageSoundManualTestActive);
        }

        private static bool ShouldMonitorEmergencyDamageSound(
            bool enabled,
            bool hasTemplates,
            bool minerActive,
            bool manualTest)
        {
            return manualTest || (enabled && hasTemplates && minerActive);
        }

        private void StartEmergencyDamageSoundMonitoring(bool manualTest)
        {
            try
            {
                string deviceId = ResolveEmergencyDamageSoundDeviceId(
                    out string sourceDescription,
                    out bool matchedMinecraft);
                _damageSoundDetector.StartMonitoring(
                    deviceId,
                    _settings.EmergencyDamageSoundTemplates,
                    _settings.EmergencyDamageSoundSimilarityPercent,
                    _settings.EmergencyDamageSoundMinimumDb);
                _emergencyDamageSoundMonitoringForMiner = !manualTest;
                UpdateEmergencyDamageSoundSourcePreview();
                string sourceNote = matchedMinecraft
                    ? $" Audio: {sourceDescription}."
                    : $" Użyto źródła awaryjnego: {sourceDescription}.";
                UpdateEmergencyDamageSoundStatus(
                    manualTest
                        ? "Bezpieczny test trwa 15 s — odtwórz dźwięk obrażeń. Minecraft pozostanie na serwerze." + sourceNote
                        : _settings.EmergencyDamageSoundTestMode
                            ? "Nasłuch aktywny z Kopaczem • TRYB TESTOWY." + sourceNote
                            : "Nasłuch aktywny z Kopaczem • TRYB REALNY UZBROJONY." + sourceNote,
                    manualTest || _settings.EmergencyDamageSoundTestMode ? "Green" : "Orange");
                UpdateEnabledStates();
            }
            catch (Exception ex)
            {
                _emergencyDamageSoundManualTestActive = false;
                _emergencyDamageSoundMonitoringForMiner = false;
                UpdateEmergencyDamageSoundStatus("Nie udało się uruchomić nasłuchu: " + ex.Message, "Red");
            }
        }

        private void StopEmergencyDamageSoundListening(bool updateStatus)
        {
            _emergencyDamageSoundManualTestActive = false;
            _emergencyDamageSoundManualTestUntilUtc = DateTime.MinValue;
            _emergencyDamageSoundMonitoringForMiner = false;
            _damageSoundDetector.Stop();
            if (updateStatus)
                UpdateEmergencyDamageSoundStatus("Nieaktywny. Nasłuch jest wyłączony.");
        }

        private void DamageSoundDetector_ProgressChanged(object? sender, DamageSoundProgressEventArgs e)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (DateTime.UtcNow - _emergencyDamageSoundLastAlarmAtUtc < TimeSpan.FromSeconds(2))
                    return;
                if (_damageSoundDetector.IsRunning)
                {
                    string mode = _emergencyDamageSoundManualTestActive
                        ? "Test"
                        : _settings.EmergencyDamageSoundTestMode ? "Nasłuch testowy" : "Nasłuch uzbrojony";
                    UpdateEmergencyDamageSoundStatus(
                        $"{mode} • podobieństwo {e.Similarity * 100:0}% • poziom {e.LevelDb:0} dB.",
                        _settings.EmergencyDamageSoundTestMode || _emergencyDamageSoundManualTestActive ? "Green" : "Orange");
                }
            });
        }

        private void DamageSoundDetector_CaptureFailed(object? sender, string message)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                _emergencyDamageSoundManualTestActive = false;
                _emergencyDamageSoundMonitoringForMiner = false;
                UpdateEmergencyDamageSoundStatus(message, "Red");
                UpdateEnabledStates();
            });
        }

        private void DamageSoundDetector_DamageDetected(object? sender, DamageSoundDetectedEventArgs e)
        {
            _ = Dispatcher.BeginInvoke(new Action(async () => await HandleEmergencyDamageSoundDetectedAsync(e)));
        }

        private async Task HandleEmergencyDamageSoundDetectedAsync(DamageSoundDetectedEventArgs e)
        {
            if (_emergencyDamageSoundHandlingAlarm)
                return;

            bool minerActive = _kopacz533RuntimeEnabled || _kopacz633RuntimeEnabled;
            bool emergencyGuardActive = _emergencyReconnectSoundGuardActive;
            bool safeTest = _emergencyDamageSoundManualTestActive || _settings.EmergencyDamageSoundTestMode;
            string measurement = $"podobieństwo {e.Similarity * 100:0}%, poziom {e.LevelDb:0} dB";
            _emergencyDamageSoundLastAlarmAtUtc = DateTime.UtcNow;
            if (safeTest)
            {
                bool wasManualTest = _emergencyDamageSoundManualTestActive;
                if (minerActive)
                    RecordEmergencyDamageSoundEvent(measurement, testMode: true);
                if (wasManualTest)
                {
                    StopEmergencyDamageSoundListening(updateStatus: false);
                    UpdateEnabledStates();
                }
                UpdateEmergencyDamageSoundStatus(
                    $"WYKRYTO DŹWIĘK OBRAŻEŃ ({measurement}). Tryb testowy — bez reakcji.",
                    "Red");
                UpdateStatusBar("Wykryto testowy alarm dźwięku obrażeń — bez wychodzenia z serwera.", "Orange");
                return;
            }

            if ((!minerActive && !emergencyGuardActive)
                || ChkEmergencyDamageSoundEnabled?.IsChecked != true)
                return;

            _emergencyDamageSoundHandlingAlarm = true;
            IntPtr minecraftWindow = _targetGameWindowHandle;
            bool minecraftGuiAlreadyOpen = IsMinecraftGuiLikelyOpenForEmergency();
            bool useEmergencyReconnect = ChkEmergencyDamageSoundEnabled?.IsChecked == true
                && _settings.EmergencyReconnectEnabled;
            try
            {
                if (useEmergencyReconnect)
                {
                    if (!_emergencyReconnectActive)
                    {
                        _emergencyReconnectResumeKopacz533 = _kopacz533RuntimeEnabled;
                        _emergencyReconnectResumeKopacz633 = _kopacz633RuntimeEnabled;
                        _autoReconnectLogMiningRunId = _kopacz533RuntimeEnabled
                            ? _kopacz533MiningRunId
                            : _kopacz633RuntimeEnabled
                                ? _kopacz633MiningRunId
                                : string.Empty;
                    }

                    if (_autoReconnectStage != AutoReconnectStage.None)
                        ResetAutoReconnectStageForEmergencyRedetection();

                    _emergencyReconnectActive = true;
                    _emergencyReconnectSoundGuardActive = true;
                }

                RecordEmergencyDamageSoundEvent(measurement, testMode: false);
                if (_kopacz533RuntimeEnabled)
                    StopMiningForEmergency(InventoryCleanupOwner.Kopacz533);
                if (_kopacz633RuntimeEnabled)
                    StopMiningForEmergency(InventoryCleanupOwner.Kopacz633);
                if (!useEmergencyReconnect)
                    StopEmergencyDamageSoundListening(updateStatus: false);

                (bool disconnected, string result) = await TryDisconnectMinecraftAfterEmergencyAsync(
                    minecraftWindow,
                    minecraftGuiAlreadyOpen);
                if (disconnected && useEmergencyReconnect)
                    BeginEmergencyReconnectAfterDisconnect(DateTime.UtcNow);
                else if (!disconnected && useEmergencyReconnect)
                {
                    RecordAutomationLogEvent(
                        MiningLogEventTypes.EmergencyProtectionFinished,
                        MiningLogStatuses.Aborted,
                        $"Nie udało się wyjść z serwera po alarmie: {result}. Reconnect nie został uruchomiony.");
                    ClearEmergencyReconnectRuntimeState(stopSoundMonitoring: true);
                }
                UpdateEmergencyDamageSoundStatus(
                    disconnected
                        ? useEmergencyReconnect
                            ? $"ALARM ({measurement}) — kliknięto Disconnect; uruchomiono awaryjny reconnect."
                            : $"ALARM ({measurement}) — Kopacz zatrzymany, kliknięto Disconnect."
                        : $"ALARM ({measurement}) — Kopacz zatrzymany, ale wyjście z serwera nie powiodło się: {result}",
                    "Red");
                UpdateStatusBar(
                    disconnected
                        ? useEmergencyReconnect
                            ? "Awaryjne wyjście wykonane — trwa odliczanie do reconnectu."
                            : "Awaryjne wyjście: wykryto obrażenia i kliknięto ESC → Disconnect."
                        : "Awaryjne wyjście: zatrzymano Kopacza, lecz nie udało się kliknąć Disconnect.",
                    "Red");
            }
            finally
            {
                _emergencyDamageSoundHandlingAlarm = false;
                RefreshTopTiles();
                UpdateEnabledStates();
            }
        }

        private bool IsMinecraftGuiLikelyOpenForEmergency()
        {
            bool inventoryOpen = _inventoryCleanupScanInProgress
                || _inventoryCleanupStage is InventoryCleanupStage.WaitForInventory
                    or InventoryCleanupStage.MoveToSlot
                    or InventoryCleanupStage.PressDropModifier
                    or InventoryCleanupStage.PressDropKey
                    or InventoryCleanupStage.ReleaseDropKeys
                    or InventoryCleanupStage.CloseInventory;
            bool cleanupChatOpen = _inventoryCleanupStage is InventoryCleanupStage.TypeCobbleXCommand
                or InventoryCleanupStage.SubmitCobbleXCommand;
            bool minerChatOpen = _kopacz533CommandStage is Kopacz533CommandStage.TypeCommand
                    or Kopacz533CommandStage.SubmitCommand
                || _kopacz633CommandStage is Kopacz633CommandStage.TypeCommand
                    or Kopacz633CommandStage.SubmitCommand;
            bool reconnectInventoryOpen = _autoReconnectStage is AutoReconnectStage.HealthVerifyInventory
                or AutoReconnectStage.VerifyAfterTeleport
                or AutoReconnectStage.EmergencyVerifyInventory;
            return inventoryOpen || cleanupChatOpen || minerChatOpen || reconnectInventoryOpen;
        }

        private async Task<(bool Success, string Result)> TryDisconnectMinecraftAfterEmergencyAsync(
            IntPtr minecraftWindow,
            bool closeExistingGuiFirst)
        {
            if (minecraftWindow == IntPtr.Zero)
                return (false, "brak uchwytu okna Minecrafta");
            if (GetForegroundWindow() != minecraftWindow)
                return (false, "Minecraft nie był aktywnym oknem");

            if (closeExistingGuiFirst)
            {
                SendKeyTap(VK_ESCAPE);
                await Task.Delay(120);
                if (GetForegroundWindow() != minecraftWindow)
                    return (false, "Minecraft utracił fokus po zamknięciu poprzedniego GUI");
            }

            SendKeyTap(VK_ESCAPE);
            await Task.Delay(180);
            if (GetForegroundWindow() != minecraftWindow)
                return (false, "Minecraft utracił fokus przed kliknięciem Disconnect");
            if (!TryClickMinecraftDisconnectButton(minecraftWindow))
                return (false, "nie udało się wyznaczyć położenia przycisku Disconnect");

            return (true, "kliknięto Disconnect");
        }

        private bool TryClickMinecraftDisconnectButton(IntPtr minecraftWindow)
        {
            if (!TryGetWindowClientRectOnScreen(minecraftWindow, out RECT rect))
                return false;

            int clientWidth = rect.Right - rect.Left;
            int clientHeight = rect.Bottom - rect.Top;
            if (clientWidth <= 0 || clientHeight <= 0)
                return false;

            int x = rect.Left + clientWidth / 2;
            int y = rect.Top + CalculateMinecraftDisconnectButtonClientY(clientWidth, clientHeight);
            if (y < rect.Top || y >= rect.Bottom)
                return false;

            if (!NativeInput.SetCursorPosition(x, y))
                return false;
            SendMouseClick(leftButton: true, holdPulseMode: false);
            return true;
        }

        private static int CalculateMinecraftDisconnectButtonClientY(int clientWidth, int clientHeight)
        {
            if (clientWidth <= 0 || clientHeight <= 0)
                return 0;

            const int configuredGuiScale = 3; // wymagane GUI Scale: Large
            int actualScale = 1;
            while (actualScale < configuredGuiScale
                && clientWidth / (actualScale + 1) >= 320
                && clientHeight / (actualScale + 1) >= 240)
            {
                actualScale++;
            }

            int scaledHeight = (int)Math.Ceiling(clientHeight / (double)actualScale);
            // Minecraft 1.8.8: y = scaledHeight / 4 + 120 - 16,
            // button height = 20, so its centre is scaledHeight / 4 + 114.
            int buttonCenterGuiY = scaledHeight / 4 + 114;
            int clientY = (int)Math.Round(buttonCenterGuiY * clientHeight / (double)scaledHeight);
            return Math.Clamp(clientY, 0, clientHeight - 1);
        }

        private void RecordEmergencyDamageSoundEvent(string measurement, bool testMode)
        {
            InventoryCleanupOwner owner = _kopacz533RuntimeEnabled || _emergencyReconnectResumeKopacz533
                ? InventoryCleanupOwner.Kopacz533
                : InventoryCleanupOwner.Kopacz633;
            string ownerLabel = GetInventoryCleanupOwnerLabel(owner);
            string miningRunId = _emergencyReconnectActive && !string.IsNullOrWhiteSpace(_autoReconnectLogMiningRunId)
                ? _autoReconnectLogMiningRunId
                : GetMiningRunId(owner);
            _ = _miningLogService.RecordAutomationEvent(
                miningRunId,
                ownerLabel,
                MiningLogEventTypes.EmergencyDamageSoundDetected,
                MiningLogStatuses.Completed,
                testMode
                    ? $"Test: rozpoznano dźwięk obrażeń ({measurement}); nie wykonano reakcji."
                    : $"Rozpoznano dźwięk obrażeń ({measurement}); zatrzymano automat i uruchomiono ESC → Disconnect.",
                out _);
            RefreshMiningLogsSummary();
        }

        private void StopMiningForEmergency(InventoryCleanupOwner owner)
        {
            string label = GetInventoryCleanupOwnerLabel(owner);
            ReleaseMiningInputs(owner);
            if (IsReconnectForMiner(owner))
                StopAutoReconnect($"{label}: awaryjne zatrzymanie po dźwięku obrażeń.", resumeMining: false, warning: true);
            if (_inventoryCleanupOwner == owner && _inventoryCleanupStage != InventoryCleanupStage.None)
            {
                RecordAbortedInventoryCleanup("Auto EQ przerwane przez alarm dźwięku obrażeń.");
                _inventoryCleanupLastResult = "Przerwano przez alarm dźwięku obrażeń";
                _inventoryCleanupLastResultWarning = true;
            }
            StopMiningRuntime(owner);
            EndMiningLogRun(owner, "Kopanie przerwane przez awaryjny alarm dźwięku obrażeń.", MiningLogStatuses.Interrupted);
        }

        private void ChkTestFastUpExitEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkTestFastUpExitEnabled.IsChecked != true)
            {
                _testFastUpExitRuntimeEnabled = false;
                ResetTestFastUpExitRuntimeState();
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkTestAutoFishingEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkTestAutoFishingEnabled.IsChecked != true)
            {
                _testAutoFishingRuntimeEnabled = false;
                ResetTestAutoFishingRuntimeState();
            }

            UpdateEnabledStates();
            RefreshTopTiles();
            UpdateTestAutoFishingStatusLabel();
            MarkDirty();
        }

        private void CbTestFastUpExitConfig_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ReferenceEquals(sender, CbTestFastUpExitPickaxeType))
            {
                string selectedType = GetSelectedTestFastUpPickaxeType();
                ApplyFastUpLookSliderForPickaxe(selectedType);
                ApplyFastUpBreakSliderForPickaxe(selectedType);
            }

            MarkDirty();
        }

        private void ChkTestFastUpExitTimingEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            MarkDirty();
        }

        private void SlTestFastUpExitLookMs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int lookMs = Math.Clamp((int)Math.Round(e.NewValue), FastUpLookDurationMinMs, FastUpLookDurationMaxMs);
            UpdateTestFastUpExitLookDurationLabel(lookMs);

            if (_isLoadingUi)
                return;

            SetFastUpLookDurationForPickaxe(GetSelectedTestFastUpPickaxeType(), lookMs);
            MarkDirty();
        }

        private void SlTestFastUpExitBreakMs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int breakMs = NormalizeFastUpBreakDurationMs((int)Math.Round(e.NewValue));
            UpdateTestFastUpExitBreakDurationLabel(breakMs);

            if (_isLoadingUi)
                return;

            SetFastUpBreakDurationForPickaxe(GetSelectedTestFastUpPickaxeType(), breakMs);
            MarkDirty();
        }

        private void SlTestFastUpExitPlaceMs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int placeMs = NormalizeFastUpPlaceAfterJumpMs((int)Math.Round(e.NewValue));
            UpdateTestFastUpExitPlaceDurationLabel(placeMs);

            if (_isLoadingUi)
                return;

            _settings.TestFastUpExitPlaceAfterJumpMs = placeMs;
            MarkDirty();
        }

        private void ChkPauseWhenCursorVisible_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkPauseWhenCursorVisible.IsChecked != true)
                SetCursorPauseState(false);

            UpdateEnabledStates();
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkKopacz533Enabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz633Enabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkInventoryCleanupEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            if (ChkInventoryCleanupEnabled.IsChecked != true)
            {
                ResetInventoryCleanupState(scheduleNext: false);
                UpdateInventoryCleanupStatus("Automatyczne wyrzucanie jest wyłączone.", "Default");
            }
            else
            {
                _nextInventoryCleanupAtUtc = DateTime.UtcNow.AddSeconds(GetConfiguredInventoryCleanupIntervalSeconds());
                UpdateInventoryCleanupStatus("Gotowe. Czyszczenie uruchomi się podczas pracy kopacza.", "Green");
            }

            UpdateEnabledStates();
            RefreshTopTiles();
            MarkDirty();
        }

        private void ChkCobbleXEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            UpdateEnabledStates();
            RefreshTopTiles();
            RefreshOverlayHud(DateTime.UtcNow);
            MarkDirty();
        }

        private void ChkInventoryCleanupEatAfterCleanup_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingUi)
                return;

            RefreshOverlayHud(DateTime.UtcNow);
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
                _targetGameWindowHandle = IntPtr.Zero;
                _ = TryResolveTargetWindow(allowPendingSelection: false, out _targetGameWindowHandle);
                UpdateEmergencyDamageSoundSourcePreview();
                _settingsService.Save(_settings);

                _pendingChanges = false;
                _dirtyTimer.Stop();
                TxtSettingsSaved.Text = "✓ Tak";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(56, 214, 180));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(56, 214, 180));

                _isMinecraftFocused = CheckGameFocus();
                if (selectedProcess != null && LooksLikeLauncherWindow(selectedProcess))
                {
                    UpdateStatusBar("Zapisano launcher. Bindy działają w wybranym oknie; wybierz okno właściwej gry Minecraft.", "Orange");
                }
                else
                {
                    UpdateStatusBar("Program gry zapisany", "Green");
                }
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
                _trayIcon.ContextMenuStrip?.Dispose();
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _dirtyTimer.Stop();
            _focusTimer.Stop();
            _macroTimer.Stop();
            _autoReconnectTimer.Stop();
            _transientStatusTimer.Stop();
            _bindyHudClearTimer.Stop();
            _autoClickScheduler.Dispose();
            _macroDiagnosticsService.Dispose();
            _damageSoundDetector.Dispose();

            // Release every injected state before removing the physical-mouse hook.
            // This also covers closing the app while HOLD PPM is active.
            ResetHoldLeftToggleState(clearToggleEnabled: true);
            SetKopacz533MiningHold(false);
            SetKopacz633AttackHold(false);
            SetKopacz633StrafeDirection(Kopacz633StrafeDirection.None);
            SetAutoLeftDabHold(false);
            ResetInventoryCleanupState(scheduleNext: false);
            EndMiningLogRun(
                InventoryCleanupOwner.Kopacz533,
                "Sesja zakończona wraz z zamknięciem aplikacji.",
                MiningLogStatuses.Interrupted);
            EndMiningLogRun(
                InventoryCleanupOwner.Kopacz633,
                "Sesja zakończona wraz z zamknięciem aplikacji.",
                MiningLogStatuses.Interrupted);
            _testFastUpExitRuntimeEnabled = false;
            ResetTestFastUpExitRuntimeState();
            _testAutoFishingRuntimeEnabled = false;
            ResetTestAutoFishingRuntimeState();
            ResetAutoArmorRuntimeState();
            _autoWaterCalibrationPending = false;
            _autoWaterRecognitionTestPending = false;
            ResetAutoWaterRuntimeState();
            ResetBindyRuntimeState();
            StopMouseHook();

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
            base.OnClosed(e);
        }
    }
}
