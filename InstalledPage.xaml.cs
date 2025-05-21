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
        private readonly DatabaseManager _dbManager;
        private readonly ChocolateyManager _chocoManager;
        private bool _isRefreshing;
        
        public InstalledPage()
        {
            InitializeComponent();
            
            _dbManager = DatabaseManager.Instance;
            _chocoManager = ChocolateyManager.Instance;
            
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
                
                
                if (forceRefresh)
                {
                    await UpdateInstalledPackagesInDbAsync();
                }
                
                
                var packages = _dbManager.GetInstalledPackages();
                
                
                if (packages.Count == 0)
                {
                    EmptyMessage.Text = "No installed packages found";
                    EmptyMessage.Visibility = Visibility.Visible;
                    return;
                }
                
                
                EmptyMessage.Visibility = Visibility.Collapsed;
                
                
                foreach (var package in packages)
                {
                    var card = new PackageCard(package);
                    card.InstallationChanged += HandleInstallationChange;
                    
                    card.MinWidth = 400;
                    card.MaxWidth = 800;
                    
                    InstalledContainer.Children.Add(card);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }
        
        private async Task UpdateInstalledPackagesInDbAsync()
        {
            await Task.Run(() =>
            {
                
                var (installedPackages, _) = _chocoManager.GetInstalledPackages();
                
                
                var updates = new Dictionary<string, string>();
                
                foreach (var package in installedPackages)
                {
                    var installDate = package.InstallDate?.ToString("o") ?? DateTime.Now.ToString("o");
                    updates[package.Id] = installDate;
                }
                
                
                _dbManager.BatchUpdateInstallationStatus(updates);
            });
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