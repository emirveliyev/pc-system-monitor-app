using pc_system_monitor_app.Utils;
using System;
using System.Threading;
using System.Windows.Threading;

namespace pc_system_monitor_app
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.WriteException(e.Exception, "Unhandled UI exception");
            }
            catch
            {
            }

            string path;
            try
            {
                path = Logger.GetLogPath();
            }
            catch
            {
                path = "лог недоступен";
            }

            System.Windows.MessageBox.Show($"Произошла непредвиденная ошибка. Подробности в логе: {path}", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            e.Handled = true;
            Environment.Exit(1);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
                Logger.WriteException(ex, "Unhandled domain exception");
            }
            catch
            {
            }

            string path;
            try
            {
                path = Logger.GetLogPath();
            }
            catch
            {
                path = "лог недоступен";
            }

            System.Windows.MessageBox.Show($"Критическая ошибка. Смотрите лог: {path}", "Критическая ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Thread.Sleep(200);
            Environment.Exit(1);
        }
    }
}
