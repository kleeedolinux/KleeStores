using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KleeStore.Managers;
using KleeStore.Models;
using KleeStore.Utilities;
using System.Threading.Tasks;

namespace KleeStore
{
    public partial class PackageCard : UserControl
    {
        private Package _package;
        private readonly ChocolateyManager _chocoManager;
        
        public event Action<string, bool>? InstallationChanged;
        
        public string PackageName
        {
            get => (string)GetValue(PackageNameProperty);
            set => SetValue(PackageNameProperty, value);
        }
        
        public static readonly DependencyProperty PackageNameProperty =
            DependencyProperty.Register("PackageName", typeof(string), typeof(PackageCard), new PropertyMetadata(string.Empty));
        
        public string PackageVersion
        {
            get => (string)GetValue(PackageVersionProperty);
            set => SetValue(PackageVersionProperty, value);
        }
        
        public static readonly DependencyProperty PackageVersionProperty =
            DependencyProperty.Register("PackageVersion", typeof(string), typeof(PackageCard), new PropertyMetadata(string.Empty));
        
        public string PackageDescription
        {
            get => (string)GetValue(PackageDescriptionProperty);
            set => SetValue(PackageDescriptionProperty, value);
        }
        
        public static readonly DependencyProperty PackageDescriptionProperty =
            DependencyProperty.Register("PackageDescription", typeof(string), typeof(PackageCard), new PropertyMetadata(string.Empty));
        
        public string DownloadsCount
        {
            get => (string)GetValue(DownloadsCountProperty);
            set => SetValue(DownloadsCountProperty, value);
        }
        
        public static readonly DependencyProperty DownloadsCountProperty =
            DependencyProperty.Register("DownloadsCount", typeof(string), typeof(PackageCard), new PropertyMetadata(string.Empty));
        
        public PackageCard(Package package)
        {
            InitializeComponent();
            
            _package = package;
            _chocoManager = ChocolateyManager.Instance;
            
            PackageName = package.Name;
            PackageVersion = package.Version;
            PackageDescription = package.Description;
            DownloadsCount = package.Downloads.ToString("N0");
            
            UpdateButtonState();
            
            SetDefaultImage();
            
            if (!string.IsNullOrEmpty(package.ImageUrl) && package.ImageUrl != "No image")
            {
                var imageCache = ImageCache.Instance;
                imageCache.QueueDownload(package.ImageUrl, package.Id);
                imageCache.ImageReady += ImageCache_ImageReady;
            }
            
            if (package.CanUpdate && !string.IsNullOrEmpty(package.AvailableVersion))
            {
                UpdateButton.Visibility = Visibility.Visible;
                UpdateButton.Content = $"Update to {package.AvailableVersion}";
            }
            else
            {
                UpdateButton.Visibility = Visibility.Collapsed;
            }
        }
        
        private void ImageCache_ImageReady(string packageId, BitmapImage image)
        {
            if (packageId == _package.Id)
            {
                Dispatcher.Invoke(() => {
                    PackageImageControl.Source = image;
                });
            }
        }
        
        private void SetDefaultImage()
        {
            var drawingVisual = new DrawingVisual();
            
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(
                    Brushes.Transparent,
                    null,
                    new Rect(0, 0, 64, 64));
                
                context.DrawRoundedRectangle(
                    new SolidColorBrush(Color.FromRgb(0x35, 0x84, 0xE4)), 
                    null,
                    new Rect(12, 12, 40, 40),
                    6, 6);
                
                var pen = new Pen(Brushes.White, 2);
                context.DrawLine(pen, new Point(32, 12), new Point(32, 52));
                context.DrawLine(pen, new Point(12, 32), new Point(52, 32));
            }
            
            var renderTarget = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);
            renderTarget.Freeze();
            
