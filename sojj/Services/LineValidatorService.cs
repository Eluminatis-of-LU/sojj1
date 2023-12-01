using Sojj.Dtos;
using Sojj.Services.Contracts;

namespace Sojj.Services
{
    public class LineValidatorService : IValidatorService
    {
        public ValidatorType Type => ValidatorType.LineValidator;

        private static readonly string[] _lineSeparators = new[] { "\r\n", "\r", "\n" };
        private static readonly char[] _wordSeparators = new[] { ' ', '\t' };
        private ILogger<LineValidatorService> logger;

        public LineValidatorService(ILogger<LineValidatorService> logger)
        {
            this.logger = logger;
        }

        public async Task<TestCaseResult> ValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            try
            {
                return await TryValidateAsync(testCase, testCaseResult);
            }
            catch(Exception e)
            {
                this.logger.LogError(e, "Error while validating test case");
                testCaseResult.Status = JudgeStatus.STATUS_SYSTEM_ERROR;
                return testCaseResult;
            }
        }

        private Task<TestCaseResult> TryValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            var expected = testCase.Output.Split(_lineSeparators, StringSplitOptions.None);
            var actual = testCaseResult.Output.Split(_lineSeparators, StringSplitOptions.None);
            if (expected.Length != actual.Length)
            {
                testCaseResult.Status = JudgeStatus.STATUS_WRONG_ANSWER;
                return Task.FromResult(testCaseResult);
            }

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedLine = expected[i].Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries);
                var actualLine = actual[i].Split(_wordSeparators, StringSplitOptions.RemoveEmptyEntries);

                if (expectedLine.Length != actualLine.Length)
                {
                    testCaseResult.Status = JudgeStatus.STATUS_WRONG_ANSWER;
                    return Task.FromResult(testCaseResult);
                }

                for (var j = 0; j < expectedLine.Length; j++)
                {
                    if (!expectedLine[j].Equals(actualLine[j]))
                    {
                        testCaseResult.Status = JudgeStatus.STATUS_WRONG_ANSWER;
                        return Task.FromResult(testCaseResult);
                    }
                }
            }

            testCaseResult.Status = JudgeStatus.STATUS_ACCEPTED;

            return Task.FromResult(testCaseResult);
        }
    }
}
