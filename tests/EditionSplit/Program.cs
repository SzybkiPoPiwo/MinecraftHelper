using System.Text;

internal static class Program
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            string root = FindRepositoryRoot();
            string autoClickerProject = Read(root, "MinecraftHelper.Pvp", "MinecraftHelper.Pvp.csproj");
            string autoClickerResources = Read(root, "MinecraftHelper.Pvp", "App.xaml");
            string autoClickerView = Read(root, "MinecraftHelper.Pvp", "MainWindow.xaml");
            string autoClickerWindow = Read(root, "MinecraftHelper.Pvp", "MainWindow.xaml.cs");
            string autoClickerQuickActions = Read(root, "MinecraftHelper.Pvp", "MainWindow.PvpQuickActions.cs");
            string autoClickerAppleCutter = Read(root, "MinecraftHelper.Pvp", "MainWindow.AppleCutter.cs");
            string autoClickerModels = Read(root, "MinecraftHelper.Pvp", "Models", "PvpSettings.cs");
            string autoClickerScheduler = Read(root, "MinecraftHelper.Pvp", "Services", "LightweightClickScheduler.cs");
            string autoClickerMouseTracker = Read(root, "MinecraftHelper.Pvp", "Services", "PhysicalMouseStateTracker.cs");
            string autoClickerSettings = Read(root, "MinecraftHelper.Pvp", "Services", "PvpSettingsService.cs");
            string minerProject = Read(root, "MinecraftHelper", "MinecraftHelper.csproj");
            string minerWindow = Read(root, "MinecraftHelper", "MainWindow.xaml");
            string minerCode = Read(root, "MinecraftHelper", "MainWindow.xaml.cs");
            string installer = Read(root, "Installer", "MinecraftHelper.iss");

            Check(autoClickerProject.Contains("<AssemblyName>MinecraftHelper.AutoClicker</AssemblyName>"),
                "Auto Clicker has its own executable name");
            Check(autoClickerProject.Contains("Assets\\autoclicker.ico", StringComparison.Ordinal)
                && minerProject.Contains("Assets\\miner.ico", StringComparison.Ordinal),
                "Auto Clicker and Kopacz use separate application icons");
            Check(!autoClickerProject.Contains("NAudio", StringComparison.OrdinalIgnoreCase)
                && !autoClickerProject.Contains("Tesseract", StringComparison.OrdinalIgnoreCase),
                "Auto Clicker does not load miner OCR/audio packages");
            Check(autoClickerResources.Contains("<Setter TargetName=\"ButtonBorder\" Property=\"Background\" Value=\"#2A5D80\"/>", StringComparison.Ordinal)
                && autoClickerResources.Contains("<Setter TargetName=\"ButtonBorder\" Property=\"Background\" Value=\"#172638\"/>", StringComparison.Ordinal),
                "button hover and disabled states stay dark instead of using the bright accent");
            Check(autoClickerResources.Contains("Themes/ScrollBarStyles.xaml", StringComparison.Ordinal)
                && autoClickerProject.Contains("ScrollBarStyles.xaml", StringComparison.Ordinal),
                "Auto Clicker uses the shared dark scrollbar theme");
            Check(autoClickerWindow.Contains("UpdatePhysicalMouseTracking(now)", StringComparison.Ordinal)
                && autoClickerWindow.Contains("UsesPhysicalPointerGate", StringComparison.Ordinal)
                && autoClickerWindow.Contains("&& leftPointerDown", StringComparison.Ordinal),
                "physical mouse tracking runs only while a mouse-gated clicker is physically held");
            Check(autoClickerMouseTracker.Contains("InjectedMouseFlags", StringComparison.Ordinal)
                && !autoClickerMouseTracker.Contains("WmMouseMove", StringComparison.Ordinal)
                && autoClickerMouseTracker.Contains("message is WmLeftButtonDown or WmLeftButtonUp", StringComparison.Ordinal),
                "combination tracker ignores injected input and does no mouse-move work");
            Check(autoClickerScheduler.Contains("requiredHoldKey != 0", StringComparison.Ordinal)
                && autoClickerScheduler.Contains("requiredPointerKey != 0", StringComparison.Ordinal),
                "click scheduler verifies bind and physical mouse button");
            Check(autoClickerWindow.Contains("(leftComboMode || leftHoldMode) && rightComboMode", StringComparison.Ordinal)
                && autoClickerWindow.Contains("&& CanShareAutoClickerBind()", StringComparison.Ordinal),
                "shared clicker bind supports LPM Hold/Combo with PPM Combo");
            Check(autoClickerScheduler.Contains("_isPhysicalPointerDown(requiredPointerKey)", StringComparison.Ordinal)
                && autoClickerScheduler.Contains("SendMouseClick(leftButton, holdPulseMode: false)", StringComparison.Ordinal),
                "combination scheduler separates physical state from injected clicks");
            Check(autoClickerView.Contains("Aktywacja bindem + trzymanie LPM", StringComparison.Ordinal)
                && autoClickerView.Contains("Aktywacja bindem + trzymanie PPM", StringComparison.Ordinal)
                && autoClickerModels.Contains("AutoLeftPhysicalToggleMode", StringComparison.Ordinal)
                && autoClickerModels.Contains("AutoRightPhysicalToggleMode", StringComparison.Ordinal)
                && autoClickerWindow.Contains("case \"PhysicalToggle\":", StringComparison.Ordinal)
                && autoClickerWindow.Contains("GOTOWY • trzymaj LPM", StringComparison.Ordinal)
                && autoClickerWindow.Contains("GOTOWY • trzymaj PPM", StringComparison.Ordinal),
                "LPM and PPM expose persistent physical-button activation with independent saved modes and HUD state");
            int cursorGuardCheck = autoClickerScheduler.IndexOf("suppressClick = _shouldSuppressClick();", StringComparison.Ordinal);
            int injectedClick = autoClickerScheduler.IndexOf("NativeInput.SendMouseClick(leftButton, holdPulseMode: false)", StringComparison.Ordinal);
            Check(cursorGuardCheck >= 0
                && injectedClick > cursorGuardCheck
                && autoClickerWindow.Contains("ShouldSuppressScheduledClick", StringComparison.Ordinal)
                && autoClickerWindow.Contains("_nextCursorProbeUtc = now + RuntimeInputInterval", StringComparison.Ordinal),
                "every injected click rechecks the Minecraft GUI cursor without waiting for the UI cache");
            Check(autoClickerScheduler.Contains("timeBeginPeriod(1)", StringComparison.Ordinal)
                && autoClickerScheduler.Contains("timeEndPeriod(1)", StringComparison.Ordinal),
                "high-resolution timer is paired and released");
            Check(autoClickerWindow.Contains("BINDY: anulowano po utracie fokusu Minecrafta.", StringComparison.Ordinal),
                "BINDY sequence is cancelled after focus loss");
            Check(autoClickerWindow.Contains("NormalizeAutoClickerHudEntrySizes(entries);", StringComparison.Ordinal)
                && autoClickerWindow.Contains("with { Emphasize = false }", StringComparison.Ordinal),
                "Auto Clicker HUD keeps every message at one visual size");
            Check(autoClickerWindow.Contains("TargetResolveRetryInterval = TimeSpan.FromSeconds(5)", StringComparison.Ordinal)
                && autoClickerWindow.Contains("_settings.TargetProcessId <= 0", StringComparison.Ordinal),
                "idle Auto Clicker avoids unnecessary process scans");
            Check(autoClickerSettings.Contains("\"Minecraft Helper\"", StringComparison.Ordinal)
                && autoClickerSettings.Contains("\"AutoClicker\"", StringComparison.Ordinal)
                && autoClickerSettings.Contains("LegacySettingsPath", StringComparison.Ordinal),
                "Auto Clicker has isolated settings with prototype migration");
            Check(autoClickerView.Contains("Header=\"Akcje PVP\"", StringComparison.Ordinal)
                && autoClickerView.Contains("Tag=\"PvpAutoBlock\"", StringComparison.Ordinal)
                && autoClickerView.Contains("Tag=\"PvpSnowball\"", StringComparison.Ordinal)
                && autoClickerView.Contains("Tag=\"PvpPearl\"", StringComparison.Ordinal)
                && autoClickerView.Contains("Tag=\"PvpRod\"", StringComparison.Ordinal),
                "PVP action view exposes four independent binds");
            Check(autoClickerModels.Contains("PvpSwordSlot", StringComparison.Ordinal)
                && autoClickerModels.Contains("PvpBlockSlot", StringComparison.Ordinal)
                && autoClickerModels.Contains("PvpRodRightClickHoldMs", StringComparison.Ordinal),
                "PVP slots and rod hold time are persisted");
            Check(autoClickerQuickActions.Contains("IsAutoLeftClickerRuntimeActive()", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("return _autoLeftRuntimeEnabled && ChkAutoLeftEnabled.IsChecked == true;", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("PvpQuickActionKind.AutoBlock => GetPvpQuickActionSelectedSlot(CbPvpBlockSlot, 5)", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("KeepAutoLeftClickerRunningDuringPvpAction", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("_autoClickScheduler.StopRightImmediately();", StringComparison.Ordinal)
                && !autoClickerQuickActions.Contains("_autoClickScheduler.Stop();", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("ReleasePvpQuickActionRightButton();", StringComparison.Ordinal)
                && autoClickerQuickActions.Contains("IsPvpQuickActionGuiVisible()", StringComparison.Ordinal),
                "PVP actions keep AUTO LPM running, pause only AUTO PPM and safely release the action PPM");
            Check(autoClickerView.Contains("Header=\"Jabłka\"", StringComparison.Ordinal)
                && autoClickerView.Contains("Tag=\"AppleCutter\"", StringComparison.Ordinal)
                && autoClickerView.Contains("TxtAppleCutterSwitchMinMs", StringComparison.Ordinal)
                && autoClickerView.Contains("TxtAppleCutterBreakMinMs", StringComparison.Ordinal)
                && autoClickerView.Contains("TxtAppleCutterPlaceMinMs", StringComparison.Ordinal),
                "Apple cutter has a separate tab, bind and three configurable timing ranges");
            Check(autoClickerModels.Contains("AppleCutterShearsSlot", StringComparison.Ordinal)
                && autoClickerModels.Contains("AppleCutterLeavesSlot", StringComparison.Ordinal)
                && autoClickerModels.Contains("AppleCutterRepairEveryCycles", StringComparison.Ordinal)
                && autoClickerModels.Contains("AppleCutterRepairCommand", StringComparison.Ordinal),
                "Apple cutter slots, repair interval and command are persisted");
            Check(autoClickerAppleCutter.Contains("AppleCutterPlacementCps = 40", StringComparison.Ordinal)
                && autoClickerAppleCutter.Contains("1000 / AppleCutterPlacementCps", StringComparison.Ordinal)
                && autoClickerAppleCutter.Contains("SendTextByKeyboard(NormalizeAppleCutterCommand", StringComparison.Ordinal)
                && autoClickerAppleCutter.Contains("_appleCutterCyclesCompleted % GetAppleCutterRepairEveryCycles() == 0", StringComparison.Ordinal),
                "Apple cutter places at 40 CPS and repairs on the configured cycle using the shears stage");
            Check(autoClickerAppleCutter.Contains("ReleaseAppleCutterInjectedButtons();", StringComparison.Ordinal)
                && autoClickerAppleCutter.Contains("ReleaseAppleCutterLeftButton();", StringComparison.Ordinal)
                && autoClickerWindow.Contains("Ścinanie jabłek przerwane po utracie fokusu", StringComparison.Ordinal)
                && autoClickerWindow.Contains("cursorVisible && !IsAppleCutterRepairStage", StringComparison.Ordinal),
                "Apple cutter releases held input immediately on stop, focus loss and unexpected GUI detection");

            Check(minerProject.Contains("<AssemblyName>MinecraftHelper.Miner</AssemblyName>"),
                "Kopacz has its own executable name");
            Check(minerProject.Contains("MINER_EDITION", StringComparison.Ordinal),
                "Kopacz is compiled as the miner edition");
            Check(minerWindow.Contains("Name=\"TabPvp\" Header=\"PVP\" Visibility=\"Collapsed\"", StringComparison.Ordinal)
                && minerWindow.Contains("Name=\"TabBindy\" Header=\"BINDY\" Visibility=\"Collapsed\"", StringComparison.Ordinal),
                "Kopacz visual tree excludes PVP and BINDY views");
            Check(minerWindow.Contains("Name=\"BorderAutoArmorSection\" Visibility=\"Collapsed\"", StringComparison.Ordinal)
                && minerWindow.Contains("Name=\"BorderAutoWaterSection\" Visibility=\"Collapsed\"", StringComparison.Ordinal),
                "Kopacz visual tree excludes Auto zbroja and AutoWater");
            Check(minerCode.Contains("#if !MINER_EDITION", StringComparison.Ordinal)
                && minerCode.Contains("InitializeMouseHookThread();", StringComparison.Ordinal),
                "Kopacz startup does not initialize the mouse hook");

            Check(installer.Contains("Name: \"autoclicker\"", StringComparison.Ordinal)
                && installer.Contains("Name: \"miner\"", StringComparison.Ordinal),
                "installer exposes both selectable applications");
            Check(installer.Contains("MinecraftHelper.AutoClicker.exe", StringComparison.Ordinal)
                && installer.Contains("MinecraftHelper.Miner.exe", StringComparison.Ordinal),
                "installer points to both independent executables");

            Console.WriteLine($"PASS: {_passed} checks; podział aplikacji i instalatora jest spójny.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MinecraftHelper.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Nie znaleziono katalogu repozytorium.");
    }

    private static string Read(string root, params string[] parts)
    {
        string path = parts.Aggregate(root, Path.Combine);
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("FAIL: " + description);
        _passed++;
    }
}
