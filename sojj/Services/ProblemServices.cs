using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Services
{
    public class ProblemServices : IProblemService
    {
        private readonly ILogger<ProblemServices> logger;
        private readonly IConfiguration configuration;
        private readonly string cacheLocation;

        public ProblemServices(ILogger<ProblemServices> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.cacheLocation = this.configuration["JudgeService:CacheLocation"] ?? throw new ArgumentNullException("CacheLocation");
        }

        public async IAsyncEnumerable<TestCase> GetTestCasesAsync(string problemId, string domainId)
        {
            string path = Path.Combine(this.cacheLocation, domainId, problemId);
            this.logger.LogInformation("Get test cases for problem {problemId} in domain {domainId} from {path}", problemId, domainId, path);
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Problem {problemId} not found");
            }

            var config = await File.ReadAllLinesAsync(Path.Combine(path, "Config.ini"));

            string inputPath = Path.Combine(path, "Input");
            string outputPath = Path.Combine(path, "Output");

            int testCases = int.Parse(config[0]);

            for (int line = 1; line <= testCases; line++)
            {
                var splitted = config[line].Split('|');
                yield return new TestCase
                {
                    CaseNumber = line,
                    Input = await File.ReadAllTextAsync(Path.Combine(inputPath, splitted[0])),
                    Output = await File.ReadAllTextAsync(Path.Combine(outputPath, splitted[1])),
                    TimeLimit = long.Parse(splitted[2]) * Constants.NanoSecondInSecond,
                    Score = int.Parse(splitted[3]),
                    MemoryLimit = long.Parse(splitted[4]) * Constants.ByteInKiloByte,
                    TotalCase = testCases,
                };
            }
        }
    }
}
