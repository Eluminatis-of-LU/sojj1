using Sojj.Dtos;

namespace Sojj.Services.Abstractions;

public interface ISandboxService
{
    Task CheckHealthAsync();
    Task<CompileResult> CompileAsync(string sourceCode, string runId, string language);
    Task<TestCaseResult> RunAsync(TestCase testCase, CompileResult compileResult);
    Task<TestCaseResult> RunInterpreterAsync(TestCase testCase, string code, string runId, string language);
}

