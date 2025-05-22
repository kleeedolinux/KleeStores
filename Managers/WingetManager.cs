using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public class WingetManager
    {
        private readonly WingetScraper _scraper;
        private readonly PackageManager _packageManager;
        
        public WingetManager(PackageManager packageManager)
        {
            _scraper = new WingetScraper();
            _packageManager = packageManager;
        }
        
        public async Task<List<Package>> GetPackagesAsync(
            int page = 1,
            int limit = 20,
            string? searchQuery = null,
            Action<List<Package>, int, int>? batchCallback = null,
            CancellationToken cancellationToken = default)
        {
            return await _scraper.GetPackagesAsync(page, limit, searchQuery, batchCallback, cancellationToken);
        }
        
        public async Task<Package?> GetPackageByIdAsync(string packageId, CancellationToken cancellationToken = default)
        {
            return await _scraper.GetPackageByIdAsync(packageId, cancellationToken);
        }
        
        public async Task<bool> InstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
        {
            var package = await GetPackageByIdAsync(packageId, cancellationToken);
            if (package == null)
            {
                return false;
            }
            
            return await _packageManager.InstallPackageAsync(package, cancellationToken);
        }
        
        public async Task<bool> UninstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
        {
            var package = await GetPackageByIdAsync(packageId, cancellationToken);
            if (package == null)
            {
                return false;
            }
            
            return await _packageManager.UninstallPackageAsync(package, cancellationToken);
        }
        
        public async Task<bool> IsPackageInstalledAsync(string packageId, CancellationToken cancellationToken = default)
        {
            var package = await GetPackageByIdAsync(packageId, cancellationToken);
            if (package == null)
            {
                return false;
            }
            
            return await _packageManager.IsPackageInstalledAsync(package, cancellationToken);
        }
    }
} 