using System.Collections.Generic;

namespace MinecraftHelper.Models;

internal sealed class MacroButton
{
    public bool Enabled { get; set; }
    public string Key { get; set; } = string.Empty;
    public int MinCps { get; set; }
    public int MaxCps { get; set; }
}

internal sealed class BindyEntry
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

internal sealed class RelativeScreenPointSetting
{
    public bool Configured { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
}

internal sealed class PvpSettings
{
    public string LastAcknowledgedVersion { get; set; } = string.Empty;
    public MacroButton AutoLeftButton { get; set; } = new();
    public MacroButton AutoRightButton { get; set; } = new();
    public bool AutoLeftComboMode { get; set; }
    public bool AutoLeftHoldBindMode { get; set; }
    public bool AutoLeftPhysicalToggleMode { get; set; }
    public bool AutoLeftDabMode { get; set; }
    public bool AutoRightComboMode { get; set; }
    public bool AutoRightHoldBindMode { get; set; }
    public bool AutoRightPhysicalToggleMode { get; set; }
    public bool BindyEnabled { get; set; }
    public List<BindyEntry> BindyEntries { get; set; } = new();
    public bool PauseWhenCursorVisible { get; set; } = true;
    public bool OverlayHudEnabled { get; set; }
    public string ChatOpenKey { get; set; } = "T";

    public string PvpAutoBlockBind { get; set; } = string.Empty;
    public string PvpSnowballBind { get; set; } = string.Empty;
    public string PvpPearlBind { get; set; } = string.Empty;
    public string PvpRodBind { get; set; } = string.Empty;
    public int PvpSwordSlot { get; set; } = 1;
    public int PvpBlockSlot { get; set; } = 5;
    public int PvpSnowballSlot { get; set; } = 2;
    public int PvpPearlSlot { get; set; } = 3;
    public int PvpRodSlot { get; set; } = 4;
    public int PvpRodRightClickHoldMs { get; set; } = 80;

    public bool AppleCutterEnabled { get; set; }
    public string AppleCutterBind { get; set; } = string.Empty;
    public int AppleCutterShearsSlot { get; set; } = 1;
    public int AppleCutterLeavesSlot { get; set; } = 2;
    public int AppleCutterSwitchMinMs { get; set; } = 35;
    public int AppleCutterSwitchMaxMs { get; set; } = 55;
    public int AppleCutterBreakMinMs { get; set; } = 90;
    public int AppleCutterBreakMaxMs { get; set; } = 140;
    public int AppleCutterPlaceMinMs { get; set; } = 50;
    public int AppleCutterPlaceMaxMs { get; set; } = 90;
    public int AppleCutterRepairEveryCycles { get; set; } = 40;
    public string AppleCutterRepairCommand { get; set; } = "/repair";

    public bool AutoWaterEnabled { get; set; }
    public string AutoWaterBind { get; set; } = string.Empty;
    public int AutoWaterWaterSlot { get; set; } = 9;
    public int AutoWaterReturnSlot { get; set; } = 1;
    public int AutoWaterPlaceIntervalMs { get; set; } = 65;
    public int AutoWaterPickupDelayMs { get; set; } = 340;
    public int AutoWaterSimilarityPercent { get; set; } = 84;
    public int AutoWaterSampleOffsetX { get; set; }
    public int AutoWaterSampleOffsetFromBottom { get; set; }
    public int AutoWaterSamplePatchSize { get; set; } = 9;
    public string AutoWaterEmptyBucketTemplateBase64 { get; set; } = string.Empty;

    public bool AutoArmorEnabled { get; set; }
    public string AutoArmorBind { get; set; } = string.Empty;
    public int AutoArmorClickDelayMs { get; set; } = 45;
    public List<RelativeScreenPointSetting> AutoArmorEquippedSlots { get; set; } = new();
    public List<RelativeScreenPointSetting> AutoArmorInventorySlots { get; set; } = new();

    public string TargetWindowTitle { get; set; } = string.Empty;
    public int TargetProcessId { get; set; }
    public string TargetProcessName { get; set; } = string.Empty;
}
