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
            this.logger.LogInformation("Get cache update time");
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
                this.logger.LogInformation("Cache last update at {lastUpdateAt}", lastUpdateAt);
                return lastUpdateAt;
            }

            return 0;
        }

        public Task InvalidateCacheAsync()
        {
            this.logger.LogInformation("Invalidate cache");
            Directory.CreateDirectory(this.cacheLocation);
            Directory.Delete(this.cacheLocation, true);
            return Task.CompletedTask;
        }

        public Task InvalidateCacheAsync(string domainId, string problemId)
        {
            this.logger.LogInformation("Invalidate cache {domainId}-{problemId}", domainId, problemId);
            string path = Path.Combine(this.cacheLocation, domainId, problemId);
            Directory.CreateDirectory(path);
            Directory.Delete(path, true);
            return Task.CompletedTask;
        }

        public async Task WriteCacheAsync(ZipArchive zipData, string domainId, string problemId, int unixTimestamp)
        {
            this.logger.LogInformation("Write cache for problem {problemId} in domain {domainId}", problemId, domainId);
            string path = Path.Combine(this.cacheLocation, domainId, problemId);
            this.logger.LogInformation("Write cache to {path}", path);
            Directory.CreateDirectory(path);
            zipData.ExtractToDirectory(path, false);
            this.logger.LogInformation("Write cache to {path} completed", path);
            await File.WriteAllTextAsync(Path.Combine(this.cacheLocation, "cache.txt"), unixTimestamp.ToString());
        }
    }
}
