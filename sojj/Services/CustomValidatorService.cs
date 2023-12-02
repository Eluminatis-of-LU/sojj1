using Sojj.Dtos;
using Sojj.Services.Contracts;

namespace Sojj.Services
{
    public class CustomValidatorService : IValidatorService
    {
        public ValidatorType Type => ValidatorType.CustomValidator;

        private readonly ILogger<CustomValidatorService> logger;
        private readonly ISandboxService sandboxService;

        public CustomValidatorService(ILogger<CustomValidatorService> logger, ISandboxService sandboxService)
        {
            this.logger = logger;
            this.sandboxService = sandboxService;
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
            var compileResult = await this.sandboxService.CompileAsync(testCase.ValidatorSourceCode!, runId, testCase.ValidatorLanguage!);
            var validId = await this.sandboxService.UploadFileAsync("valid.txt", testCase.Output);
            var outputId = await this.sandboxService.UploadFileAsync("output.txt", testCaseResult.Output);

            var copyIn = new Dictionary<string, SandboxFile>
            {
                { "valid.txt", new SandboxPreparedFile { FileId = validId } },
                { "output.txt", new SandboxPreparedFile { FileId = outputId } }
            };

            var runResult = await this.sandboxService.RunAsync(testCase, compileResult, copyIn);

            await this.sandboxService.DeleteFileAsync(outputId!);
            await this.sandboxService.DeleteFileAsync(validId!);
            await this.sandboxService.DeleteFileAsync(compileResult.OutputFileId);

            if (runResult.Status != JudgeStatus.STATUS_ACCEPTED)
            {
                testCaseResult.Status = runResult.Status;
            }
            else
            {
                runResult.Output = runResult.Output.Trim();
                testCaseResult.Status = runResult.Output.ToJudgeStatus();
            }

            return testCaseResult;
        }
    }
}
