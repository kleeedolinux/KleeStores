using System;
using System.Windows;
using System.Diagnostics;

namespace KleeStore
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                Console.WriteLine("App starting...");
                base.OnStartup(e);
                Console.WriteLine("Creating main window...");
                MainWindow mainWindow = new MainWindow();
                Console.WriteLine("Showing main window...");
                mainWindow.Show();
                Console.WriteLine("Main window shown");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show($"Application failed to start: {ex.Message}\n\n{ex.StackTrace}", 
                               "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
} 