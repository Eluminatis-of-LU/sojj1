using Sojj.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Services.Abstractions
{
    public interface ISandboxService
    {
        Task<CompileResult>  CompileAsync(string sourceCode, string runId, string language);
        Task<TestCaseResult> RunAsync(TestCase testCase, CompileResult compileResult);
        Task<TestCaseResult> RunInterpreterAsync(TestCase testCase, string code, string runId, string language);
    }
}
