using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using MinecraftHelper.Models;
using MinecraftHelper.Services;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static int Main()
    {
        try
        {
            foreach (string mode in new[] { "Toggle", "Hold", "Combo", "PhysicalToggle" })
            foreach (string action in new[] { "AutoBlock", "Snowball", "Pearl", "Rod" })
            {
                using var window = new MinecraftHelper.MainWindow();
                window.VerifyActionContinuity(mode, action);
            }

            foreach (string action in new[] { "AutoBlock", "Snowball", "Pearl", "Rod" })
            {
                using var window = new MinecraftHelper.MainWindow();
                window.VerifyInputFailure(action);
            }

            using (var window = new MinecraftHelper.MainWindow())
                window.VerifyCancellationGuards();

            Console.WriteLine($"PASS: {_checks} checks; production PVP stages and scheduler tested without desktop input or user data.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL: " + description);
        _checks++;
    }
}

namespace MinecraftHelper
{
    internal enum OverlayHudTone { Warning }
    internal readonly record struct OverlayHudEntry(string Title, string Body, OverlayHudTone Tone, bool Emphasize);

    // Only the UI/input host is substituted. Both the action state machine and
    // click scheduler are linked unchanged from production. No Window is opened.
    public partial class MainWindow : IDisposable
    {
        private const int VkLeftButton = 1;
        private readonly PvpSettings _settings = new();
        private readonly LightweightClickScheduler _autoClickScheduler = new(_ => true, () => true);
        private readonly MouseState _physicalMouseTracker = new();
        private readonly IntPtr _targetGameWindowHandle = new(1);
        private bool _isLoadingUi = false;
        private bool _autoLeftRuntimeEnabled = true;
        private bool _dabHeld;
        private bool IsAutoArmorRunning => false;
        private AutoWaterStage _autoWaterStage = AutoWaterStage.None;
        private BindyStage _bindyStage = BindyStage.None;
        private enum AutoWaterStage { None }
        private enum BindyStage { None }
        private sealed class MouseState { public bool Down = true; public bool IsButtonDown(int _) => Down; }

        private readonly TextBox TxtPvpAutoBlockBind = new();
        private readonly TextBox TxtPvpSnowballBind = new();
        private readonly TextBox TxtPvpPearlBind = new();
        private readonly TextBox TxtPvpRodBind = new();
        private readonly TextBox TxtPvpRodHoldMs = new() { Text = "240" };
        private readonly TextBox TxtPvpQuickActionStatus = new();
        private readonly TextBox TxtAutoLeftKey = new() { Text = "R" };
        private readonly TextBox TxtAutoLeftMinCps = new() { Text = "15" };
        private readonly TextBox TxtAutoLeftMaxCps = new() { Text = "15" };
        private readonly CheckBox ChkAutoLeftEnabled = new() { IsChecked = true };
        private readonly CheckBox ChkAutoLeftDabMode = new() { IsChecked = true };
        private readonly ComboBox CbAutoLeftMode = new();
        private readonly ComboBox CbPvpSwordSlot = Slot(1);
        private readonly ComboBox CbPvpBlockSlot = Slot(5);
        private readonly ComboBox CbPvpSnowballSlot = Slot(2);
        private readonly ComboBox CbPvpPearlSlot = Slot(3);
        private readonly ComboBox CbPvpRodSlot = Slot(4);

        public MainWindow()
        {
            // Drive stages with a synthetic clock, without asynchronous native
            // key/focus reads. Channel state and generation use the real scheduler.
            foreach (string name in new[] { "_left", "_right" })
            {
                object channel = Channel(name);
                var timerProperty = channel.GetType().GetProperty("Timer")!;
                ((System.Threading.Timer?)timerProperty.GetValue(channel))?.Dispose();
                timerProperty.SetValue(channel, null);
            }
        }

