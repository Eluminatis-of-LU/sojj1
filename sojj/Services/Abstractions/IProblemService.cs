using Sojj.Dtos;

namespace Sojj.Services.Abstractions;

public interface IProblemService
{
    IAsyncEnumerable<TestCase> GetTestCasesAsync(string problemId, string domainId);
}
