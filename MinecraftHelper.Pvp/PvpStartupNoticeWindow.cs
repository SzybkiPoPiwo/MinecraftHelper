using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MinecraftHelper;

internal sealed class PvpStartupNoticeWindow : Window
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private readonly CheckBox _confirmationCheckBox;
    private readonly Button _okButton;
    private readonly TextBlock _validationText;
    private bool _confirmed;

    public PvpStartupNoticeWindow(bool isFirstRun, string currentVersion, string previousVersion)
    {
        string safeCurrentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "nieznana" : currentVersion.Trim();
        string safePreviousVersion = previousVersion?.Trim() ?? string.Empty;

        Title = isFirstRun
            ? "Pierwsze uruchomienie — Minecraft Helper Auto Clicker"
            : $"Informacje o wersji {safeCurrentVersion} — Auto Clicker";
        Width = 650;
        Height = 680;
        MinWidth = 560;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brush(7, 17, 31);
        Foreground = Brush(234, 244, 255);

        var root = new Border
        {
            Background = Brush(11, 23, 39),
            BorderBrush = Brush(57, 81, 110),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18)
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = isFirstRun
                ? "Witaj w Minecraft Helper Auto Clicker"
                : $"Co nowego w Auto Clickerze {safeCurrentVersion}",
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(98, 200, 255),
            Margin = new Thickness(0, 0, 0, 10)
        };
        layout.Children.Add(title);

        var introduction = new TextBlock
        {
            Text = BuildIntroduction(isFirstRun, safePreviousVersion, safeCurrentVersion),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(207, 219, 235),
            Margin = new Thickness(0, 0, 0, 14)
        };
        Grid.SetRow(introduction, 1);
        layout.Children.Add(introduction);

        var contentPanel = new StackPanel();
        if (isFirstRun)
        {
            contentPanel.Children.Add(BuildCard(
                "Najpierw przygotuj Auto Clicker",
                new[]
                {
                    "Uruchom Minecrafta, wejdź do gry, wybierz jego proces u góry okna i kliknij Zapisz.",
                    "Włącz tylko potrzebne moduły, przypisz bindy i ustaw bezpieczny zakres CPS.",
                    "Najedź na przyciski ?, aby przeczytać dokładne działanie każdej opcji.",
                    "Clickery i automatyzacje działają wyłącznie w zapisanym, aktywnym oknie Minecrafta."
                }));
        }

        foreach (AppReleaseNotes release in ReleaseNotesCatalog.GetNotesForStartup(
                     safePreviousVersion,
                     safeCurrentVersion,
                     isFirstRun))
        {
            contentPanel.Children.Add(BuildCard(
                $"{release.Version} — {release.Title}",
                release.Changes));
        }

        var scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false
        };
        Grid.SetRow(scrollViewer, 2);
        layout.Children.Add(scrollViewer);

        var footer = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        var confirmationBorder = new Border
        {
            Background = Brush(13, 36, 57),
            BorderBrush = Brush(46, 168, 255),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 9, 12, 9)
        };
        _confirmationCheckBox = new CheckBox
        {
            Content = "Przeczytałem informacje i wiem, że szczegółowe opisy znajdują się pod przyciskami ?.",
            Foreground = Brush(232, 238, 248),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        _confirmationCheckBox.Checked += ConfirmationChanged;
        _confirmationCheckBox.Unchecked += ConfirmationChanged;
        confirmationBorder.Child = _confirmationCheckBox;
        footer.Children.Add(confirmationBorder);

        _validationText = new TextBlock
        {
            Text = "Najpierw zaznacz potwierdzenie powyżej.",
            Foreground = Brush(251, 191, 36),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 7, 0, 0),
            Visibility = Visibility.Collapsed
        };
        footer.Children.Add(_validationText);

        _okButton = new Button
        {
            Content = "OK — przejdź do programu",
            Width = 210,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0),
            IsEnabled = false,
            IsDefault = true
        };
        _okButton.Click += OkButton_Click;
        footer.Children.Add(_okButton);
        Grid.SetRow(footer, 3);
        layout.Children.Add(footer);

        root.Child = layout;
        Content = root;

        Closing += Window_Closing;
        PreviewKeyDown += Window_PreviewKeyDown;
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        Loaded += (_, _) => _confirmationCheckBox.Focus();
    }

    private static string BuildIntroduction(bool isFirstRun, string previousVersion, string currentVersion)
    {
        if (isFirstRun)
            return "Nie znaleziono zapisanych ustawień Auto Clickera. Poniżej znajdziesz krótki start oraz najważniejsze zmiany tej wersji.";

        return string.IsNullOrWhiteSpace(previousVersion)
            ? $"Auto Clicker działa teraz w wersji {currentVersion}. Przeczytaj najważniejsze informacje dotyczące tego wydania."
            : $"Auto Clicker został zmieniony z wersji {previousVersion} na {currentVersion}. Twoje ustawienia i bindy pozostają zachowane.";
    }

    private static Border BuildCard(string title, System.Collections.Generic.IEnumerable<string> entries)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brush(127, 200, 255),
            Margin = new Thickness(0, 0, 0, 7)
        });

        foreach (string entry in entries)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "• " + entry,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush(220, 232, 247),
                Margin = new Thickness(0, 2, 0, 3)
            });
        }

        return new Border
        {
            Child = panel,
            Background = Brush(17, 29, 43),
            BorderBrush = Brush(57, 81, 110),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };
    }

    private void ConfirmationChanged(object sender, RoutedEventArgs e)
    {
        bool accepted = _confirmationCheckBox.IsChecked == true;
        _okButton.IsEnabled = accepted;
        if (accepted)
            _validationText.Visibility = Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_confirmationCheckBox.IsChecked != true)
        {
            _validationText.Visibility = Visibility.Visible;
            return;
        }

        _confirmed = true;
        DialogResult = true;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_confirmed)
            return;

        e.Cancel = true;
        _validationText.Visibility = Visibility.Visible;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _validationText.Visibility = Visibility.Visible;
            e.Handled = true;
        }
    }

    private void ApplyDarkTitleBar()
    {
        IntPtr window = new WindowInteropHelper(this).Handle;
        if (window == IntPtr.Zero)
            return;

        int enabled = 1;
        int attribute = Environment.OSVersion.Version.Build >= 18985
            ? DwmwaUseImmersiveDarkMode
            : DwmwaUseImmersiveDarkModeBefore20H1;
        _ = DwmSetWindowAttribute(window, attribute, ref enabled, sizeof(int));
    }

    private static SolidColorBrush Brush(byte red, byte green, byte blue) =>
        new(Color.FromRgb(red, green, blue));
}