        internal void VerifyActionContinuity(string mode, string action)
        {
            NativeInput.Reset();
            CbAutoLeftMode.Items.Add(new ComboBoxItem { Tag = mode });
            CbAutoLeftMode.SelectedIndex = 0;
            int holdKey = mode == "Hold" ? 0x52 : 0;
            int pointerKey = UsesPhysicalPointerGate(mode) ? VkLeftButton : 0;
            _autoClickScheduler.Update(true, 15, 15, true, 40, 40, holdKey, pointerKey, 0, 0, _targetGameWindowHandle);
            int generation = Value<int>("_left", "Generation");
            long deadline = Value<long>("_left", "NextTimestamp");
            DateTime start = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
            var kind = Enum.Parse<PvpQuickActionKind>(action);
            StartPvpQuickAction(kind, start);
            AssertContinuous(generation, deadline, mode + " " + action);

            Tick(0);
            Tick(39);
            Program.Check(NativeInput.Clicks == 0 && !NativeInput.RightHeld, "PPM waits for selected slot");
            Tick(40);
            if (kind == PvpQuickActionKind.Rod)
            {
                Program.Check(NativeInput.RightHeld && NativeInput.Clicks == 0, "rod preserves configured PPM hold");
                Tick(279);
                Program.Check(NativeInput.RightHeld, "rod is not released before 240 ms");
                Tick(280);
                Program.Check(!NativeInput.RightHeld, "rod releases PPM after configured hold");
                Tick(329);
                Program.Check(IsPvpQuickActionRunning, "sword return respects item release delay");
                Tick(330);
            }
            else
            {
                Program.Check(NativeInput.Clicks == 1 && NativeInput.RightDowns == 0 && !NativeInput.RightHeld,
                    "standard item uses one complete click, never a held PPM");
                Tick(89);
                Program.Check(IsPvpQuickActionRunning, "standard item return waits 50 ms");
                Tick(90);
            }

            Program.Check(!IsPvpQuickActionRunning, "action completes");
            Program.Check(NativeInput.Slots.SequenceEqual(new[] { GetPvpQuickActionItemSlot(kind), 1 }),
                "one item selection and one return to sword");
            Program.Check(_autoLeftRuntimeEnabled && _dabHeld, "LPM runtime and DAB stay enabled after completion");

            void Tick(int milliseconds)
            {
                KeepAutoLeftClickerRunningDuringPvpAction(blockedByCursor: false);
                RunPvpQuickAction(start.AddMilliseconds(milliseconds));
                AssertContinuous(generation, deadline, mode + " " + action);
            }
        }

        internal void VerifyInputFailure(string action)
        {
            NativeInput.Reset();
            _autoClickScheduler.Update(true, 15, 15, false, 1, 1, 0, 0, 0, 0, _targetGameWindowHandle);
            int generation = Value<int>("_left", "Generation");
            DateTime start = DateTime.UtcNow;
            StartPvpQuickAction(Enum.Parse<PvpQuickActionKind>(action), start);
            RunPvpQuickAction(start);
            NativeInput.Succeed = false;
            RunPvpQuickAction(start.AddMilliseconds(40));
            Program.Check(!IsPvpQuickActionRunning && !NativeInput.RightHeld, "failed PPM does not leave action or use button active");
            Program.Check(_autoLeftRuntimeEnabled && Value<bool>("_left", "Enabled")
                && Value<int>("_left", "Generation") == generation, "failed PVP action does not reset LPM");
        }

