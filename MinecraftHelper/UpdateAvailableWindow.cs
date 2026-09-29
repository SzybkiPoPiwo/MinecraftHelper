using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using MinecraftHelper.Services;

namespace MinecraftHelper
{
    internal sealed class UpdateAvailableWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly Button _okButton;
        private readonly TextBlock _linkErrorText;

        public UpdateAvailableWindow(AppUpdateInfo update)
        {
            ArgumentNullException.ThrowIfNull(update);

            Title = "Aktualizacja Minecraft Helper";
            Width = 570;
            MinWidth = 500;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(Color.FromRgb(11, 23, 39));
            Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 248));

            var root = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 28, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 98, 131)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18)
            };

            var panel = new StackPanel();

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 14)
            };
            header.Children.Add(new TextBlock
            {
                Text = "i",
                Width = 24,
                Height = 24,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255)),
                Background = new SolidColorBrush(Color.FromRgb(18, 50, 77))
            });
            header.Children.Add(new TextBlock
            {
                Text = "Dostępna nowa wersja",
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255))
            });
            panel.Children.Add(header);

            panel.Children.Add(new TextBlock
            {
                Text = "Dostępna jest nowa wersja Minecraft Helper.",
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });

            panel.Children.Add(BuildVersionLine("Obecna wersja:", update.CurrentVersion));
            panel.Children.Add(BuildVersionLine("Najnowsza wersja:", update.LatestVersion));

            if (!string.IsNullOrWhiteSpace(update.ReleaseName))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = update.ReleaseName,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193))
                });
            }

            var downloadLink = new Hyperlink
            {
                NavigateUri = update.DownloadUri,
                Foreground = new SolidColorBrush(Color.FromRgb(70, 177, 255)),
                FontWeight = FontWeights.Bold
            };
            downloadLink.Inlines.Add(update.IsDirectInstaller
                ? "Pobierz najnowszy instalator"
                : "Otwórz stronę najnowszego wydania");
            downloadLink.RequestNavigate += DownloadLink_RequestNavigate;

            var linkText = new TextBlock
            {
                Margin = new Thickness(0, 16, 0, 0),
                FontSize = 14
            };
            linkText.Inlines.Add(downloadLink);
            panel.Children.Add(linkText);

            panel.Children.Add(new TextBlock
            {
                Text = update.IsDirectInstaller
                    ? "Link prowadzi do instalatora dołączonego do oficjalnego wydania na GitHubie."
                    : "Instalator nie został znaleziony w wydaniu. Link prowadzi do oficjalnej strony wydania na GitHubie.",
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193))
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Program nie pobiera ani nie uruchamia instalatora automatycznie.",
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193))
            });

            _linkErrorText = new TextBlock
            {
                Text = "Nie udało się otworzyć linku. Otwórz ręcznie stronę GitHub projektu.",
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                Visibility = Visibility.Collapsed
            };
            panel.Children.Add(_linkErrorText);

            _okButton = new Button
            {
                Content = "OK — przypomnij później",
                Width = 190,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0),
                Style = CreatePrimaryButtonStyle()
            };
            _okButton.Click += (_, __) => CloseDialog();
            panel.Children.Add(_okButton);

            root.Child = panel;
            Content = root;

            PreviewKeyDown += UpdateAvailableWindow_PreviewKeyDown;
            SourceInitialized += (_, __) => ApplyDarkTitleBar();
            Loaded += (_, __) => _okButton.Focus();
        }

        private static TextBlock BuildVersionLine(string label, string value)
        {
            var line = new TextBlock
            {
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            line.Inlines.Add(new Run(label + " ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193)),
                FontWeight = FontWeights.SemiBold
            });
            line.Inlines.Add(new Run(value)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(245, 200, 96)),
                FontWeight = FontWeights.Bold
            });
            return line;
        }

        private void DownloadLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                _linkErrorText.Visibility = Visibility.Visible;
            }
        }

        private void UpdateAvailableWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                CloseDialog();
            }
        }

        private void CloseDialog()
        {
            DialogResult = true;
            Close();
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
            style.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(232, 238, 248))));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(46, 168, 255))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(127, 200, 255))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 4, 12, 4)));

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

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
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(37, 136, 206))));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger
            {
                Property = ButtonBase.IsPressedProperty,
                Value = true
            };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(27, 108, 166))));
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }
    }
}
