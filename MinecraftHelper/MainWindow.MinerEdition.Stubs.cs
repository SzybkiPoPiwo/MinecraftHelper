using System.Windows.Controls;

namespace MinecraftHelper
{
    public partial class MainWindow
    {
#if MINER_EDITION
        // Odłączone kontrolki zachowują zgodność kodu i importu starszych ustawień.
        // Nie należą do drzewa wizualnego, nie mają zdarzeń i nie uruchamiają modułów PvP.
        private Border BorderAutoLeftSection = new();
        private Border BorderAutoRightSection = new();
        private Border BorderBindySection = new();
        private Border BorderCursorPauseSection = new();
        private Border BorderJablkaSection = new();
        private Border BorderManualBindSection = new();
        private Border BorderManualLeftSection = new();
        private Border BorderManualRightSection = new();
        private Button BtnAutoArmorBind = new();
        private Button BtnAutoArmorBindClear = new();
        private Button BtnAutoArmorEquippedBoots = new();
        private Button BtnAutoArmorEquippedChestplate = new();
        private Button BtnAutoArmorEquippedHelmet = new();
        private Button BtnAutoArmorEquippedLeggings = new();
        private Button BtnAutoArmorInventoryBoots = new();
        private Button BtnAutoArmorInventoryChestplate = new();
        private Button BtnAutoArmorInventoryHelmet = new();
        private Button BtnAutoArmorInventoryLeggings = new();
        private Button BtnAutoLeftCapture = new();
        private Button BtnAutoLeftClear = new();
        private Button BtnAutoRightCapture = new();
        private Button BtnAutoRightClear = new();
        private Button BtnAutoWaterBind = new();
        private Button BtnAutoWaterBindClear = new();
        private Button BtnBindyAddCommand = new();
        private Button BtnJablkaZLisciCapture = new();
        private Button BtnJablkaZLisciClear = new();
        private Button BtnMacroManualCapture = new();
        private Button BtnMacroManualClear = new();
        private Button BtnSaveJablkaZLisciCommand = new();
        private ComboBox CbAutoWaterReturnSlot = new();
        private ComboBox CbAutoWaterWaterSlot = new();
        private CheckBox ChkAutoArmorEnabled = new();
        private CheckBox ChkAutoLeftComboMode = new();
        private CheckBox ChkAutoLeftDabMode = new();
        private CheckBox ChkAutoLeftEnabled = new();
        private CheckBox ChkAutoLeftHoldBindMode = new();
        private CheckBox ChkAutoRightComboMode = new();
        private CheckBox ChkAutoRightEnabled = new();
        private CheckBox ChkAutoRightHoldBindMode = new();
        private CheckBox ChkAutoWaterEnabled = new();
        private CheckBox ChkBindyEnabled = new();
        private CheckBox ChkHoldLeftEnabled = new();
        private CheckBox ChkHoldRightEnabled = new();
        private CheckBox ChkJablkaZLisciEnabled = new();
        private CheckBox ChkMacroManualEnabled = new();
        private CheckBox ChkPauseWhenCursorVisible = new();
        private StackPanel PanelAutoArmorContent = new();
        private StackPanel PanelAutoArmorExpandableContent = new();
        private StackPanel PanelAutoLeftContent = new();
        private StackPanel PanelAutoRightContent = new();
        private StackPanel PanelAutoWaterContent = new();
        private StackPanel PanelAutoWaterExpandableContent = new();
        private StackPanel PanelBindyCommands = new();
        private StackPanel PanelBindyContent = new();
        private StackPanel PanelCursorPauseContent = new();
        private StackPanel PanelJablkaContent = new();
        private Slider SlAutoWaterSimilarity = new();
        private TextBox TxtAutoArmorBind = new();
        private TextBox TxtAutoArmorClickDelayMs = new();
        private TextBlock TxtAutoArmorStatus = new();
        private TextBox TxtAutoLeftKey = new();
        private TextBox TxtAutoLeftMaxCps = new();
        private TextBox TxtAutoLeftMinCps = new();
        private TextBox TxtAutoRightKey = new();
        private TextBox TxtAutoRightMaxCps = new();
        private TextBox TxtAutoRightMinCps = new();
        private TextBox TxtAutoWaterBind = new();
        private TextBox TxtAutoWaterPickupDelayMs = new();
        private TextBox TxtAutoWaterPlaceIntervalMs = new();
        private TextBlock TxtAutoWaterSimilarityValue = new();
        private TextBlock TxtAutoWaterStatus = new();
        private TextBox TxtJablkaZLisciCommand = new();
        private TextBox TxtJablkaZLisciKey = new();
        private TextBox TxtMacroManualKey = new();
        private TextBox TxtManualLeftMaxCps = new();
        private TextBox TxtManualLeftMinCps = new();
        private TextBox TxtManualRightMaxCps = new();
        private TextBox TxtManualRightMinCps = new();
#endif
    }
}

