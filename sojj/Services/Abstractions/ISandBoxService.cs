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
    }
}
