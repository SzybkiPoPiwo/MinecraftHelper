using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using MinecraftHelper;
using MinecraftHelper.Models;

// Never construct a Window, register a native hook, send input or touch AppData.
// Inspect the actual miner assembly and exercise only isolated in-memory state.
internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            CheckNativeIsolation();
            CheckMiningCadence();
            CheckLegacySettings();
            Console.WriteLine($"PASS: {_passed} checks; miner isolation, cadence and legacy settings. No user input/settings changed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckNativeIsolation()
    {
        Type[] types = typeof(MainWindow).Assembly.GetTypes();
        foreach (Type type in types)
        {
            Check(type.Name is not "AutoClickScheduler" and not "MacroDiagnosticsService",
                $"no retired clicker service: {type.FullName}");
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                DllImportAttribute? import = method.GetCustomAttribute<DllImportAttribute>();
                if (import is null) continue;
                Check(!(import.EntryPoint ?? method.Name).StartsWith("SetWindowsHookEx", StringComparison.OrdinalIgnoreCase),
                    $"miner cannot register global hooks: {type.FullName}.{method.Name}");
            }
        }
        foreach (FieldInfo field in typeof(MainWindow).GetFields(PrivateInstance))
        {
            Check(field.FieldType != typeof(Thread) && field.FieldType != typeof(ManualResetEventSlim),
                $"no retired mouse tracking thread/synchronization: {field.Name}");
        }
        Check(typeof(MainWindow).GetMethod("StartMouseHook", PrivateInstance) is null, "mining input has no mouse hook registration path");
        Check(typeof(MainWindow).GetMethod("SendMouseClick", PrivateInstance) is not null, "mining click output is preserved");
        Check(typeof(MainWindow).GetMethod("SendMouseButton", PrivateInstance) is not null, "mining held-button output is preserved");
    }

    private static void CheckMiningCadence()
    {
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        Check(!(bool)Call(window, "IsFastMacroCadenceRequired")!, "idle mining uses reduced dispatcher cadence");
        foreach (string name in new[]
        {
            "_kopacz533RuntimeEnabled", "_kopacz633RuntimeEnabled", "_jablkaRuntimeEnabled",
            "_testFastUpExitRuntimeEnabled", "_testAutoFishingRuntimeEnabled", "_inventoryCleanupScanInProgress",
            "_inventoryCleanupEatingRightButtonDown", "_kopacz533Holding", "_kopacz633HoldingAttack",
            "_testFastUpExitBreakHoldActive", "_testFastUpExitPlaceHoldActive", "_emergencyDamageSoundHandlingAlarm"
        })
        {
            Set(window, name, true);
            Check((bool)Call(window, "IsFastMacroCadenceRequired")!, $"fast cadence while {name}");
            Set(window, name, false);
            Check(!(bool)Call(window, "IsFastMacroCadenceRequired")!, $"idle cadence after {name}");
        }
        foreach (string name in new[] { "_inventoryCleanupStage", "_autoReconnectStage" })
        {
            Type type = Field(window, name).FieldType;
            foreach (object value in Enum.GetValues(type))
            {
                if (Convert.ToInt32(value) == 0) continue;
                Set(window, name, value);
                Check((bool)Call(window, "IsFastMacroCadenceRequired")!, $"fast cadence during {name}={value}");
            }
            Set(window, name, Enum.ToObject(type, 0));
            Check(!(bool)Call(window, "IsFastMacroCadenceRequired")!, $"idle cadence after {name}");
        }
    }

    private static void CheckLegacySettings()
    {
        const string legacyJson = """
        {
          "MacroLeftButton": {"Enabled":true,"Key":"F6"},
          "MacroRightButton": {"Enabled":true,"Key":"F7"},
          "HoldLeftButton": {"Enabled":true,"Key":"F8"},
          "HoldRightButton": {"Enabled":true,"Key":"F9"},
          "AutoLeftButton": {"Enabled":true,"Key":"R","MinCps":15,"MaxCps":18},
          "AutoRightButton": {"Enabled":true,"Key":"F10"},
          "BindyEnabled":true,"AutoArmorEnabled":true,"AutoWaterEnabled":true,
          "AutoArmorBind":"F11","AutoWaterBind":"F12",
          "Kopacz533Enabled":true,"Kopacz533Key":"Insert",
          "Kopacz633Enabled":true,"Kopacz633Key":"Delete",
          "ChatOpenKey":"T","DropItemKey":"Q",
          "InventoryCleanupIntervalSeconds":180,
          "AutoReconnectTeleportDelaySeconds":10
        }
        """;
        AppSettings settings = JsonSerializer.Deserialize<AppSettings>(legacyJson)!;
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        Set(window, "_settings", settings);
        Call(window, "EnsureSettingsConsistency");
        foreach (string name in new[] { "MacroLeftButton", "MacroRightButton", "HoldLeftButton", "HoldRightButton", "AutoLeftButton", "AutoRightButton" })
            Check(!((MacroButton)typeof(AppSettings).GetProperty(name)!.GetValue(settings)!).Enabled, $"legacy {name} cannot activate in miner");
        Check(!settings.BindyEnabled && !settings.AutoArmorEnabled && !settings.AutoWaterEnabled, "legacy PVP modules remain disabled after import");
        Check(settings.AutoLeftButton.Key == "R" && settings.AutoLeftButton.MinCps == 15 && settings.AutoLeftButton.MaxCps == 18,
            "legacy clicker values are retained for compatibility");
        Check(settings.AutoArmorBind == "F11" && settings.AutoWaterBind == "F12", "legacy feature binds are retained");
        Check(settings.Kopacz533Enabled && settings.Kopacz533Key == "Insert" && settings.Kopacz633Enabled && settings.Kopacz633Key == "Delete",
            "mining modes and binds survive migration");
        Check(settings.InventoryCleanupIntervalSeconds == 180 && settings.AutoReconnectTeleportDelaySeconds == 10,
            "Auto EQ and teleport timings survive migration");
        Check(settings.ChatOpenKey == "T" && settings.DropItemKey == "Q", "Minecraft control keys survive migration");
        string normalized = JsonSerializer.Serialize(settings);
        Check(normalized.Contains("AutoLeftButton", StringComparison.Ordinal) && normalized.Contains("AutoArmorBind", StringComparison.Ordinal),
            "legacy JSON fields are not lost when settings are saved");
        Call(window, "EnsureSettingsConsistency");
        Check(JsonSerializer.Serialize(settings) == normalized, "settings normalization is idempotent");
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PrivateInstance)
        ?? throw new MissingFieldException(target.GetType().FullName, name);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static object? Call(object target, string name, params object[] arguments) =>
        (target.GetType().GetMethod(name, PrivateInstance) ?? throw new MissingMethodException(name)).Invoke(target, arguments);
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _passed++;
    }
}
