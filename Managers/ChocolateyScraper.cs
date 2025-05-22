using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public class ChocolateyScraper
    {
        private readonly string _baseApiUrl = "https://kleestoreapi.vercel.app/api/packages";
        private readonly HttpClient _httpClient;
        private readonly int _packagesPerPage = 20;
        private readonly int _maxRequestsPerMinute = 10;
        private readonly SemaphoreSlim _rateLimitSemaphore;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, object> _packageCache = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, DateTime> _packageCacheExpiry = new ConcurrentDictionary<string, DateTime>();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);
        
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        public ChocolateyScraper()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KleeStore Software Manager");
            _rateLimitSemaphore = new SemaphoreSlim(_maxRequestsPerMinute, _maxRequestsPerMinute);
        }
        
        private async Task<HttpResponseMessage> MakeApiRequest(string url, CancellationToken cancellationToken)
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
                if (timeSinceLastRequest.TotalSeconds < 6 && _lastRequestTime != DateTime.MinValue)
                {
                    await Task.Delay(TimeSpan.FromSeconds(6) - timeSinceLastRequest, cancellationToken);
                }
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                _lastRequestTime = DateTime.Now;
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        if (int.TryParse(values.FirstOrDefault(), out int seconds))
                        {
                            Console.WriteLine($"Rate limited: Waiting {seconds} seconds before retrying");
                            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
                            response = await _httpClient.GetAsync(url, cancellationToken);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Rate limited: Waiting 60 seconds before retrying");
                        await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                        response = await _httpClient.GetAsync(url, cancellationToken);
                    }
                }
                
                return response;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        
        public async Task<List<Package>> GetPackagesAsync(
            int page = 1,
            int limit = 20,
            string? searchQuery = null,
            Action<List<Package>, int, int>? batchCallback = null,
            CancellationToken cancellationToken = default)
        {
            var packages = new List<Package>();
            
            try
            {
                
                string cacheKey = $"page_{page}_limit_{limit}_search_{searchQuery ?? "none"}";
                
                
                if (_packageCacheExpiry.TryGetValue(cacheKey, out var expiry) && 
                    DateTime.Now < expiry && 
                    _packageCache.TryGetValue(cacheKey, out var cachedObj))
                {
                    if (cachedObj is List<Package> cachedPackages)
                    {
                        if (batchCallback != null)
                        {
                            
                            var paginationInfo = await GetPaginationInfoAsync(cancellationToken);
                            if (paginationInfo?.Pagination != null)
                            {
                                batchCallback(cachedPackages, page, paginationInfo.Pagination.Pages);
                            }
                        }
                        return cachedPackages;
                    }
                }
                
                string apiUrl;
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    apiUrl = $"{_baseApiUrl}/search?q={Uri.EscapeDataString(searchQuery)}&page={page}&limit={limit}";
                }
                else
                {
                    apiUrl = $"{_baseApiUrl}?page={page}&limit={limit}&sort=downloads";
                }
                
                var response = await MakeApiRequest(apiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
                
                if (apiResponse != null && apiResponse.Packages != null)
                {
                    foreach (var apiPackage in apiResponse.Packages)
                    {
                        var package = new Package
                        {
                            Id = apiPackage.Id,
                            Name = apiPackage.Name,
                            Version = apiPackage.Version,
                            Description = apiPackage.Description,
                            ImageUrl = apiPackage.ImageUrl,
                            InstallCommand = apiPackage.InstallCommand,
                            Downloads = apiPackage.Downloads,
                            DetailsUrl = apiPackage.DetailsUrl
                        };
                        
                        packages.Add(package);
                        
                        
                        _packageCache[package.Id] = package;
                        _packageCacheExpiry[package.Id] = DateTime.Now.Add(_cacheDuration);
                    }
                    
                    
                    _packageCache[cacheKey] = packages;
                    _packageCacheExpiry[cacheKey] = DateTime.Now.Add(_cacheDuration);
                    
                    if (batchCallback != null && apiResponse.Pagination != null)
                    {
                        batchCallback(packages, apiResponse.Pagination.Page, apiResponse.Pagination.Pages);
                    }
                    
                    return packages;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching packages: {ex.Message}");
            }
            
            return packages;
        }
        
        public async Task<ApiResponse?> GetPaginationInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                
                string cacheKey = "pagination_info";
                if (_packageCacheExpiry.TryGetValue(cacheKey, out var expiry) && 
                    DateTime.Now < expiry && 
                    _packageCache.TryGetValue(cacheKey, out var cachedObj))
                {
                    if (cachedObj is ApiResponse cachedInfo)
                    {
                        return cachedInfo;
                    }
                }
                
                var response = await MakeApiRequest($"{_baseApiUrl}?page=1&limit=1", cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json, _jsonOptions);
                
                if (apiResponse != null)
                {
                    
                    _packageCache[cacheKey] = apiResponse;
                    _packageCacheExpiry[cacheKey] = DateTime.Now.Add(_cacheDuration);
                }
                
                return apiResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting pagination info: {ex.Message}");
                return null;
            }
        }
        
        public async Task<Package?> GetPackageByIdAsync(string packageId, CancellationToken cancellationToken = default)
        {
            try
            {
                
                if (_packageCacheExpiry.TryGetValue(packageId, out var expiry) && 
                    DateTime.Now < expiry && 
                    _packageCache.TryGetValue(packageId, out var cachedObj))
                {
                    if (cachedObj is Package cachedPackage)
                    {
                        return cachedPackage;
                    }
                }
                
                var response = await MakeApiRequest($"{_baseApiUrl}/{packageId}", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var apiResponse = JsonSerializer.Deserialize<PackageResponse>(json, _jsonOptions);
                
                if (apiResponse?.Package != null)
                {
                    var apiPackage = apiResponse.Package;
                    var package = new Package
                    {
                        Id = apiPackage.Id,
                        Name = apiPackage.Name,
                        Version = apiPackage.Version,
                        Description = apiPackage.Description,
                        ImageUrl = apiPackage.ImageUrl,
                        InstallCommand = apiPackage.InstallCommand,
                        Downloads = apiPackage.Downloads,
                        DetailsUrl = apiPackage.DetailsUrl
                    };
                    
                    
                    _packageCache[packageId] = package;
                    _packageCacheExpiry[packageId] = DateTime.Now.Add(_cacheDuration);
                    
                    return package;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching package by ID: {ex.Message}");
                return null;
            }
        }
        
        public class ApiResponse
        {
            public List<ApiPackage> Packages { get; set; } = new List<ApiPackage>();
            public ApiPagination Pagination { get; set; } = new ApiPagination();
            public string? Query { get; set; }
        }
        
        public class PackageResponse
        {
            public ApiPackage Package { get; set; } = new ApiPackage();
        }
        
        public class ApiPackage
        {
            public string _id { get; set; } = string.Empty;
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;
            public string InstallCommand { get; set; } = string.Empty;
            public int Downloads { get; set; }
            public string DetailsUrl { get; set; } = string.Empty;
            public int __v { get; set; }
            public string CreatedAt { get; set; } = string.Empty;
            public string UpdatedAt { get; set; } = string.Empty;
            public string LastUpdated { get; set; } = string.Empty;
        }
        
        public class ApiPagination
        {
            public int Page { get; set; }
            public int Limit { get; set; }
            public int Total { get; set; }
            public int Pages { get; set; }
        }
    }
} 