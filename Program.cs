using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;

namespace KleeStore
{
    public class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        
        [STAThread]
        public static void Main()
        {
            try
            {
                var handle = GetConsoleWindow();
                ShowWindow(handle, SW_HIDE);
                
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