            PackageImageControl.Source = BitmapFrame.Create(renderTarget);
        }
        
        private void UpdateButtonState()
        {
            InstallButton.Content = _package.IsInstalled ? "Uninstall" : "Install";
            InstallButton.Style = (Style)Application.Current.Resources[_package.IsInstalled ? "DangerButton" : "ActionButton"];
        }
        
        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_package.Id))
            {
                ShowMessage("Error", "Package ID not found.");
                return;
            }
            
            try
            {
                InstallButton.IsEnabled = false;
                OperationProgress.Visibility = Visibility.Visible;
                OperationProgress.IsIndeterminate = true;
                
                if (_package.IsInstalled)
                {
                    var (success, message) = await Task.Run(() => _chocoManager.UninstallPackage(_package.Id));
                    if (success)
                    {
                        _package.IsInstalled = false;
                        ShowMessage("Success", $"Successfully uninstalled {_package.Id}");
                        InstallationChanged?.Invoke(_package.Id, false);
                    }
                    else
                    {
                        if (message.Contains("privileges") && !AdminUtils.IsAdmin())
                        {
                            var result = MessageBox.Show(
                                $"Failed to uninstall {_package.Id}. Do you want to restart as administrator?",
                                "Admin Privileges Required",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                AdminUtils.RunAsAdmin();
                                return;
                            }
                        }
                        
                        ShowMessage("Error", $"Failed to uninstall: {message}");
                    }
                }
                else
                {
                    var (success, message) = await Task.Run(() => _chocoManager.InstallPackage(_package.Id));
                    if (success)
                    {
                        var installDate = DateTime.Now.ToString("o");
                        _package.IsInstalled = true;
                        _package.InstallDate = DateTime.Now;
                        ShowMessage("Success", $"Successfully installed {_package.Id}");
                        InstallationChanged?.Invoke(_package.Id, true);
                    }
                    else
                    {
                        if (message.Contains("privileges") && !AdminUtils.IsAdmin())
                        {
                            var result = MessageBox.Show(
                                $"Failed to install {_package.Id}. Do you want to restart as administrator?",
                                "Admin Privileges Required",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                AdminUtils.RunAsAdmin();
                                return;
                            }
                        }
                        
                        ShowMessage("Error", $"Failed to install: {message}");
                    }
                }
                
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"An error occurred: {ex.Message}");
            }
            finally
            {
                InstallButton.IsEnabled = true;
                OperationProgress.Visibility = Visibility.Collapsed;
                OperationProgress.IsIndeterminate = false;
            }
        }
        
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_package.Id))
            {
                ShowMessage("Error", "Package ID not found.");
                return;
            }
            
            try
            {
                UpdateButton.IsEnabled = false;
                OperationProgress.Visibility = Visibility.Visible;
                OperationProgress.IsIndeterminate = true;
                
                var (success, message) = await Task.Run(() => _chocoManager.UpgradePackage(_package.Id));
                if (success)
                {
                    _package.Version = _package.AvailableVersion;
                    _package.AvailableVersion = string.Empty;
                    _package.CanUpdate = false;
                    
                    UpdateButton.Visibility = Visibility.Collapsed;
                    PackageVersion = _package.Version;
                    
                    ShowMessage("Success", $"Successfully updated {_package.Id} to version {_package.Version}");
                    InstallationChanged?.Invoke(_package.Id, true);
                }
                else
                {
                    if (message.Contains("privileges") && !AdminUtils.IsAdmin())
                    {
                        var result = MessageBox.Show(
                            $"Failed to update {_package.Id}. Do you want to restart as administrator?",
                            "Admin Privileges Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            AdminUtils.RunAsAdmin();
                            return;
                        }
                    }
                    
                    ShowMessage("Error", $"Failed to update: {message}");
                }
            }
            catch (Exception ex)
            {
                ShowMessage("Error", $"An error occurred: {ex.Message}");
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                OperationProgress.Visibility = Visibility.Collapsed;
                OperationProgress.IsIndeterminate = false;
            }
        }
        
        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            var details = $"<b>Name:</b> {_package.Name}<br/>" +
                          $"<b>ID:</b> {_package.Id}<br/>" +
                          $"<b>Version:</b> {_package.Version}<br/>" +
                          $"<b>Downloads:</b> {_package.Downloads:N0}<br/>" +
                          $"<b>Status:</b> {(_package.IsInstalled ? "Installed" : "Not Installed")}<br/>";
            
            if (_package.InstallDate.HasValue)
            {
                details += $"<b>Install Date:</b> {_package.InstallDate.Value:g}<br/>";
            }
            
            details += $"<br/><b>Description:</b><br/>{_package.Description}<br/><br/>";
            
            if (!string.IsNullOrEmpty(_package.InstallCommand))
            {
                details += $"<b>Install Command:</b><br/>{_package.InstallCommand}<br/><br/>";
            }
            
            if (!string.IsNullOrEmpty(_package.DetailsUrl))
            {
                details += $"<b>More Info:</b><br/><a href=\"{_package.DetailsUrl}\">{_package.DetailsUrl}</a>";
            }
            
            var detailsWindow = new Window
            {
                Title = $"Package Details - {_package.Name}",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };
            
            var webBrowser = new System.Windows.Controls.WebBrowser();
            webBrowser.NavigateToString($"<html><body style='font-family: Segoe UI, sans-serif; padding: 15px;'>{details}</body></html>");
            
            detailsWindow.Content = webBrowser;
            detailsWindow.ShowDialog();
        }
        
        private void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, 
                title.Contains("Error") ? MessageBoxImage.Error : MessageBoxImage.Information);
        }
    }
} 