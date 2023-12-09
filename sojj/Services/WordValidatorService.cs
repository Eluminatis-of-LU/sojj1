using Sojj.Dtos;
using Sojj.Services.Contracts;

namespace Sojj.Services
{
    public class WordValidatorService : IValidatorService
    {
        public ValidatorType Type => ValidatorType.WordValidator;
        private static readonly string[] _wordSeparators = new[] { "\r\n", "\r", "\n", " ", "\t" };
        private ILogger<WordValidatorService> logger;

        public WordValidatorService(ILogger<WordValidatorService> logger)
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
            var expected = testCase.Output.Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries);
            var actual = testCaseResult.Output.Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries);
            testCaseResult.Score = 0;

            if (expected.Length != actual.Length)
            {
                testCaseResult.Status = JudgeStatus.STATUS_WRONG_ANSWER;
                return Task.FromResult(testCaseResult);
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (!expected[i].Equals(actual[i]))
                {
                    testCaseResult.Status = JudgeStatus.STATUS_WRONG_ANSWER;
                    return Task.FromResult(testCaseResult);
                }
            }

            testCaseResult.Status = JudgeStatus.STATUS_ACCEPTED;
            testCaseResult.Score = testCase.Score;

            return Task.FromResult(testCaseResult);
        }
    }
}
