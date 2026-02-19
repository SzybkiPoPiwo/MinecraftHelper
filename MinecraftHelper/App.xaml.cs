using System;
using System.Threading.Tasks;
using System.Windows;

namespace MinecraftHelper
{
    public partial class App : Application
    {
        private const int SplashMinDurationMs = 1300;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
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
    }
}
