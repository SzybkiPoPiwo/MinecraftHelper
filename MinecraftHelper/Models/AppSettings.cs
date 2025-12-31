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
        public MacroButton MacroLeftButton { get; set; } = new MacroButton();
        public MacroButton MacroRightButton { get; set; } = new MacroButton();

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
        public bool PauseWhenCursorVisible { get; set; } = true;
        public string TargetWindowTitle { get; set; } = "";
        public List<string> WindowTitleHistory { get; set; } = new List<string>();
    }
}
