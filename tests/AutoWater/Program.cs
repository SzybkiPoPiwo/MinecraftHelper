using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MinecraftHelper.Models;
using Color = System.Drawing.Color;

internal static class Program
{
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            foreach (int tick in new[] { 5, 16, 33 })
            foreach (int interval in new[] { 25, 40, 50, 65, 150 })
            foreach (int iconDelay in new[] { 0, 5, 10, 20 })
            foreach (int pickupDelay in new[] { 100, 340, 1500 })
                new MinecraftHelper.MainWindow().VerifySequence(interval, tick, iconDelay, pickupDelay);
            new MinecraftHelper.MainWindow().VerifySequence(65, 16, 50, 340);
            MinecraftHelper.MainWindow.VerifyGuardsAndFailures();
            MinecraftHelper.MainWindow.VerifyCalibration();
            MinecraftHelper.MainWindow.VerifyRecognitionFocus();
            MinecraftHelper.MainWindow.VerifySettings();
            MinecraftHelper.MainWindow.VerifyBundledBucketIcons(FindRoot());
            CheckHostIntegration();
            Console.WriteLine($"PASS: {_passed} checks; production AutoWater stages, calibration and delayed icons. No GUI, screen capture, native input or user data touched.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckHostIntegration()
    {
        string root = FindRoot();
        string host = File.ReadAllText(Path.Combine(root, "MinecraftHelper.Pvp", "MainWindow.xaml.cs"));
        int core = host.IndexOf("private void RuntimeTimerTickCore()", StringComparison.Ordinal);
        int exclusive = host.IndexOf("bool cancelWater = IsBindPressed", core, StringComparison.Ordinal);
        int clicker = host.IndexOf("HandleClickerRuntime(blockedByCursor);", core, StringComparison.Ordinal);
        Check(exclusive > core && exclusive < clicker, "active water branch runs before other gameplay binds/clickers");
        Check(host[exclusive..clicker].Contains("SynchronizeBindStatesWhileGameIsInactive();", StringComparison.Ordinal),
            "keys pressed during water cannot trigger deferred other binds");
        string view = File.ReadAllText(Path.Combine(root, "MinecraftHelper.Pvp", "MainWindow.xaml"));
        Check(view.Contains("Content=\"Kalibruj puste\" Tag=\"Empty\"", StringComparison.Ordinal)
            && view.Contains("Content=\"Kalibruj wodę\" Tag=\"Full\"", StringComparison.Ordinal), "GUI exposes both calibration steps");
    }

    private static string FindRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MinecraftHelper.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        _passed++;
    }
}

namespace MinecraftHelper
{
    internal enum OverlayHudTone { Active, Warning }
    internal readonly record struct OverlayHudEntry(string Title, string Body, OverlayHudTone Tone, bool Emphasize);

