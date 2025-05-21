using System;
using System.Windows;

namespace KleeStore
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application failed to start: {ex.Message}", 
                               "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
} 