using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using MinecraftHelper.Services;
using Microsoft.Win32;

namespace MinecraftHelper
{
    internal sealed class MiningLogsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly MiningLogService _logService;
        private readonly TextBlock _cobbleXValue;
        private readonly TextBlock _discardedValue;
        private readonly TextBlock _periodValue;
        private readonly StackPanel _entriesPanel;
        private readonly HashSet<string> _expandedEntryKeys = new(StringComparer.Ordinal);
        private TextBox _searchBox = null!;
        private TextBlock _resultCount = null!;
        private Button _clearFilterButton = null!;
        private Button _saveReportButton = null!;
        private Button _clearHistoryButton = null!;
        private IReadOnlyList<MiningLogEntry> _visibleEntries = Array.Empty<MiningLogEntry>();

        public MiningLogsWindow(MiningLogService logService)
        {
            _logService = logService;

            Title = "Minecraft Helper — Logi kopania";
            Width = 920;
            Height = 640;
            MinWidth = 760;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = Brush(11, 18, 29);
            Foreground = Brush(232, 238, 248);

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(BuildHeader());

            var summaryGrid = new Grid { Margin = new Thickness(0, 16, 0, 16) };
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition());
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition());
            summaryGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _cobbleXValue = BuildSummaryValue();
            _discardedValue = BuildSummaryValue();
            _periodValue = BuildSummaryValue(fontSize: 13);
            AddSummaryCard(summaryGrid, 0, "SESJE EQ / UTWORZONE COBBLEX", _cobbleXValue, new Thickness(0, 0, 7, 0));
            AddSummaryCard(summaryGrid, 1, "WYRZUCONE PRZEDMIOTY", _discardedValue, new Thickness(7, 0, 7, 0));
            AddSummaryCard(summaryGrid, 2, "ZAKRES HISTORII", _periodValue, new Thickness(7, 0, 0, 0));
            Grid.SetRow(summaryGrid, 1);
            root.Children.Add(summaryGrid);

            UIElement filterBar = BuildFilterBar();
            Grid.SetRow(filterBar, 2);
            root.Children.Add(filterBar);

            var historyBorder = new Border
            {
                Background = Brush(16, 26, 39),
                BorderBrush = Brush(75, 98, 131),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5)
            };
            var historyGrid = new Grid();
            historyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            historyGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            historyGrid.Children.Add(BuildColumnHeader());

            _entriesPanel = new StackPanel();
            var scrollViewer = new ScrollViewer
            {
                Content = _entriesPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(0, 0, 3, 0)
            };
            scrollViewer.Resources[typeof(ScrollBar)] = CreateDarkScrollBarStyle();
            Grid.SetRow(scrollViewer, 1);
            historyGrid.Children.Add(scrollViewer);
            historyBorder.Child = historyGrid;
            Grid.SetRow(historyBorder, 3);
            root.Children.Add(historyBorder);

            var footer = BuildFooter();
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            Content = root;
            SourceInitialized += (_, __) => ApplyDarkTitleBar();
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
            };
            _logService.Changed += LogService_Changed;
            Closed += (_, __) => _logService.Changed -= LogService_Changed;

            RefreshView();
        }

        private void LogService_Changed(object? sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshView));
                return;
            }

            RefreshView();
        }

        private UIElement BuildHeader()
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Logi kopania",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(127, 200, 255)
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Każde automatyczne otwarcie EQ tworzy osobną sesję. Kliknij wiersz, aby rozwinąć listę wyrzuconych przedmiotów, przebiegi skanowania i wynik CobbleX. Starsze wpisy oznaczone * nie zawierają dokładnej liczby sztuk.",
                Margin = new Thickness(0, 5, 0, 0),
                FontSize = 12,
                Foreground = Brush(146, 166, 193),
                TextWrapping = TextWrapping.Wrap
            });
            return panel;
        }

        private UIElement BuildFilterBar()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Szukaj w logach:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(168, 186, 211)
            };
            grid.Children.Add(label);

            _searchBox = new TextBox
            {
                Height = 32,
                Padding = new Thickness(9, 5, 9, 5),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brush(16, 26, 39),
                Foreground = Brush(232, 238, 248),
                BorderBrush = Brush(75, 98, 131),
                BorderThickness = new Thickness(1),
                CaretBrush = Brush(127, 200, 255),
                ToolTip = "Wpisz datę, godzinę, tryb kopacza, status, nazwę przedmiotu, CobbleX albo dowolny fragment szczegółów."
            };
            _searchBox.TextChanged += (_, __) => RefreshEntries();
            Grid.SetColumn(_searchBox, 1);
            grid.Children.Add(_searchBox);

            _resultCount = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 12, 0),
                FontSize = 11,
                Foreground = Brush(146, 166, 193)
            };
            Grid.SetColumn(_resultCount, 2);
            grid.Children.Add(_resultCount);

            _clearFilterButton = BuildButton("Wyczyść filtr", primary: false);
            _clearFilterButton.MinWidth = 108;
            _clearFilterButton.Click += (_, __) =>
            {
                _searchBox.Clear();
                _searchBox.Focus();
            };
            Grid.SetColumn(_clearFilterButton, 3);
            grid.Children.Add(_clearFilterButton);
            return grid;
        }

        private static TextBlock BuildSummaryValue(double fontSize = 22)
        {
            return new TextBlock
            {
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(56, 214, 180),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static void AddSummaryCard(Grid parent, int column, string label, TextBlock value, Thickness margin)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(146, 166, 193),
                Margin = new Thickness(0, 0, 0, 7)
            });
            panel.Children.Add(value);

            var border = new Border
            {
                Background = Brush(22, 31, 43),
                BorderBrush = Brush(75, 98, 131),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(14, 11, 14, 11),
                Margin = margin,
                Child = panel
            };
            Grid.SetColumn(border, column);
            parent.Children.Add(border);
        }

        private static UIElement BuildColumnHeader()
        {
            Grid header = BuildHistoryGrid();
            header.Background = Brush(30, 46, 64);
            header.Children.Add(BuildCell("DATA I GODZINA", 0, FontWeights.Bold, Brush(168, 186, 211)));
            header.Children.Add(BuildCell("SESJA / TRYB", 1, FontWeights.Bold, Brush(168, 186, 211)));
            header.Children.Add(BuildCell("WYNIK", 2, FontWeights.Bold, Brush(168, 186, 211)));
            header.Children.Add(BuildCell("STATUS", 3, FontWeights.Bold, Brush(168, 186, 211)));
            header.Children.Add(BuildCell("▼", 4, FontWeights.Bold, Brush(127, 200, 255)));
            return header;
        }

        private UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pathText = new TextBlock
            {
                Text = "Plik historii: " + _logService.LogsFilePath,
                FontSize = 10,
                Foreground = Brush(112, 132, 159),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 16, 0)
            };
            grid.Children.Add(pathText);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            _saveReportButton = BuildButton("Zapisz raport", primary: false);
            _saveReportButton.ToolTip = "Zapisuje aktualnie widoczne wpisy. Domyślne miejsce to Pulpit.";
            _saveReportButton.Click += (_, __) => SaveReport();
            buttons.Children.Add(_saveReportButton);
            _clearHistoryButton = BuildButton("Wyczyść historię", primary: false);
            _clearHistoryButton.Margin = new Thickness(10, 0, 0, 0);
            _clearHistoryButton.Click += (_, __) => ClearHistory();
            buttons.Children.Add(_clearHistoryButton);
            var closeButton = BuildButton("Zamknij", primary: true);
            closeButton.Margin = new Thickness(10, 0, 0, 0);
            closeButton.Click += (_, __) => Close();
            buttons.Children.Add(closeButton);
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            return grid;
        }

        private void RefreshView()
        {
            MiningLogSummary summary = _logService.GetSummary();
            _cobbleXValue.Text = $"{summary.InventorySessions:N0} skanów EQ\n{summary.CobbleXCreated:N0} CobbleX";
            int exactStacks = Math.Max(0, summary.DiscardedStacks - summary.LegacyDiscardedStacks);
            _discardedValue.Text = $"{summary.DiscardedItems:N0} szt.\n{exactStacks:N0} stosów";
            if (summary.LegacyDiscardedStacks > 0)
                _discardedValue.Text += $"\n+ {summary.LegacyDiscardedStacks:N0} starych stosów*";
            _periodValue.Text = summary.FirstActivity.HasValue && summary.LastActivity.HasValue
                ? $"{summary.FirstActivity.Value.LocalDateTime:dd.MM.yyyy HH:mm}\n— {summary.LastActivity.Value.LocalDateTime:dd.MM.yyyy HH:mm}"
                : "Brak danych";

            RefreshEntries();
        }

        private void RefreshEntries()
        {
            IReadOnlyList<MiningLogEntry> allEntries = _logService.GetEntriesNewestFirst();
            string query = _searchBox?.Text?.Trim() ?? string.Empty;
            _visibleEntries = string.IsNullOrWhiteSpace(query)
                ? allEntries
                : allEntries.Where(entry => EntryMatchesSearch(entry, query)).ToList();

            if (_resultCount != null)
                _resultCount.Text = $"Wyświetlono {_visibleEntries.Count:N0} z {allEntries.Count:N0}";
            if (_clearFilterButton != null)
                _clearFilterButton.IsEnabled = !string.IsNullOrWhiteSpace(query);
            if (_saveReportButton != null)
                _saveReportButton.IsEnabled = _visibleEntries.Count > 0;
            if (_clearHistoryButton != null)
                _clearHistoryButton.IsEnabled = allEntries.Count > 0;

            _entriesPanel.Children.Clear();
            if (_visibleEntries.Count == 0)
            {
                _entriesPanel.Children.Add(new TextBlock
                {
                    Text = allEntries.Count == 0
                        ? "Brak zapisanych sesji. Historia pojawi się przy pierwszym automatycznym otwarciu EQ."
                        : "Brak wpisów pasujących do wyszukiwania.",
                    Margin = new Thickness(18, 24, 18, 24),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush(146, 166, 193)
                });
                return;
            }

            for (int index = 0; index < _visibleEntries.Count; index++)
                _entriesPanel.Children.Add(BuildHistoryRow(_visibleEntries[index], index));
        }

        private static bool EntryMatchesSearch(MiningLogEntry entry, string query)
        {
            string eventLabel = GetEventLabel(entry);
            string countLabel = GetCountLabel(entry);
            string searchable = string.Join(" ",
                entry.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"),
                entry.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                eventLabel,
                countLabel,
                entry.Owner ?? string.Empty,
                GetStatusLabel(entry),
                entry.Details ?? string.Empty,
                entry.CobbleXCommand ?? string.Empty,
                string.Join(" ", (entry.Items ?? new List<MiningLogItemDetail>()).Select(item => $"{item.Label} {item.ItemId} {item.ItemCount} {item.StackCount}")));
            string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return terms.All(term => searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private UIElement BuildHistoryRow(MiningLogEntry entry, int index)
        {
            bool isSession = entry.Kind == MiningLogKinds.InventorySession;
            bool isCobbleX = entry.Kind == MiningLogKinds.CobbleXCreated || entry.CobbleXCreated;
            Brush rowBackground = index % 2 == 0 ? Brush(16, 26, 39) : Brush(19, 31, 46);
            Brush eventBrush = isCobbleX ? Brush(245, 200, 96) : Brush(127, 200, 255);
            Brush statusBrush = GetStatusBrush(entry);

            Grid row = BuildHistoryGrid();
            row.Children.Add(BuildCell(entry.Timestamp.LocalDateTime.ToString("dd.MM.yyyy  HH:mm:ss"), 0, FontWeights.Normal, Brush(207, 219, 235)));
            row.Children.Add(BuildCell(GetEventLabel(entry), 1, FontWeights.SemiBold, eventBrush, wrap: true));
            row.Children.Add(BuildCell(GetCountLabel(entry), 2, FontWeights.Bold, Brush(56, 214, 180), wrap: true));
            row.Children.Add(BuildCell(GetStatusLabel(entry), 3, FontWeights.SemiBold, statusBrush, wrap: true));

            var arrow = new TextBlock
            {
                Text = "▶",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush(127, 200, 255),
                FontSize = 12
            };
            Grid.SetColumn(arrow, 4);
            row.Children.Add(arrow);

            string entryKey = GetEntryKey(entry);
            bool initiallyExpanded = _expandedEntryKeys.Contains(entryKey);
            UIElement details = BuildHistoryDetails(entry, isSession);
            details.Visibility = initiallyExpanded ? Visibility.Visible : Visibility.Collapsed;
            arrow.Text = initiallyExpanded ? "▼" : "▶";

            var toggle = new Button
            {
                Content = row,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
                Style = CreateHistoryRowButtonStyle(rowBackground),
                ToolTip = "Kliknij, aby rozwinąć lub zwinąć szczegóły tej sesji."
            };
            toggle.Click += (_, __) =>
            {
                bool expand = details.Visibility != Visibility.Visible;
                details.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                arrow.Text = expand ? "▼" : "▶";
                if (expand)
                    _expandedEntryKeys.Add(entryKey);
                else
                    _expandedEntryKeys.Remove(entryKey);
            };

            var content = new StackPanel();
            content.Children.Add(toggle);
            content.Children.Add(details);

            return new Border
            {
                BorderBrush = Brush(48, 68, 95),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = content
            };
        }

        private static string GetEntryKey(MiningLogEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.SessionId))
                return entry.SessionId;

            return string.Join("|",
                entry.Kind ?? string.Empty,
                entry.Timestamp.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture),
                entry.Count.ToString(CultureInfo.InvariantCulture),
                entry.ItemCount.ToString(CultureInfo.InvariantCulture),
                entry.Details ?? string.Empty);
        }

        private static Grid BuildHistoryGrid()
        {
            var grid = new Grid { MinHeight = 42 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            return grid;
        }

        private static string GetEventLabel(MiningLogEntry entry)
        {
            if (entry.Kind == MiningLogKinds.InventorySession)
                return string.IsNullOrWhiteSpace(entry.Owner) ? "Skan EQ" : $"Skan EQ • {entry.Owner}";
            return entry.Kind == MiningLogKinds.CobbleXCreated ? "Utworzono CobbleX" : "Stary wpis wyrzucania*";
        }

        private static string GetCountLabel(MiningLogEntry entry)
        {
            if (entry.Kind == MiningLogKinds.CobbleXCreated)
                return $"{Math.Max(0, entry.Count):N0} szt.";
            if (entry.Kind == MiningLogKinds.InventorySession)
            {
                if (entry.Status == MiningLogStatuses.InProgress)
                    return "W toku";

                string result = $"{Math.Max(0, entry.ItemCount):N0} szt. / {Math.Max(0, entry.StackCount):N0} stos.";
                return entry.CobbleXCreated ? result + "\n+ 1 CobbleX" : result;
            }
            if (entry.ItemCount > 0)
                return $"{entry.ItemCount:N0} szt. / {MiningLogService.GetDiscardedStackCount(entry):N0} stos.";

            return $"{MiningLogService.GetDiscardedStackCount(entry):N0} stos.\n(stary wpis*)";
        }

        private static string GetStatusLabel(MiningLogEntry entry)
        {
            if (entry.Kind != MiningLogKinds.InventorySession)
                return "Wpis starszego formatu";

            return entry.Status switch
            {
                MiningLogStatuses.Completed when entry.RemainingStacks > 0 => $"Zakończono z ostrzeżeniem • {entry.DropPasses} przeb.",
                MiningLogStatuses.Completed => $"Zakończono • {entry.DropPasses} przeb.",
                MiningLogStatuses.Aborted => "Przerwano",
                MiningLogStatuses.Interrupted => "Niedokończono",
                _ => "Skanowanie w toku"
            };
        }

        private static Brush GetStatusBrush(MiningLogEntry entry)
        {
            if (entry.Kind != MiningLogKinds.InventorySession)
                return Brush(146, 166, 193);
            if (entry.Status == MiningLogStatuses.Completed && entry.RemainingStacks == 0)
                return Brush(56, 214, 180);
            if (entry.Status == MiningLogStatuses.InProgress || entry.RemainingStacks > 0)
                return Brush(251, 191, 36);
            return Brush(255, 107, 107);
        }

        private static UIElement BuildHistoryDetails(MiningLogEntry entry, bool isSession)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = isSession ? "SZCZEGÓŁY SESJI EQ" : "SZCZEGÓŁY STAREGO WPISU",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brush(127, 200, 255),
                Margin = new Thickness(0, 0, 0, 9)
            });

            AddDetailLine(panel, "Otwarcie EQ", entry.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"), Brush(207, 219, 235));
            if (entry.CompletedAt.HasValue)
            {
                AddDetailLine(panel, "Zakończenie", entry.CompletedAt.Value.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"), Brush(207, 219, 235));
                TimeSpan duration = entry.CompletedAt.Value - entry.Timestamp;
                AddDetailLine(panel, "Czas sesji", FormatDuration(duration), Brush(245, 200, 96));
            }
            if (!string.IsNullOrWhiteSpace(entry.Owner))
                AddDetailLine(panel, "Tryb", entry.Owner, Brush(127, 200, 255));
            AddDetailLine(panel, "Status", GetStatusLabel(entry), GetStatusBrush(entry));

            if (isSession)
            {
                AddDetailLine(panel, "Przebiegi EQ", Math.Max(0, entry.DropPasses).ToString("N0"), Brush(127, 200, 255));
                AddDetailLine(panel, "Pozostałe stosy", Math.Max(0, entry.RemainingStacks).ToString("N0"), entry.RemainingStacks > 0 ? Brush(251, 191, 36) : Brush(56, 214, 180));

                panel.Children.Add(new Border
                {
                    BorderBrush = Brush(48, 68, 95),
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, 10, 0, 10)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = "WYRZUCONE PRZEDMIOTY",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(146, 166, 193),
                    Margin = new Thickness(0, 0, 0, 7)
                });

                IReadOnlyList<MiningLogItemDetail> items = entry.Items ?? new List<MiningLogItemDetail>();
                if (items.Count == 0)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = "Nie wyrzucono żadnych oznaczonych przedmiotów.",
                        Foreground = Brush(146, 166, 193),
                        FontStyle = FontStyles.Italic,
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                }
                else
                {
                    for (int index = 0; index < items.Count; index++)
                    {
                        MiningLogItemDetail item = items[index];
                        var itemGrid = new Grid();
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        itemGrid.Children.Add(new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(item.Label) ? item.ItemId : item.Label,
                            Foreground = Brush(127, 200, 255),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 11
                        });
                        var itemCount = new TextBlock
                        {
                            Text = $"{Math.Max(0, item.ItemCount):N0} szt.  •  {Math.Max(0, item.StackCount):N0} stos.",
                            Foreground = Brush(56, 214, 180),
                            FontWeight = FontWeights.Bold,
                            FontSize = 11
                        };
                        Grid.SetColumn(itemCount, 1);
                        itemGrid.Children.Add(itemCount);
                        panel.Children.Add(new Border
                        {
                            Background = index % 2 == 0 ? Brush(19, 31, 46) : Brush(22, 37, 54),
                            BorderBrush = Brush(48, 68, 95),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(10, 7, 10, 7),
                            Margin = new Thickness(0, 0, 0, 5),
                            Child = itemGrid
                        });
                    }
                }

                string cobbleXValue = entry.CobbleXCreated
                    ? $"Utworzono • komenda: {(string.IsNullOrWhiteSpace(entry.CobbleXCommand) ? "brak" : entry.CobbleXCommand)}"
                    : "Nie utworzono";
                AddDetailLine(
                    panel,
                    "CobbleX",
                    $"{cobbleXValue} • Cobble 64: {Math.Max(0, entry.FullCobblestoneStacks)}/{Math.Max(0, entry.RequiredCobblestoneStacks)}",
                    entry.CobbleXCreated ? Brush(245, 200, 96) : Brush(146, 166, 193));
            }

            if (!string.IsNullOrWhiteSpace(entry.Details))
                AddDetailLine(panel, "Wynik", entry.Details, Brush(207, 219, 235));

            return new Border
            {
                Background = Brush(11, 20, 31),
                BorderBrush = Brush(48, 68, 95),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 12, 18, 14),
                Child = panel
            };
        }

        private static void AddDetailLine(StackPanel panel, string label, string value, Brush valueBrush)
        {
            var text = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5)
            };
            text.Inlines.Add(new Run(label + ": ")
            {
                Foreground = Brush(146, 166, 193),
                FontWeight = FontWeights.SemiBold
            });
            text.Inlines.Add(new Run(value)
            {
                Foreground = valueBrush,
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(text);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private static Style CreateHistoryRowButtonStyle(Brush normalBackground)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(232, 238, 248)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, normalBackground));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brush(27, 45, 65)));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brush(22, 58, 86)));
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static TextBlock BuildCell(string text, int column, FontWeight weight, Brush foreground, bool wrap = false)
        {
            var block = new TextBlock
            {
                Text = text,
                Margin = new Thickness(12, 9, 12, 9),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = weight,
                Foreground = foreground,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(block, column);
            return block;
        }

        private void ClearHistory()
        {
            var confirmation = new DarkLogDialog(
                "Wyczyść logi kopania",
                "Usunąć całą historię kopania? Tej operacji nie można cofnąć.",
                confirmText: "Usuń historię",
                showCancel: true)
            {
                Owner = this
            };
            if (confirmation.ShowDialog() != true)
                return;

            if (!_logService.Clear(out string error))
            {
                ShowDarkInfo("Błąd", "Nie udało się wyczyścić historii: " + error);
                return;
            }

            RefreshView();
        }

        private void SaveReport()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var dialog = new SaveFileDialog
            {
                Title = "Zapisz raport z logów kopania",
                InitialDirectory = Directory.Exists(desktop) ? desktop : null,
                FileName = $"MinecraftHelper-raport-kopania-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                Filter = "Raport CSV (*.csv)|*.csv|Raport tekstowy (*.txt)|*.txt",
                FilterIndex = 1,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                string report = Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                    ? BuildTextReport(_visibleEntries)
                    : BuildCsvReport(_visibleEntries);
                File.WriteAllText(dialog.FileName, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                ShowDarkInfo(
                    "Raport zapisany",
                    $"Zapisano {_visibleEntries.Count:N0} widocznych wpisów do:\n{dialog.FileName}");
            }
            catch (Exception ex)
            {
                ShowDarkInfo("Błąd zapisu", "Nie udało się zapisać raportu: " + ex.Message);
            }
        }

        private string BuildCsvReport(IReadOnlyList<MiningLogEntry> entries)
        {
            (int cobbleX, int discardedItems, int exactStacks, int legacyStacks) = CalculateReportTotals(entries);
            var builder = new StringBuilder();
            builder.AppendLine("Raport Minecraft Helper - logi kopania");
            builder.AppendLine($"Wygenerowano;{EscapeCsv(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"))}");
            builder.AppendLine($"Widoczne wpisy;{entries.Count}");
            builder.AppendLine($"Utworzone CobbleX;{cobbleX}");
            builder.AppendLine($"Wyrzucone przedmioty (dokładne);{discardedItems}");
            builder.AppendLine($"Wyrzucone stosy (dokładne);{exactStacks}");
            builder.AppendLine($"Stare stosy bez liczby sztuk;{legacyStacks}");
            builder.AppendLine();
            builder.AppendLine("Data i godzina;Zdarzenie;Status;Sztuki;Stosy;CobbleX;Szczegóły");
            foreach (MiningLogEntry entry in entries)
            {
                bool isLegacyCobbleX = entry.Kind == MiningLogKinds.CobbleXCreated;
                string items = isLegacyCobbleX
                    ? string.Empty
                    : entry.ItemCount > 0 ? entry.ItemCount.ToString() : "0";
                string stacks = isLegacyCobbleX
                    ? string.Empty
                    : MiningLogService.GetDiscardedStackCount(entry).ToString();
                string cobbleXValue = isLegacyCobbleX
                    ? Math.Max(0, entry.Count).ToString()
                    : entry.CobbleXCreated ? "1" : "0";
                builder.Append(EscapeCsv(entry.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"))).Append(';')
                    .Append(EscapeCsv(GetEventLabel(entry))).Append(';')
                    .Append(EscapeCsv(GetStatusLabel(entry))).Append(';')
                    .Append(EscapeCsv(items)).Append(';')
                    .Append(EscapeCsv(stacks)).Append(';')
                    .Append(EscapeCsv(cobbleXValue)).Append(';')
                    .AppendLine(EscapeCsv(BuildReportDetails(entry)));
            }

            return builder.ToString();
        }

        private string BuildTextReport(IReadOnlyList<MiningLogEntry> entries)
        {
            (int cobbleX, int discardedItems, int exactStacks, int legacyStacks) = CalculateReportTotals(entries);
            var builder = new StringBuilder();
            builder.AppendLine("MINECRAFT HELPER - RAPORT Z LOGÓW KOPANIA");
            builder.AppendLine($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            builder.AppendLine($"Widoczne wpisy: {entries.Count:N0}");
            builder.AppendLine($"Utworzone CobbleX: {cobbleX:N0}");
            builder.AppendLine($"Wyrzucone przedmioty: {discardedItems:N0} szt. z {exactStacks:N0} stosów");
            if (legacyStacks > 0)
                builder.AppendLine($"Starsze dane: {legacyStacks:N0} stosów bez zapisanej liczby sztuk");
            builder.AppendLine();

            foreach (MiningLogEntry entry in entries)
            {
                builder.Append('[').Append(entry.Timestamp.LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss")).Append("] ")
                    .Append(GetEventLabel(entry)).Append(" | ")
                    .Append(GetStatusLabel(entry)).Append(" | ")
                    .Append(GetCountLabel(entry).Replace('\n', ' ')).Append(" | ")
                    .Append(BuildReportDetails(entry));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static (int CobbleX, int Items, int ExactStacks, int LegacyStacks) CalculateReportTotals(
            IReadOnlyList<MiningLogEntry> entries)
        {
            int cobbleX = entries
                .Sum(entry => entry.Kind == MiningLogKinds.CobbleXCreated
                    ? Math.Max(0, entry.Count)
                    : entry.Kind == MiningLogKinds.InventorySession && entry.CobbleXCreated ? 1 : 0);
            int items = entries
                .Where(entry => entry.Kind is MiningLogKinds.ItemsDiscarded or MiningLogKinds.InventorySession)
                .Sum(entry => Math.Max(0, entry.ItemCount));
            int exactStacks = entries
                .Where(entry => (entry.Kind == MiningLogKinds.ItemsDiscarded && entry.ItemCount > 0)
                    || entry.Kind == MiningLogKinds.InventorySession)
                .Sum(MiningLogService.GetDiscardedStackCount);
            int legacyStacks = entries
                .Where(entry => entry.Kind == MiningLogKinds.ItemsDiscarded && entry.ItemCount <= 0)
                .Sum(MiningLogService.GetDiscardedStackCount);
            return (cobbleX, items, exactStacks, legacyStacks);
        }

        private static string BuildReportDetails(MiningLogEntry entry)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(entry.Details))
                parts.Add(entry.Details.Trim());
            if (entry.Items is { Count: > 0 })
            {
                parts.Add("Przedmioty: " + string.Join(", ", entry.Items.Select(item =>
                    $"{(string.IsNullOrWhiteSpace(item.Label) ? item.ItemId : item.Label)}: {Math.Max(0, item.ItemCount)} szt. / {Math.Max(0, item.StackCount)} stos.")));
            }
            if (entry.Kind == MiningLogKinds.InventorySession)
            {
                parts.Add($"Przebiegi: {Math.Max(0, entry.DropPasses)}; pozostało: {Math.Max(0, entry.RemainingStacks)} stos.");
                parts.Add(entry.CobbleXCreated
                    ? $"CobbleX: utworzono ({entry.CobbleXCommand}); Cobble 64: {entry.FullCobblestoneStacks}/{entry.RequiredCobblestoneStacks}"
                    : $"CobbleX: nie utworzono; Cobble 64: {entry.FullCobblestoneStacks}/{entry.RequiredCobblestoneStacks}");
            }

            return string.Join(" ", parts);
        }

        private static string EscapeCsv(string? value)
        {
            string text = value ?? string.Empty;
            return '"' + text.Replace("\"", "\"\"") + '"';
        }

        private void ShowDarkInfo(string title, string message)
        {
            var dialog = new DarkLogDialog(title, message, confirmText: "OK", showCancel: false)
            {
                Owner = this
            };
            dialog.ShowDialog();
        }

        private sealed class DarkLogDialog : Window
        {
            public DarkLogDialog(string title, string message, string confirmText, bool showCancel)
            {
                Title = title;
                Width = 500;
                MinHeight = 190;
                SizeToContent = SizeToContent.Height;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ShowInTaskbar = false;
                Background = Brush(14, 24, 37);
                Foreground = Brush(232, 238, 248);

                var root = new Grid { Margin = new Thickness(20) };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var heading = new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brush(127, 200, 255),
                    Margin = new Thickness(0, 0, 0, 12)
                };
                root.Children.Add(heading);

                var messageBorder = new Border
                {
                    Background = Brush(22, 31, 43),
                    BorderBrush = Brush(75, 98, 131),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(14),
                    Child = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(207, 219, 235),
                        FontSize = 12
                    }
                };
                Grid.SetRow(messageBorder, 1);
                root.Children.Add(messageBorder);

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                if (showCancel)
                {
                    Button cancel = BuildButton("Anuluj", primary: false);
                    cancel.Click += (_, __) => DialogResult = false;
                    buttons.Children.Add(cancel);
                }

                Button confirm = BuildButton(confirmText, primary: true);
                confirm.Margin = showCancel ? new Thickness(10, 0, 0, 0) : new Thickness(0);
                confirm.Click += (_, __) => DialogResult = true;
                buttons.Children.Add(confirm);
                Grid.SetRow(buttons, 2);
                root.Children.Add(buttons);

                Content = root;
                SourceInitialized += (_, __) =>
                {
                    IntPtr hwnd = new WindowInteropHelper(this).Handle;
                    if (hwnd == IntPtr.Zero)
                        return;

                    int useDark = 1;
                    int attr = Environment.OSVersion.Version.Build >= 18985
                        ? DWMWA_USE_IMMERSIVE_DARK_MODE
                        : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                    _ = DwmSetWindowAttribute(hwnd, attr, ref useDark, sizeof(int));
                };
                PreviewKeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        DialogResult = false;
                        e.Handled = true;
                    }
                };
            }
        }

        private static Button BuildButton(string text, bool primary)
        {
            return new Button
            {
                Content = text,
                MinWidth = 120,
                Height = 32,
                Padding = new Thickness(14, 4, 14, 4),
                FontWeight = FontWeights.SemiBold,
                Style = CreateButtonStyle(primary)
            };
        }

        private static Style CreateButtonStyle(bool primary)
        {
            Brush normalBackground = primary ? Brush(46, 168, 255) : Brush(30, 46, 64);
            Brush normalBorder = primary ? Brush(127, 200, 255) : Brush(75, 98, 131);
            Brush hoverBackground = primary ? Brush(37, 136, 206) : Brush(42, 66, 93);
            Brush hoverBorder = primary ? Brush(100, 184, 232) : Brush(94, 125, 165);
            Brush pressedBackground = primary ? Brush(27, 108, 168) : Brush(23, 38, 56);
            Brush pressedBorder = primary ? Brush(78, 160, 215) : Brush(75, 98, 131);

            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brush(232, 238, 248)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, normalBackground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, normalBorder));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(4, 0, 4, 0));
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
            hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, hoverBorder));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, pressedBackground));
            pressedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, pressedBorder));
            template.Triggers.Add(pressedTrigger);

            var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brush(20, 33, 49)));
            disabledTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, Brush(48, 68, 95)));
            disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brush(92, 113, 140)));
            disabledTrigger.Setters.Add(new Setter(Control.CursorProperty, Cursors.Arrow));
            template.Triggers.Add(disabledTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static Style CreateDarkScrollBarStyle()
        {
            const string xaml = """
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       TargetType="{x:Type ScrollBar}">
                    <Setter Property="Width" Value="11"/>
                    <Setter Property="Background" Value="#0F1926"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                <Border Background="#0F1926" BorderBrush="#30445F" BorderThickness="1" CornerRadius="4">
                                    <Track x:Name="PART_Track"
                                           Margin="1"
                                           Orientation="Vertical"
                                           Minimum="{TemplateBinding Minimum}"
                                           Maximum="{TemplateBinding Maximum}"
                                           Value="{TemplateBinding Value}"
                                           ViewportSize="{TemplateBinding ViewportSize}"
                                           IsDirectionReversed="True">
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Command="{x:Static ScrollBar.PageUpCommand}"
                                                          CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                                          Focusable="False">
                                                <RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.DecreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb MinHeight="34">
                                                <Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#355171" BorderBrush="#4B6283" BorderThickness="1" CornerRadius="3"/></ControlTemplate></Thumb.Template>
                                            </Thumb>
                                        </Track.Thumb>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}"
                                                          CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                                          Focusable="False">
                                                <RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.IncreaseRepeatButton>
                                    </Track>
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
                """;
            return (Style)XamlReader.Parse(xaml);
        }

        private void ApplyDarkTitleBar()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            int useDark = 1;
            int attr = Environment.OSVersion.Version.Build >= 18985
                ? DWMWA_USE_IMMERSIVE_DARK_MODE
                : DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
            _ = DwmSetWindowAttribute(hwnd, attr, ref useDark, sizeof(int));
        }

        private static SolidColorBrush Brush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
