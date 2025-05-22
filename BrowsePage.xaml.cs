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
        private readonly ChocolateyManager _chocoManager;
        private readonly ChocolateyScraper _scraper;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private int _itemsPerPage = 20;
        private string _searchQuery = string.Empty;
        private CancellationTokenSource? _scraperCts;
        private bool _isLoading = false;
        
        public BrowsePage()
        {
            InitializeComponent();
            
            _chocoManager = ChocolateyManager.Instance;
            _scraper = new ChocolateyScraper();
            
            LoadInitialData();
        }
        
        private async void LoadInitialData()
        {
            try
            {
                
                var paginationInfo = await _scraper.GetPaginationInfoAsync();
                if (paginationInfo?.Pagination != null)
                {
                    _totalPages = paginationInfo.Pagination.Pages;
                }
                
                await LoadPackages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading initial data: {ex.Message}");
            }
        }
        
        public async Task<List<Package>> DisplayPackages(string? searchQuery = null)
        {
            if (_isLoading) return new List<Package>();
            _isLoading = true;
            
            try
            {
                _searchQuery = searchQuery ?? string.Empty;
                
                PackagesContainer.Children.Clear();
                PackagesContainer.Children.Add(EmptyMessage);
                
                EmptyMessage.TextWrapping = TextWrapping.Wrap;
                EmptyMessage.Text = "Loading packages...";
                EmptyMessage.Visibility = Visibility.Visible;
                
                return await LoadPackages();
            }
            finally
            {
                _isLoading = false;
            }
        }
        
        private async Task<List<Package>> LoadPackages()
        {
            try
            {
                _scraperCts?.Cancel();
                _scraperCts = new CancellationTokenSource();
                
                PageLabel.Text = $"Page {_currentPage} of {_totalPages}";
                
                var packages = await _scraper.GetPackagesAsync(
                    page: _currentPage,
                    limit: _itemsPerPage,
                    searchQuery: _searchQuery,
                    cancellationToken: _scraperCts.Token);
                
                if (packages.Count == 0)
                {
                    if (string.IsNullOrEmpty(_searchQuery))
                    {
                        EmptyMessage.Text = "No packages found. The API may be unavailable.";
                    }
                    else
                    {
                        EmptyMessage.Text = $"No packages found for '{_searchQuery}'";
                    }
                    EmptyMessage.Visibility = Visibility.Visible;
                    
                    return packages;
                }
                
                EmptyMessage.Visibility = Visibility.Collapsed;
                PackagesContainer.Children.Clear();
                
                UpdateInstalledStatus(packages);
                
                foreach (var package in packages)
                {
                    try
                    {
                        var card = new PackageCard(package);
                        card.InstallationChanged += HandleInstallationChange;
                        
                        card.MinWidth = 400;
                        card.MaxWidth = 800;
                        
                        PackagesContainer.Children.Add(card);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating package card: {ex.Message}");
                    }
                }
                
                return packages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading packages: {ex.Message}");
                EmptyMessage.Text = $"Error loading packages: {ex.Message}";
                EmptyMessage.Visibility = Visibility.Visible;
                return new List<Package>();
            }
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
                }
            }
        }
        
        private void HandleInstallationChange(string packageId, bool isInstalled)
        {
            
            _ = LoadPackages();
            
            
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.RefreshInstalledPackages();
            }
        }
        
        public void ProcessScrapedPackages(List<Package> packages, int currentPage, int totalPages)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _totalPages = totalPages;
                        
                        if (EmptyMessage.Visibility == Visibility.Visible)
                        {
                            PackagesContainer.Children.Clear();
                            EmptyMessage.Visibility = Visibility.Collapsed;
                        }
                        
                        UpdateInstalledStatus(packages);
                        
                        foreach (var package in packages)
                        {
                            try
                            {
                                var card = new PackageCard(package);
                                card.InstallationChanged += HandleInstallationChange;
                                
                                card.MinWidth = 400;
                                card.MaxWidth = 800;
                                
                                PackagesContainer.Children.Add(card);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error creating package card: {ex.Message}");
                            }
                        }
                        
                        PageLabel.Text = $"Page {_currentPage} of {totalPages} - {PackagesContainer.Children.Count} packages";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing scraped packages: {ex.Message}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error invoking dispatcher: {ex.Message}");
            }
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
                _ = LoadPackages();
            }
        }
        
        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                _ = LoadPackages();
            }
        }
    }
} 