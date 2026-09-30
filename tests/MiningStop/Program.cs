using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using MinecraftHelper;

// No window, settings, filesystem logs or desktop input are created by these
// tests. Exercise the real reset methods with all native holds initially UP.
internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyStopEdges();
            VerifyCommandCountdownPause();
            VerifyMiningLogMessages();
            foreach (string miner in new[] { "533", "633" })
                VerifyMiner(miner);
            VerifyReconnectGuards();
            VerifyPriority();
            Console.WriteLine($"PASS: {_passed} checks; no user settings, logs or desktop input changed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyStopEdges()
    {
        bool wasDown = false;
        Check(Press(true, false, ref wasDown), "first stop press is consumed");
        Check(!Press(true, false, ref wasDown), "held stop bind does not repeat/restart");
        Check(!Press(false, false, ref wasDown), "release does not toggle");
        Check(Press(true, false, ref wasDown), "a new press is recognized");
        wasDown = false;
        Check(!Press(true, true, ref wasDown), "injected drop/control DOWN cannot stop mining");
        Check(!Press(false, false, ref wasDown), "injected release does not stop mining");
        Check(Press(true, false, ref wasDown), "physical press after injected release works");
    }

    private static bool Press(bool down, bool injected, ref bool wasDown)
    {
        object[] args = { down, injected, wasDown };
        bool result = (bool)typeof(MainWindow).GetMethod("ConsumeMiningStopPress", Static)!.Invoke(null, args)!;
        wasDown = (bool)args[2];
        return result;
    }

    private static void VerifyCommandCountdownPause()
    {
        DateTime started = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        foreach (string miner in new[] { "533", "633" })
        {
            MainWindow window = NewWindow();
            object owner = EnumValue("InventoryCleanupOwner", "Kopacz" + miner);
            Set(window, "_kopacz" + miner + "RuntimeEnabled", true);
            Set(window, "_nextKopacz" + miner + "CommandAtUtc", started.AddSeconds(18));

            Call(window, "PauseKopaczCommandCountdown", owner, started);
            Check((bool)Call(window, "IsKopaczCommandCountdownPaused", owner)!, miner + " countdown paused");
            Check((int)Call(window, "GetKopaczCommandRemainingSeconds", owner, started.AddMinutes(2))! == 18,
                miner + " countdown does not elapse during full EQ/CX/eating");

            Call(window, "ResumeKopaczCommandCountdown", started.AddSeconds(42));
            Check(!(bool)Call(window, "IsKopaczCommandCountdownPaused", owner)!, miner + " countdown resumed");
            Check((DateTime)Get(window, "_nextKopacz" + miner + "CommandAtUtc")! == started.AddSeconds(60),
                miner + " deadline moved by paused duration");
            Check((int)Call(window, "GetKopaczCommandRemainingSeconds", owner, started.AddSeconds(42))! == 18,
                miner + " resumes from preserved remainder");

            // A cancelled miner must clear pause bookkeeping without moving a
            // deadline that no longer belongs to an active runtime.
            Call(window, "PauseKopaczCommandCountdown", owner, started.AddSeconds(42));
            Set(window, "_kopacz" + miner + "RuntimeEnabled", false);
            Call(window, "ResumeKopaczCommandCountdown", started.AddSeconds(50));
            Check((DateTime)Get(window, "_nextKopacz" + miner + "CommandAtUtc")! == started.AddSeconds(60),
                miner + " cancellation does not reschedule a dead runtime");
        }

        DateTime nearMaximum = DateTime.MaxValue.AddTicks(-5);
        DateTime saturated = (DateTime)typeof(MainWindow).GetMethod("AddWithoutOverflow", Static)!
            .Invoke(null, new object[] { nearMaximum, TimeSpan.FromSeconds(1) })!;
        Check(saturated == DateTime.MaxValue, "deadline extension cannot overflow DateTime");
    }

    private static void VerifyMiningLogMessages()
    {
        Check(InventoryStatus("completed", remainingStacks: 0, dropPasses: 0) == "Zakończono — EQ było czyste",
            "clean EQ message without drop passes");
        Check(InventoryStatus("completed", remainingStacks: 0, dropPasses: 2) == "Zakończono — EQ wyczyszczone • 2 przebiegi",
            "clean EQ message after dropping");
        Check(InventoryStatus("completed", remainingStacks: 1, dropPasses: 3) == "Nie wszystko wyrzucono • pozostał 1 stos • 3 przebiegi",
            "remaining stack singular message");
        Check(InventoryStatus("completed", remainingStacks: 2, dropPasses: 3) == "Nie wszystko wyrzucono • pozostały 2 stosy • 3 przebiegi",
            "remaining stacks paucal message");
        Check(InventoryStatus("completed", remainingStacks: 5, dropPasses: 3) == "Nie wszystko wyrzucono • pozostało 5 stosów • 3 przebiegi",
            "remaining stacks plural message");
        Check(InventoryStatus("aborted", remainingStacks: 0, dropPasses: 1) == "Przerwano — sprawdź powód",
            "aborted EQ message");
        Check(InventoryStatus("interrupted", remainingStacks: 0, dropPasses: 1) == "Niedokończono — program lub makro zatrzymane",
            "interrupted EQ message");
        Check(InventoryStatus("in-progress", remainingStacks: 0, dropPasses: 0) == "Auto EQ w toku",
            "active EQ message");
    }

    private static string InventoryStatus(string status, int remainingStacks, int dropPasses)
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        Type entryType = assembly.GetType("MinecraftHelper.Services.MiningLogEntry")!;
        object entry = Activator.CreateInstance(entryType)!;
        entryType.GetProperty("Kind")!.SetValue(entry, "inventory-session");
        entryType.GetProperty("Status")!.SetValue(entry, status);
        entryType.GetProperty("RemainingStacks")!.SetValue(entry, remainingStacks);
        entryType.GetProperty("DropPasses")!.SetValue(entry, dropPasses);

        Type windowType = assembly.GetType("MinecraftHelper.MiningLogsWindow")!;
        MethodInfo method = windowType.GetMethod("GetStatusLabel", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)method.Invoke(null, new[] { entry })!;
    }

    private static void VerifyMiner(string miner)
    {
        object owner = EnumValue("InventoryCleanupOwner", "Kopacz" + miner);
        Type stages = Nested("InventoryCleanupStage");
        foreach (object stage in Enum.GetValues(stages))
        {
            if (stage.ToString() == "None") continue;
            MainWindow window = NewWindow();
            Set(window, "_kopacz" + miner + "RuntimeEnabled", true);
            Set(window, "_kopacz" + miner + "ResumeMiningPending", true);
            Set(window, "_kopacz" + miner + "PendingCommand", "/pending");
            Set(window, "_inventoryCleanupGeneration", 7);
            Set(window, "_inventoryCleanupOwner", owner);
            Set(window, "_inventoryCleanupStage", stage);
            Set(window, "_inventoryCleanupPendingCobbleXCommand", "/cx");
            Set(window, "_inventoryCleanupCobbleXCommandPending", true);
            Set(window, "_inventoryCleanupEatAfterCleanupPending", true);
            ((IList)Get(window, "_inventoryCleanupTargets")!).Add(new System.Drawing.Point(5, 5));

            Check((bool)Call(window, "HasMiningWork", owner)!, miner + " can stop " + stage);
            Call(window, "StopMiningRuntime", owner);
            Check(!(bool)Get(window, "_kopacz" + miner + "RuntimeEnabled")!, "runtime stopped");
            Check(!(bool)Get(window, "_kopacz" + miner + "ResumeMiningPending")!, "resume cancelled");
            Check((string)Get(window, "_kopacz" + miner + "PendingCommand")! == "", "command cancelled");
            Check(Get(window, "_inventoryCleanupStage")!.ToString() == "None", "EQ stage cancelled");
            Check(Get(window, "_inventoryCleanupOwner")!.ToString() == "None", "EQ owner reset");
            Check(((IList)Get(window, "_inventoryCleanupTargets")!).Count == 0, "remaining drops removed");
            Check(!(bool)Get(window, "_inventoryCleanupCobbleXCommandPending")!, "CX cancelled");
            Check(!(bool)Get(window, "_inventoryCleanupEatAfterCleanupPending")!, "eating cancelled");
            Check((DateTime)Get(window, "_nextInventoryCleanupAtUtc")! == DateTime.MaxValue, "no rescheduled EQ");
            Check((int)Get(window, "_inventoryCleanupGeneration")! == 8, "old scan invalidated");
            Check(!(bool)Call(window, "HasMiningWork", owner)!, "stop does not leave pending work");

            // Restart the same miner while an old worker is finishing. The same
            // owner/stage must not make the old generation valid again.
            Set(window, "_kopacz" + miner + "RuntimeEnabled", true);
            Set(window, "_inventoryCleanupOwner", owner);
            Set(window, "_inventoryCleanupStage", EnumValue("InventoryCleanupStage", "WaitForInventory"));
            Check(!(bool)Call(window, "IsInventoryCleanupScanCurrent", 7, owner)!, "late result rejected after restart");
            Check((bool)Call(window, "IsInventoryCleanupScanCurrent", 8, owner)!, "current result accepted");
        }

        foreach (object stage in Enum.GetValues(Nested("Kopacz" + miner + "CommandStage")))
        {
            MainWindow window = NewWindow();
            Set(window, "_kopacz" + miner + "RuntimeEnabled", true);
            Set(window, "_kopacz" + miner + "CommandStage", stage);
            Call(window, "StopMiningRuntime", owner);
            Check(Get(window, "_kopacz" + miner + "CommandStage")!.ToString() == "None", "cancel command stage " + stage);
        }
    }

    private static void VerifyReconnectGuards()
    {
        MainWindow window = NewWindow();
        IntPtr foreground = (IntPtr)typeof(MainWindow).GetMethod("GetForegroundWindow", Static)!.Invoke(null, null)!;
        Set(window, "_targetGameWindowHandle", foreground);
        object owner = EnumValue("InventoryCleanupOwner", "Kopacz533");
        Set(window, "_autoReconnectResumeKopacz533", true);
        foreach (object stage in Enum.GetValues(Nested("AutoReconnectStage")))
        {
            if (stage.ToString() == "None") continue;
            Set(window, "_autoReconnectStage", stage);
            Set(window, "_autoReconnectGeneration", 4);
            Check((bool)Call(window, "HasMiningWork", owner)!, "mining stop available during reconnect " + stage);
            Set(window, "_autoReconnectGeneration", 5);
            Check(!(bool)Call(window, "IsAutoReconnectResultCurrent", 4, stage, foreground)!, "late reconnect result rejected " + stage);
        }
        Set(window, "_autoReconnectStage", EnumValue("AutoReconnectStage", "None"));
        Check(!(bool)Call(window, "HasMiningWork", owner)!, "completed reconnect is not active work");
    }

    private static void VerifyPriority()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MinecraftHelper", "MainWindow.xaml.cs")))
            directory = directory.Parent;
        if (directory == null) throw new InvalidOperationException("Run the test from the repository.");
        string source = File.ReadAllText(Path.Combine(directory.FullName, "MinecraftHelper", "MainWindow.xaml.cs"));
        int core = source.IndexOf("private void RunMacroTickCore()", StringComparison.Ordinal);
        int stop = source.IndexOf("if (TryStopMiningFromBinds())", core, StringComparison.Ordinal);
        int reconnect = source.IndexOf("if (_autoReconnectStage != AutoReconnectStage.None)", core, StringComparison.Ordinal);
        int commands = source.IndexOf("bool internalCommandTyping", core, StringComparison.Ordinal);
        Check(stop > core && stop < reconnect && stop < commands, "STOP precedes reconnect and command guards");
    }

    private static MainWindow NewWindow()
    {
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        foreach (string name in new[]
        {
            "_inventoryCleanupTargets", "_inventoryCleanupInitialItemTypeCounts",
            "_inventoryCleanupInitialItemTypeStackCounts", "_inventoryCleanupItemTypeCounts",
            "_inventoryCleanupItemTypeStackCounts"
        })
        {
            var field = typeof(MainWindow).GetField(name, Instance)!;
            field.SetValue(window, Activator.CreateInstance(field.FieldType));
        }
        return window;
    }

    private static Type Nested(string name) => typeof(MainWindow).GetNestedType(name, BindingFlags.NonPublic)!;
    private static object EnumValue(string type, string value) => Enum.Parse(Nested(type), value);
    private static object? Get(object o, string n) => typeof(MainWindow).GetField(n, Instance)!.GetValue(o);
    private static void Set(object o, string n, object v) => typeof(MainWindow).GetField(n, Instance)!.SetValue(o, v);
    private static object? Call(object o, string n, params object[] args) => typeof(MainWindow).GetMethod(n, Instance)!.Invoke(o, args);
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _passed++;
    }
}
