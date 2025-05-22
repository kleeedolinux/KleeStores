using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json.Serialization;

namespace KleeStore
{
    public class AutoUpdate
    {
        private const string VersionApiUrl = "https://kleestoreapi.vercel.app/api/version";
        private static readonly HttpClient _httpClient;
        private static readonly Version CurrentVersion = new Version("2.1.0");
        private const int MaxRetries = 3;
        private const int RetryDelayMs = 1000;

        static AutoUpdate()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KleeStore");
        }

        public class VersionInfo
        {
            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;
            
            [JsonPropertyName("downloadUrl")]
            public string DownloadUrl { get; set; } = string.Empty;
            
            [JsonPropertyName("lastUpdated")]
            public DateTime LastUpdated { get; set; }
        }

        public static async Task CheckForUpdates()
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(VersionApiUrl);
                    //console.WriteLine($"API Response: {response}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    var versionInfo = JsonSerializer.Deserialize<VersionInfo>(response, options);
                    //console.WriteLine($"Current Version: {CurrentVersion}");
                    //console.WriteLine($"Latest Version: {versionInfo?.Version}");
                    //console.WriteLine($"Download URL: {versionInfo?.DownloadUrl}");
                    //console.WriteLine($"Last Updated: {versionInfo?.LastUpdated}");

                    if (!string.IsNullOrEmpty(versionInfo?.Version))
                    {
                        var versionString = versionInfo.Version.TrimStart('v');
                        //console.WriteLine($"Cleaned Version String: {versionString}");
                        
                        if (Version.TryParse(versionString, out Version? latestVersion) && latestVersion != null)
                        {
                            //console.WriteLine($"Parsed Latest Version: {latestVersion}");
                            //console.WriteLine($"Version Comparison: {latestVersion.CompareTo(CurrentVersion)}");
                            
                            if (latestVersion.CompareTo(CurrentVersion) < 0)
                            {
                                var result = MessageBox.Show(
                                    $"A new version ({versionInfo.Version}) is available. Would you like to download it?",
                                    "Update Available",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Information);

                                if (result == MessageBoxResult.Yes)
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = versionInfo.DownloadUrl,
                                        UseShellExecute = true
                                    });
                                }
                            }
                            else
                            {
                                //console.WriteLine("No update available - current version is up to date");
                            }
                        }
                        else
                        {
                            //console.WriteLine($"Failed to parse version string: {versionString}");
                        }
                    }
                    else
                    {
                        //console.WriteLine("Version info is null or empty");
                    }
                    return;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == MaxRetries)
                    {
                        MessageBox.Show($"Failed to check for updates after {MaxRetries} attempts: {ex.Message}", 
                            "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await Task.Delay(RetryDelayMs * attempt);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to check for updates: {ex.Message}", 
                        "Update Check Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
        }
    }
} 