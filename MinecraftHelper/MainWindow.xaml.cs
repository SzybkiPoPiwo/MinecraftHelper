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
using System.Windows.Interop;

namespace MinecraftHelper
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _settings;

        private bool _pendingChanges = false;
        private readonly DispatcherTimer _dirtyTimer;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void ApplyDarkTitleBar()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            int useDark = 1;
            int attr = Environment.OSVersion.Version.Build >= 18985
                ? DWMWA_USE_IMMERSIVE_DARK_MODE
                : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;

            _ = DwmSetWindowAttribute(hwnd, attr, ref useDark, sizeof(int));
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => ApplyDarkTitleBar();

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();

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
            RefreshTopTiles(); // <-- NOWE: odśwież kafelki po starcie
        }

        // FOCUS MINECRAFT
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
            EllMinecraftFocus.Fill = focused
                ? new SolidColorBrush(Color.FromRgb(78, 201, 176))
                : new SolidColorBrush(Color.FromRgb(255, 85, 85));

            return focused;
        }

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

            // AUTO
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

            RefreshWindowTitleHistory();

            // JABŁKA Z LIŚCI
            ChkJablkaZLisciEnabled.IsChecked = _settings.JablkaZLisciEnabled;
            TxtJablkaZLisciKey.Text = _settings.JablkaZLisciKey;
        }

        // NOWE: odświeżanie kafelków CPS u góry
        private void RefreshTopTiles()
        {
            // MANUAL
            int manualLeftMin = int.TryParse(TxtManualLeftMinCps.Text, out var mlMin) ? mlMin : 0;
            int manualLeftMax = int.TryParse(TxtManualLeftMaxCps.Text, out var mlMax) ? mlMax : 0;
            int manualRightMin = int.TryParse(TxtManualRightMinCps.Text, out var mrMin) ? mrMin : 0;
            int manualRightMax = int.TryParse(TxtManualRightMaxCps.Text, out var mrMax) ? mrMax : 0;

            // AUTO LEFT
            int autoLeftMin = int.TryParse(TxtAutoLeftMinCps.Text, out var alMin) ? alMin : 0;
            int autoLeftMax = int.TryParse(TxtAutoLeftMaxCps.Text, out var alMax) ? alMax : 0;

            // AUTO RIGHT
            int autoRightMin = int.TryParse(TxtAutoRightMinCps.Text, out var arMin) ? arMin : 0;
            int autoRightMax = int.TryParse(TxtAutoRightMaxCps.Text, out var arMax) ? arMax : 0;

            bool manualOn = ChkMacroManualEnabled.IsChecked ?? false;
            bool autoLeftOn = ChkAutoLeftEnabled.IsChecked ?? false;
            bool autoRightOn = ChkAutoRightEnabled.IsChecked ?? false;

            if (TxtManualCps != null)
            {
                TxtManualCps.Text = manualOn
                    ? $"CPS: L {manualLeftMin}-{manualLeftMax} | P {manualRightMin}-{manualRightMax}"
                    : "CPS: Wyłączony";
            }

            if (TxtAutoLeftCps != null)
            {
                TxtAutoLeftCps.Text = autoLeftOn
                    ? $"CPS: {autoLeftMin}-{autoLeftMax}"
                    : "CPS: Wyłączony";
            }

            if (TxtAutoRightCps != null)
            {
                TxtAutoRightCps.Text = autoRightOn
                    ? $"CPS: {autoRightMin}-{autoRightMax}"
                    : "CPS: Wyłączony";
            }
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
            CbKopacz633Direction.IsHitTestVisible = kop633On; // blokuje myszkę gdy OFF
            CbKopacz633Direction.Focusable = kop633On;        // nie łapie focusa gdy OFF
            CbKopacz633Direction.Opacity = kop633On ? 1.0 : 0.85; // opcjonalnie lekko przygaś
            PanelKopaczNaWprost.IsEnabled = kop633On;
            PanelKopaczDoGory.IsEnabled = kop633On;
            PanelKopacz633Commands.IsEnabled = kop633On;
            BtnKopacz633AddCommand.IsEnabled = kop633On;

            // JABŁKA Z LIŚCI
            bool jablkaOn = ChkJablkaZLisciEnabled.IsChecked ?? false;
            TxtJablkaZLisciKey.IsEnabled = jablkaOn;
            BtnJablkaZLisciCapture.IsEnabled = jablkaOn;

            Brush activeBg = (Brush)FindResource("TileBgActive") ?? new SolidColorBrush(Color.FromRgb(31, 42, 51));
            Brush inactiveBg = (Brush)FindResource("TileBg") ?? new SolidColorBrush(Color.FromRgb(45, 45, 48));
            Brush activeBorder = (Brush)FindResource("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));
            Brush inactiveBorder = (Brush)FindResource("TileBorder") ?? new SolidColorBrush(Color.FromRgb(69, 69, 69));

            BorderManualStatus.Background = manualOn ? activeBg : inactiveBg;
            BorderManualStatus.BorderBrush = manualOn ? activeBorder : inactiveBorder;
            BorderManualStatus.Opacity = manualOn ? 1.0 : 0.85;

            BorderAutoLeftStatus.Background = autoLeftOn ? activeBg : inactiveBg;
            BorderAutoLeftStatus.BorderBrush = autoLeftOn ? activeBorder : inactiveBorder;
            BorderAutoLeftStatus.Opacity = autoLeftOn ? 1.0 : 0.85;

            BorderAutoRightStatus.Background = autoRightOn ? activeBg : inactiveBg;
            BorderAutoRightStatus.BorderBrush = autoRightOn ? activeBorder : inactiveBorder;
            BorderAutoRightStatus.Opacity = autoRightOn ? 1.0 : 0.85;

            BorderKopacz533Status.Background = kop533On ? activeBg : inactiveBg;
            BorderKopacz533Status.BorderBrush = kop533On ? activeBorder : inactiveBorder;
            BorderKopacz533Status.Opacity = kop533On ? 1.0 : 0.85;

            BorderKopacz633Status.Background = kop633On ? activeBg : inactiveBg;
            BorderKopacz633Status.BorderBrush = kop633On ? activeBorder : inactiveBorder;
            BorderKopacz633Status.Opacity = kop633On ? 1.0 : 0.85;

            BorderJablkaZLisciStatus.Background = jablkaOn ? activeBg : inactiveBg;
            BorderJablkaZLisciStatus.BorderBrush = jablkaOn ? activeBorder : inactiveBorder;
            BorderJablkaZLisciStatus.Opacity = jablkaOn ? 1.0 : 0.85;

            TxtJablkaZLisciStatus.Text = jablkaOn ? "Włączony" : "Wyłączony";
            RefreshTopTiles();
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
                BorderBrush = (Brush)FindResource("TileBorder"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(4),
                Background = (Brush)FindResource("TileBg")
            };

            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };

            TextBlock textBlock = new TextBlock
            {
                Text = title,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
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
                FontSize = 11,
                Background = (Brush)FindResource("AccentBrush")
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

            Button deleteBtn = new Button
            {
                Content = "Usuń",
                Width = 60,
                Margin = new Thickness(5, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(180, 50, 50))
            };

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
            UpdateStatusBar("BINDOWANIE: LPM+PPM - naciśnij klawisz", "Red");
            Focus();
        }

        private void BtnAutoLeftCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: AUTO LPM - naciśnij klawisz", "Red");
            Focus();
        }

        private void BtnAutoRightCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: AUTO PPM - naciśnij klawisz", "Red");
            Focus();
        }

        private void BtnKopacz533Capture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: Kopacz 5/3/3 - naciśnij klawisz", "Red");
            Focus();
        }

        private void BtnKopacz633Capture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: Kopacz 6/3/3 - naciśnij klawisz", "Red");
            Focus();
        }

        private void BtnJablkaZLisciCapture_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusBar("BINDOWANIE: Jabłka z liści - naciśnij klawisz", "Red");
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
            RefreshTopTiles();
        }

        private void MarkDirty()
        {
            _pendingChanges = true;

            TxtSettingsSaved.Text = "✗ Nie";
            TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
            EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(255, 85, 85));

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
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(78, 201, 176));

                UpdateStatusBar("Ustawienia zapisane", "Green");
            }
            catch (Exception ex)
            {
                _pendingChanges = true;

                TxtSettingsSaved.Text = "Błąd";
                TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(255, 85, 85));

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

            // JABŁKA Z LIŚCI
            _settings.JablkaZLisciEnabled = ChkJablkaZLisciEnabled.IsChecked ?? false;
            _settings.JablkaZLisciKey = TxtJablkaZLisciKey.Text;

            _settings.TargetWindowTitle = TxtTargetWindowTitle.Text;
        }

        private void UpdateStatusBar(string message, string colorName)
        {
            if (TxtStatusBar == null) return;

            TxtStatusBar.Text = message;
            TxtStatusBar.Foreground =
                colorName == "Red" ? new SolidColorBrush(Color.FromRgb(255, 85, 85)) :
                colorName == "Green" ? new SolidColorBrush(Color.FromRgb(78, 201, 176)) :
                colorName == "Orange" ? new SolidColorBrush(Colors.Orange) :
                new SolidColorBrush(Colors.LightGray);
        }

        private void ChkMacroManualEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkMacroManualEnabled.IsChecked == true)
            {
                ChkAutoLeftEnabled.IsChecked = false;
                ChkAutoRightEnabled.IsChecked = false;

                // NOWE: przy HOLD wyłącz "Jabłka z liści"
                ChkJablkaZLisciEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoLeftEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkAutoLeftEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkAutoRightEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkAutoRightEnabled.IsChecked == true)
            {
                ChkMacroManualEnabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkJablkaZLisciEnabled_Changed(object sender, RoutedEventArgs e)
        {
            // może działać z Auto LPM/PPM, ale nie z HOLD:
            if (ChkJablkaZLisciEnabled.IsChecked == true && ChkMacroManualEnabled.IsChecked == true)
                ChkMacroManualEnabled.IsChecked = false;

            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz533Enabled_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkKopacz533Enabled.IsChecked == true)
            {
                ChkKopacz633Enabled.IsChecked = false;
            }
            UpdateEnabledStates();
            MarkDirty();
        }

        private void ChkKopacz633Enabled_Changed(object sender, RoutedEventArgs e)
        {
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
                    LoadToUi();
                    UpdateEnabledStates();
                    RefreshTopTiles();
                    UpdateStatusBar("Ustawienia wczytane", "Green");

                    _pendingChanges = false;
                    TxtSettingsSaved.Text = "✓ Tak";
                    TxtSettingsSaved.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
                    EllSettingsSaved.Fill = new SolidColorBrush(Color.FromRgb(78, 201, 176));
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
            base.OnClosed(e);
        }
    }
}
