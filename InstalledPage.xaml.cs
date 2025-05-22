using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KleeStore.Managers;
using KleeStore.Models;

namespace KleeStore
{
    public partial class InstalledPage : Page
    {
        private readonly ChocolateyManager _chocoManager;
        private readonly ChocolateyScraper _scraper;
        private bool _isRefreshing;
        private readonly ConcurrentDictionary<string, Package> _packageCache = new ConcurrentDictionary<string, Package>();
        
        public InstalledPage()
        {
            InitializeComponent();
            
            _chocoManager = ChocolateyManager.Instance;
            _scraper = new ChocolateyScraper();
            
            DisplayInstalledPackages();
        }
        
        public async Task DisplayInstalledPackages(bool forceRefresh = false)
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            
            try
            {
                InstalledContainer.Children.Clear();
                InstalledContainer.Children.Add(EmptyMessage);
                
                EmptyMessage.Text = "Loading installed packages...";
                EmptyMessage.Visibility = Visibility.Visible;
                ProgressIndicator.Visibility = Visibility.Visible;
                PackageCountLabel.Text = "Loading...";
                
                var installedPackages = await GetInstalledPackagesAsync();
                
                if (installedPackages.Count == 0)
                {
                    EmptyMessage.Text = "No installed packages found";
                    EmptyMessage.Visibility = Visibility.Visible;
                    PackageCountLabel.Text = "No packages installed";
                    return;
                }
                
                EmptyMessage.Visibility = Visibility.Collapsed;
                PackageCountLabel.Text = $"{installedPackages.Count} package{(installedPackages.Count != 1 ? "s" : "")} installed";
                
                
                var (updatablePackages, _) = _chocoManager.GetUpdatablePackages();
                var updatableDict = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var package in updatablePackages)
                {
                    updatableDict[package.Id] = package;
                }
                
                foreach (var package in installedPackages)
                {
                    try
                    {
                        if (updatableDict.TryGetValue(package.Id, out var updatablePackage))
                        {
                            package.CanUpdate = true;
                            package.AvailableVersion = updatablePackage.AvailableVersion;
                        }
                        
                        var card = new PackageCard(package);
                        card.InstallationChanged += HandleInstallationChange;
                        
                        card.MinWidth = 400;
                        card.MaxWidth = 800;
                        
                        InstalledContainer.Children.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating package card: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                EmptyMessage.Text = $"Error loading installed packages: {ex.Message}";
                EmptyMessage.Visibility = Visibility.Visible;
                EmptyMessage.Foreground = new SolidColorBrush(Colors.Red);
                PackageCountLabel.Text = "Error loading packages";
                Console.WriteLine($"Error displaying installed packages: {ex.Message}");
            }
            finally
            {
                ProgressIndicator.Visibility = Visibility.Collapsed;
                _isRefreshing = false;
            }
        }
        
        private async Task<List<Package>> GetInstalledPackagesAsync()
        {
            var result = new List<Package>();
            var (installedPackages, _) = _chocoManager.GetInstalledPackages();
            
            
            var tasks = new List<Task<Package?>>();
            foreach (var package in installedPackages)
            {
                tasks.Add(Task.Run(async () => 
                {
                    try
                    {
                        
                        if (_packageCache.TryGetValue(package.Id, out var cachedPackage))
                        {
                            cachedPackage.IsInstalled = true;
                            cachedPackage.InstallDate = package.InstallDate ?? DateTime.Now;
                            return cachedPackage;
                        }
                        
                        
                        var apiPackage = await _scraper.GetPackageByIdAsync(package.Id);
                        
                        if (apiPackage != null)
                        {
                            apiPackage.IsInstalled = true;
                            apiPackage.InstallDate = package.InstallDate ?? DateTime.Now;
                            _packageCache.TryAdd(package.Id, apiPackage);
                            return apiPackage;
                        }
                        
                        
                        package.IsInstalled = true;
                        package.InstallDate = package.InstallDate ?? DateTime.Now;
                        return package;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching package details for {package.Id}: {ex.Message}");
                        package.IsInstalled = true;
                        package.InstallDate = package.InstallDate ?? DateTime.Now;
                        return package;
                    }
                }));
            }
            
            
            var packages = await Task.WhenAll(tasks);
            
            
            result.AddRange(packages.Where(p => p != null));
            
            return result;
        }
        
        private void HandleInstallationChange(string packageId, bool isInstalled)
        {
            if (!isInstalled)
            {
                _packageCache.TryRemove(packageId, out _);
                DisplayInstalledPackages(true);
            }
        }
    }
} 