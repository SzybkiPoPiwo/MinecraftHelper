using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MinecraftHelper
{
    internal enum OverlayCorner
    {
        BottomRight,
        BottomLeft,
        TopRight,
        TopLeft
    }

    internal enum OverlayHudTone
    {
        Active,
        Warning
    }

    internal readonly record struct OverlayHudEntry(string Title, string Body, OverlayHudTone Tone, bool Emphasize = false);

    internal sealed class OverlayHudWindow : Window
    {
        private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(146, 166, 193));
        private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(127, 200, 255));
        private static readonly Brush OnBrush = new SolidColorBrush(Color.FromRgb(74, 222, 128));
        private static readonly Brush OffBrush = new SolidColorBrush(Color.FromRgb(255, 107, 107));
        private static readonly Brush PauseBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        private static readonly Brush TimeBrush = new SolidColorBrush(Color.FromRgb(245, 200, 96));

        private readonly StackPanel _itemsPanel;
        private string _lastSignature = string.Empty;
        private string _lastStructureSignature = string.Empty;
        private Rect _workArea = SystemParameters.WorkArea;
        private OverlayCorner _corner = OverlayCorner.BottomRight;
        private double _stackOffset = 16;
        private bool _animationsEnabled = true;
        private bool _isStructureAnimating;
        private List<OverlayHudEntry>? _queuedStructureEntries;

        public OverlayHudWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            _itemsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            _itemsPanel.RenderTransform = new TranslateTransform();

            Content = _itemsPanel;
        }

        public bool IsHudVisible => IsVisible;

        public double CurrentHeight => ActualHeight > 0 ? ActualHeight : RenderSize.Height;

        public void SetAnimationsEnabled(bool enabled)
        {
            _animationsEnabled = enabled;
        }

        public void SetLayout(Rect workArea, OverlayCorner corner, double stackOffset)
        {
            _workArea = workArea;
            _corner = corner;
            _stackOffset = Math.Max(0, stackOffset);
            if (IsVisible)
                RepositionToAnchor();
        }

        public void UpdateEntries(IReadOnlyList<OverlayHudEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                _lastSignature = string.Empty;
                _lastStructureSignature = string.Empty;
                _isStructureAnimating = false;
                _queuedStructureEntries = null;
                CancelItemsAnimations();
                HideWindow();
                return;
            }

            string signature = BuildSignature(entries);
            string structureSignature = BuildStructureSignature(entries);
            bool signatureChanged = !string.Equals(signature, _lastSignature, StringComparison.Ordinal);
            bool structureChanged = !string.Equals(structureSignature, _lastStructureSignature, StringComparison.Ordinal);
            if (signatureChanged || structureChanged)
            {
                _lastSignature = signature;
                _lastStructureSignature = structureSignature;
                if (IsVisible && structureChanged)
                    AnimateStructureRefresh(entries);
                else
                    RebuildTiles(entries);
            }

            ShowWindow();
            if (!_isStructureAnimating)
                RepositionToAnchor();
        }

        private void RebuildTiles(IReadOnlyList<OverlayHudEntry> entries)
        {
            _itemsPanel.Children.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                OverlayHudEntry entry = entries[i];
                _itemsPanel.Children.Add(BuildTile(entry, i));
            }
        }

        private static Border BuildTile(OverlayHudEntry entry, int index)
        {
            bool warning = entry.Tone == OverlayHudTone.Warning;
            Brush border = warning
                ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                : new SolidColorBrush(Color.FromRgb(74, 222, 128));
            Brush background = warning
                ? new SolidColorBrush(Color.FromArgb(242, 43, 33, 18))
                : new SolidColorBrush(Color.FromArgb(242, 23, 50, 74));

            var title = new TextBlock
            {
                Text = entry.Title,
                FontSize = entry.Emphasize ? 14 : 12,
                FontWeight = FontWeights.Bold,
                Foreground = entry.Emphasize
                    ? new SolidColorBrush(Color.FromRgb(127, 200, 255))
                    : new SolidColorBrush(Color.FromRgb(232, 238, 248))
            };

            var bodyPanel = BuildBodyPanel(entry.Body, warning, entry.Emphasize);

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(bodyPanel);

            return new Border
            {
                Width = entry.Emphasize ? 420 : 360,
                Background = background,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = entry.Emphasize ? new Thickness(12, 11, 12, 11) : new Thickness(10),
                Margin = new Thickness(0, index == 0 ? 0 : 8, 0, 0),
                Child = panel,
                IsHitTestVisible = false
            };
        }

        private static StackPanel BuildBodyPanel(string body, bool warningTile, bool emphasize)
        {
            var bodyPanel = new StackPanel
            {
                Margin = new Thickness(0, 4, 0, 0)
            };

            string[] lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                if (emphasize)
                {
                    bodyPanel.Children.Add(BuildEmphasizedLine(line, warningTile));
                    continue;
                }

                var text = new TextBlock
                {
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                };

                int separator = line.IndexOf(':');
                if (separator > 0)
                {
                    string label = line.Substring(0, separator + 1);
                    string value = line.Substring(separator + 1).TrimStart();

                    text.Inlines.Add(new Run(label + " ")
                    {
                        Foreground = LabelBrush,
                        FontWeight = FontWeights.SemiBold
                    });
                    AppendStyledValue(text, value, warningTile);
                }
                else
                {
                    AppendStyledValue(text, line, warningTile);
                }

                bodyPanel.Children.Add(text);
            }

            return bodyPanel;
        }

        private static TextBlock BuildEmphasizedLine(string line, bool warningTile)
        {
            var text = new TextBlock
            {
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            int separator = line.IndexOf(':');
            if (separator > 0)
            {
                string label = line.Substring(0, separator + 1);
                string value = line.Substring(separator + 1).TrimStart();
                bool isPlayersLine = label.StartsWith("Gracze", StringComparison.OrdinalIgnoreCase);

                text.Inlines.Add(new Run(label + " ")
                {
                    Foreground = LabelBrush,
                    FontWeight = FontWeights.Bold,
                    FontSize = isPlayersLine ? 16 : 14
                });
                text.Inlines.Add(new Run(value)
                {
                    Foreground = isPlayersLine ? ResolvePlayersBrush(value, warningTile) : ResolveValueBrush(value, warningTile),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = isPlayersLine ? 30 : 18
                });
                return text;
            }

            text.Inlines.Add(new Run(line)
            {
                Foreground = ResolveValueBrush(line, warningTile),
                FontWeight = FontWeights.Bold,
                FontSize = 18
            });
            return text;
        }

        private static void AppendStyledValue(TextBlock block, string value, bool warningTile)
        {
            string[] parts = value.Split(new[] { " | " }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                block.Inlines.Add(new Run(part)
                {
                    Foreground = ResolveValueBrush(part, warningTile),
                    FontWeight = FontWeights.SemiBold
                });

                if (i < parts.Length - 1)
                {
                    block.Inlines.Add(new Run(" | ")
                    {
                        Foreground = LabelBrush
                    });
                }
            }
        }

        private static Brush ResolveValueBrush(string valuePart, bool warningTile)
        {
            string lower = valuePart.ToLowerInvariant();

            if (lower.Contains("pauza") || lower.Contains("wstrzym"))
                return PauseBrush;

            if (lower.Contains("off") || lower.Contains("wyłącz") || lower.Contains("brak") || lower == "stop")
                return OffBrush;

            if (lower.Contains("on") || lower.Contains("kopanie") || lower.Contains("aktywn") || lower.Contains("teraz") || lower.Contains("wznawianie") || lower.Contains("wykonane"))
                return OnBrush;

            if (lower.Contains("za ") || lower.EndsWith("ms", StringComparison.Ordinal) || lower.EndsWith("s", StringComparison.Ordinal))
                return TimeBrush;

            if (warningTile)
                return PauseBrush;

            return AccentBrush;
        }

        private static Brush ResolvePlayersBrush(string valuePart, bool warningTile)
        {
            string lower = valuePart.ToLowerInvariant();
            if (lower.Contains("brak", StringComparison.Ordinal))
                return warningTile ? PauseBrush : OffBrush;

            int index = 0;
            while (index < valuePart.Length && !char.IsDigit(valuePart[index]))
                index++;

            int start = index;
            while (index < valuePart.Length && char.IsDigit(valuePart[index]))
                index++;

            if (start < index && int.TryParse(valuePart.Substring(start, index - start), out int parsedPlayers))
                return parsedPlayers > 0 ? OnBrush : PauseBrush;

            return warningTile ? PauseBrush : AccentBrush;
        }

        private static string BuildSignature(IReadOnlyList<OverlayHudEntry> entries)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                OverlayHudEntry entry = entries[i];
                sb.Append(entry.Title);
                sb.Append('|');
                sb.Append(entry.Body);
                sb.Append('|');
                sb.Append((int)entry.Tone);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string BuildStructureSignature(IReadOnlyList<OverlayHudEntry> entries)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                OverlayHudEntry entry = entries[i];
                sb.Append(entry.Title);
                sb.Append('|');
                sb.Append((int)entry.Tone);
                sb.Append('|');
                sb.Append(entry.Emphasize ? '1' : '0');
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private void AnimateStructureRefresh(IReadOnlyList<OverlayHudEntry> entries)
        {
            if (!_animationsEnabled)
            {
                RebuildTiles(entries);
                RepositionToAnchor();
                return;
            }

            if (_isStructureAnimating)
            {
                _queuedStructureEntries = new List<OverlayHudEntry>(entries);
                return;
            }

            _isStructureAnimating = true;
            _queuedStructureEntries = null;
            CancelItemsAnimations();

            TranslateTransform translate = EnsureItemsTranslateTransform();
            var hideEase = new CubicEase { EasingMode = EasingMode.EaseIn };
            var showEase = new CubicEase { EasingMode = EasingMode.EaseOut };

            double hideY = _corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? -6 : 6;
            var hideOpacity = new DoubleAnimation
            {
                From = _itemsPanel.Opacity <= 0 ? 1 : _itemsPanel.Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(90),
                EasingFunction = hideEase
            };

            hideOpacity.Completed += (_, __) =>
            {
                if (!IsVisible)
                {
                    _isStructureAnimating = false;
                    _queuedStructureEntries = null;
                    return;
                }

                RebuildTiles(entries);
                RepositionToAnchor();

                double showFromY = _corner is OverlayCorner.TopLeft or OverlayCorner.TopRight ? -12 : 12;
                translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.Y = showFromY;

                _itemsPanel.BeginAnimation(UIElement.OpacityProperty, null);
                _itemsPanel.Opacity = 0;

                var showOpacity = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = showEase
                };
                showOpacity.Completed += (_, __2) =>
                {
                    _isStructureAnimating = false;
                    if (_queuedStructureEntries is { Count: > 0 } queued)
                    {
                        _queuedStructureEntries = null;
                        AnimateStructureRefresh(queued);
                    }
                };
                _itemsPanel.BeginAnimation(UIElement.OpacityProperty, showOpacity);

                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
                {
                    From = showFromY,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(210),
                    EasingFunction = showEase
                });
            };

            _itemsPanel.BeginAnimation(UIElement.OpacityProperty, hideOpacity);

            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation
            {
                From = 0,
                To = hideY,
                Duration = TimeSpan.FromMilliseconds(90),
                EasingFunction = hideEase
            });
        }

        private void ShowWindow()
        {
            BeginAnimation(OpacityProperty, null);

            if (!IsVisible)
            {
                if (_animationsEnabled)
                {
                    Opacity = 0;
                    Show();
                    _itemsPanel.BeginAnimation(UIElement.OpacityProperty, null);
                    _itemsPanel.Opacity = 1;
                    BeginAnimation(OpacityProperty, new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(140),
                    });
                }
                else
                {
                    Opacity = 1;
                    Show();
                }
            }
            else
            {
                Opacity = 1;
            }
        }

        private void HideWindow()
        {
            if (!IsVisible)
                return;

            CancelItemsAnimations();
            _isStructureAnimating = false;
            _queuedStructureEntries = null;
            BeginAnimation(OpacityProperty, null);
            if (!_animationsEnabled)
            {
                Opacity = 1;
                Hide();
                return;
            }

            var fade = new DoubleAnimation
            {
                From = Opacity,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(180),
            };
            fade.Completed += (_, __) =>
            {
                BeginAnimation(OpacityProperty, null);
                Opacity = 1;
                Hide();
            };
            BeginAnimation(OpacityProperty, fade);
        }

        private TranslateTransform EnsureItemsTranslateTransform()
        {
            if (_itemsPanel.RenderTransform is TranslateTransform translate)
                return translate;

            translate = new TranslateTransform();
            _itemsPanel.RenderTransform = translate;
            return translate;
        }

        private void CancelItemsAnimations()
        {
            _itemsPanel.BeginAnimation(UIElement.OpacityProperty, null);
            TranslateTransform translate = EnsureItemsTranslateTransform();
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            _itemsPanel.Opacity = 1;
            translate.Y = 0;
        }

        private void RepositionToAnchor()
        {
            UpdateLayout();

            double left = _corner is OverlayCorner.BottomLeft or OverlayCorner.TopLeft
                ? _workArea.Left + 16
                : _workArea.Right - ActualWidth - 16;

            double top = _corner is OverlayCorner.TopLeft or OverlayCorner.TopRight
                ? _workArea.Top + _stackOffset
                : _workArea.Bottom - ActualHeight - _stackOffset;

            Left = left;
            Top = top;
        }
    }
}
