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
        // Legacy fields kept for backward compatibility with old settings.json files.
        public MacroButton MacroLeftButton { get; set; } = new MacroButton();
        public MacroButton MacroRightButton { get; set; } = new MacroButton();

        public bool HoldEnabled { get; set; }
        public string HoldToggleKey { get; set; } = "";
        public bool HoldLeftEnabled { get; set; } = true;
        public bool HoldRightEnabled { get; set; } = true;
        public MacroButton HoldLeftButton { get; set; } = new MacroButton();
        public MacroButton HoldRightButton { get; set; } = new MacroButton();

        public MacroButton AutoLeftButton { get; set; } = new MacroButton();
        public MacroButton AutoRightButton { get; set; } = new MacroButton();

        public bool Kopacz533Enabled { get; set; }
        public string Kopacz533Key { get; set; } = "";
        public List<MinerCommand> Kopacz533Commands { get; set; } = new List<MinerCommand>();

        public bool Kopacz633Enabled { get; set; }
        public string Kopacz633Key { get; set; } = "";
        public string Kopacz633Direction { get; set; } = "";
        public int Kopacz633Width { get; set; }
        public int Kopacz633Length { get; set; }
        public List<MinerCommand> Kopacz633Commands { get; set; } = new List<MinerCommand>();
        public bool JablkaZLisciEnabled { get; set; }
        public string JablkaZLisciKey { get; set; } = "";
        public string JablkaZLisciCommand { get; set; } = "";
        public bool BindyEnabled { get; set; }
        // Legacy bindy fields kept for compatibility with older settings.
        public string BindyKey { get; set; } = "";
        public List<MinerCommand> BindyCommands { get; set; } = new List<MinerCommand>();
        public List<BindyEntry> BindyEntries { get; set; } = new List<BindyEntry>();
        public bool PauseWhenCursorVisible { get; set; } = true;
        public bool TestEntitiesEnabled { get; set; } = true;
        public bool TestCustomCaptureEnabled { get; set; }
        public string TestCustomCaptureBind { get; set; } = "";
        public int TestCustomCaptureX { get; set; }
        public int TestCustomCaptureY { get; set; }
        public int TestCustomCaptureWidth { get; set; }
        public int TestCustomCaptureHeight { get; set; }
        public bool OverlayHudEnabled { get; set; } = true;
        public bool OverlayAnimationsEnabled { get; set; } = true;
        public int OverlayMonitorIndex { get; set; }
        public string OverlayCorner { get; set; } = "RightBottom";
        public string TargetWindowTitle { get; set; } = "";
        public int TargetProcessId { get; set; }
        public string TargetProcessName { get; set; } = "";
    }
}
