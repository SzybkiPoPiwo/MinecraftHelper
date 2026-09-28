using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MinecraftHelper.Models;

namespace MinecraftHelper
{
    public partial class AutoReconnectProfileWindow : Window
    {
        private readonly string _profileId;
        private int _rows;
        private int _columns;
        private int _selectedSlot;
        private int _missingPickaxeRows;
        private int _missingPickaxeColumns;
        private int _missingPickaxeSelectedSlot;
        private readonly int _watchdogSeconds;

        public AutoReconnectServerProfile? ResultProfile { get; private set; }

        public AutoReconnectProfileWindow(AutoReconnectServerProfile? profile)
        {
            InitializeComponent();

            _profileId = string.IsNullOrWhiteSpace(profile?.Id)
                ? Guid.NewGuid().ToString("N")
                : profile!.Id;
            _rows = Math.Clamp(profile?.HomeGuiRows ?? 3, 1, 6);
            _columns = Math.Clamp(profile?.HomeGuiColumns ?? 9, 1, 9);
            _selectedSlot = Math.Clamp(profile?.HomeGuiSlot ?? Math.Min(11, _rows * _columns), 1, _rows * _columns);
            _missingPickaxeRows = profile?.MissingPickaxeHomeGuiRows > 0
                ? Math.Clamp(profile.MissingPickaxeHomeGuiRows, 1, 6)
                : _rows;
            _missingPickaxeColumns = profile?.MissingPickaxeHomeGuiColumns > 0
                ? Math.Clamp(profile.MissingPickaxeHomeGuiColumns, 1, 9)
                : _columns;
            _missingPickaxeSelectedSlot = profile?.MissingPickaxeHomeGuiSlot > 0
                ? Math.Clamp(profile.MissingPickaxeHomeGuiSlot, 1, _missingPickaxeRows * _missingPickaxeColumns)
                : Math.Clamp(_selectedSlot, 1, _missingPickaxeRows * _missingPickaxeColumns);
            _watchdogSeconds = Math.Clamp(profile?.WatchdogSeconds ?? 60, 15, 3600);

            TxtProfileName.Text = profile?.Name ?? string.Empty;
            TxtServerAddress.Text = profile?.ServerAddress ?? string.Empty;
            TxtHomeCommand.Text = profile?.HomeCommand ?? "/home";
            ChkHomeHasGui.IsChecked = profile?.HomeHasGui ?? true;
            TxtHomeGuiDelay.Text = (profile?.HomeGuiDelaySeconds ?? 1).ToString(CultureInfo.InvariantCulture);
            TxtJoinDelay.Text = (profile?.JoinDelaySeconds ?? 8).ToString(CultureInfo.InvariantCulture);
            TxtTeleportDelay.Text = (profile?.TeleportDelaySeconds ?? 10).ToString(CultureInfo.InvariantCulture);
            TxtMaxAttempts.Text = (profile?.MaxAttempts ?? 3).ToString(CultureInfo.InvariantCulture);
            ChkMissingPickaxeRecovery.IsChecked = profile?.MissingPickaxeRecoveryEnabled ?? true;
            TxtMissingPickaxeHomeCommand.Text = string.IsNullOrWhiteSpace(profile?.MissingPickaxeHomeCommand)
                ? profile?.HomeCommand ?? "/home"
                : profile!.MissingPickaxeHomeCommand;
            ChkMissingPickaxeHomeHasGui.IsChecked = profile?.MissingPickaxeHomeHasGui
                ?? profile?.HomeHasGui
                ?? true;
            int missingPickaxeGuiDelay = profile?.MissingPickaxeHomeGuiDelaySeconds >= 0
                ? profile.MissingPickaxeHomeGuiDelaySeconds
                : profile?.HomeGuiDelaySeconds ?? 1;
            TxtMissingPickaxeHomeGuiDelay.Text = missingPickaxeGuiDelay.ToString(CultureInfo.InvariantCulture);

            RefreshHomeGuiVisibility();
            RefreshSlotGrid();
            RefreshMissingPickaxeRecoveryVisibility();
            RefreshMissingPickaxeHomeGuiVisibility();
            RefreshMissingPickaxeSlotGrid();
        }

        private void RefreshHomeGuiVisibility()
        {
            if (PanelHomeGui != null)
                PanelHomeGui.Visibility = ChkHomeHasGui.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshSlotGrid()
        {
            if (PanelHomeSlots == null)
                return;

            PanelHomeSlots.Rows = _rows;
            PanelHomeSlots.Columns = _columns;
            PanelHomeSlots.Children.Clear();
            BorderHomeSlots.Width = _columns * 40 + 12;

            int slotCount = _rows * _columns;
            _selectedSlot = Math.Clamp(_selectedSlot, 1, slotCount);
            for (int slot = 1; slot <= slotCount; slot++)
            {
                bool selected = slot == _selectedSlot;
                var button = new Button
                {
                    Content = slot.ToString(CultureInfo.InvariantCulture),
                    Tag = slot,
                    Width = 36,
                    Height = 28,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0),
                    FontSize = 10,
                    Background = new SolidColorBrush(selected ? Color.FromRgb(23, 50, 74) : Color.FromRgb(30, 42, 57)),
                    BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(46, 168, 255) : Color.FromRgb(62, 83, 110)),
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    Foreground = new SolidColorBrush(selected ? Color.FromRgb(56, 214, 180) : Color.FromRgb(216, 226, 240))
                };
                button.Click += Slot_Click;
                PanelHomeSlots.Children.Add(button);
            }

