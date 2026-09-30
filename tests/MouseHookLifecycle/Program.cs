using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using MinecraftHelper;

// Windows-only lifecycle checks. Do not construct/show a WPF window, load/save
// user settings, send input or launch Minecraft. Only register/unregister the
// real native hook and feed synthetic data directly into its state handler.
internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        Set(window, "_mouseHookLifecycleSync", new object());
        Set(window, "_mouseHookThreadReady", new ManualResetEventSlim(false));
        Set(window, "_mouseHookStateChanged", new ManualResetEventSlim(false));

        // Suppress diagnostics output too: the test must not touch AppData.
        Type diagnosticsType = Field(window, "_macroDiagnosticsService").FieldType;
        object diagnostics = RuntimeHelpers.GetUninitializedObject(diagnosticsType);
        Set(diagnostics, "_stateSync", new object());
        Set(diagnostics, "_disposed", true);
        Set(window, "_macroDiagnosticsService", diagnostics);

        try
        {
            Call(window, "InitializeMouseHookThread");
            var thread = (Thread)Get(window, "_mouseHookThread")!;
            Check(thread.IsAlive, "message thread is ready");
            Check(Handle(window) == IntPtr.Zero, "startup has no global hook");
            for (int i = 0; i < 20; i++) Call(window, "SuspendMouseHookWhenIdle");
            Check(Handle(window) == IntPtr.Zero, "idle ticks do not install a hook");

            var activation = Stopwatch.StartNew();
            Call(window, "StartMouseHook");
            activation.Stop();
            Check(Handle(window) != IntPtr.Zero, "activation registers the native hook");
            Console.WriteLine($"Hook activation acknowledgement: {activation.Elapsed.TotalMilliseconds:F3} ms");
            IntPtr firstHandle = Handle(window);
            Call(window, "StartMouseHook");
            Check(Handle(window) == firstHandle, "repeated activation preserves registration");

            foreach (string field in new[]
            {
                "_holdMacroRuntimeEnabled", "_autoLeftRuntimeEnabled", "_autoRightRuntimeEnabled",
                "_jablkaRuntimeEnabled", "_kopacz533RuntimeEnabled", "_kopacz633RuntimeEnabled",
                "_testFastUpExitRuntimeEnabled", "_testAutoFishingRuntimeEnabled",
                "_holdRightInjectedButtonDown", "_inventoryCleanupEatingRightButtonDown",
                "_kopacz533Holding", "_kopacz633HoldingAttack",
                "_testFastUpExitBreakHoldActive", "_testFastUpExitPlaceHoldActive"
            })
            {
                Set(window, field, true);
                Call(window, "SuspendMouseHookWhenIdle");
                Check(Handle(window) == firstHandle, $"keeps physical state while {field}");
                Set(window, field, false);
            }
            foreach (string field in new[] { "_inventoryCleanupStage", "_autoReconnectStage" })
            {
                Type type = Field(window, field).FieldType;
                foreach (object value in Enum.GetValues(type))
                {
                    if (Convert.ToInt32(value) == 0) continue;
                    Set(window, field, value);
                    Call(window, "SuspendMouseHookWhenIdle");
                    Check(Handle(window) == firstHandle, $"keeps hook during {field}={value}");
                }
                Set(window, field, Enum.ToObject(type, 0));
            }

            // No native SendInput: verify that injected UP/DOWN cannot alter
            // the state used by bind+LPM/PPM and HOLD activation/release.
            foreach ((int key, int down, int up) in new[] { (1, 0x201, 0x202), (2, 0x204, 0x205) })
            {
                MouseEvent(window, down, injected: false);
                Check((bool)Call(window, "IsPhysicalMouseButtonDown", key)!, $"physical DOWN ({key})");
                MouseEvent(window, up, injected: true);
                Check((bool)Call(window, "IsPhysicalMouseButtonDown", key)!, $"ignores injected UP ({key})");
                MouseEvent(window, up, injected: false);
                Check(!(bool)Call(window, "IsPhysicalMouseButtonDown", key)!, $"physical release ({key})");
                MouseEvent(window, down, injected: true);
                Check(!(bool)Call(window, "IsPhysicalMouseButtonDown", key)!, $"ignores injected DOWN ({key})");
            }

            for (int cycle = 0; cycle < 25; cycle++)
            {
                Call(window, "SuspendMouseHookWhenIdle");
                Check(Handle(window) == IntPtr.Zero, $"idle detaches hook, cycle {cycle}");
                Check(ReferenceEquals(thread, Get(window, "_mouseHookThread")) && thread.IsAlive,
                    $"idle retains the sleeping message thread, cycle {cycle}");
                Call(window, "StartMouseHook");
                Check(Handle(window) != IntPtr.Zero, $"can reactivate, cycle {cycle}");
            }
            Call(window, "StopMouseHook");
            Check(Handle(window) == IntPtr.Zero && !thread.IsAlive, "shutdown removes hook and thread");
            Console.WriteLine($"PASS: {_passed} checks; no user settings or input changed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            Call(window, "StopMouseHook");
        }
    }

    private static void MouseEvent(MainWindow window, int message, bool injected)
    {
        Type type = typeof(MainWindow).GetNestedType("MSLLHOOKSTRUCT", BindingFlags.NonPublic)!;
        object data = Activator.CreateInstance(type)!;
        type.GetField("flags")!.SetValue(data, injected ? 1u : 0u);
        Call(window, "ProcessPhysicalMouseEvent", message, data);
    }

    private static IntPtr Handle(MainWindow window) => (IntPtr)Call(window, "GetMouseHookHandle")!;
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PrivateInstance)!;
    private static object? Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static object? Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, args);
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        _passed++;
    }
}
