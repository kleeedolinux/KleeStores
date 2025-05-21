using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KleeStore.Managers;
using KleeStore.Models;
using KleeStore.Utilities;

namespace KleeStore
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DatabaseManager _dbManager;
        private readonly ChocolateyManager _chocoManager;
        private readonly BrowsePage _browsePage;
        private readonly InstalledPage _installedPage;
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading;
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        private bool _isAdminButtonVisible;
        public Visibility IsAdminButtonVisible
        {
            get => _isAdminButtonVisible ? Visibility.Visible : Visibility.Collapsed;
            set
            {
                _isAdminButtonVisible = (value == Visibility.Visible);
                OnPropertyChanged(nameof(IsAdminButtonVisible));
            }
        }
        
        public MainWindow()
        {
            try
            {
                //console.WriteLine("Initializing MainWindow...");
                InitializeComponent();
                //console.WriteLine("InitializeComponent completed");
                
                DataContext = this;
                //console.WriteLine("DataContext set");
                
                
                _dbManager = DatabaseManager.Instance;
                //console.WriteLine("DatabaseManager initialized");
                _chocoManager = ChocolateyManager.Instance;
                //console.WriteLine("ChocolateyManager initialized");
                
                
                IsAdminButtonVisible = !AdminUtils.IsAdmin() ? Visibility.Visible : Visibility.Collapsed;
                //console.WriteLine("AdminButton visibility set");
                
                
                _browsePage = new BrowsePage();
                //console.WriteLine("BrowsePage created");
                _installedPage = new InstalledPage();
                //console.WriteLine("InstalledPage created");
                
                
                ContentFrame.Navigate(_browsePage);
                //console.WriteLine("Navigated to BrowsePage");
                
                
                CheckChocolatey();
                //console.WriteLine("CheckChocolatey completed");
            }
            catch (Exception ex)
            {
                //console.WriteLine($"ERROR in MainWindow constructor: {ex.Message}");
                //console.WriteLine(ex.StackTrace);
                MessageBox.Show($"Error initializing main window: {ex.Message}\n\n{ex.StackTrace}", 
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CheckChocolatey()
        {
            if (!_chocoManager.IsInstalled())
            {
                var result = MessageBox.Show(
                    "Chocolatey package manager is not installed. Would you like to install it now?",
                    "Chocolatey Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);
                
                if (result == MessageBoxResult.Yes)
                {
                    
                    if (!AdminUtils.IsAdmin())
                    {
                        var adminResult = MessageBox.Show(
                            "Installing Chocolatey requires administrator privileges. Do you want to restart as administrator?",
                            "Admin Privileges Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question,
                            MessageBoxResult.Yes);
                        
                        if (adminResult == MessageBoxResult.Yes)
                        {
                            AdminUtils.RunAsAdmin();
                            Application.Current.Shutdown();
                            return;
                        }
                        else
                        {
                            MessageBox.Show(
                                "Cannot install Chocolatey without administrator privileges.",
                                "Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return;
                        }
                    }
                    
                    
                    UpdateProgress(true, "Installing Chocolatey...", 0);
                    
                    
                    Task.Run(() =>
                    {
                        var (success, message) = _chocoManager.InstallChocolatey();
                        
                        Dispatcher.Invoke(() =>
                        {
                            if (success)
                            {
                                MessageBox.Show(
                                    "Chocolatey has been installed successfully.",
                                    "Success",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Failed to install Chocolatey: {message}",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }
                            
                            UpdateProgress(false, "", 0);
                        });
                    });
                }
            }
        }
        
        private void NavBrowse_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(0);
        }
        
        private void NavInstalled_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(1);
        }
        
        private void SwitchTab(int index)
        {
            
            if ((index == 0 && ContentFrame.Content == _browsePage) ||
                (index == 1 && ContentFrame.Content == _installedPage))
            {
                return;
            }
            
            
            NavBrowse.Style = (Style)Application.Current.Resources[index == 0 ? "NavButtonActive" : "NavButton"];
            NavInstalled.Style = (Style)Application.Current.Resources[index == 1 ? "NavButtonActive" : "NavButton"];
            
            
            ContentFrame.Navigate(index == 0 ? _browsePage : _installedPage);
            
            
            if (index == 1)
            {
                RefreshInstalledPackages();
            }
        }
        
        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            
            
        }
        
        private void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
            }
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }
        
        private void PerformSearch()
        {
            
            if (ContentFrame.Content != _browsePage)
            {
                ContentFrame.Navigate(_browsePage);
                NavBrowse.Style = (Style)Application.Current.Resources["NavButtonActive"];
                NavInstalled.Style = (Style)Application.Current.Resources["NavButton"];
            }
            
            
            var query = SearchInput.Text?.Trim() ?? string.Empty;
            
            
            _browsePage.DisplayPackages(query);
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshCurrentView();
        }
        
        private void RefreshCurrentView()
        {
            if (ContentFrame.Content == _browsePage)
            {
                StartDownload();
            }
            else if (ContentFrame.Content == _installedPage)
            {
                RefreshInstalledPackages();
            }
            
            
            RefreshButton.IsEnabled = false;
            Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => RefreshButton.IsEnabled = true));
        }
        
        public void RefreshInstalledPackages()
        {
            _installedPage.DisplayInstalledPackages(true);
        }
        
        public void StartDownload()
        {
            if (_isDownloading)
            {
                MessageBox.Show(
                    "Download already in progress.",
                    "Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            
            
            UpdateProgress(true, "Starting download...", 0);
            
            
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            
            
            if (ContentFrame.Content != _browsePage)
            {
                ContentFrame.Navigate(_browsePage);
                NavBrowse.Style = (Style)Application.Current.Resources["NavButtonActive"];
                NavInstalled.Style = (Style)Application.Current.Resources["NavButton"];
            }
            
            _isDownloading = true;
            
            
            Task.Run(async () =>
            {
                try
                {
                    var scraper = new ChocolateyScraper(_dbManager);
                    
                    await scraper.ScrapePackagesAsync(
                        maxPages: 50,
                        maxWorkers: 5,
                        batchSize: 5,
                        batchCallback: ProcessScrapedBatch,
                        cancellationToken: _downloadCts.Token);
                    
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress(false, "", 0);
                        MessageBox.Show(
                            "Package download completed successfully.",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        
                        
                        _browsePage.DisplayPackages();
                    });
                }
                catch (OperationCanceledException)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress(false, "", 0);
                        MessageBox.Show(
                            "Package download was cancelled.",
                            "Cancelled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateProgress(false, "", 0);
                        MessageBox.Show(
                            $"An error occurred while downloading packages: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
                finally
                {
                    _isDownloading = false;
                }
            });
        }
        
        private void ProcessScrapedBatch(List<Package> packages, int currentPage, int totalPages)
        {
            Dispatcher.Invoke(() =>
            {
                
                int progressValue = Math.Min((int)((double)currentPage / totalPages * 100), 99);
                UpdateProgress(true, $"Scraped {currentPage} of {totalPages} pages ({packages.Count} packages)", progressValue);
                
                
                _browsePage.ProcessScrapedPackages(packages, currentPage, totalPages);
            });
        }
        
        private void UpdateProgress(bool visible, string message, int value)
        {
            ProgressBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ProgressText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            
            ProgressBar.Value = value;
            ProgressText.Text = message;
        }
        
        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will restart the application with administrator privileges. Continue?",
                "Restart as Administrator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);
            
            if (result == MessageBoxResult.Yes)
            {
                AdminUtils.RunAsAdmin();
                Application.Current.Shutdown();
            }
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            
            
            _downloadCts?.Cancel();
            
            
            _dbManager.Close();
            
            
            ImageCache.Instance.Stop();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 