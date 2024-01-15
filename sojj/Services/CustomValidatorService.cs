using Microsoft.Extensions.Caching.Memory;
using Sojj.Dtos;
using Sojj.Services.Contracts;

namespace Sojj.Services
{
    public class CustomValidatorService : IValidatorService
    {
        public ValidatorType Type => ValidatorType.CustomValidator;

        private readonly ILogger<CustomValidatorService> logger;
        private readonly ISandboxService sandboxService;
        private readonly IConfiguration configuration;
        private readonly long memoryLimitForRuns;
        private readonly long cpuLimitForRuns;
        private readonly IMemoryCache cache;

        public CustomValidatorService(ILogger<CustomValidatorService> logger, ISandboxService sandboxService, IConfiguration configuration, IMemoryCache cache)
        {
            this.logger = logger;
            this.sandboxService = sandboxService;
            this.configuration = configuration;
            this.memoryLimitForRuns = this.configuration.GetValue<long>("MemoryLimitForRuns");
            this.cpuLimitForRuns = this.configuration.GetValue<long>("CpuLimitForRuns");
            this.cache = cache;
        }

        public async Task<TestCaseResult> ValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            try
            {
                return await TryValidateAsync(testCase, testCaseResult);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Error while validating test case");
                testCaseResult.Status = JudgeStatus.STATUS_SYSTEM_ERROR;
                return testCaseResult;
            }
        }

        private async Task<TestCaseResult> TryValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            string runId = Guid.NewGuid().ToString();
            var compileResult = await cache.GetOrCreateAsync($"{testCase.DomainId}-{testCase.ProblemId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(25);
                return await this.sandboxService.CompileAsync(testCase.ValidatorSourceCode!, runId, testCase.ValidatorLanguage!);
            });
            var inputId = await this.sandboxService.UploadFileAsync("input.txt", testCase.Input);
            var validId = await this.sandboxService.UploadFileAsync("valid.txt", testCase.Output);
            var outputId = await this.sandboxService.UploadFileAsync("output.txt", testCaseResult.Output);

            var testCaseMemoryLimit = testCase.MemoryLimit;
            var testCaseTimeLimit = testCase.TimeLimit;

            var copyIn = new Dictionary<string, SandboxFile>
            {
                { "input.txt", new SandboxPreparedFile { FileId = inputId } },
                { "valid.txt", new SandboxPreparedFile { FileId = validId } },
                { "output.txt", new SandboxPreparedFile { FileId = outputId } }
            };

            testCase.MemoryLimit = this.memoryLimitForRuns * Constants.ByteInMegaByte;
            testCase.TimeLimit = this.cpuLimitForRuns * Constants.NanoSecondInSecond;

            var runResult = await this.sandboxService.RunAsync(testCase, compileResult, copyIn);

            await this.sandboxService.DeleteFileAsync(inputId!);
            await this.sandboxService.DeleteFileAsync(outputId!);
            await this.sandboxService.DeleteFileAsync(validId!);

            testCase.MemoryLimit = testCaseMemoryLimit;
            testCase.TimeLimit = testCaseTimeLimit;

            testCaseResult.Score = 0;

            if (runResult.Status != JudgeStatus.STATUS_ACCEPTED)
            {
                testCaseResult.Status = runResult.Status;
            }
            else
            {
                runResult.Output = runResult.Output.Trim();
                testCaseResult.Status = runResult.Output.ToJudgeStatus();
                if (testCaseResult.Status == JudgeStatus.STATUS_ACCEPTED)
                {
                    testCaseResult.Score = testCase.Score;
                }
            }

            return testCaseResult;
        }
    }
}