    // Real feature code is source-linked. Only WPF host, GDI capture and native
    // input boundaries are replaced; no application Window is ever constructed.
    public partial class MainWindow
    {
        private static readonly DateTime Epoch = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        private const int VK_LCONTROL = 0xA2;
        private PvpSettings _settings = new();
        private readonly IntPtr _targetGameWindowHandle = new(1);
        private bool _isLoadingUi = false;
        private bool _suppressBindToggleUntilRelease;
        private bool _testAutoFishingRuntimeEnabled = true;
        private bool GuiVisible;
        private bool TargetFocused = true;
        private bool LeftControlDown;
        private bool WrongItem;
        private bool BucketEmpty;
        private bool VisibleBucketEmpty;
        private bool PendingIconEmpty;
        private bool IsAutoArmorRunning;
        private bool AcceptKeys = true;
        private bool AcceptMouse = true;
        private bool IgnorePickup;
        private bool IgnorePlacement;
        private bool LoseFocusDuringCapture;
        private Color? OverridePatchColor;
        private Func<int, int, bool, Color>? PackedPixel;
        private DateTime PendingIconAt = DateTime.MaxValue;
        private DateTime Now = Epoch;
        private int SelectedSlot = 1;
        private int IconDelayMs;
        private int Captures;
        private int SavedChanges;
        private int ForeignInputs;
        private bool FullSeenBeforeLastClick;
        private POINT Cursor = new() { X = 980, Y = 770 };
        private RECT Client = new() { Left = 100, Top = 100, Right = 1380, Bottom = 820 };
        private enum BindTarget { AutoWater }
        private struct POINT { public int X; public int Y; }
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private readonly CheckBox ChkAutoWaterEnabled = new() { IsChecked = true };
        private readonly TextBox TxtAutoWaterBind = new() { Text = "N" };
        private readonly ComboBox CbAutoWaterWaterSlot = Slot(9);
        private readonly ComboBox CbAutoWaterReturnSlot = Slot(1);
        private readonly TextBox TxtAutoWaterPlaceIntervalMs = new() { Text = "40" };
        private readonly TextBox TxtAutoWaterPickupDelayMs = new() { Text = "340" };
        private readonly Slider SlAutoWaterSimilarity = new() { Minimum = 70, Maximum = 99, Value = 84 };
        private readonly TextBlock TxtAutoWaterSimilarityValue = new();
        private readonly TextBlock TxtAutoWaterStatus = new();
        private readonly StackPanel PanelAutoWaterExpandableContent = new();
        private readonly StackPanel PanelAutoWaterContent = new();
        private readonly List<(int Time, string Stage)> Clicks = new();
        private readonly List<int> Slots = new();
        private readonly List<(int Key, bool Down)> KeyEvents = new();

        public MainWindow()
        {
            System.Drawing.FakeScreen.Current = this;
            _autoWaterTemplateRgb = Enumerable.Repeat((byte)255, 9 * 9 * 3).ToArray();
            _autoWaterFullTemplateRgb = new byte[9 * 9 * 3];
            _settings.AutoWaterEmptyBucketTemplateBase64 = Convert.ToBase64String(_autoWaterTemplateRgb);
            _settings.AutoWaterFullBucketTemplateBase64 = Convert.ToBase64String(_autoWaterFullTemplateRgb);
            _settings.AutoWaterSampleOffsetX = 240;
            _settings.AutoWaterSampleOffsetFromBottom = 50;
            _settings.AutoWaterCalibrationClientWidth = 1280;
            _settings.AutoWaterCalibrationClientHeight = 720;
            _settings.AutoWaterCalibrationSlot = 9;
        }

        internal void VerifySequence(int interval, int tick, int iconDelay, int pickupDelay)
        {
            IconDelayMs = iconDelay;
            TxtAutoWaterPlaceIntervalMs.Text = interval.ToString();
            TxtAutoWaterPickupDelayMs.Text = pickupDelay.ToString();
            Start();
            int observedEmptyAt = -1;
            for (int ms = 0; ms <= 10100 && _autoWaterStage != AutoWaterStage.None; ms += tick)
            {
                Tick(ms);
                if (observedEmptyAt < 0 && _autoWaterStage == AutoWaterStage.ConfirmPlacement) observedEmptyAt = ms;
            }
            Program.Check(TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal),
                $"completion interval={interval}, tick={tick}, render={iconDelay}, pickup={pickupDelay}: {TxtAutoWaterStatus.Text}");
            Program.Check(_autoWaterStage == AutoWaterStage.None && !BucketEmpty, "finished with water in the bucket");
            Program.Check(Slots.SequenceEqual(new[] { 9, 1 }), "selected water then return slot once");
            Program.Check(Clicks.Count == 2 && Clicks[0].Stage == "PlaceWater" && Clicks[1].Stage == "WaitBeforePickup",
                "exactly one placement and one pickup; no placement PPM during confirmation");
            Program.Check(observedEmptyAt >= 0 && Clicks[1].Time - observedEmptyAt >= pickupDelay,
                "full configured pickup delay after the first empty observation");
            Program.Check(FullSeenBeforeLastClick, "placement click was preceded by a fresh full-bucket read");
            Program.Check(!_testAutoFishingRuntimeEnabled, "unrelated legacy fishing runtime remains stopped");
        }

