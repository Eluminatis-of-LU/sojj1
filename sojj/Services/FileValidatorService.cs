using Sojj.Dtos;
using Sojj.Services.Contracts;

namespace Sojj.Services
{
    public class FileValidatorService : IValidatorService
    {
        private ILogger<FileValidatorService> logger;

        public ValidatorType Type => ValidatorType.FileValidator;

        public FileValidatorService(ILogger<FileValidatorService> logger) 
        {
            this.logger = logger;
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

        private Task<TestCaseResult> TryValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            var expected = testCase.Output.Trim();
            var actual = testCaseResult.Output.Trim();
            if (expected.Equals(actual))
            {
                testCaseResult.Status = JudgeStatus.STATUS_ACCEPTED;
            }
            else
            {
                testCaseResult.Status = JudgeStatus.STATUS_ACCEPTED;
            }

            return Task.FromResult(testCaseResult);
        }
    }
}
