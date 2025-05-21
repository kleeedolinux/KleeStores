using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KleeStore.Managers;
using KleeStore.Models;

namespace KleeStore
{
    public partial class BrowsePage : Page
    {
        private readonly DatabaseManager _dbManager;
        private readonly ChocolateyManager _chocoManager;
        private int _currentPage = 1;
        private int _itemsPerPage = 30;
        private string _searchQuery = string.Empty;
        private CancellationTokenSource? _scraperCts;
        
        public BrowsePage()
        {
            InitializeComponent();
            
            _dbManager = DatabaseManager.Instance;
            _chocoManager = ChocolateyManager.Instance;
            
            DisplayPackages();
        }
        
        public List<Package> DisplayPackages(string? searchQuery = null)
        {
            
            _searchQuery = searchQuery ?? string.Empty;
            
            
            PackagesContainer.Children.Clear();
            PackagesContainer.Children.Add(EmptyMessage);
            
            
            int offset = (_currentPage - 1) * _itemsPerPage;
            
            
            PageLabel.Text = $"Page {_currentPage}";
            
            
            List<Package> packages;
            
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                packages = _dbManager.SearchPackages(_searchQuery, _itemsPerPage, offset);
            }
            else
            {
                packages = _dbManager.GetAllPackages(_itemsPerPage, offset);
            }
            
            
            if (packages.Count == 0)
            {
                if (string.IsNullOrEmpty(_searchQuery) && _dbManager.GetPackageCount() == 0)
                {
                    
                    EmptyMessage.Text = "No packages found. Starting automatic download...";
                    EmptyMessage.Visibility = Visibility.Visible;
                    
                    
                    var mainWindow = Window.GetWindow(this) as MainWindow;
                    mainWindow?.StartDownload();
                }
                else
                {
                    
                    EmptyMessage.Text = string.IsNullOrEmpty(_searchQuery)
                        ? "No more packages to display"
                        : $"No packages found for '{_searchQuery}'";
                    EmptyMessage.Visibility = Visibility.Visible;
                }
                
                return packages;
            }
            
            
            EmptyMessage.Visibility = Visibility.Collapsed;
            
            
            UpdateInstalledStatus(packages);
            
            
            foreach (var package in packages)
            {
                var card = new PackageCard(package);
                card.InstallationChanged += HandleInstallationChange;
                
                card.MinWidth = 400;
                card.MaxWidth = 800;
                
                PackagesContainer.Children.Add(card);
            }
            
            return packages;
        }
        
        private void UpdateInstalledStatus(List<Package> packages)
        {
            var (installedPackages, _) = _chocoManager.GetInstalledPackages();
            var installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var package in installedPackages)
            {
                installedIds.Add(package.Id);
            }
            
            foreach (var package in packages)
            {
                if (installedIds.Contains(package.Id))
                {
                    package.IsInstalled = true;
                    _dbManager.UpdatePackageInstallationStatus(package.Id, true);
                }
            }
        }
        
        private void HandleInstallationChange(string packageId, bool isInstalled)
        {
            
            DisplayPackages(_searchQuery);
            
            
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.RefreshInstalledPackages();
            }
        }
        
        public void ProcessScrapedPackages(List<Package> packages, int currentPage, int maxPages)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentPage == 1 && string.IsNullOrEmpty(_searchQuery))
                {
                    if (EmptyMessage.Visibility == Visibility.Visible)
                    {
                        PackagesContainer.Children.Clear();
                        EmptyMessage.Visibility = Visibility.Collapsed;
                    }
                    
                    foreach (var package in packages)
                    {
                        var card = new PackageCard(package);
                        card.InstallationChanged += HandleInstallationChange;
                        
                        card.MinWidth = 400;
                        card.MaxWidth = 800;
                        
                        PackagesContainer.Children.Add(card);
                    }
                    
                    PageLabel.Text = $"Page {_currentPage} - {PackagesContainer.Children.Count} packages";
                }
            });
        }
        
        public void CancelScraping()
        {
            _scraperCts?.Cancel();
            _scraperCts = null;
        }
        
        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                DisplayPackages(_searchQuery);
            }
        }
        
        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            var packages = DisplayPackages(_searchQuery);
            
            
            if (EmptyMessage.Visibility == Visibility.Visible && _currentPage > 1)
            {
                _currentPage--;
                DisplayPackages(_searchQuery);
            }
        }
    }
} 