using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace MinecraftHelper
{
    internal sealed class FirstRunGuideWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly CheckBox _confirmationCheckBox;
        private readonly Button _okButton;
        private readonly TextBlock _validationText;
        private bool _isConfirmed;

        public FirstRunGuideWindow(bool isFirstRun, string currentVersion, string previousVersion)
        {
            string safeCurrentVersion = string.IsNullOrWhiteSpace(currentVersion) ? "nieznana" : currentVersion.Trim();
            string safePreviousVersion = previousVersion?.Trim() ?? string.Empty;

            Title = isFirstRun
                ? "Pierwsze uruchomienie — Minecraft Helper"
                : $"Informacje o wersji {safeCurrentVersion} — Minecraft Helper";
            Width = 700;
            Height = Math.Min(650, Math.Max(540, SystemParameters.WorkArea.Height - 70));
            MinWidth = 620;
            MinHeight = 540;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = BrushFromRgb(11, 23, 39);
            Foreground = BrushFromRgb(232, 238, 248);
            UseLayoutRounding = true;

            var root = new Border
            {
                Background = BrushFromRgb(16, 28, 46),
                BorderBrush = BrushFromRgb(75, 98, 131),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(22)
            };

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };
            header.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = BrushFromRgb(23, 58, 88),
                BorderBrush = BrushFromRgb(46, 168, 255),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "i",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromRgb(127, 200, 255),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            });
            header.Children.Add(new TextBlock
            {
                Text = isFirstRun
                    ? "Witaj w Minecraft Helper"
                    : $"Wykryto zmianę wersji — {safeCurrentVersion}",
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(127, 200, 255)
            });
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var introduction = new TextBlock
            {
                Text = BuildIntroduction(isFirstRun, safePreviousVersion, safeCurrentVersion),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 20,
                Foreground = BrushFromRgb(207, 219, 235),
                Margin = new Thickness(0, 0, 0, 14)
            };
            Grid.SetRow(introduction, 1);
            layout.Children.Add(introduction);

            var guidePanel = new StackPanel
            {
                Margin = new Thickness(0, 0, 8, 0)
            };

            if (isFirstRun)
            {
                guidePanel.Children.Add(BuildInfoCard(
                    "Najpierw przygotuj program",
                    "1. Uruchom Minecrafta lub BlazingPacka i wejdź do gry.\n" +
                    "2. W zakładce Ustawienia kliknij Odśwież, wybierz właściwy proces gry — nie launcher — i kliknij Zapisz program.\n" +
                    "3. Rozwiń funkcje, których chcesz używać, i przeczytaj ich pełne opisy pod przyciskami ?.\n" +
                    "4. Ustaw bindy tak, aby nie kolidowały ze sobą ani ze sterowaniem Minecrafta."));
                guidePanel.Children.Add(BuildInfoCard(
                    "AUTO LPM i AUTO PPM — najważniejsze",
                    "• Zaznaczenie sekcji włącza moduł i pokazuje ustawienia, ale samo nie uruchamia klikania.\n" +
                    "• Kliknij pole Klawisz, naciśnij wybrany bind, a potem kliknij Zapisz.\n" +
                    "• Ustaw minimalny i maksymalny CPS. Na początek użyj spokojnych wartości i sprawdź działanie.\n" +
                    "• W trybie klasycznym zapisany bind włącza clicker, a ponowne naciśnięcie go wyłącza.\n" +
                    "• W trybie kombinacji użyj binda razem z fizycznym LPM lub PPM i trzymaj odpowiedni przycisk myszy.\n" +
                    "• Clickery działają tylko wtedy, gdy zapisane okno Minecrafta ma fokus."));
            }
            else
            {
                guidePanel.Children.Add(BuildInfoCard(
                    "Zgodność zapisanych ustawień",
                    BuildCompatibilityText(safePreviousVersion, safeCurrentVersion)));
            }

            AddReleaseNotesCards(guidePanel, isFirstRun, safePreviousVersion, safeCurrentVersion);
            guidePanel.Children.Add(BuildInfoCard(
                "Ważne przed rozpoczęciem",
                "Najpierw przetestuj każdą funkcję osobno. Obserwuj górne kafelki stanu i HUD. Jeżeli coś działa inaczej niż oczekujesz, wyłącz funkcję jej bindem i sprawdź instrukcję pod ?."));
            guidePanel.Children.Add(BuildInfoCard(
                "Problemy, błędy i propozycje",
                "Jeżeli napotkasz błąd, problem z działaniem albo masz propozycję nowej funkcji, napisz na Discordzie. Dane kontaktowe znajdziesz w zakładce Ustawienia w aplikacji."));

            var scrollViewer = new ScrollViewer
            {
                Content = guidePanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = true
            };
            scrollViewer.Resources.Add(typeof(ScrollBar), CreateDarkScrollBarStyle());
            Grid.SetRow(scrollViewer, 2);
            layout.Children.Add(scrollViewer);

            var footer = new StackPanel
            {
                Margin = new Thickness(0, 16, 0, 0)
            };

            var confirmationBorder = new Border
            {
                Background = BrushFromRgb(13, 36, 57),
                BorderBrush = BrushFromRgb(46, 168, 255),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 10, 12, 10)
            };
            _confirmationCheckBox = new CheckBox
            {
                VerticalContentAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Foreground = BrushFromRgb(232, 238, 248),
                Content = new TextBlock
                {
                    Text = "Przeczytałem informacje, changelog i instrukcje dotyczące tej wersji. Wiem, że szczegółowe opisy znajdują się pod przyciskami ?.",
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BrushFromRgb(232, 238, 248)
                }
            };
            _confirmationCheckBox.Checked += ConfirmationCheckBox_Changed;
            _confirmationCheckBox.Unchecked += ConfirmationCheckBox_Changed;
            confirmationBorder.Child = _confirmationCheckBox;
            footer.Children.Add(confirmationBorder);

            _validationText = new TextBlock
            {
                Text = "Najpierw zaznacz potwierdzenie powyżej.",
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = BrushFromRgb(251, 191, 36),
                FontWeight = FontWeights.SemiBold,
                Visibility = Visibility.Collapsed
            };
            footer.Children.Add(_validationText);

            _okButton = new Button
            {
                Content = "OK — przejdź do programu",
                Width = 210,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0),
                IsEnabled = false,
                IsDefault = true,
                Style = CreatePrimaryButtonStyle()
            };
            _okButton.Click += OkButton_Click;
            footer.Children.Add(_okButton);

            Grid.SetRow(footer, 3);
            layout.Children.Add(footer);

            root.Child = layout;
            Content = root;

            Closing += FirstRunGuideWindow_Closing;
            PreviewKeyDown += FirstRunGuideWindow_PreviewKeyDown;
            SourceInitialized += (_, __) => ApplyDarkTitleBar();
            Loaded += (_, __) => _confirmationCheckBox.Focus();
        }

        private static string BuildIntroduction(bool isFirstRun, string previousVersion, string currentVersion)
        {
            if (isFirstRun)
            {
                return "Nie znaleziono zapisanego pliku ustawień, dlatego wygląda na to, że uruchamiasz program po raz pierwszy. Zanim włączysz makra, zapoznaj się z interfejsem, changelogiem i instrukcjami pod przyciskami ?.";
            }

            if (string.IsNullOrWhiteSpace(previousVersion))
            {
                return $"Wykryto zapis ustawień ze starszej wersji, która nie przechowywała numeru wydania. Program działa teraz w wersji {currentVersion}. Przeczytaj informacje o zgodności i listę zmian.";
            }

            int comparison = ReleaseNotesCatalog.CompareVersions(previousVersion, currentVersion);
            if (comparison > 0)
            {
                return $"Plik ustawień został zapisany przez nowszą wersję {previousVersion}, a uruchomiony program ma wersję {currentVersion}. Niektóre nowsze ustawienia mogą nie być dostępne w tej wersji.";
            }

            return $"Program został zmieniony z wersji {previousVersion} na {currentVersion}. Twoje dotychczasowe ustawienia zostaną wczytane, a poniżej znajdziesz najważniejsze nowe funkcje i poprawki.";
        }

        private static string BuildCompatibilityText(string previousVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(previousVersion))
            {
                return "Starszy plik ustawień zostanie automatycznie uzupełniony o brakujące pola. Dotychczasowe bindy, CPS, komendy i pozostałe obsługiwane ustawienia pozostaną zachowane.";
            }

            int comparison = ReleaseNotesCatalog.CompareVersions(previousVersion, currentVersion);
            if (comparison > 0)
            {
                return "Uruchomiono starszy program z zapisem utworzonym przez nowszą wersję. Rozpoznane ustawienia zostaną wczytane, ale opcje dodane później mogą być niedostępne. Przed większymi zmianami wykonaj eksport ustawień.";
            }

            return "Program automatycznie uzupełni brakujące pola nowej wersji. Dotychczasowe bindy, CPS, komendy i pozostałe obsługiwane ustawienia pozostaną zachowane.";
        }

        private static void AddReleaseNotesCards(
            Panel target,
            bool isFirstRun,
            string previousVersion,
            string currentVersion)
        {
            IReadOnlyList<AppReleaseNotes> notes = ReleaseNotesCatalog.GetNotesForStartup(
                previousVersion,
                currentVersion,
                isFirstRun);

            foreach (AppReleaseNotes release in notes)
            {
                string changes = string.Join("\n", release.Changes.Select(change => "• " + change));
                target.Children.Add(BuildInfoCard(
                    $"Wersja {release.Version} — {release.Title}",
                    changes));
            }
        }

        private static Border BuildInfoCard(string title, string text)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFromRgb(127, 200, 255),
                Margin = new Thickness(0, 0, 0, 7)
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                LineHeight = 19,
                Foreground = BrushFromRgb(207, 219, 235)
            });

            return new Border
            {
                Background = BrushFromRgb(21, 35, 55),
                BorderBrush = BrushFromRgb(62, 83, 110),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(13),
                Margin = new Thickness(0, 0, 0, 10),
                Child = panel
            };
        }

        private void ConfirmationCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool isAccepted = _confirmationCheckBox.IsChecked == true;
            _okButton.IsEnabled = isAccepted;
            if (isAccepted)
                _validationText.Visibility = Visibility.Collapsed;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_confirmationCheckBox.IsChecked != true)
            {
                ShowValidationMessage();
                return;
            }

            _isConfirmed = true;
            DialogResult = true;
        }

        private void FirstRunGuideWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isConfirmed)
                return;

            e.Cancel = true;
            ShowValidationMessage();
        }

        private void FirstRunGuideWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ShowValidationMessage();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && _confirmationCheckBox.IsChecked == true)
            {
                OkButton_Click(_okButton, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void ShowValidationMessage()
        {
            _validationText.Visibility = Visibility.Visible;
            Activate();
            _confirmationCheckBox.Focus();
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

        private static Style CreatePrimaryButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(232, 238, 248)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(46, 168, 255)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(127, 200, 255)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 4, 12, 4)));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
            presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(37, 136, 206)));
            hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(100, 184, 232)));
            template.Triggers.Add(hoverTrigger);

            var disabledTrigger = new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false
            };
            disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(34, 57, 83)));
            disabledTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(62, 83, 110)));
            disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(146, 166, 193)));
            disabledTrigger.Setters.Add(new Setter(Control.CursorProperty, Cursors.Arrow));
            template.Triggers.Add(disabledTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private static Style CreateDarkScrollBarStyle()
        {
            const string styleXaml = """
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                       TargetType="{x:Type ScrollBar}">
                    <Setter Property="Width" Value="12"/>
                    <Setter Property="Background" Value="#0D1A2B"/>
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                <Border Background="{TemplateBinding Background}"
                                        BorderBrush="#2F425B"
                                        BorderThickness="1"
                                        CornerRadius="4">
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
                                                <RepeatButton.Template>
                                                    <ControlTemplate TargetType="{x:Type RepeatButton}">
                                                        <Border Background="Transparent"/>
                                                    </ControlTemplate>
                                                </RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.DecreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb MinHeight="34">
                                                <Thumb.Template>
                                                    <ControlTemplate TargetType="{x:Type Thumb}">
                                                        <Border x:Name="ThumbBorder"
                                                                Margin="1"
                                                                Background="#355171"
                                                                BorderBrush="#46658A"
                                                                BorderThickness="1"
                                                                CornerRadius="3"/>
                                                        <ControlTemplate.Triggers>
                                                            <Trigger Property="IsMouseOver" Value="True">
                                                                <Setter TargetName="ThumbBorder" Property="Background" Value="#46658A"/>
                                                                <Setter TargetName="ThumbBorder" Property="BorderBrush" Value="#7FC8FF"/>
                                                            </Trigger>
                                                            <Trigger Property="IsDragging" Value="True">
                                                                <Setter TargetName="ThumbBorder" Property="Background" Value="#2EA8FF"/>
                                                                <Setter TargetName="ThumbBorder" Property="BorderBrush" Value="#9BD7FF"/>
                                                            </Trigger>
                                                        </ControlTemplate.Triggers>
                                                    </ControlTemplate>
                                                </Thumb.Template>
                                            </Thumb>
                                        </Track.Thumb>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}"
                                                          CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                                          Focusable="False">
                                                <RepeatButton.Template>
                                                    <ControlTemplate TargetType="{x:Type RepeatButton}">
                                                        <Border Background="Transparent"/>
                                                    </ControlTemplate>
                                                </RepeatButton.Template>
                                            </RepeatButton>
                                        </Track.IncreaseRepeatButton>
                                    </Track>
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
                """;

            return (Style)XamlReader.Parse(styleXaml);
        }

        private static SolidColorBrush BrushFromRgb(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
