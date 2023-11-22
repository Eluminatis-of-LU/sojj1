﻿using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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

            var config = await File.ReadAllTextAsync(Path.Combine(path, "Config.json"));

            var testCaseConfig = JsonSerializer.Deserialize<TestCaseConfig>(config);

            string inputPath = Path.Combine(path, "Input");
            string outputPath = Path.Combine(path, "Output");

            for (int caseNumber = 0; caseNumber < testCaseConfig.TestCases.Length; caseNumber++)
            {
                yield return new TestCase
                {
                    CaseNumber = caseNumber,
                    Input = await File.ReadAllTextAsync(Path.Combine(inputPath, testCaseConfig.TestCases[caseNumber].Input)),
                    Output = await File.ReadAllTextAsync(Path.Combine(outputPath, testCaseConfig.TestCases[caseNumber].Output)),
                    TimeLimit = testCaseConfig.TimeLimit * Constants.NanoSecondInMillisecond,
                    Score = testCaseConfig.TestCases[caseNumber].Score,
                    MemoryLimit = testCaseConfig.MemoryLimit * Constants.ByteInKiloByte,
                    TotalCase = testCaseConfig.TestCases.Length,
                    ValidatorType = testCaseConfig.ValidatorType,
                };
            }
        }
    }
}
