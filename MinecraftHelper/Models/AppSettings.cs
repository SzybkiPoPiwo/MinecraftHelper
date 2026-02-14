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

    public class AppSettings
    {
        // Legacy fields kept for backward compatibility with old settings.json files.
        public MacroButton MacroLeftButton { get; set; } = new MacroButton();
        public MacroButton MacroRightButton { get; set; } = new MacroButton();

        public bool HoldEnabled { get; set; }
        public string HoldToggleKey { get; set; } = "";
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
        public bool PauseWhenCursorVisible { get; set; } = true;
        public string TargetWindowTitle { get; set; } = "";
        public List<string> WindowTitleHistory { get; set; } = new List<string>();
    }
}
