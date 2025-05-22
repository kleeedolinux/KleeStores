using System;

namespace KleeStore.Models
{
    public class Package
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string AvailableVersion { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string InstallCommand { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public int Downloads { get; set; }
        public string DetailsUrl { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public DateTime? InstallDate { get; set; }
        public bool CanUpdate { get; set; }
    }
} 