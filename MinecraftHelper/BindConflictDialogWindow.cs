using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MinecraftHelper
{
    internal sealed class BindConflictDialogWindow : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private readonly Button _okButton;

        public BindConflictDialogWindow(string requestedOwnerLabel, string keyText, string conflictOwnerLabel)
        {
            string requestedOwner = string.IsNullOrWhiteSpace(requestedOwnerLabel) ? "funkcji" : requestedOwnerLabel.Trim();
            string key = string.IsNullOrWhiteSpace(keyText) ? "?" : keyText.Trim();
            string conflictOwner = string.IsNullOrWhiteSpace(conflictOwnerLabel) ? "innej funkcji" : conflictOwnerLabel.Trim();

            Title = "Klawisz zajety";
            Width = 520;
            MinWidth = 460;
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
                Padding = new Thickness(16)
            };

            var panel = new StackPanel();

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };
            header.Children.Add(new TextBlock
            {
                Text = "!",
                Width = 22,
                Height = 22,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                Background = new SolidColorBrush(Color.FromRgb(43, 33, 18))
            });
            header.Children.Add(new TextBlock
            {
                Text = "Klawisz jest juz zajety",
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 200, 255))
            });
            panel.Children.Add(header);

            panel.Children.Add(new TextBlock
            {
                Text = $"Nie mozna zapisac bindu dla: {requestedOwner}.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 248))
            });

            panel.Children.Add(BuildValueLine("Klawisz:", key));
            panel.Children.Add(BuildValueLine("Zajety przez:", conflictOwner));

            panel.Children.Add(new TextBlock
            {
                Text = "Wybierz inny klawisz i kliknij \"Zapisz\" ponownie.",
                Margin = new Thickness(0, 12, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(146, 166, 193))
            });

            _okButton = new Button
            {
                Content = "OK",
                Width = 98,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                Style = CreatePrimaryButtonStyle()
            };
            _okButton.Click += (_, __) =>
            {
                DialogResult = true;
                Close();
            };
            panel.Children.Add(_okButton);

            root.Child = panel;
            Content = root;

            PreviewKeyDown += BindConflictDialogWindow_PreviewKeyDown;
            SourceInitialized += (_, __) => ApplyDarkTitleBar();
            Loaded += (_, __) => _okButton.Focus();
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

        private static UIElement BuildValueLine(string label, string value)
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
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(4, 0, 4, 0));
            border.AppendChild(presenter);

            template.VisualTree = border;

            var hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(37, 136, 206))));
            hoverTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(100, 184, 232))));
            template.Triggers.Add(hoverTrigger);

            var pressedTrigger = new Trigger
            {
                Property = ButtonBase.IsPressedProperty,
                Value = true
            };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(27, 108, 168))));
            pressedTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(78, 160, 215))));
            template.Triggers.Add(pressedTrigger);

            var disabledTrigger = new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false
            };
            disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(34, 57, 83))));
            disabledTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(62, 83, 110))));
            disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(146, 166, 193))));
            template.Triggers.Add(disabledTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private void BindConflictDialogWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }
    }
}
