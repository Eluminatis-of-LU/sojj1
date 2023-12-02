using Sojj.Dtos;

namespace Sojj.Services.Contracts;

public interface ISandboxService
{
    Task CheckHealthAsync();
    Task<CompileResult> CompileAsync(string sourceCode, string runId, string language);
    Task<TestCaseResult> RunAsync(TestCase testCase, CompileResult compileResult, Dictionary<string, SandboxFile>? copyIn = null);
    Task DeleteFileAsync(string fileId);
    Task<string?> UploadFileAsync(string fileName, string fileContent);
}

