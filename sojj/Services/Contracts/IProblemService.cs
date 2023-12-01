using Sojj.Dtos;

namespace Sojj.Services.Contracts;

public interface IProblemService
{
    IAsyncEnumerable<TestCase> GetTestCasesAsync(string problemId, string domainId);
}
