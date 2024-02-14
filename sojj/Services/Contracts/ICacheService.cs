using System.IO.Compression;

namespace Sojj.Services.Contracts;

public interface ICacheService
{
    Task InvalidateCacheAsync();

    Task InvalidateCacheAsync(string domainId, string problemId);

    Task<int> GetCacheUpdateTimeAsync();

    void WriteCache(ZipArchive zipData, string domainId, string problemId);

    Task UpdateCacheTimeAsync(int unixTimestamp);
}