        internal static void VerifyGuardsAndFailures()
        {
            foreach (string reason in new[] { "MissingEmpty", "MissingFull", "Legacy", "Slot", "Size", "Gui", "Focus", "Armor" })
            {
                var w = new MainWindow();
                switch (reason)
                {
                    case "MissingEmpty": w._autoWaterTemplateRgb = null; break;
                    case "MissingFull": w._autoWaterFullTemplateRgb = null; break;
                    case "Legacy": w._settings.AutoWaterCalibrationClientWidth = 0; break;
                    case "Slot": w.CbAutoWaterWaterSlot.SelectedIndex = 7; break;
                    case "Size": w.Client.Right++; break;
                    case "Gui": w.GuiVisible = true; break;
                    case "Focus": w.TargetFocused = false; break;
                    case "Armor": w.IsAutoArmorRunning = true; break;
                }
                Program.Check(!w.TryStartAutoWater(Epoch) && w.Clicks.Count == 0 && w.Slots.Count == 0,
                    "start rejected without any input: " + reason);
            }
            foreach (bool empty in new[] { false, true })
            {
                var wrong = new MainWindow { WrongItem = !empty, BucketEmpty = empty, VisibleBucketEmpty = empty };
                wrong.Start();
                wrong.DriveUntilStop();
                Program.Check(wrong.Clicks.Count == 0 && !wrong.TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal),
                    empty ? "initial empty bucket rejected" : "arbitrary item rejected before PPM");
            }
            foreach (string stage in Enum.GetNames<AutoWaterStage>().Where(s => s != "None"))
            foreach (bool focusLoss in new[] { true, false })
            {
                var w = new MainWindow();
                w.Start();
                w._autoWaterStage = Enum.Parse<AutoWaterStage>(stage);
                w.TargetFocused = !focusLoss;
                w.GuiVisible = !focusLoss;
                w.Tick(10);
                Program.Check(w._autoWaterStage == AutoWaterStage.None && w.Clicks.Count == 0 && w.Slots.Count == 0 && w.ForeignInputs == 0,
                    "all stages cancel without input after focus/GUI change: " + stage);
            }
            var cancel = new MainWindow();
            cancel.Start();
            cancel.Tick(0);
            cancel.CancelAutoWater("Cancelled", System.Windows.Media.Brushes.Orange, restoreSlot: true);
            cancel.Tick(5000);
            Program.Check(cancel.Slots.SequenceEqual(new[] { 9, 1 }) && cancel.Clicks.Count == 0, "explicit cancellation restores slot and stops pending stages");

            var justChanged = new MainWindow { IgnorePlacement = true };
            justChanged.Start();
            justChanged.Tick(0);
            justChanged.Tick(80);
            justChanged.Tick(85);
            justChanged.Tick(120); // Regular scan sees water; next scan is due at 155.
            justChanged.BucketEmpty = justChanged.VisibleBucketEmpty = true;
            justChanged.Tick(125); // PPM is due, but the icon changed AFTER the scan.
            Program.Check(justChanged.Clicks.Count == 1 && justChanged._autoWaterStage == AutoWaterStage.ConfirmPlacement,
                "a due PPM rechecks pixels instead of using the previous scheduled scan");

