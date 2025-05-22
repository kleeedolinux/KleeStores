using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KleeStore.Utilities
{
    public class ImageCache
    {
        private static ImageCache? _instance;
        private static readonly object _lock = new object();
        
        private readonly HttpClient _httpClient;
        private readonly ConcurrentDictionary<string, BitmapImage> _cache;
        private readonly string _cacheDir;
        private readonly int _maxCacheSize;
        private readonly CancellationTokenSource _cts;
        private bool _isRunning;
        
        public event Action<string, BitmapImage>? ImageReady;
        
        private readonly ConcurrentQueue<(string Url, string Id)> _downloadQueue;
        
        private ImageCache(int maxCacheSize = 200)
        {
            _httpClient = new HttpClient();
            _cache = new ConcurrentDictionary<string, BitmapImage>();
            _downloadQueue = new ConcurrentQueue<(string, string)>();
            _maxCacheSize = maxCacheSize;
            _cts = new CancellationTokenSource();
            
            _cacheDir = Path.Combine(Path.GetTempPath(), "kleestore_cache");
            if (!Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }
            
            LoadCachedImages();
            
            _isRunning = true;
            Task.Run(DownloadWorker, _cts.Token);
        }
        
        public static ImageCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ImageCache();
                    }
                }
                return _instance;
            }
        }
        
        private void LoadCachedImages()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheDir, "*.png"))
                {
                    string url = Path.GetFileNameWithoutExtension(file).Replace("_", "/");
                    url = url.Replace("https/", "https://");
                    
                    if (!_cache.ContainsKey(url))
                    {
                        var bitmap = new BitmapImage();
                        
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(file);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        
                        _cache[url] = bitmap;
                    }
                }
                
                
            }
            catch (Exception ex)
            {
                
            }
        }
        
        private void SaveToCache(string url, BitmapImage image)
        {
            try
            {
                string filename = url.Replace(":", "_").Replace("/", "_");
                string path = Path.Combine(_cacheDir, $"{filename}.png");
                
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                
                using var stream = File.Create(path);
                encoder.Save(stream);
            }
            catch (Exception ex)
            {
                
            }
        }
        
        public void QueueDownload(string url, string packageId)
        {
            if (_cache.TryGetValue(url, out var cachedImage))
            {
                ImageReady?.Invoke(packageId, cachedImage);
                return;
            }
            
            _downloadQueue.Enqueue((url, packageId));
        }
        
        private async Task DownloadWorker()
        {
            while (_isRunning)
            {
                try
                {
                    if (_downloadQueue.TryDequeue(out var item))
                    {
                        var (url, packageId) = item;
                        
                        if (_cache.TryGetValue(url, out var cachedImage))
                        {
                            ImageReady?.Invoke(packageId, cachedImage);
                            continue;
                        }
                        
                        var bitmap = await DownloadImageAsync(url);
                        if (bitmap != null)
                        {
                            if (_cache.Count >= _maxCacheSize)
                            {
                                var keyToRemove = _cache.Keys.FirstOrDefault();
                                if (keyToRemove != null)
                                {
                                    _cache.TryRemove(keyToRemove, out _);
                                }
                            }
                            
                            _cache[url] = bitmap;
                            SaveToCache(url, bitmap);
                            
                            ImageReady?.Invoke(packageId, bitmap);
                        }
                    }
                    else
                    {
                        await Task.Delay(100, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    
                    await Task.Delay(500, _cts.Token);
                }
            }
        }
        
        private async Task<BitmapImage?> DownloadImageAsync(string url)
        {
            try
            {
                if (url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateDefaultImage();
                }
                
                using var response = await _httpClient.GetAsync(url, _cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                
                using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, _cts.Token);
                memoryStream.Position = 0;
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze();
                
                return bitmap;
            }
            catch (Exception ex)
            {
                
                return CreateDefaultImage();
            }
        }
        
        private BitmapImage CreateDefaultImage()
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
            
            var bitmap = new BitmapImage();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            
            using var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);
            memoryStream.Position = 0;
            
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memoryStream;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        
        public void Stop()
        {
            _isRunning = false;
            _cts.Cancel();
        }
    }
} 