using Sojj.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Services.Abstractions
{
    public interface IProblemService
    {
        IAsyncEnumerable<TestCase> GetTestCasesAsync(string problemId, string domainId);
    }
}
