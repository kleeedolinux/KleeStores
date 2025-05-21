using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public class ChocolateyManager
    {
        private static ChocolateyManager? _instance;
        private static readonly object _lock = new object();
        
        private readonly string _chocoPath;
        private readonly string _installScript;
        
        private ChocolateyManager()
        {
            _chocoPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "choco.exe");
            _installScript = @"Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))";
        }
        
        public static ChocolateyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ChocolateyManager();
                    }
                }
                return _instance;
            }
        }
        
        public bool IsInstalled()
        {
            return File.Exists(_chocoPath);
        }
        
        public (bool Success, string Message) InstallChocolatey()
        {
            if (IsInstalled())
            {
                return (true, "Chocolatey is already installed");
            }
            
            try
            {
                var scriptPath = Path.Combine(Path.GetTempPath(), "install_chocolatey.ps1");
                File.WriteAllText(scriptPath, _installScript);
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Start-Process powershell -ArgumentList '-ExecutionPolicy Bypass -File {scriptPath}' -Verb RunAs -Wait\"",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                

                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
                
                
                if (IsInstalled())
                {
                    return (true, "Chocolatey installed successfully");
                }
                else
                {
                    return (false, "Failed to install Chocolatey. Please try installing it manually.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error installing Chocolatey: {ex.Message}");
            }
        }
        
        public (List<Package> Packages, string Message) GetInstalledPackages()
        {
            if (!IsInstalled())
            {
                return (new List<Package>(), "Chocolatey is not installed");
            }
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _chocoPath,
                    Arguments = "list --local-only --limit-output",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (new List<Package>(), "Failed to start process");
                }
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                var packages = new List<Package>();
                
                if (process.ExitCode == 0)
                {
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            var installDate = DateTime.Now.ToString("o");
                            
                            packages.Add(new Package
                            {
                                Id = parts[0].Trim(),
                                Version = parts[1].Trim(),
                                IsInstalled = true,
                                InstallDate = DateTime.Now
                            });
                        }
                    }
                    
                    return (packages, "Success");
                }
                else
                {
                    return (new List<Package>(), $"Failed to get installed packages: Exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                return (new List<Package>(), $"Error getting installed packages: {ex.Message}");
            }
        }
        
        public (bool Success, string Message) InstallPackage(string packageId)
        {
            if (!IsInstalled())
            {
                return (false, "Chocolatey is not installed");
            }
            
            try
            {
                var (installedPackages, _) = GetInstalledPackages();
                
                if (installedPackages.Any(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (true, $"Package {packageId} is already installed");
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Start-Process '{_chocoPath}' -ArgumentList 'install {packageId} -y' -Verb RunAs -Wait\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                
                    
                (installedPackages, _) = GetInstalledPackages();
                
                if (installedPackages.Any(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (true, $"Successfully installed {packageId}");
                }
                else
                {
                    return (false, $"Failed to install {packageId}. The operation may require admin privileges.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error installing package {packageId}: {ex.Message}");
            }
        }
        
        public (bool Success, string Message) UninstallPackage(string packageId)
        {
            if (!IsInstalled())
            {
                return (false, "Chocolatey is not installed");
            }
            
            try
            {
                var (installedPackages, _) = GetInstalledPackages();
                
                if (!installedPackages.Any(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (true, $"Package {packageId} is not installed");
                }
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Start-Process '{_chocoPath}' -ArgumentList 'uninstall {packageId} -y' -Verb RunAs -Wait\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                
                (installedPackages, _) = GetInstalledPackages();
                
                if (!installedPackages.Any(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (true, $"Successfully uninstalled {packageId}");
                }
                else
                {
                    return (false, $"Failed to uninstall {packageId}. The operation may require admin privileges.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error uninstalling package {packageId}: {ex.Message}");
            }
        }
        
        public (List<Package> Packages, string Message) SearchPackage(string query)
        {
            if (!IsInstalled())
            {
                return (new List<Package>(), "Chocolatey is not installed");
            }
            
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _chocoPath,
                    Arguments = $"search {query} --limit-output",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (new List<Package>(), "Failed to start process");
                }
                
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                var packages = new List<Package>();
                
                if (process.ExitCode == 0)
                {
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            packages.Add(new Package
                            {
                                Id = parts[0].Trim(),
                                Version = parts[1].Trim()
                            });
                        }
                    }
                    
                    return (packages, "Success");
                }
                else
                {
                    return (new List<Package>(), $"Failed to search for packages: Exit code {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                return (new List<Package>(), $"Error searching packages: {ex.Message}");
            }
        }
        
        public (bool Success, string Message) UpgradePackage(string packageId)
        {
            if (!IsInstalled())
            {
                return (false, "Chocolatey is not installed");
            }
            
            try
            {
                var (installedPackages, _) = GetInstalledPackages();
                var existingPackage = installedPackages.FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                
                if (existingPackage == null)
                {
                    return (false, $"Package {packageId} is not installed");
                }
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Start-Process '{_chocoPath}' -ArgumentList 'upgrade {packageId} -y' -Verb RunAs -Wait\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                
                using var process = Process.Start(psi);
                process?.WaitForExit();
                
                (installedPackages, _) = GetInstalledPackages();
                var upgradedPackage = installedPackages.FirstOrDefault(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                
                if (upgradedPackage != null)
                {
                    return (true, $"Successfully upgraded {packageId} to version {upgradedPackage.Version}");
                }
                else
                {
                    return (false, $"Failed to upgrade {packageId}. The operation may require admin privileges.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error upgrading package {packageId}: {ex.Message}");
            }
        }
    }
} 