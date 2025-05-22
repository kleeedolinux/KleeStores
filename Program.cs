using System;
using System.Threading;
using System.Windows;

namespace KleeStore
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal error: {ex.Message}",
                               "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 