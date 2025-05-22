using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                ShowLoadingIndicator("Initializing...");
                
                var paginationInfo = await _scraper.GetPaginationInfoAsync();
                if (paginationInfo?.Pagination != null)
                {
                    _totalPages = paginationInfo.Pagination.Pages;
                }
                
                await LoadPackages();
                
                HideLoadingIndicator();
            }
            catch (Exception ex)
            {
                ShowError($"Error loading initial data: {ex.Message}");
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
                
                ShowLoadingIndicator(!string.IsNullOrEmpty(searchQuery) ? 
                    $"Searching for '{searchQuery}'..." : 
                    "Loading packages...");
                
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
                
                UpdatePaginationUI();
                
                var packages = await _scraper.GetPackagesAsync(
                    page: _currentPage,
                    limit: _itemsPerPage,
                    searchQuery: _searchQuery,
                    cancellationToken: _scraperCts.Token);
                
                UpdatePackagesDisplay(packages);
                
                return packages;
            }
            catch (Exception ex)
            {
                ShowError($"Error loading packages: {ex.Message}");
                Console.WriteLine($"Error loading packages: {ex.Message}");
                return new List<Package>();
            }
        }
        
        private void UpdatePaginationUI()
        {
            PageLabel.Text = $"Page {_currentPage} of {_totalPages}";
            
            
            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
            
            
            PrevPageButton.Opacity = PrevPageButton.IsEnabled ? 1.0 : 0.5;
            NextPageButton.Opacity = NextPageButton.IsEnabled ? 1.0 : 0.5;
        }
        
        private void UpdatePackagesDisplay(List<Package> packages)
        {
            PackagesContainer.Children.Clear();
            
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
                return;
            }
            
            EmptyMessage.Visibility = Visibility.Collapsed;
            
            UpdateInstalledStatus(packages);
            UpdateUpdatableStatus(packages);
            
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
            
            PageLabel.Text = $"Page {_currentPage} of {_totalPages} - {PackagesContainer.Children.Count} packages";
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
        
        private void UpdateUpdatableStatus(List<Package> packages)
        {
            var (updatablePackages, _) = _chocoManager.GetUpdatablePackages();
            var updatableDict = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var package in updatablePackages)
            {
                updatableDict[package.Id] = package;
            }
            
            foreach (var package in packages)
            {
                if (updatableDict.TryGetValue(package.Id, out var updatablePackage))
                {
                    package.CanUpdate = true;
                    package.AvailableVersion = updatablePackage.AvailableVersion;
                }
            }
        }
        
        private void ShowLoadingIndicator(string message)
        {
            LoadingMessage.Text = message;
            ProgressIndicator.Visibility = Visibility.Visible;
        }
        
        private void HideLoadingIndicator()
        {
            ProgressIndicator.Visibility = Visibility.Collapsed;
        }
        
        private void ShowError(string errorMessage)
        {
            EmptyMessage.Text = errorMessage;
            EmptyMessage.Visibility = Visibility.Visible;
            EmptyMessage.Foreground = new SolidColorBrush(Colors.Red);
            HideLoadingIndicator();
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
                        UpdatePackagesDisplay(packages);
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