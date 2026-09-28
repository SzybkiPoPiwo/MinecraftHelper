using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MinecraftHelper
{
    public partial class App : Application
    {
        private const int SplashMinDurationMs = 1300;
        private const string SingleInstanceMutexName = @"Local\MinecraftHelper.SingleInstance";
        private Mutex? _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out bool createdNew);

            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown(0);
                return;
            }

            _ownsSingleInstanceMutex = true;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var splash = new StartupSplashWindow(SplashMinDurationMs);
            try
            {
                splash.Show();
                await Task.Delay(SplashMinDurationMs);

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            catch (Exception)
            {
                Shutdown(-1);
            }
            finally
            {
                splash.Close();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ownsSingleInstanceMutex)
            {
                try
                {
                    _singleInstanceMutex?.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Proces kończy działanie; uchwyt zostanie zwolniony poniżej.
                }
            }

            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            _ownsSingleInstanceMutex = false;
            base.OnExit(e);
        }
    }
}