            var changedConfirmation = new MainWindow();
            changedConfirmation.Start();
            changedConfirmation.DriveTo(AutoWaterStage.ConfirmPlacement);
            changedConfirmation.BucketEmpty = changedConfirmation.VisibleBucketEmpty = false;
            changedConfirmation.PendingIconAt = DateTime.MaxValue;
            changedConfirmation.DriveUntilStop();
            Program.Check(changedConfirmation.Clicks.Count == 1 && !changedConfirmation.TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal),
                "unstable placement confirmation aborts without another PPM");

            var ambiguous = new MainWindow { OverridePatchColor = Color.FromArgb(180, 180, 180) };
            ambiguous._autoWaterTemplateRgb = Enumerable.Repeat((byte)200, 243).ToArray();
            ambiguous._autoWaterFullTemplateRgb = Enumerable.Repeat((byte)160, 243).ToArray();
            Program.Check(ambiguous.TryReadAutoWaterBucket(out AutoWaterBucketState ambiguousState, out _)
                && ambiguousState == AutoWaterBucketState.Unknown, "similarity threshold alone cannot accept an ambiguous match");

            var keyFailure = new MainWindow { AcceptKeys = false };
            keyFailure.Start();
            keyFailure.Tick(0);
            Program.Check(keyFailure._autoWaterStage == AutoWaterStage.None && keyFailure.Clicks.Count == 0, "failed slot key cancels before PPM");
            Program.Check(keyFailure.KeyEvents.Any(e => !e.Down), "slot failure still attempts key release");
            var clickFailure = new MainWindow { AcceptMouse = false };
            clickFailure.Start();
            clickFailure.DriveUntilStop();
            Program.Check(!clickFailure.TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal) && clickFailure._autoWaterStage == AutoWaterStage.None,
                "failed PPM is not treated as success");
            var noPlacement = new MainWindow { IgnorePlacement = true };
            noPlacement.Start();
            noPlacement.DriveUntilStop();
            Program.Check(noPlacement.TxtAutoWaterStatus.Text.Contains("10 sekund", StringComparison.Ordinal), "unconfirmed placement times out");
            var noPickup = new MainWindow { IgnorePickup = true };
            noPickup.Start();
            noPickup.DriveUntilStop();
            Program.Check(noPickup.Clicks.Count(c => c.Stage != "PlaceWater") == 6 && noPickup.TxtAutoWaterStatus.Text.Contains("6 prób", StringComparison.Ordinal),
                "pickup attempts are bounded at six without false success");

            var unknownPickup = new MainWindow { WrongItem = true };
            unknownPickup.Start();
            unknownPickup._autoWaterStage = AutoWaterStage.VerifyPickup;
            unknownPickup._autoWaterPickupAttempts = 1;
            unknownPickup.DriveUntilStop();
            Program.Check(!unknownPickup.TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal) && unknownPickup.Clicks.Count == 0,
                "any nonempty icon no longer counts as collected water");

            var focusDuringCapture = new MainWindow { LoseFocusDuringCapture = true };
            focusDuringCapture.Start();
            focusDuringCapture.DriveUntilStop();
            Program.Check(focusDuringCapture.Clicks.Count == 0 && focusDuringCapture.ForeignInputs == 0,
                "focus loss during capture never sends input to another app");

            var manualPickup = new MainWindow();
            manualPickup.Start();
            manualPickup.DriveTo(AutoWaterStage.WaitBeforePickup);
            manualPickup.BucketEmpty = manualPickup.VisibleBucketEmpty = false;
            manualPickup.DriveUntilStop();
            Program.Check(manualPickup.Clicks.Count == 1 && manualPickup.TxtAutoWaterStatus.Text.StartsWith("Gotowe.", StringComparison.Ordinal),
                "already collected water is verified without placing it again");
        }

        internal static void VerifyCalibration()
        {
            var w = new MainWindow { GuiVisible = true, BucketEmpty = true, VisibleBucketEmpty = true };
            w.BtnAutoWaterCalibrate_Click(new Button { Tag = "Empty" }, new RoutedEventArgs());
            w.LeftControlDown = true;
            w.TryHandleAutoWaterCalibrationOrTest(Epoch);
            Program.Check(w._autoWaterTemplateRgb != null && w._autoWaterFullTemplateRgb == null && w.SavedChanges == 1,
                "empty calibration saves point and invalidates previous water template");
            Program.Check(w._settings.AutoWaterCalibrationSlot == 9 && w._settings.AutoWaterSampleOffsetX == 240
                && w._settings.AutoWaterSampleOffsetFromBottom == 50 && w._suppressBindToggleUntilRelease,
                "records hotbar point/slot and suppresses held Ctrl after calibration");
            w.LeftControlDown = false;
            w.BtnAutoWaterCalibrate_Click(new Button { Tag = "Full" }, new RoutedEventArgs());
            w.Cursor = new POINT { X = 0, Y = 0 }; // Full step must reuse saved point, not cursor.
            w.BucketEmpty = w.VisibleBucketEmpty = false;
            w.LeftControlDown = true;
            w.TryHandleAutoWaterCalibrationOrTest(Epoch);
            Program.Check(w.HasCompleteAutoWaterCalibration && w.SavedChanges == 2, "full calibration reuses exactly the same hotbar point");
            Program.Check(w.Slots.Count == 0 && w.Clicks.Count == 0, "calibration never sends gameplay input");
            w.BtnAutoWaterResetCalibration_Click(new Button(), new RoutedEventArgs());
            Program.Check(!w.HasCompleteAutoWaterCalibration && w._autoWaterTemplateRgb == null && w._autoWaterFullTemplateRgb == null
                && w._settings.AutoWaterCalibrationSlot == 0, "reset clears both templates and geometry");

            foreach (string reason in new[] { "Inventory", "WrongSlot", "ChatClosed", "Focus", "SameTemplate" })
            {
                var bad = new MainWindow { GuiVisible = true };
                switch (reason)
                {
                    case "Inventory": bad.Cursor.Y = 660; break;
                    case "WrongSlot": bad.Cursor.X = 800; break;
                    case "ChatClosed": bad.GuiVisible = false; break;
                    case "Focus": bad.TargetFocused = false; break;
                    case "SameTemplate": bad._autoWaterCalibratingFullBucket = true; bad.VisibleBucketEmpty = true; break;
                }
                int before = bad.Captures;
                Program.Check(!bad.TryCalibrateAutoWaterAtCursor(out _), "bad calibration rejected: " + reason);
                if (reason != "SameTemplate") Program.Check(bad.Captures == before, "invalid calibration does not capture: " + reason);
            }
            for (int slot = 1; slot <= 9; slot++)
            {
                int x = 100 + 640 - 88 * 3 + (slot - 1) * 20 * 3 + 24;
                Program.Check(IsAutoWaterHotbarSample(new RECT { Left = 100, Top = 100, Right = 1380, Bottom = 820 },
                    new Rectangle(x - 4, 766, 9, 9), slot), "calibration geometry supports hotbar slot " + slot);
            }
        }

        internal static void VerifyRecognitionFocus()
        {
            var w = new MainWindow { TargetFocused = false, VisibleBucketEmpty = true, BucketEmpty = true };
            w.BtnAutoWaterTestRecognition_Click(new Button(), new RoutedEventArgs());
            w.TryHandleAutoWaterCalibrationOrTest(Epoch);
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(10));
            Program.Check(w.Captures == 0 && w._autoWaterRecognitionTestPending && w._autoWaterRecognitionTestAtUtc == DateTime.MinValue,
                "recognition waits without captures while Minecraft is inactive");
            w.TargetFocused = true;
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(10));
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(11));
            Program.Check(w.Captures == 0, "countdown starts only after focus returns");
            w.TargetFocused = false;
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(11));
            w.TargetFocused = true;
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(15));
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(16));
            Program.Check(w.Captures == 0, "lost focus resets the entire countdown");
            w.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(17));
            Program.Check(w.Captures == 1 && w.TxtAutoWaterStatus.Text.Contains("PUSTE WIADRO", StringComparison.Ordinal),
                "empty recognition occurs after two consecutive focused seconds");

            foreach (bool unknown in new[] { false, true })
            {
                var test = new MainWindow { WrongItem = unknown };
                test.BtnAutoWaterTestRecognition_Click(new Button(), new RoutedEventArgs());
                test.GuiVisible = true;
                test.TryHandleAutoWaterCalibrationOrTest(Epoch);
                test.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(10));
                Program.Check(test.Captures == 0, "recognition waits for GUI to close");
                test.GuiVisible = false;
                test.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(10));
                test.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(12));
                Program.Check(test.TxtAutoWaterStatus.Text.Contains(unknown ? "NIEZNANY" : "WIADRO Z WODĄ", StringComparison.Ordinal),
                    "recognition distinguishes water from unrelated icon");
            }
            var ctrl = new MainWindow { TargetFocused = false, GuiVisible = true, LeftControlDown = true };
            ctrl._autoWaterCalibrationPending = true;
            ctrl.TryHandleAutoWaterCalibrationOrTest(Epoch);
            ctrl.TargetFocused = true;
            ctrl.TryHandleAutoWaterCalibrationOrTest(Epoch.AddSeconds(1));
            Program.Check(ctrl.Captures == 0 && ctrl._autoWaterCalibrationPending, "held Ctrl from another app is not a calibration press");
        }

        internal static void VerifySettings()
        {
            var w = new MainWindow();
            string saved = JsonSerializer.Serialize(w._settings);
            w._settings = JsonSerializer.Deserialize<PvpSettings>(saved)!;
            w.NormalizeAutoWaterSettings();
            Program.Check(w._autoWaterTemplateRgb != null && w._autoWaterFullTemplateRgb != null && w.HasCompleteAutoWaterCalibration,
                "both templates and geometry survive JSON save/load");
            w._autoWaterBindWasDown = true;
            w.LoadAutoWaterToUi();
            Program.Check(!w._autoWaterBindWasDown, "loading saved UI resets the held-bind latch");
            foreach (string invalid in new[] { "", "not-base64", Convert.ToBase64String(new byte[2]), new string('x', 2000) })
                Program.Check(DecodeAutoWaterTemplate(invalid) == null, "invalid template rejected");
            var old = new MainWindow();
            old._settings = JsonSerializer.Deserialize<PvpSettings>("{\"AutoWaterBind\":\"N\",\"AutoWaterPlaceIntervalMs\":40}")!;
            old.NormalizeAutoWaterSettings();
            Program.Check(!old.HasCompleteAutoWaterCalibration && old._settings.AutoWaterBind == "N" && old._settings.AutoWaterPlaceIntervalMs == 40,
                "legacy settings retained but require two-template recalibration");
        }

        internal static void VerifyBundledBucketIcons(string root)
        {
            using var archive = ZipFile.OpenRead(Path.Combine(root, "MinecraftHelper_AutoEQ_CobbleX_1.8.8.zip"));
            var emptyImage = ReadImage("bucket_empty.png");
            var waterImage = ReadImage("bucket_water.png");
            var w = new MainWindow { GuiVisible = true, BucketEmpty = true, VisibleBucketEmpty = true };
            w.PackedPixel = (x, y, empty) => ReadItemPixel(empty ? emptyImage : waterImage, x - 956, y - 763);
            double best = 100;
            POINT point = default;
            // Find a distinguishing 9x9 sample INSIDE the part of the actual
            // 1.8 hotbar icon visible above chat at GUI Scale Large (3).
            for (int y = 767; y <= 773; y++)
            for (int x = 960; x <= 1000; x++)
            {
                byte[] empty = Patch(x, y, true);
                byte[] full = Patch(x, y, false);
                double similarity = CalculateAutoWaterSimilarity(empty, full);
                if (similarity < best) { best = similarity; point = new POINT { X = x, Y = y }; }
            }
            Program.Check(best <= AutoWaterMaximumTemplateSimilarity, "bundled bucket icons have a distinguishing patch above chat");
            w.Cursor = point;
            Program.Check(w.TryCalibrateAutoWaterAtCursor(out string calibrationError), "bundled empty icon calibration: " + calibrationError);
            w._autoWaterCalibratingFullBucket = true;
            w.BucketEmpty = w.VisibleBucketEmpty = false;
            Program.Check(w.TryCalibrateAutoWaterAtCursor(out calibrationError), "bundled full icon calibration: " + calibrationError);
            w.GuiVisible = false;
            Program.Check(w.TryReadAutoWaterBucket(out AutoWaterBucketState fullState, out _) && fullState == AutoWaterBucketState.Water,
                "bundled water texture recognized as water, not just nonempty");
            w.BucketEmpty = w.VisibleBucketEmpty = true;
            Program.Check(w.TryReadAutoWaterBucket(out AutoWaterBucketState emptyState, out _) && emptyState == AutoWaterBucketState.Empty,
                "bundled empty texture recognized separately");
            foreach (string item in new[] { "diamond_sword.png", "ender_pearl.png", "bucket_lava.png", "snowball.png", "apple.png" })
            {
                var otherImage = ReadImage(item);
                w.PackedPixel = (x, y, _) => ReadItemPixel(otherImage, x - 956, y - 763);
                Program.Check(w.TryReadAutoWaterBucket(out AutoWaterBucketState otherState, out _) && otherState != AutoWaterBucketState.Water,
                    "bundled unrelated item is not accepted as a water bucket: " + item);
            }

            (int Width, int Height, byte[] Pixels) ReadImage(string file)
            {
                using Stream entry = archive.GetEntry("assets/minecraft/textures/items/" + file)!.Open();
                using var stream = new MemoryStream();
                entry.CopyTo(stream);
                stream.Position = 0;
                var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var converted = new FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
                byte[] pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
                converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
                return (converted.PixelWidth, converted.PixelHeight, pixels);
            }
            static Color ReadItemPixel((int Width, int Height, byte[] Pixels) image, int dx, int dy)
            {
                int x = Math.Clamp(dx * image.Width / 48, 0, image.Width - 1);
                int y = Math.Clamp(dy * image.Height / 48, 0, image.Height - 1);
                int index = (y * image.Width + x) * 4;
                int a = image.Pixels[index + 3];
                int Channel(int c) => (c * a + 45 * (255 - a)) / 255;
                return Color.FromArgb(Channel(image.Pixels[index + 2]), Channel(image.Pixels[index + 1]), Channel(image.Pixels[index]));
            }
            byte[] Patch(int x, int y, bool empty)
            {
                var result = new byte[243];
                int index = 0;
                for (int py = y - 4; py <= y + 4; py++)
                for (int px = x - 4; px <= x + 4; px++)
                {
                    Color color = w.PackedPixel(px, py, empty);
                    result[index++] = color.R; result[index++] = color.G; result[index++] = color.B;
                }
                return result;
            }
        }

        private void Start() => Program.Check(TryStartAutoWater(Now), "calibrated module starts");
        private void Tick(int milliseconds)
        {
            Now = Epoch.AddMilliseconds(milliseconds);
            RenderIcon();
            RunAutoWaterTick(Now);
        }
        private void DriveUntilStop()
        {
            int start = (int)(Now - Epoch).TotalMilliseconds;
            for (int ms = start; ms <= 10100 && _autoWaterStage != AutoWaterStage.None; ms += 5) Tick(ms);
        }
        private void DriveTo(AutoWaterStage target)
        {
            int start = (int)(Now - Epoch).TotalMilliseconds;
            for (int ms = start; ms <= 10100 && _autoWaterStage != target && _autoWaterStage != AutoWaterStage.None; ms += 5) Tick(ms);
            Program.Check(_autoWaterStage == target, "reached stage " + target);
        }
        internal void BeginFakeCapture()
        {
            Captures++;
            if (LoseFocusDuringCapture) TargetFocused = false;
        }
        internal Color ReadFakePixel(int x, int y)
        {
            RenderIcon();
            int expectedX = Client.Left + (Client.Right - Client.Left) / 2 + 240;
            int expectedY = Client.Bottom - 50;
            if (OverridePatchColor is Color overridden) return overridden;
            if (PackedPixel != null) return PackedPixel(x, y, VisibleBucketEmpty);
            if (WrongItem || Math.Abs(x - expectedX) > 4 || Math.Abs(y - expectedY) > 4) return Color.Red;
            return VisibleBucketEmpty ? Color.White : Color.Black;
        }
        private void RenderIcon()
        {
            if (Now < PendingIconAt) return;
            VisibleBucketEmpty = PendingIconEmpty;
            PendingIconAt = DateTime.MaxValue;
        }
        internal bool SendFakeMouse(bool leftButton, bool holdPulseMode)
        {
            Program.Check(!leftButton && !holdPulseMode, "AutoWater only uses complete right clicks");
            if (!TargetFocused || GuiVisible) ForeignInputs++;
            if (_autoWaterStage == AutoWaterStage.PlaceWater)
                FullSeenBeforeLastClick = _autoWaterLastFullSimilarity >= GetAutoWaterSimilarityThreshold();
            Clicks.Add(((int)(Now - Epoch).TotalMilliseconds, _autoWaterStage.ToString()));
            if (!AcceptMouse) return false;
            if (WrongItem || SelectedSlot != 9 || (BucketEmpty ? IgnorePickup : IgnorePlacement)) return true;
            RenderIcon();
            BucketEmpty = !BucketEmpty;
            PendingIconEmpty = BucketEmpty;
            PendingIconAt = Now.AddMilliseconds(IconDelayMs);
            return true;
        }
        internal bool SendFakeKey(int key, bool down)
        {
            KeyEvents.Add((key, down));
            if (!TargetFocused || GuiVisible) ForeignInputs++;
            if (!AcceptKeys) return false;
            if (down)
            {
                SelectedSlot = key - 0x30;
                Slots.Add(SelectedSlot);
            }
            return true;
        }
        private static ComboBox Slot(int slot)
        {
            var box = new ComboBox();
            for (int i = 1; i <= 9; i++) box.Items.Add(new ComboBoxItem { Content = i });
            box.SelectedIndex = slot - 1;
            return box;
        }
        private static int GetSlotVirtualKey(int slot) => 0x30 + Math.Clamp(slot, 1, 9);
        private static int ParseNonNegativeInt(string? value) => int.TryParse(value, out int parsed) ? Math.Max(0, parsed) : 0;
        private static bool IsVirtualKeyDown(int _) => System.Drawing.FakeScreen.Current!.LeftControlDown;
        private static bool TryGetWindowClientRectOnScreen(IntPtr _, out RECT rect) { rect = System.Drawing.FakeScreen.Current!.Client; return true; }
        private static bool GetCursorPos(out POINT point) { point = System.Drawing.FakeScreen.Current!.Cursor; return true; }
        private static IntPtr GetForegroundWindow() => System.Drawing.FakeScreen.Current!.TargetFocused ? new IntPtr(1) : IntPtr.Zero;
        private bool IsInventoryCursorVisible() => GuiVisible;
        private void StopClickerRuntimesForExclusivePointerMacro() { }
        private void StopOtherExclusivePointerMacros() { }
        private void ResetTestAutoFishingRuntimeState(DateTime _) { }
        private void SetAutoLeftDabHold(bool _) { }
        private void RefreshOverlayHud(DateTime _) { }
        private void UpdateStatusBar(string _, string __) { }
        private void MarkDirty() => SavedChanges++;
        private void ConfirmPendingBind(BindTarget _) { }
        private void SetExpandableSectionState(StackPanel _, bool __) { }
    }
}

