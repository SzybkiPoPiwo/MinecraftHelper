using System.Collections.Generic;

namespace MinecraftHelper.Models
{
    public class MacroButton
    {
        public string Key { get; set; } = "R";
        public bool Enabled { get; set; } = false;
        public int MinCps { get; set; } = 10;
        public int MaxCps { get; set; } = 10;
    }

    public class MinerCommand
    {
        public int Minutes { get; set; } = 3;
        public string Command { get; set; } = "/repair";
    }

    public class AppSettings
    {
        // ========== PVP (Macro) ==========
        public MacroButton MacroLeftButton { get; set; } = new MacroButton
        {
            Key = "R",
            Enabled = false,
            MinCps = 10,
            MaxCps = 10
        };

        public MacroButton MacroRightButton { get; set; } = new MacroButton
        {
            Key = "L",
            Enabled = false,
            MinCps = 50,
            MaxCps = 50
        };

        // ========== KOPACZ ==========
        public bool Kopacz533Enabled { get; set; } = false;
        public List<MinerCommand> Kopacz533Commands { get; set; } = new List<MinerCommand>
        {
            new MinerCommand { Minutes = 3, Command = "/repair" }
        };

        public bool Kopacz633Enabled { get; set; } = false;
        public string Kopacz633Direction { get; set; } = "";
        public int Kopacz633Width { get; set; } = 6;
        public int Kopacz633Length { get; set; } = 6;
        public List<MinerCommand> Kopacz633Commands { get; set; } = new List<MinerCommand>
        {
            new MinerCommand { Minutes = 3, Command = "/repair" }
        };

        // ========== BINDY ==========
        public string BindEatWhileRunning { get; set; } = "R";
        public string BindThrowPearl { get; set; } = "T";

        // ========== USTAWIENIA ==========
        public string TargetWindowTitle { get; set; } = "Minecraft 1.8.8";
        public string UiLanguage { get; set; } = "pl";
        public int UiGuiSize { get; set; } = 1;
    }
}
