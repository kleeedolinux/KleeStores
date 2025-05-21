using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public class ChocolateyScraper
    {
        private readonly string _baseUrl = "https://community.chocolatey.org/packages";
        private readonly HttpClient _httpClient;
        private readonly DatabaseManager _dbManager;
        
        public ChocolateyScraper(DatabaseManager? dbManager = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KleeStore Software Manager");
            _dbManager = dbManager ?? DatabaseManager.Instance;
        }
        
        public async Task<List<Package>> ScrapePackagesAsync(
            int maxPages = 900, 
            int maxWorkers = 5, 
            int batchSize = 0,
            Action<List<Package>, int, int>? batchCallback = null,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Starting to scrape Chocolatey packages...");
            
            var allPackages = new List<Package>();
            var currentBatch = new List<Package>();
            var pageUrls = new List<(int PageNumber, string Url)>();
            
            for (int pageNumber = 1; pageNumber <= maxPages; pageNumber++)
            {
                var url = $"{_baseUrl}?sortOrder=package-download-count&page={pageNumber}&prerelease=False&moderatorQueue=False&moderationStatus=all-statuses";
                pageUrls.Add((pageNumber, url));
            }
            
            using var semaphore = new SemaphoreSlim(maxWorkers);
            var tasks = new List<Task<List<Package>>>();
            
            foreach (var (pageNumber, url) in pageUrls)
            {
                await semaphore.WaitAsync(cancellationToken);
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await ScrapePageAsync(pageNumber, url, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);
                
                tasks.Add(task);
            }
            
            int completedPages = 0;
            
            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                
                try
                {
                    var packages = await completedTask;
                    
                    if (packages.Count > 0)
                    {
                        lock (allPackages)
                        {
                            allPackages.AddRange(packages);
                            currentBatch.AddRange(packages);
                            completedPages++;
                            
                            Console.WriteLine($"Completed page: Added {packages.Count} packages");
                            
                            if (batchSize > 0 && batchCallback != null && completedPages % batchSize == 0)
                            {
                                Console.WriteLine($"Batch of {currentBatch.Count} packages ready");
                                var batchCopy = new List<Package>(currentBatch);
                                batchCallback(batchCopy, completedPages, maxPages);
                                currentBatch.Clear();
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No packages found on page. May have reached the end.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing page: {ex.Message}");
                }
            }
            
            if (batchSize > 0 && batchCallback != null && currentBatch.Count > 0)
            {
                var batchCopy = new List<Package>(currentBatch);
                batchCallback(batchCopy, maxPages, maxPages);
            }
            
            Console.WriteLine($"Scraping completed. Found {allPackages.Count} packages.");
            
            if (_dbManager != null && allPackages.Count > 0)
            {
                SaveToDatabase(allPackages);
            }
            
            return allPackages;
        }
        
        private async Task<List<Package>> ScrapePageAsync(int pageNumber, string url, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Scraping page {pageNumber}: {url}");
            
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                var doc = new HtmlDocument();
                doc.LoadHtml(content);
                
                var packageNodes = doc.DocumentNode.SelectNodes("//ul[contains(@class, 'list-unstyled') and contains(@class, 'pt-3') and contains(@class, 'package-list-view')]/li");
                
                if (packageNodes == null || packageNodes.Count == 0)
                {
                    Console.WriteLine($"No packages found on page {pageNumber}.");
                    return new List<Package>();
                }
                
                var pagePackages = new List<Package>();
                
                foreach (var packageNode in packageNodes)
                {
                    try
                    {
                        var package = ExtractPackageInfo(packageNode);
                        if (package != null)
                        {
                            pagePackages.Add(package);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting package information: {ex.Message}");
                    }
                }
                
                return pagePackages;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Page {pageNumber} not found (404). Stopping.");
                return new List<Package>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error for page {pageNumber}: {ex.Message}");
                return new List<Package>();
            }
        }
        
        private Package? ExtractPackageInfo(HtmlNode packageNode)
        {
            var nameElement = packageNode.SelectSingleNode(".//a[contains(@class, 'h5') and contains(@class, 'fw-bold')]");
            if (nameElement == null) return null;
            
            var fullName = nameElement.InnerText.Trim();
            var nameParts = fullName.Split(' ');
            
            string name, version;
            
            if (nameParts.Length > 1)
            {
                version = nameParts[^1];
                name = string.Join(" ", nameParts, 0, nameParts.Length - 1);
            }
            else
            {
                name = fullName;
                version = "Unknown";
            }
            
            var imgElement = packageNode.SelectSingleNode(".//div[contains(@class, 'package-icon')]//img");
            var imageUrl = imgElement?.GetAttributeValue("src", "No image") ?? "No image";
            
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http"))
            {
                imageUrl = $"https://community.chocolatey.org{imageUrl}";
            }
            
            var descriptionElement = packageNode.SelectSingleNode(".//p[contains(@class, 'mt-2') and contains(@class, 'mb-0') and contains(@class, 'package-list-align')]");
            var description = descriptionElement?.InnerText.Trim() ?? "No description";
            
            var commandInput = packageNode.SelectSingleNode(".//input[contains(@class, 'form-control')]");
            var installCommand = commandInput?.GetAttributeValue("value", "Unknown") ?? "Unknown";
            
            string? packageId = null;
            
            if (installCommand.StartsWith("choco install "))
            {
                packageId = installCommand.Replace("choco install ", "").Trim();
            }
            else
            {
                var link = packageNode.SelectSingleNode(".//a[starts-with(@href, '/packages/')]");
                if (link != null)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        var parts = href.Split('/');
                        if (parts.Length > 0)
                        {
                            packageId = parts[^1];
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(packageId))
            {
                packageId = name.ToLowerInvariant().Replace(" ", "-");
            }
            
            var downloadsElement = packageNode.SelectSingleNode(".//span[contains(@class, 'badge')]");
            var downloads = 0;
            
            if (downloadsElement != null)
            {
                var downloadsText = downloadsElement.InnerText.Trim();
                var downloadsMatch = Regex.Match(downloadsText, @"([\d,]+)\s*Downloads");
                
                if (downloadsMatch.Success)
                {
                    var downloadsStr = downloadsMatch.Groups[1].Value.Replace(",", "");
                    int.TryParse(downloadsStr, out downloads);
                }
            }
            
            var detailsUrl = $"https://community.chocolatey.org/packages/{packageId}";
            
            var package = new Package
            {
                Id = packageId,
                Name = name.Trim(),
                Version = version.Trim(),
                Description = description,
                ImageUrl = imageUrl,
                InstallCommand = installCommand,
                Downloads = downloads,
                DetailsUrl = detailsUrl
            };
            
            Console.WriteLine($"Extracted: {name} ({packageId})");
            return package;
        }
        
        private void SaveToDatabase(List<Package> packages)
        {
            if (_dbManager == null)
            {
                Console.WriteLine("No database manager provided. Cannot save packages.");
                return;
            }
            
            int successful = 0;
            
            foreach (var package in packages)
            {
                if (_dbManager.AddOrUpdatePackage(package))
                {
                    successful++;
                }
            }
            
            Console.WriteLine($"Successfully saved {successful} out of {packages.Count} packages to database");
        }
    }
} 