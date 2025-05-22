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
        private readonly ChocolateyManager _chocoManager = null!;
        private readonly BrowsePage _browsePage = null!;
        private readonly InstalledPage _installedPage = null!;
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
        
        private int _processedPackages = 0;
        
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                DataContext = this;
                
                _chocoManager = ChocolateyManager.Instance;
                
                IsAdminButtonVisible = !AdminUtils.IsAdmin() ? Visibility.Visible : Visibility.Collapsed;
                
                _browsePage = new BrowsePage();
                
                _installedPage = new InstalledPage();
                
                ContentFrame.Navigate(_browsePage);
                
                CheckChocolatey();
                
                _ = Task.Run(async () => await AutoUpdate.CheckForUpdates());
            }
            catch (Exception ex)
            {
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
                RefreshCurrentView();
            }
        }
        
        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            
            
        }
        
        private async void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearch();
            }
        }
        
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }
        
        private async Task PerformSearch()
        {
            
            if (ContentFrame.Content != _browsePage)
            {
                ContentFrame.Navigate(_browsePage);
                NavBrowse.Style = (Style)Application.Current.Resources["NavButtonActive"];
                NavInstalled.Style = (Style)Application.Current.Resources["NavButton"];
            }
            
            
            var query = SearchInput.Text?.Trim() ?? string.Empty;
            
            
            await _browsePage.DisplayPackages(query);
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshCurrentView();
        }
        
        private async void RefreshCurrentView()
        {
            if (ContentFrame.Content == _browsePage)
            {
                await StartDownload();
            }
            else if (ContentFrame.Content == _installedPage)
            {
                await _installedPage.DisplayInstalledPackages(true);
            }
            
            
            RefreshButton.IsEnabled = false;
            await Task.Delay(300);
            Dispatcher.Invoke(() => RefreshButton.IsEnabled = true);
        }
        
        public async Task RefreshInstalledPackages()
        {
            await _installedPage.DisplayInstalledPackages(true);
        }
        
        public async Task StartDownload()
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
            
            _processedPackages = 0;
            UpdateProgress(true, "Initializing API connection...", 1);
            
            if (ContentFrame.Content != _browsePage)
            {
                ContentFrame.Navigate(_browsePage);
                NavBrowse.Style = (Style)Application.Current.Resources["NavButtonActive"];
                NavInstalled.Style = (Style)Application.Current.Resources["NavButton"];
            }
            
            _isDownloading = true;
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            
            await Task.Factory.StartNew(async () => 
            {
                try
                {
                    await Task.Delay(100); 
                    
                    await Dispatcher.BeginInvoke(new Action(() => 
                    {
                        UpdateProgress(true, "Connecting to Chocolatey API...", 5);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                    
                    var scraper = new ChocolateyScraper();
                    var paginationInfo = await scraper.GetPaginationInfoAsync(_downloadCts.Token);
                    int totalPages = paginationInfo?.Pagination?.Pages ?? 3;
                    
                    
                    var packages = await scraper.GetPackagesAsync(
                        page: 1,
                        limit: 20,
                        batchCallback: ProcessScrapedBatch,
                        cancellationToken: _downloadCts.Token);
                    
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdateProgress(false, "", 0);
                            _browsePage.ProcessScrapedPackages(packages, 1, totalPages);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error updating UI: {ex.Message}");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (OperationCanceledException)
                {
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdateProgress(false, "", 0);
                        }
                        catch
                        {
                            
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdateProgress(false, "", 0);
                            MessageBox.Show($"Error connecting to API: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        catch
                        {
                            
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                finally
                {
                    _isDownloading = false;
                }
            }, TaskCreationOptions.LongRunning);
        }
        
        private void ProcessScrapedBatch(List<Package> packages, int currentPage, int totalPages)
        {
            try
            {
                _processedPackages += packages.Count;
                int progressValue = Math.Min((int)((double)currentPage / totalPages * 100), 99);
                
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        UpdateProgress(true, $"Downloaded {_processedPackages} packages from page {currentPage} of {totalPages}", progressValue);
                        
                        if (_processedPackages % 10 == 0 || currentPage % 2 == 0)
                        {
                            _browsePage.ProcessScrapedPackages(packages, currentPage, totalPages);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing batch: {ex.Message}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessScrapedBatch: {ex.Message}");
            }
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
            
            ImageCache.Instance.Stop();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 