namespace MinecraftHelper.Services
{
    internal static class NativeInput
    {
        internal static bool SendMouseClick(bool leftButton, bool holdPulseMode) => System.Drawing.FakeScreen.Current!.SendFakeMouse(leftButton, holdPulseMode);
        internal static bool SendKey(int key, bool down) => System.Drawing.FakeScreen.Current!.SendFakeKey(key, down);
    }
}

namespace System.Drawing
{
    internal static class FakeScreen { internal static MinecraftHelper.MainWindow? Current; }
    internal sealed class Bitmap : IDisposable
    {
        internal int Width { get; }
        internal int Height { get; }
        internal int X;
        internal int Y;
        internal Bitmap(int width, int height, Imaging.PixelFormat _) { Width = width; Height = height; }
        internal Color GetPixel(int x, int y) => FakeScreen.Current!.ReadFakePixel(X + x, Y + y);
        public void Dispose() { }
    }
    internal sealed class Graphics : IDisposable
    {
        private readonly Bitmap _bitmap;
        private Graphics(Bitmap bitmap) => _bitmap = bitmap;
        internal static Graphics FromImage(Bitmap bitmap) => new(bitmap);
        internal void CopyFromScreen(int x, int y, int _, int __, Size ___, CopyPixelOperation ____)
        {
            FakeScreen.Current!.BeginFakeCapture();
            _bitmap.X = x;
            _bitmap.Y = y;
        }
        public void Dispose() { }
    }
    internal enum CopyPixelOperation { SourceCopy }
}
namespace System.Drawing.Imaging { internal enum PixelFormat { Format24bppRgb } }