            int selectedRow = ((_selectedSlot - 1) / _columns) + 1;
            int selectedColumn = ((_selectedSlot - 1) % _columns) + 1;
            TxtSelectedSlot.Text = $"{_selectedSlot} (rząd {selectedRow}, kolumna {selectedColumn})";
            TxtGridSize.Text = $"Układ: {_rows} × {_columns} • {_rows * _columns} pól";
        }

        private void Slot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int slot })
            {
                _selectedSlot = slot;
                RefreshSlotGrid();
            }
        }

        private void RefreshMissingPickaxeRecoveryVisibility()
        {
            if (PanelMissingPickaxeRecovery != null)
            {
                PanelMissingPickaxeRecovery.Visibility = ChkMissingPickaxeRecovery.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void RefreshMissingPickaxeHomeGuiVisibility()
        {
            if (PanelMissingPickaxeHomeGui != null)
            {
                PanelMissingPickaxeHomeGui.Visibility = ChkMissingPickaxeHomeHasGui.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void RefreshMissingPickaxeSlotGrid()
        {
            if (PanelMissingPickaxeHomeSlots == null)
                return;

            PanelMissingPickaxeHomeSlots.Rows = _missingPickaxeRows;
            PanelMissingPickaxeHomeSlots.Columns = _missingPickaxeColumns;
            PanelMissingPickaxeHomeSlots.Children.Clear();
            BorderMissingPickaxeHomeSlots.Width = _missingPickaxeColumns * 40 + 12;

            int slotCount = _missingPickaxeRows * _missingPickaxeColumns;
            _missingPickaxeSelectedSlot = Math.Clamp(_missingPickaxeSelectedSlot, 1, slotCount);
            for (int slot = 1; slot <= slotCount; slot++)
            {
                bool selected = slot == _missingPickaxeSelectedSlot;
                var button = new Button
                {
                    Content = slot.ToString(CultureInfo.InvariantCulture),
                    Tag = slot,
                    Width = 36,
                    Height = 28,
                    Margin = new Thickness(2),
                    Padding = new Thickness(0),
                    FontSize = 10,
                    Background = new SolidColorBrush(selected ? Color.FromRgb(23, 50, 74) : Color.FromRgb(30, 42, 57)),
                    BorderBrush = new SolidColorBrush(selected ? Color.FromRgb(46, 168, 255) : Color.FromRgb(62, 83, 110)),
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    Foreground = new SolidColorBrush(selected ? Color.FromRgb(56, 214, 180) : Color.FromRgb(216, 226, 240))
                };
                button.Click += MissingPickaxeSlot_Click;
                PanelMissingPickaxeHomeSlots.Children.Add(button);
            }

            int selectedRow = ((_missingPickaxeSelectedSlot - 1) / _missingPickaxeColumns) + 1;
            int selectedColumn = ((_missingPickaxeSelectedSlot - 1) % _missingPickaxeColumns) + 1;
            TxtMissingPickaxeSelectedSlot.Text = $"{_missingPickaxeSelectedSlot} (rząd {selectedRow}, kolumna {selectedColumn})";
            TxtMissingPickaxeGridSize.Text = $"Układ: {_missingPickaxeRows} × {_missingPickaxeColumns} • {_missingPickaxeRows * _missingPickaxeColumns} pól";
        }

        private void MissingPickaxeSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int slot })
            {
                _missingPickaxeSelectedSlot = slot;
                RefreshMissingPickaxeSlotGrid();
            }
        }

        private void BtnAddColumn_Click(object sender, RoutedEventArgs e)
        {
            ChangeColumns(Math.Min(9, _columns + 1));
        }

        private void BtnRemoveColumn_Click(object sender, RoutedEventArgs e)
        {
            ChangeColumns(Math.Max(1, _columns - 1));
        }

        private void ChangeColumns(int newColumns)
        {
            int row = (_selectedSlot - 1) / _columns;
            int column = (_selectedSlot - 1) % _columns;
            _columns = newColumns;
            _selectedSlot = row * _columns + Math.Min(column, _columns - 1) + 1;
            RefreshSlotGrid();
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            _rows = Math.Min(6, _rows + 1);
            RefreshSlotGrid();
        }

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            _rows = Math.Max(1, _rows - 1);
            RefreshSlotGrid();
        }

        private void ChkHomeHasGui_Changed(object sender, RoutedEventArgs e)
        {
            RefreshHomeGuiVisibility();
        }

        private void ChkMissingPickaxeRecovery_Changed(object sender, RoutedEventArgs e)
        {
            RefreshMissingPickaxeRecoveryVisibility();
        }

        private void ChkMissingPickaxeHomeHasGui_Changed(object sender, RoutedEventArgs e)
        {
            RefreshMissingPickaxeHomeGuiVisibility();
        }

        private void BtnMissingPickaxeAddColumn_Click(object sender, RoutedEventArgs e)
        {
            ChangeMissingPickaxeColumns(Math.Min(9, _missingPickaxeColumns + 1));
        }

        private void BtnMissingPickaxeRemoveColumn_Click(object sender, RoutedEventArgs e)
        {
            ChangeMissingPickaxeColumns(Math.Max(1, _missingPickaxeColumns - 1));
        }

        private void ChangeMissingPickaxeColumns(int newColumns)
        {
            int row = (_missingPickaxeSelectedSlot - 1) / _missingPickaxeColumns;
            int column = (_missingPickaxeSelectedSlot - 1) % _missingPickaxeColumns;
            _missingPickaxeColumns = newColumns;
            _missingPickaxeSelectedSlot = row * _missingPickaxeColumns
                + Math.Min(column, _missingPickaxeColumns - 1)
                + 1;
            RefreshMissingPickaxeSlotGrid();
        }

        private void BtnMissingPickaxeAddRow_Click(object sender, RoutedEventArgs e)
        {
            _missingPickaxeRows = Math.Min(6, _missingPickaxeRows + 1);
            RefreshMissingPickaxeSlotGrid();
        }

        private void BtnMissingPickaxeRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            _missingPickaxeRows = Math.Max(1, _missingPickaxeRows - 1);
            RefreshMissingPickaxeSlotGrid();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtProfileName.Text.Trim();
            string address = TxtServerAddress.Text.Trim();
            string command = TxtHomeCommand.Text.Trim();
            string missingPickaxeCommand = TxtMissingPickaxeHomeCommand.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TxtValidation.Text = "Podaj nazwę profilu.";
                return;
            }
            if (string.IsNullOrWhiteSpace(address))
            {
                TxtValidation.Text = "Podaj adres serwera używany przez Direct Connect.";
                return;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                TxtValidation.Text = "Podaj komendę domu, np. /home albo /home kopalnia.";
                return;
            }
            if (!command.StartsWith("/", StringComparison.Ordinal))
                command = "/" + command;
            if (ChkMissingPickaxeRecovery.IsChecked == true && string.IsNullOrWhiteSpace(missingPickaxeCommand))
            {
                TxtValidation.Text = "Podaj komendę powrotu używaną po wykryciu braku diamentowego kilofa.";
                return;
            }
            if (string.IsNullOrWhiteSpace(missingPickaxeCommand))
                missingPickaxeCommand = command;
            else if (!missingPickaxeCommand.StartsWith("/", StringComparison.Ordinal))
                missingPickaxeCommand = "/" + missingPickaxeCommand;

            ResultProfile = new AutoReconnectServerProfile
            {
                Id = _profileId,
                Name = name,
                ServerAddress = address,
                HomeCommand = command,
                HomeHasGui = ChkHomeHasGui.IsChecked == true,
                HomeGuiDelaySeconds = ReadNumber(TxtHomeGuiDelay, 1, 0, 120),
                HomeGuiRows = _rows,
                HomeGuiColumns = _columns,
                HomeGuiSlot = _selectedSlot,
                JoinDelaySeconds = ReadNumber(TxtJoinDelay, 8, 2, 120),
                TeleportDelaySeconds = ReadNumber(TxtTeleportDelay, 10, 1, 120),
                WatchdogSeconds = _watchdogSeconds,
                MaxAttempts = ReadNumber(TxtMaxAttempts, 3, 1, 10),
                MissingPickaxeRecoveryEnabled = ChkMissingPickaxeRecovery.IsChecked == true,
                MissingPickaxeHomeCommand = missingPickaxeCommand,
                MissingPickaxeHomeHasGui = ChkMissingPickaxeHomeHasGui.IsChecked == true,
                MissingPickaxeHomeGuiDelaySeconds = ReadNumber(TxtMissingPickaxeHomeGuiDelay, 1, 0, 120),
                MissingPickaxeHomeGuiRows = _missingPickaxeRows,
                MissingPickaxeHomeGuiColumns = _missingPickaxeColumns,
                MissingPickaxeHomeGuiSlot = _missingPickaxeSelectedSlot
            };
            DialogResult = true;
        }

        private static int ReadNumber(TextBox textBox, int fallback, int minimum, int maximum)
        {
            return int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button)
                    return;
                source = VisualTreeHelper.GetParent(source);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