        internal void VerifyCancellationGuards()
        {
            NativeInput.Reset();
            DateTime start = DateTime.UtcNow;
            _autoClickScheduler.Update(true, 15, 15, false, 1, 1, 0, 0, 0, 0, _targetGameWindowHandle);
            StartPvpQuickAction(PvpQuickActionKind.Rod, start);
            RunPvpQuickAction(start);
            RunPvpQuickAction(start.AddMilliseconds(40));
            HandlePvpQuickActionBinds(start.AddMilliseconds(41), cursorVisible: true);
            KeepAutoLeftClickerRunningDuringPvpAction(blockedByCursor: true);
            Program.Check(!IsPvpQuickActionRunning && !NativeInput.RightHeld, "opening GUI cancels action and releases PPM");
            Program.Check(!Value<bool>("_left", "Enabled"), "GUI cursor still blocks LPM");

            StartPvpQuickAction(PvpQuickActionKind.Snowball, start);
            _autoLeftRuntimeEnabled = false;
            KeepAutoLeftClickerRunningDuringPvpAction(blockedByCursor: false);
            HandlePvpQuickActionBinds(start, cursorVisible: false);
            Program.Check(!IsPvpQuickActionRunning && !Value<bool>("_left", "Enabled"), "user STOP cancels action without re-enabling LPM");
        }

        private void AssertContinuous(int generation, long deadline, string description)
        {
            Program.Check(_autoLeftRuntimeEnabled && Value<bool>("_left", "Enabled"), description + ": LPM stays enabled");
            Program.Check(Value<int>("_left", "Generation") == generation, description + ": LPM is never restarted");
            Program.Check(Value<long>("_left", "NextTimestamp") == deadline, description + ": planned click deadline is preserved");
            Program.Check(!Value<bool>("_right", "Enabled"), description + ": AUTO PPM stays paused");
        }

        private object Channel(string field) => typeof(LightweightClickScheduler)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_autoClickScheduler)!;
        private T Value<T>(string field, string property) => (T)Channel(field).GetType()
            .GetProperty(property)!.GetValue(Channel(field))!;
        private static ComboBox Slot(int number)
        {
            var combo = new ComboBox();
            for (int slot = 1; slot <= 9; slot++) combo.Items.Add(slot);
            combo.SelectedIndex = number - 1;
            return combo;
        }

        private static string GetSelectedMode(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Toggle";
        private static bool UsesPhysicalPointerGate(string mode) => mode is "Combo" or "PhysicalToggle";
        private static int ReadCps(string text, int fallback) => int.TryParse(text, out int cps) ? Math.Clamp(cps, 1, 100) : fallback;
        private static bool TryGetVirtualKey(string text, out int key) { key = 0x52; return text == "R"; }
        private static bool IsBindPressed(string text, ref bool wasDown) { wasDown = false; return false; }
        private static bool IsConfiguredBindKeyDown(string text) => false;
        private static bool IsInventoryCursorVisible() => false;
        private static IntPtr GetForegroundWindow() => new(1);
        private static int GetSlotVirtualKey(int slot) => 0x30 + slot;
        private static void SendKeyTap(int key) => NativeInput.Slots.Add(key - 0x30);
        private void SetAutoLeftDabHold(bool down) => _dabHeld = down;
        private static void MarkDirty() { }
        private static void UpdateStatusBar(string message, string color) { }
        private static void RefreshOverlayHud(DateTime now) { }
        public void Dispose() => _autoClickScheduler.Dispose();
    }
}

namespace MinecraftHelper.Services
{
    internal static class NativeInput
    {
        internal static bool Succeed = true;
        internal static bool RightHeld;
        internal static int RightDowns;
        internal static int Clicks;
        internal static readonly List<int> Slots = new();

        internal static bool SendMouseButton(bool leftButton, bool down)
        {
            Program.Check(!leftButton, "PVP item action never injects LPM presses/releases");
            if (down) RightDowns++;
            RightHeld = down && Succeed;
            return Succeed;
        }

        internal static bool SendMouseClick(bool leftButton, bool holdPulseMode)
        {
            Program.Check(!leftButton && !holdPulseMode, "standard PVP action sends one normal PPM click");
            if (Succeed) Clicks++;
            return Succeed;
        }

        internal static void Reset()
        {
            Succeed = true;
            RightHeld = false;
            RightDowns = 0;
            Clicks = 0;
            Slots.Clear();
        }
    }
}
