using System;
using System.Windows;
using System.Threading.Tasks;

namespace KleeStore
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                base.OnStartup(e);
                
                
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application failed to start: {ex.Message}", 
                               "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            
            MessageBox.Show($"An error occurred: {e.Exception.Message}", 
                           "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
} 