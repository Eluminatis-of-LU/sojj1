using Sojj.Dtos;
using System.IO.Compression;
using System.Net.WebSockets;

namespace Sojj.Services.Abstractions;

public interface IJudgeService
{
    Task<bool> NoopAsync();

    Task LoginAsync();

    Task EnsureLoggedinAsync();

    Task<DataList?> GetDataListAsync(int lastUpdatedAtTimeStamp);

    Task<ZipArchive?> GetProblemDataAsync(int problemId, string domainId);

    Task<ClientWebSocket> ConsumeWebSocketAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<TestCase> GetPretestCasesAsync(string runId);
}