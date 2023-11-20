using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Services.Abstractions
{
    public interface ICacheService
    {
        Task InvalidateCacheAsync();

        Task InvalidateCacheAsync(string domainId, string problemId);

        Task<int> GetCacheUpdateTimeAsync();

        Task WriteCacheAsync(ZipArchive zipData, string domainId, string problemId, int unixTimestamp);
    }
}
