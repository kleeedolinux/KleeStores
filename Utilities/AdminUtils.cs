using System;
using System.Diagnostics;
using System.Security.Principal;

namespace KleeStore.Utilities
{
    public static class AdminUtils
    {
        public static bool IsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        
        public static bool RunAsAdmin(string? command = null)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? ".",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                if (!string.IsNullOrEmpty(command))
                {
                    processInfo.Arguments = command;
                }
                
                Process.Start(processInfo);
                return true;
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error running as admin: {ex.Message}");
                return false;
            }
        }
    }
} 