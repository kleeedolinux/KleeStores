using System;
using System.IO;
using System.Windows;

namespace KleeStore
{
    public class Program
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $".kleestore_log.txt");

        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                
                try
                {
                    File.WriteAllText(LogFilePath, $"FATAL ERROR: {ex.Message}\n{ex.StackTrace}");
                }
                catch
                {
                    
                }
                
                MessageBox.Show($"Fatal error: {ex.Message}\n\nCheck the log file for details.",
                               "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 