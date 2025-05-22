using System;
using System.Threading;
using System.Threading.Tasks;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public abstract class PackageManager
    {
        public abstract Task<bool> InstallPackageAsync(Package package, CancellationToken cancellationToken = default);
        public abstract Task<bool> UninstallPackageAsync(Package package, CancellationToken cancellationToken = default);
        public abstract Task<bool> IsPackageInstalledAsync(Package package, CancellationToken cancellationToken = default);
    }
} 