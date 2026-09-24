using System.Collections.Generic;

namespace MinecraftHelper.Models
{
    public class MacroButton
    {
        public bool Enabled { get; set; }
        public string Key { get; set; } = "";
        public int MinCps { get; set; }
        public int MaxCps { get; set; }
    }

    public class MinerCommand
    {
        public int Seconds { get; set; }
        public string Command { get; set; } = "";
    }

    public class BindyEntry
    {
        public string Id { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = "";
        public string Key { get; set; } = "";
        public string Command { get; set; } = "";
    }

    public class AppSettings
    {
        public string LastAcknowledgedVersion { get; set; } = "";

        // Legacy fields kept for backward compatibility with old settings.json files.
        public MacroButton MacroLeftButton { get; set; } = new MacroButton();
        public MacroButton MacroRightButton { get; set; } = new MacroButton();

        public bool HoldEnabled { get; set; }
        public string HoldToggleKey { get; set; } = "";
        public bool HoldLeftEnabled { get; set; }
        public bool HoldRightEnabled { get; set; }
        public MacroButton HoldLeftButton { get; set; } = new MacroButton();
        public MacroButton HoldRightButton { get; set; } = new MacroButton();

        public MacroButton AutoLeftButton { get; set; } = new MacroButton();
        public MacroButton AutoRightButton { get; set; } = new MacroButton();
        public bool AutoLeftComboMode { get; set; }
        public bool AutoLeftDabMode { get; set; }
        public bool AutoRightComboMode { get; set; }

        public bool Kopacz533Enabled { get; set; }
        public string Kopacz533Key { get; set; } = "";
        public List<MinerCommand> Kopacz533Commands { get; set; } = new List<MinerCommand>();

        public bool Kopacz633Enabled { get; set; }
        public string Kopacz633Key { get; set; } = "";
        public string Kopacz633Direction { get; set; } = "";
        public int Kopacz633Width { get; set; }
        public int Kopacz633Length { get; set; }
        public List<MinerCommand> Kopacz633Commands { get; set; } = new List<MinerCommand>();
        public bool InventoryCleanupEnabled { get; set; }
        public int InventoryCleanupIntervalSeconds { get; set; } = 120;
        public List<int> InventoryCleanupSlots { get; set; } = new List<int>
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16, 17,
            18, 19, 20, 21, 22, 23, 24, 25, 26
        };
        public List<string> InventoryCleanupItemTypes { get; set; } = new List<string>
        {
            "diamond", "gold_ingot", "iron_ingot", "obsidian", "apple", "sand", "gunpowder",
            "emerald", "coal", "quartz", "book", "ender_pearl", "redstone"
        };
        public bool CobbleXEnabled { get; set; }
        public string CobbleXCommand { get; set; } = "/cx";
        public int CobbleXRequiredFullStacks { get; set; } = 9;
        public bool JablkaZLisciEnabled { get; set; }
        public string JablkaZLisciKey { get; set; } = "";
        public string JablkaZLisciCommand { get; set; } = "";
        public bool BindyEnabled { get; set; }
        // Legacy bindy fields kept for compatibility with older settings.
        public string BindyKey { get; set; } = "";
        public List<MinerCommand> BindyCommands { get; set; } = new List<MinerCommand>();
        public List<BindyEntry> BindyEntries { get; set; } = new List<BindyEntry>();
        public bool PauseWhenCursorVisible { get; set; } = true;
        public bool TestEntitiesEnabled { get; set; }
        public bool TestCustomCaptureEnabled { get; set; }
        public string TestCustomCaptureBind { get; set; } = "";
        public int TestCustomCaptureX { get; set; }
        public int TestCustomCaptureY { get; set; }
        public int TestCustomCaptureWidth { get; set; }
        public int TestCustomCaptureHeight { get; set; }
        public bool TestFastUpExitEnabled { get; set; }
        public string TestFastUpExitBind { get; set; } = "";
        public int TestFastUpExitBlockSlot { get; set; } = 2;
        public int TestFastUpExitPickaxeSlot { get; set; } = 1;
        public string TestFastUpExitPickaxeType { get; set; } = "Diamentowy";
        public int TestFastUpExitLookDurationMs { get; set; } = 70;
        public bool TestFastUpExitLookDurationEnabled { get; set; } = true;
        public Dictionary<string, int> TestFastUpExitLookDurationByPickaxe { get; set; } = new Dictionary<string, int>();
        public int TestFastUpExitBreakDurationMs { get; set; } = 140;
        public bool TestFastUpExitBreakDurationEnabled { get; set; } = true;
        public Dictionary<string, int> TestFastUpExitBreakDurationByPickaxe { get; set; } = new Dictionary<string, int>();
        public int TestFastUpExitPlaceAfterJumpMs { get; set; } = 45;
        public bool TestFastUpExitPlaceAfterJumpEnabled { get; set; } = true;
        public bool TestAutoFishingEnabled { get; set; }
        public string TestAutoFishingBind { get; set; } = "";
        public string TestAutoFishingCaptureBind { get; set; } = "";
        public int TestAutoFishingCaptureX { get; set; }
        public int TestAutoFishingCaptureY { get; set; }
        public int TestAutoFishingCaptureWidth { get; set; }
        public int TestAutoFishingCaptureHeight { get; set; }
        public string TestAutoFishingRepairCommand { get; set; } = "";
        public int TestAutoFishingRepairEverySeconds { get; set; }
        public bool OverlayHudEnabled { get; set; }
        public bool OverlayAnimationsEnabled { get; set; }
        public int OverlayMonitorIndex { get; set; }
        public string OverlayCorner { get; set; } = "RightBottom";
        public string TargetWindowTitle { get; set; } = "";
        public int TargetProcessId { get; set; }
        public string TargetProcessName { get; set; } = "";
    }
}
