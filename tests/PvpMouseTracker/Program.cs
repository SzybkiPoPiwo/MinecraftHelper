using System.Diagnostics;
using MinecraftHelper.Services;

internal static class Program
{
    private static int Main()
    {
        using var tracker = new PhysicalMouseStateTracker();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            for (int cycle = 0; cycle < 10; cycle++)
            {
                if (!tracker.Start() || !tracker.IsMonitoring)
                    throw new InvalidOperationException($"Nie uruchomiono trackera (Win32: {tracker.LastError}, cykl: {cycle}).");

                tracker.Stop();
                if (tracker.IsMonitoring)
                    throw new InvalidOperationException($"Tracker pozostał aktywny po zatrzymaniu, cykl: {cycle}.");
            }

            stopwatch.Stop();
            Console.WriteLine($"PASS: 10 cykli start/stop trackera w {stopwatch.Elapsed.TotalMilliseconds:F1} ms; nie wysłano żadnego wejścia.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);
            return 1;
        }
    }
}
