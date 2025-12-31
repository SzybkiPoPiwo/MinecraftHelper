using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using MinecraftHelper.Models;
using MinecraftHelper.Services;

namespace MinecraftHelper
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        // ===== MAKRO / HOOKI – NA PÓŹNIEJ =====
        /*
        private GlobalKeyboardHook? _keyboardHook;
        private GameCommandService? _gameCommandService;
        private MacroService? _macroService;

        public MacroService MacroService => _macroService!;
        */

        private bool _pendingChanges = false;
        private readonly DispatcherTimer _dirtyTimer;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        public MainWindow()
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();

            // ===== MAKRO / KOMENDY – DO WŁĄCZENIA PÓŹNIEJ =====
            /*
            _gameCommandService = new GameCommandService(_settings.TargetWindowTitle);
            _macroService = new MacroService(_settings.TargetWindowTitle);
            */

            _dirtyTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _dirtyTimer.Tick += (s, e) =>
            {
                _dirtyTimer.Stop();
                if (_pendingChanges)
                    AutoSaveSettings();
            };

            DataContext = this;

            _settings.WindowTitleHistory ??= new List<string>();

            LoadToUi();
            UpdateEnabledStates();

            //InitializeGameHooks(); // NA PÓŹNIEJ
        }

        // FOCUS MINECRAFT – sam status, bez wywołania makra
        private bool CheckGameFocus()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            StringBuilder windowTitle = new StringBuilder(256);
            GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
            string currentWindowTitle = windowTitle.ToString();

            bool focused = currentWindowTitle.Contains(_settings.TargetWindowTitle) ||
                           _settings.TargetWindowTitle.Contains(currentWindowTitle);

            TxtMinecraftFocus.Text = focused ? "✓ Tak" : "✗ Nie";
            EllMinecraftFocus.Fill = focused ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);

            return focused;
        }

        // ===== HOOKI / MAKRO – CAŁOŚĆ ZAKOMENTOWANA NA PÓŹNIEJ =====
        /*
        private void InitializeGameHooks()
        {
            try
            {
                _keyboardHook = new GlobalKeyboardHook();
                _keyboardHook.KeyPressed += OnGlobalKeyPressed;
                _keyboardHook.Install();
                UpdateStatusBar("Hooki zainstalowane - gotowy do gry", "Green");
            }
            catch (Exception ex)
            {
                UpdateStatusBar($"Błąd instalacji hooków: {ex.Message}", "Red");
            }
        }

        private void OnGlobalKeyPressed(object? sender, KeyPressedEventArgs e)
        {
            if (!CheckGameFocus())
                return;

            string pressedKey = e.KeyCode.ToString();

            // tutaj będzie logika:
            // - LPM+PPM
            // - AUTO LPM
            // - AUTO PPM
            // - Kopacz 5/3/3
            // - Kopacz 6/3/3
        }

        private async void ExecuteManual() { ... }
        private async void ExecuteAutoLeft() { ... }
        private async void ExecuteAutoRight() { ... }
        private async void ExecuteKopacz533() { ... }
        private async void ExecuteKopacz633() { ... }
        */

        private void LoadToUi()
        {
            UpdateStatusBar("Gotowy", "Green");

            // MANUAL
            ChkMacroManualEnabled.IsChecked = _settings.MacroLeftButton.Enabled;
            TxtMacroManualKey.Text = _settings.MacroLeftButton.Key;
            TxtManualLeftMinCps.Text = _settings.MacroLeftButton.MinCps.ToString();
            TxtManualLeftMaxCps.Text = _settings.MacroLeftButton.MaxCps.ToString();
            TxtManualRightMinCps.Text = _settings.MacroRightButton.MinCps.ToString();
            TxtManualRightMaxCps.Text = _settings.MacroRightButton.MaxCps.ToString();

            // AUTO – startowo kopiujemy CPS z manuala
            ChkAutoLeftEnabled.IsChecked = false;
            TxtAutoLeftKey.Text = _settings.MacroLeftButton.Key;
            TxtAutoLeftMinCps.Text = _settings.MacroLeftButton.MinCps.ToString();
            TxtAutoLeftMaxCps.Text = _settings.MacroLeftButton.MaxCps.ToString();

            ChkAutoRightEnabled.IsChecked = false;
            TxtAutoRightKey.Text = _settings.MacroRightButton.Key;
            TxtAutoRightMinCps.Text = _settings.MacroRightButton.MinCps.ToString();
            TxtAutoRightMaxCps.Text = _settings.MacroRightButton.MaxCps.ToString();

            // KOPACZ
            ChkKopacz533Enabled.IsChecked = _settings.Kopacz533Enabled;
            TxtKopacz533Key.Text = _settings.Kopacz533Key;
            RefreshKopaczCommandsUI(533);

            ChkKopacz633Enabled.IsChecked = _settings.Kopacz633Enabled;
            TxtKopacz633Key.Text = _settings.Kopacz633Key;
            TxtKopacz633Width.Text = _settings.Kopacz633Width.ToString();
            TxtKopacz633WidthUp.Text = _settings.Kopacz633Width.ToString();
            TxtKopacz633LengthUp.Text = _settings.Kopacz633Length.ToString();
            RefreshKopaczCommandsUI(633);

            if (_settings.Kopacz633Direction == "Na wprost")
                CbKopacz633Direction.SelectedIndex = 1;
            else if (_settings.Kopacz633Direction == "Do góry")
                CbKopacz633Direction.SelectedIndex = 2;
            else
                CbKopacz633Direction.SelectedIndex = 0;

            TxtTargetWindowTitle.Text = _settings.TargetWindowTitle;
            TxtCurrentWindowTitle.Text = string.IsNullOrWhiteSpace(_settings.TargetWindowTitle) ? "Brak" : _settings.TargetWindowTitle;

            // reset kulek „klikam”
            //EllManualClicking.Fill = new SolidColorBrush(Colors.Red);
            //EllAutoLeftClicking.Fill = new SolidColorBrush(Colors.Red);
            //EllAutoRightClicking.Fill = new SolidColorBrush(Colors.Red);
            //EllKopacz533Clicking.Fill = new SolidColorBrush(Colors.Red);
            //EllKopacz633Clicking.Fill = new SolidColorBrush(Colors.Red);

            RefreshWindowTitleHistory();
        }

        private void UpdateEnabledStates(object? sender = null, RoutedEventArgs? e = null)
        {
            bool manualOn = ChkMacroManualEnabled.IsChecked ?? false;
            TxtMacroManualKey.IsEnabled = manualOn;
            BtnMacroManualCapture.IsEnabled = manualOn;
            TxtManualLeftMinCps.IsEnabled = manualOn;
            TxtManualLeftMaxCps.IsEnabled = manualOn;
            TxtManualRightMinCps.IsEnabled = manualOn;
            TxtManualRightMaxCps.IsEnabled = manualOn;

            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            TxtAutoLeftKey.IsEnabled = autoLeftOn;
            BtnAutoLeftCapture.IsEnabled = autoLeftOn;
            TxtAutoLeftMinCps.IsEnabled = autoLeftOn;
            TxtAutoLeftMaxCps.IsEnabled = autoLeftOn;

            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;
            TxtAutoRightKey.IsEnabled = autoRightOn;
            BtnAutoRightCapture.IsEnabled = autoRightOn;
            TxtAutoRightMinCps.IsEnabled = autoRightOn;
            TxtAutoRightMaxCps.IsEnabled = autoRightOn;

            bool kop533On = ChkKopacz533Enabled.IsChecked ?? false;
            TxtKopacz533Key.IsEnabled = kop533On;
            BtnKopacz533Capture.IsEnabled = kop533On;
            PanelKopacz533Commands.IsEnabled = kop533On;
            BtnKopacz533AddCommand.IsEnabled = kop533On;

            bool kop633On = ChkKopacz633Enabled.IsChecked ?? false;
            TxtKopacz633Key.IsEnabled = kop633On;
            BtnKopacz633Capture.IsEnabled = kop633On;
            CbKopacz633Direction.IsEnabled = kop633On;
            PanelKopaczNaWprost.IsEnabled = kop633On;
            PanelKopaczDoGory.IsEnabled = kop633On;
            PanelKopacz633Commands.IsEnabled = kop633On;
            BtnKopacz633AddCommand.IsEnabled = kop633On;

            // ===== PODŚWIETLANIE KAFELEK U GÓRY =====
            Brush activeBg = new SolidColorBrush(Color.FromRgb(220, 245, 255));   // jasny niebieski
            Brush inactiveBg = new SolidColorBrush(Colors.White);

            // LPM+PPM
            BorderManualStatus.Background = manualOn ? activeBg : inactiveBg;
            BorderManualStatus.Opacity = manualOn ? 1.0 : 0.6;

            // AUTO LPM
            BorderAutoLeftStatus.Background = autoLeftOn ? activeBg : inactiveBg;
            BorderAutoLeftStatus.Opacity = autoLeftOn ? 1.0 : 0.6;

            // AUTO PPM
            BorderAutoRightStatus.Background = autoRightOn ? activeBg : inactiveBg;
            BorderAutoRightStatus.Opacity = autoRightOn ? 1.0 : 0.6;

            // KOPACZ 5/3/3
            BorderKopacz533Status.Background = kop533On ? activeBg : inactiveBg;
            BorderKopacz533Status.Opacity = kop533On ? 1.0 : 0.6;

            // KOPACZ 6/3/3
            BorderKopacz633Status.Background = kop633On ? activeBg : inactiveBg;
            BorderKopacz633Status.Opacity = kop633On ? 1.0 : 0.6;
        }

        private void RefreshWindowTitleHistory()
        {
            PanelWindowTitleHistory.Children.Clear();

            if (_settings.WindowTitleHistory == null || _settings.WindowTitleHistory.Count == 0)
            {
                PanelWindowTitleHistory.Children.Add(
                    new TextBlock
                    {
                        Text = "Brak historii",
                        Foreground = new SolidColorBrush(Colors.Gray),
                        Margin = new Thickness(0, 5, 0, 0)
                    });
                return;
            }

            foreach (var title in _settings.WindowTitleHistory)
                AddHistoryItem(title);
        }

        private void AddHistoryItem(string title)
        {
            Border border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Colors.White)
            };

            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };

            TextBlock textBlock = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Black),
                TextWrapping = TextWrapping.Wrap,
                Width = 200,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Button selectBtn = new Button
            {
                Content = "Ustaw",
                Width = 70,
                Height = 28,
                Padding = new Thickness(5, 0, 5, 0),
                FontSize = 11
            };

            selectBtn.Click += (s, e) =>
            {
                TxtTargetWindowTitle.Text = title;
                TxtCurrentWindowTitle.Text = title;
                MarkDirty();
            };

            stack.Children.Add(textBlock);
            stack.Children.Add(selectBtn);
            border.Child = stack;

            PanelWindowTitleHistory.Children.Add(border);
        }

        private void AddToWindowTitleHistory(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return;

            _settings.WindowTitleHistory.Remove(title);
            _settings.WindowTitleHistory.Insert(0, title);

            if (_settings.WindowTitleHistory.Count > 5)
                _settings.WindowTitleHistory.RemoveAt(_settings.WindowTitleHistory.Count - 1);

            RefreshWindowTitleHistory();
        }

        private void RefreshKopaczCommandsUI(int option)
        {
            StackPanel panel = option == 533 ? PanelKopacz533Commands : PanelKopacz633Commands;
            List<MinerCommand> commands = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
            panel.Children.Clear();

            foreach (var cmd in commands)
                AddCommandRow(panel, cmd, option);
        }

        private void AddCommandRow(StackPanel panel, MinerCommand cmd, int option)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            TextBlock label = new TextBlock { Text = "Co ile sekund:", Width = 120, VerticalAlignment = VerticalAlignment.Center };
            TextBox secondsBox = new TextBox { Text = cmd.Seconds.ToString(), Width = 60, Margin = new Thickness(10, 0, 10, 0) };

            TextBlock label2 = new TextBlock { Text = "Komenda:", Width = 80, VerticalAlignment = VerticalAlignment.Center };
            TextBox commandBox = new TextBox { Text = cmd.Command, Width = 150, Margin = new Thickness(10, 0, 10, 0) };

            Button deleteBtn = new Button { Content = "Usuń", Width = 60, Margin = new Thickness(5, 0, 0, 0) };

            deleteBtn.Click += (s, e) =>
            {
                List<MinerCommand> list = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
                list.Remove(cmd);
                RefreshKopaczCommandsUI(option);
                MarkDirty();
            };

            row.Children.Add(label);
            row.Children.Add(secondsBox);
            row.Children.Add(label2);
            row.Children.Add(commandBox);
            row.Children.Add(deleteBtn);

            panel.Children.Add(row);

            secondsBox.TextChanged += (s, e) =>
            {
                MarkDirty();
                if (int.TryParse(secondsBox.Text, out int sec))
                    cmd.Seconds = sec;
            };

            commandBox.TextChanged += (s, e) =>
            {
                cmd.Command = commandBox.Text;
                MarkDirty();
            };
        }

        private void CbKopacz633Direction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelKopaczNaWprost == null || PanelKopaczDoGory == null) return;

            if (CbKopacz633Direction.SelectedIndex == 0)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Collapsed;
                PanelKopaczDoGory.Visibility = Visibility.Collapsed;
            }
            else if (CbKopacz633Direction.SelectedIndex == 1)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Visible;
                PanelKopaczDoGory.Visibility = Visibility.Collapsed;
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                PanelKopaczNaWprost.Visibility = Visibility.Collapsed;
                PanelKopaczDoGory.Visibility = Visibility.Visible;
            }

            MarkDirty();
        }

        private void BtnMacroManualCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: LPM+PPM - naciśnij klawisz (logika do zrobienia)", "Red");
            Focus();
        }

        private void BtnAutoLeftCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: AUTO LPM - naciśnij klawisz (logika do zrobienia)", "Red");
            Focus();
        }

        private void BtnAutoRightCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: AUTO PPM - naciśnij klawisz (logika do zrobienia)", "Red");
            Focus();
        }

        private void BtnKopacz533Capture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: Kopacz 5/3/3 - naciśnij klawisz (logika do zrobienia)", "Red");
            Focus();
        }

        private void BtnKopacz633Capture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: Kopacz 6/3/3 - naciśnij klawisz (logika do zrobienia)", "Red");
            Focus();
        }

        // DODAWANIE KOMEND

        private void BtnKopacz533AddCommand_Click(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz533Commands.Add(new MinerCommand { Seconds = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(533);
            MarkDirty();
        }

        private void BtnKopacz633AddCommand_Click(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz633Commands.Add(new MinerCommand { Seconds = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(633);
            MarkDirty();
        }

        // AUTO-SAVE

        private void AnyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            MarkDirty();
        }

        private void MarkDirty()
        {
            _pendingChanges = true;
            TxtSettingsSaved.Text = "✗ Nie";
            EllSettingsSaved.Fill = new SolidColorBrush(Colors.Red);
            _dirtyTimer.Stop();
            _dirtyTimer.Start();
        }

        private void AutoSaveSettings()
        {
            try
            {
                ReadFromUi();
                AddToWindowTitleHistory(TxtTargetWindowTitle.Text);
                _settingsService.Save(_settings);

                _pendingChanges = false;
                TxtSettingsSaved.Text = "✓ Tak";
                EllSettingsSaved.Fill = new SolidColorBrush(Colors.Green);
                UpdateStatusBar("Ustawienia zapisane", "Green");
            }
            catch (Exception ex)
            {
                _pendingChanges = true;
                TxtSettingsSaved.Text = "Błąd";
                EllSettingsSaved.Fill = new SolidColorBrush(Colors.Red);
                UpdateStatusBar("Błąd zapisu: " + ex.Message, "Red");
            }
        }

        private void ReadFromUi()
        {
            // MANUAL
            _settings.MacroLeftButton.Enabled = ChkMacroManualEnabled.IsChecked ?? false;
            _settings.MacroLeftButton.Key = TxtMacroManualKey.Text;

            if (int.TryParse(TxtManualLeftMinCps.Text, out int manualLeftMin))
                _settings.MacroLeftButton.MinCps = manualLeftMin;
            if (int.TryParse(TxtManualLeftMaxCps.Text, out int manualLeftMax))
                _settings.MacroLeftButton.MaxCps = manualLeftMax;
            if (int.TryParse(TxtManualRightMinCps.Text, out int manualRightMin))
                _settings.MacroRightButton.MinCps = manualRightMin;
            if (int.TryParse(TxtManualRightMaxCps.Text, out int manualRightMax))
                _settings.MacroRightButton.MaxCps = manualRightMax;

            // AUTO
            if (int.TryParse(TxtAutoLeftMinCps.Text, out int autoLeftMin))
                _settings.MacroLeftButton.MinCps = autoLeftMin;
            if (int.TryParse(TxtAutoLeftMaxCps.Text, out int autoLeftMax))
                _settings.MacroLeftButton.MaxCps = autoLeftMax;
            if (int.TryParse(TxtAutoRightMinCps.Text, out int autoRightMin))
                _settings.MacroRightButton.MinCps = autoRightMin;
            if (int.TryParse(TxtAutoRightMaxCps.Text, out int autoRightMax))
                _settings.MacroRightButton.MaxCps = autoRightMax;

            // KOPACZ
            _settings.Kopacz533Enabled = ChkKopacz533Enabled.IsChecked ?? false;
            _settings.Kopacz633Enabled = ChkKopacz633Enabled.IsChecked ?? false;
            _settings.Kopacz533Key = TxtKopacz533Key.Text;
            _settings.Kopacz633Key = TxtKopacz633Key.Text;

            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                _settings.Kopacz633Direction = "Na wprost";
                if (int.TryParse(TxtKopacz633Width.Text, out int width))
                    _settings.Kopacz633Width = width;
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                _settings.Kopacz633Direction = "Do góry";
                if (int.TryParse(TxtKopacz633WidthUp.Text, out int widthUp))
                    _settings.Kopacz633Width = widthUp;
                if (int.TryParse(TxtKopacz633LengthUp.Text, out int lengthUp))
                    _settings.Kopacz633Length = lengthUp;
            }
            else
            {
                _settings.Kopacz633Direction = "";
            }

            _settings.TargetWindowTitle = TxtTargetWindowTitle.Text;
        }

        private void UpdateStatusBar(string message, string colorName)
        {
            if (TxtStatusBar == null) return;

            TxtStatusBar.Text = message;
            TxtStatusBar.Foreground =
                colorName == "Red" ? new SolidColorBrush(Colors.Red) :
                colorName == "Green" ? new SolidColorBrush(Colors.Green) :
                colorName == "Orange" ? new SolidColorBrush(Colors.Orange) :
                new SolidColorBrush(Colors.Black);
        }

        private void ChkMacroManualEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // jeśli włączamy LPM+PPM HOLD, to wyłącz AUTO LPM i AUTO PPM
            if (ChkMacroManualEnabled.IsChecked == true)
            {
                ChkAutoLeftEnabled.IsChecked = false;
                ChkAutoRightEnabled.IsChecked = false;
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoLeftEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // jeśli włączamy AUTO LPM, to wyłącz LPM+PPM HOLD
            if (ChkAutoLeftEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoRightEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // jeśli włączamy AUTO PPM, to wyłącz LPM+PPM HOLD
            if (ChkAutoRightEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz533Enabled_Changed(object sender, RoutedEventArgs e)
        {
            // tylko jeden kopacz naraz
            if (ChkKopacz533Enabled.IsChecked == true)
            {
                ChkKopacz633Enabled.IsChecked = false;
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz633Enabled_Changed(object sender, RoutedEventArgs e)
        {
            // tylko jeden kopacz naraz
            if (ChkKopacz633Enabled.IsChecked == true)
            {
                ChkKopacz533Enabled.IsChecked = false;
            }

            UpdateEnabledStates();
            MarkDirty();
        }

        // IMPORT / EXPORT
        private void BtnExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON (*.json)|*.json",
                FileName = "minecraft_helper_settings.json"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ReadFromUi();
                    _settingsService.ExportToFile(_settings, dlg.FileName);
                    UpdateStatusBar("Ustawienia wyeksportowane", "Green");
                }
                catch (Exception ex)
                {
                    UpdateStatusBar("Błąd eksportu: " + ex.Message, "Red");
                }
            }
        }

        private void BtnImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON (*.json)|*.json"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _settings = _settingsService.ImportFromFile(dlg.FileName);
                    //_gameCommandService = new GameCommandService(_settings.TargetWindowTitle);
                    //_macroService = new MacroService(_settings.TargetWindowTitle);
                    LoadToUi();
                    UpdateEnabledStates();
                    UpdateStatusBar("Ustawienia wczytane", "Green");
                }
                catch (Exception ex)
                {
                    UpdateStatusBar("Błąd importu: " + ex.Message, "Red");
                }
            }
        }

        // KLIKALNY GITHUB
        private void Github_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _dirtyTimer.Stop();
            //_macroService?.Stop();
            //_keyboardHook?.Uninstall();
            base.OnClosed(e);
        }
    }
}
