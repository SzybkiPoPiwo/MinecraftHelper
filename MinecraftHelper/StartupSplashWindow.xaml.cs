using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MinecraftHelper
{
    public partial class StartupSplashWindow : Window
    {
        private readonly int _minDurationMs;
        private readonly DispatcherTimer _loadingTimer;
        private DateTime _startedAtUtc;

        public StartupSplashWindow(int minDurationMs)
        {
            InitializeComponent();

            _minDurationMs = Math.Max(300, minDurationMs);
            _loadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _loadingTimer.Tick += LoadingTimer_Tick;

            LoadAppIcon();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _startedAtUtc = DateTime.UtcNow;
            PrgLoading.Value = 0;
            _loadingTimer.Start();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _loadingTimer.Stop();
        }

        private void LoadingTimer_Tick(object? sender, EventArgs e)
        {
            double elapsedMs = (DateTime.UtcNow - _startedAtUtc).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / _minDurationMs, 0d, 1d);
            PrgLoading.Value = progress * 100d;
        }

        private void LoadAppIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                if (!File.Exists(iconPath))
                    return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                ImgAppIcon.Source = bitmap;
            }
            catch
            {
                // If icon cannot be loaded, keep splash without image.
            }
        }
    }
}
