using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MinecraftHelper.Models;
using MinecraftHelper.Services;

namespace MinecraftHelper
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        private bool _capturingMacroLeft = false;
        private bool _capturingMacroRight = false;

        public MainWindow()
        {
            InitializeComponent();

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();

            LoadToUi();
            UpdateEnabledStates();
        }

        private void LoadToUi()
        {
            // PVP (Macro)
            ChkMacroLeftEnabled.IsChecked = _settings.MacroLeftButton.Enabled;
            TxtMacroLeftKey.Text = _settings.MacroLeftButton.Key;
            TxtMacroLeftMinCps.Text = _settings.MacroLeftButton.MinCps.ToString();
            TxtMacroLeftMaxCps.Text = _settings.MacroLeftButton.MaxCps.ToString();

            ChkMacroRightEnabled.IsChecked = _settings.MacroRightButton.Enabled;
            TxtMacroRightKey.Text = _settings.MacroRightButton.Key;
            TxtMacroRightMinCps.Text = _settings.MacroRightButton.MinCps.ToString();
            TxtMacroRightMaxCps.Text = _settings.MacroRightButton.MaxCps.ToString();

            // Kopacz 533
            ChkKopacz533Enabled.IsChecked = _settings.Kopacz533Enabled;
            RefreshKopaczCommandsUI(533);

            // Kopacz 633
            ChkKopacz633Enabled.IsChecked = _settings.Kopacz633Enabled;
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

            // Ustawienia – tylko tytuł okna
            TxtTargetWindowTitle.Text = _settings.TargetWindowTitle;
        }

        // ===== Enable/disable pól =====
        private void UpdateEnabledStates()
        {
            bool leftOn = ChkMacroLeftEnabled.IsChecked ?? false;
            TxtMacroLeftKey.IsEnabled = leftOn;
            BtnMacroLeftCapture.IsEnabled = leftOn;
            TxtMacroLeftMinCps.IsEnabled = leftOn;
            TxtMacroLeftMaxCps.IsEnabled = leftOn;

            bool rightOn = ChkMacroRightEnabled.IsChecked ?? false;
            TxtMacroRightKey.IsEnabled = rightOn;
            BtnMacroRightCapture.IsEnabled = rightOn;
            TxtMacroRightMinCps.IsEnabled = rightOn;
            TxtMacroRightMaxCps.IsEnabled = rightOn;

            bool kop533On = ChkKopacz533Enabled.IsChecked ?? false;
            PanelKopacz533Commands.IsEnabled = kop533On;
            BtnKopacz533AddCommand.IsEnabled = kop533On;

            bool kop633On = ChkKopacz633Enabled.IsChecked ?? false;
            CbKopacz633Direction.IsEnabled = kop633On;
            PanelKopaczNaWprost.IsEnabled = kop633On;
            PanelKopaczDoGory.IsEnabled = kop633On;
            PanelKopacz633Commands.IsEnabled = kop633On;
            BtnKopacz633AddCommand.IsEnabled = kop633On;
        }

        private void ChkMacroLeftEnabled_Checked(object sender, RoutedEventArgs e)
        {
            UpdateEnabledStates();
        }

        private void ChkMacroRightEnabled_Checked(object sender, RoutedEventArgs e)
        {
            UpdateEnabledStates();
        }

        private void ChkKopacz533Enabled_Changed(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz533Enabled = ChkKopacz533Enabled.IsChecked ?? false;
            UpdateEnabledStates();
        }

        private void ChkKopacz633Enabled_Changed(object sender, RoutedEventArgs e)
        {
            _settings.Kopacz633Enabled = ChkKopacz633Enabled.IsChecked ?? false;
            UpdateEnabledStates();
        }

        // ===== Kopacz – UI komend =====
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

            TextBlock label = new TextBlock { Text = "Co ile minut:", Width = 120, VerticalAlignment = VerticalAlignment.Center };
            TextBox minutesBox = new TextBox { Text = cmd.Minutes.ToString(), Width = 60, Margin = new Thickness(10, 0, 10, 0) };

            TextBlock label2 = new TextBlock { Text = "Komenda:", Width = 80, VerticalAlignment = VerticalAlignment.Center };
            TextBox commandBox = new TextBox { Text = cmd.Command, Width = 150, Margin = new Thickness(10, 0, 10, 0) };

            Button deleteBtn = new Button
            {
                Content = "Usuń",
                Width = 60,
                Margin = new Thickness(5, 0, 0, 0)
            };

            deleteBtn.Click += (s, e) =>
            {
                List<MinerCommand> list = option == 533 ? _settings.Kopacz533Commands : _settings.Kopacz633Commands;
                list.Remove(cmd);
                RefreshKopaczCommandsUI(option);
            };

            row.Children.Add(label);
            row.Children.Add(minutesBox);
            row.Children.Add(label2);
            row.Children.Add(commandBox);
            row.Children.Add(deleteBtn);

            panel.Children.Add(row);

            minutesBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(minutesBox.Text, out int min))
                    cmd.Minutes = min;
            };

            commandBox.TextChanged += (s, e) =>
            {
                cmd.Command = commandBox.Text;
            };
        }

        private void CbKopacz633Direction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelKopaczNaWprost == null || PanelKopaczDoGory == null)
                return;

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
        }

        // ===== PVP – nagrywanie klawiszy =====
        private void BtnMacroLeftCapture_Click(object sender, RoutedEventArgs e)
        {
            if (!(ChkMacroLeftEnabled.IsChecked ?? false)) return;

            _capturingMacroLeft = true;
            _capturingMacroRight = false;
            TxtMacroStatus.Text = "Naciśnij klawisz dla LEWEGO przycisku (ESC = anuluj)";
            this.Focus();
            Keyboard.Focus(this);
        }

        private void BtnMacroRightCapture_Click(object sender, RoutedEventArgs e)
        {
            if (!(ChkMacroRightEnabled.IsChecked ?? false)) return;

            _capturingMacroRight = true;
            _capturingMacroLeft = false;
            TxtMacroStatus.Text = "Naciśnij klawisz dla PRAWEGO przycisku (ESC = anuluj)";
            this.Focus();
            Keyboard.Focus(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_capturingMacroLeft && !_capturingMacroRight)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Key == Key.Escape)
            {
                _capturingMacroLeft = false;
                _capturingMacroRight = false;
                TxtMacroStatus.Text = "Nagrywanie klawisza anulowane.";
                e.Handled = true;
                return;
            }

            string keyName = e.Key.ToString();

            if (_capturingMacroLeft)
            {
                TxtMacroLeftKey.Text = keyName;
                _capturingMacroLeft = false;
                TxtMacroStatus.Text = $"Lewy przycisk: zapisano klawisz {keyName}";
            }
            else if (_capturingMacroRight)
            {
                TxtMacroRightKey.Text = keyName;
                _capturingMacroRight = false;
                TxtMacroStatus.Text = $"Prawy przycisk: zapisano klawisz {keyName}";
            }

            e.Handled = true;
            base.OnKeyDown(e);
        }

        // ===== Dodawanie komend Kopacz =====
        private void BtnKopacz533AddCommand_Click(object sender, RoutedEventArgs e)
        {
            if (!(ChkKopacz533Enabled.IsChecked ?? false)) return;

            _settings.Kopacz533Commands.Add(new MinerCommand { Minutes = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(533);
        }

        private void BtnKopacz633AddCommand_Click(object sender, RoutedEventArgs e)
        {
            if (!(ChkKopacz633Enabled.IsChecked ?? false)) return;

            _settings.Kopacz633Commands.Add(new MinerCommand { Minutes = 3, Command = "/repair" });
            RefreshKopaczCommandsUI(633);
        }

        // ===== Zapis całości =====
        private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ReadFromUi();
                _settingsService.Save(_settings);
                MessageBox.Show("✅ Wszystkie ustawienia zostały zapisane!", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd podczas zapisu: " + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReadFromUi()
        {
            // PVP
            _settings.MacroLeftButton.Enabled = ChkMacroLeftEnabled.IsChecked ?? false;
            _settings.MacroLeftButton.Key = TxtMacroLeftKey.Text;
            int.TryParse(TxtMacroLeftMinCps.Text, out int leftMin);
            int.TryParse(TxtMacroLeftMaxCps.Text, out int leftMax);
            _settings.MacroLeftButton.MinCps = leftMin;
            _settings.MacroLeftButton.MaxCps = leftMax;

            _settings.MacroRightButton.Enabled = ChkMacroRightEnabled.IsChecked ?? false;
            _settings.MacroRightButton.Key = TxtMacroRightKey.Text;
            int.TryParse(TxtMacroRightMinCps.Text, out int rightMin);
            int.TryParse(TxtMacroRightMaxCps.Text, out int rightMax);
            _settings.MacroRightButton.MinCps = rightMin;
            _settings.MacroRightButton.MaxCps = rightMax;

            // Kopacz 633
            if (CbKopacz633Direction.SelectedIndex == 1)
            {
                _settings.Kopacz633Direction = "Na wprost";
                int.TryParse(TxtKopacz633Width.Text, out int width);
                _settings.Kopacz633Width = width;
            }
            else if (CbKopacz633Direction.SelectedIndex == 2)
            {
                _settings.Kopacz633Direction = "Do góry";
                int.TryParse(TxtKopacz633WidthUp.Text, out int widthUp);
                int.TryParse(TxtKopacz633LengthUp.Text, out int lengthUp);
                _settings.Kopacz633Width = widthUp;
                _settings.Kopacz633Length = lengthUp;
            }

            // Ustawienia – tylko tytuł okna
            _settings.TargetWindowTitle = TxtTargetWindowTitle.Text;
        }
    }
}
