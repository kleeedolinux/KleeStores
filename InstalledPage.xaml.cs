using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KleeStore.Managers;
using KleeStore.Models;

namespace KleeStore
{
    public partial class InstalledPage : Page
    {
        private readonly ChocolateyManager _chocoManager;
        private readonly ChocolateyScraper _scraper;
        private bool _isRefreshing;
        
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
                
                var installedPackages = await GetInstalledPackagesAsync();
                
                if (installedPackages.Count == 0)
                {
                    EmptyMessage.Text = "No installed packages found";
                    EmptyMessage.Visibility = Visibility.Visible;
                    return;
                }
                
                EmptyMessage.Visibility = Visibility.Collapsed;
                
                foreach (var package in installedPackages)
                {
                    try
                    {
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
                Console.WriteLine($"Error displaying installed packages: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }
        
        private async Task<List<Package>> GetInstalledPackagesAsync()
        {
            var result = new List<Package>();
            var (installedPackages, _) = _chocoManager.GetInstalledPackages();
            
            foreach (var package in installedPackages)
            {
                try
                {
                    
                    var apiPackage = await _scraper.GetPackageByIdAsync(package.Id);
                    
                    if (apiPackage != null)
                    {
                        
                        apiPackage.IsInstalled = true;
                        apiPackage.InstallDate = DateTime.Now; 
                        result.Add(apiPackage);
                    }
                    else
                    {
                        
                        package.IsInstalled = true;
                        package.InstallDate = DateTime.Now;
                        result.Add(package);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching package details for {package.Id}: {ex.Message}");
                    
                    package.IsInstalled = true;
                    package.InstallDate = DateTime.Now;
                    result.Add(package);
                }
            }
            
            return result;
        }
        
        private void HandleInstallationChange(string packageId, bool isInstalled)
        {
            if (!isInstalled)
            {
                DisplayInstalledPackages(true);
            }
        }
    }
} 