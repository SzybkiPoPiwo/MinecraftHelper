using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MinecraftHelper
{
    /// <summary>
    /// Lekka, nieinteraktywna warstwa tła. Wszystkie elementy są rysowane
    /// w jednym przebiegu zamiast tworzenia osobnych kontrolek WPF.
    /// </summary>
    public sealed class CyberMeshBackground : FrameworkElement
    {
        private const int ParticleCount = 260;
        private const int FallingParticleCount = 24;
        private const double TargetFrameSeconds = 1.0 / 15.0;

        public static readonly DependencyProperty IsAnimationEnabledProperty =
            DependencyProperty.Register(
                nameof(IsAnimationEnabled),
                typeof(bool),
                typeof(CyberMeshBackground),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnIsAnimationEnabledChanged));

        private readonly DispatcherTimer _animationTimer;
        private readonly Stopwatch _frameClock = Stopwatch.StartNew();
        private readonly Random _random = new Random(317);
        private readonly List<Particle> _particles = new List<Particle>(ParticleCount);
        private readonly Brush _backgroundBrush;
        private readonly Brush _cyanGlowBrush;
        private readonly Brush _purpleGlowBrush;

        private Window? _hostWindow;
        private long _lastFrameTicks;

        public CyberMeshBackground()
        {
            IsHitTestVisible = false;
            Focusable = false;
            ClipToBounds = true;

            _backgroundBrush = Freeze(new LinearGradientBrush(
                Color.FromRgb(1, 5, 13),
                Color.FromRgb(3, 13, 29),
                new Point(0.08, 0),
                new Point(0.92, 1)));

            _cyanGlowBrush = CreateGlowBrush(
                Color.FromArgb(28, 13, 92, 186),
                new Point(0.72, 0.28),
                0.72,
                0.66);
            _purpleGlowBrush = CreateGlowBrush(
                Color.FromArgb(16, 75, 37, 155),
                new Point(0.18, 0.76),
                0.64,
                0.58);

            for (int i = 0; i < ParticleCount; i++)
                _particles.Add(CreateParticle(i < FallingParticleCount));

            _animationTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(TargetFrameSeconds)
            };
            _animationTimer.Tick += AnimationTimer_Tick;

            Loaded += CyberMeshBackground_Loaded;
            Unloaded += CyberMeshBackground_Unloaded;
            IsVisibleChanged += CyberMeshBackground_IsVisibleChanged;
        }

        public bool IsAnimationEnabled
        {
            get => (bool)GetValue(IsAnimationEnabledProperty);
            set => SetValue(IsAnimationEnabledProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            var bounds = new Rect(0, 0, width, height);
            drawingContext.DrawRectangle(_backgroundBrush, null, bounds);
            drawingContext.DrawRectangle(_cyanGlowBrush, null, bounds);
            drawingContext.DrawRectangle(_purpleGlowBrush, null, bounds);

            DrawParticles(drawingContext, width, height);
        }

        private static void OnIsAnimationEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is CyberMeshBackground background)
            {
                background.RefreshAnimationState();
                background.InvalidateVisual();
            }
        }

        private void CyberMeshBackground_Loaded(object sender, RoutedEventArgs e)
        {
            AttachHostWindow();
            RefreshAnimationState();
        }

        private void CyberMeshBackground_Unloaded(object sender, RoutedEventArgs e)
        {
            _animationTimer.Stop();
            DetachHostWindow();
        }

        private void CyberMeshBackground_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RefreshAnimationState();
        }

        private void AttachHostWindow()
        {
            Window? window = Window.GetWindow(this);
            if (ReferenceEquals(window, _hostWindow))
                return;

            DetachHostWindow();
            _hostWindow = window;
            if (_hostWindow == null)
                return;

            _hostWindow.Activated += HostWindow_StateChanged;
            _hostWindow.Deactivated += HostWindow_StateChanged;
            _hostWindow.StateChanged += HostWindow_StateChanged;
        }

        private void DetachHostWindow()
        {
            if (_hostWindow == null)
                return;

            _hostWindow.Activated -= HostWindow_StateChanged;
            _hostWindow.Deactivated -= HostWindow_StateChanged;
            _hostWindow.StateChanged -= HostWindow_StateChanged;
            _hostWindow = null;
        }

        private void HostWindow_StateChanged(object? sender, EventArgs e)
        {
            RefreshAnimationState();
        }

        private void RefreshAnimationState()
        {
            bool shouldAnimate = IsAnimationEnabled
                && IsLoaded
                && IsVisible
                && (_hostWindow == null || _hostWindow.WindowState != WindowState.Minimized);

            if (shouldAnimate)
            {
                _lastFrameTicks = _frameClock.ElapsedTicks;
                if (!_animationTimer.IsEnabled)
                    _animationTimer.Start();
            }
            else
            {
                _animationTimer.Stop();
            }
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            if (!IsAnimationEnabled
                || !IsVisible
                || _hostWindow?.WindowState == WindowState.Minimized)
            {
                RefreshAnimationState();
                return;
            }

            long nowTicks = _frameClock.ElapsedTicks;
            long elapsedTicks = Math.Max(0, nowTicks - _lastFrameTicks);
            _lastFrameTicks = nowTicks;

            double elapsedSeconds = Math.Min(0.12, elapsedTicks / (double)Stopwatch.Frequency);
            foreach (Particle particle in _particles)
            {
                particle.TwinklePhase = (particle.TwinklePhase + particle.TwinkleSpeed * elapsedSeconds) % (Math.PI * 2.0);

                if (!particle.IsFalling)
                    continue;

                particle.X += particle.HorizontalSpeed * elapsedSeconds;
                particle.Y += particle.FallSpeed * elapsedSeconds;

                if (particle.Y > 1.04)
                {
                    particle.Y = -0.04 - _random.NextDouble() * 0.12;
                    particle.X = _random.NextDouble();
                }

                if (particle.X > 1.04)
                    particle.X = -0.04;
                else if (particle.X < -0.04)
                    particle.X = 1.04;
            }

            InvalidateVisual();
        }

        private void DrawParticles(DrawingContext drawingContext, double width, double height)
        {
            foreach (Particle particle in _particles)
            {
                double x = particle.X * width;
                double y = particle.Y * height;
                double wave = (Math.Sin(particle.TwinklePhase) + 1.0) * 0.5;
                double opacity = particle.IsSpark
                    ? 0.12 + wave * 0.88
                    : 0.28 + wave * 0.72;
                double size = particle.Size * (0.58 + wave * 0.62);

                drawingContext.PushOpacity(opacity);

                if (particle.IsFalling)
                {
                    double tailLength = particle.TailLength * (0.7 + wave * 0.3);
                    double horizontalTail = particle.HorizontalSpeed >= 0 ? -tailLength * 0.22 : tailLength * 0.22;
                    drawingContext.DrawLine(
                        particle.FlarePen,
                        new Point(x + horizontalTail, y - tailLength),
                        new Point(x, y));
                }

                drawingContext.DrawEllipse(
                    particle.DotBrush,
                    null,
                    new Point(x, y),
                    size,
                    size);

                if (particle.IsSpark)
                {
                    double arm = particle.FlareLength * (0.42 + wave * 0.78);
                    double coreArm = arm * 0.48;

                    if (particle.DiagonalSpark)
                    {
                        drawingContext.DrawLine(
                            particle.FlarePen,
                            new Point(x - arm, y - arm),
                            new Point(x + arm, y + arm));
                        drawingContext.DrawLine(
                            particle.FlarePen,
                            new Point(x + arm, y - arm),
                            new Point(x - arm, y + arm));
                        drawingContext.DrawLine(
                            particle.CorePen,
                            new Point(x - coreArm, y - coreArm),
                            new Point(x + coreArm, y + coreArm));
                        drawingContext.DrawLine(
                            particle.CorePen,
                            new Point(x + coreArm, y - coreArm),
                            new Point(x - coreArm, y + coreArm));
                    }
                    else
                    {
                        drawingContext.DrawLine(
                            particle.FlarePen,
                            new Point(x - arm * 0.62, y),
                            new Point(x + arm * 0.62, y));
                        drawingContext.DrawLine(
                            particle.FlarePen,
                            new Point(x, y - arm),
                            new Point(x, y + arm));
                        drawingContext.DrawLine(
                            particle.CorePen,
                            new Point(x - coreArm * 0.62, y),
                            new Point(x + coreArm * 0.62, y));
                        drawingContext.DrawLine(
                            particle.CorePen,
                            new Point(x, y - coreArm),
                            new Point(x, y + coreArm));
                    }
                }

                drawingContext.Pop();
            }
        }

        private Particle CreateParticle(bool isFalling)
        {
            double depth = _random.NextDouble();
            double colorRoll = _random.NextDouble();
            byte alpha = (byte)Math.Round(72 + depth * 165);
            Color color;
            if (colorRoll < 0.42)
                color = Color.FromArgb(alpha, 72, 178, 255);
            else if (colorRoll < 0.66)
                color = Color.FromArgb(alpha, 110, 225, 255);
            else if (colorRoll < 0.8)
                color = Color.FromArgb(alpha, 238, 248, 255);
            else if (colorRoll < 0.88)
                color = Color.FromArgb(alpha, 98, 255, 196);
            else if (colorRoll < 0.94)
                color = Color.FromArgb(alpha, 255, 235, 105);
            else if (colorRoll < 0.98)
                color = Color.FromArgb(alpha, 255, 131, 198);
            else
                color = Color.FromArgb(alpha, 177, 135, 255);

            Color flareColor = Color.FromArgb((byte)Math.Max(42, alpha / 2), color.R, color.G, color.B);
            Color coreColor = Color.FromArgb((byte)Math.Min(255, alpha + 26), color.R, color.G, color.B);
            bool isSpark = depth > 0.56 && _random.NextDouble() < 0.28;

            return new Particle
            {
                X = _random.NextDouble(),
                Y = _random.NextDouble(),
                Size = 0.32 + depth * 1.28,
                TwinklePhase = _random.NextDouble() * Math.PI * 2.0,
                TwinkleSpeed = 0.55 + _random.NextDouble() * 2.15,
                IsSpark = isSpark,
                IsFalling = isFalling,
                FallSpeed = isFalling ? 0.022 + _random.NextDouble() * 0.035 : 0,
                HorizontalSpeed = isFalling ? -0.008 + _random.NextDouble() * 0.016 : 0,
                TailLength = isFalling ? 7.0 + depth * 13.0 : 0,
                DiagonalSpark = _random.NextDouble() < 0.28,
                FlareLength = 2.8 + depth * 6.8,
                DotBrush = Freeze(new SolidColorBrush(color)),
                FlarePen = CreatePen(flareColor, 0.52 + depth * 0.48),
                CorePen = CreatePen(coreColor, 0.62 + depth * 0.48)
            };
        }

        private static Brush CreateGlowBrush(Color centerColor, Point center, double radiusX, double radiusY)
        {
            var brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                Center = center,
                GradientOrigin = center,
                RadiusX = radiusX,
                RadiusY = radiusY
            };
            brush.GradientStops.Add(new GradientStop(centerColor, 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, centerColor.R, centerColor.G, centerColor.B), 1));
            return Freeze(brush);
        }

        private static Pen CreatePen(Color color, double thickness)
        {
            var pen = new Pen(Freeze(new SolidColorBrush(color)), thickness);
            pen.Freeze();
            return pen;
        }

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze)
                freezable.Freeze();
            return freezable;
        }

        private sealed class Particle
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Size { get; set; }
            public double TwinklePhase { get; set; }
            public double TwinkleSpeed { get; set; }
            public bool IsSpark { get; set; }
            public bool IsFalling { get; set; }
            public double FallSpeed { get; set; }
            public double HorizontalSpeed { get; set; }
            public double TailLength { get; set; }
            public bool DiagonalSpark { get; set; }
            public double FlareLength { get; set; }
            public Brush DotBrush { get; set; } = Brushes.Transparent;
            public Pen FlarePen { get; set; } = new Pen(Brushes.Transparent, 1);
            public Pen CorePen { get; set; } = new Pen(Brushes.Transparent, 1);
        }
    }
}
