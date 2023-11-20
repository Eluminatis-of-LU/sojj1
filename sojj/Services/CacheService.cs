using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Services
{
    public class CacheService : ICacheService
    {
        private readonly ILogger<CacheService> logger;
        private readonly IConfiguration configuration;
        private readonly string cacheLocation;

        public CacheService(ILogger<CacheService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.cacheLocation = this.configuration["JudgeService:CacheLocation"] ?? throw new ArgumentNullException("CacheLocation");
        }

        public async Task<int> GetCacheUpdateTimeAsync()
        {
            string fileName = Path.Combine(this.cacheLocation, "cache.txt");
            Directory.CreateDirectory(this.cacheLocation);
            if (!File.Exists(fileName))
            {
                await File.WriteAllTextAsync(fileName, "0");
                return 0;
            }
            string content = await File.ReadAllTextAsync(fileName);
            if (int.TryParse(content, out int lastUpdateAt))
            {
                return lastUpdateAt;
            }

            return 0;
        }

        public Task InvalidateCacheAsync()
        {
            Directory.CreateDirectory(this.cacheLocation);
            Directory.Delete(this.cacheLocation, true);
            return Task.CompletedTask;
        }

        public async Task WriteCacheAsync(ZipArchive zipData, string domainId, string problemId, int unixTimestamp)
        {
            string path = Path.Combine(this.cacheLocation, domainId, problemId);
            Directory.CreateDirectory(path);
            zipData.ExtractToDirectory(path, true);
            await File.WriteAllTextAsync(Path.Combine(this.cacheLocation, "cache.txt"), unixTimestamp.ToString());
        }
    }
}
