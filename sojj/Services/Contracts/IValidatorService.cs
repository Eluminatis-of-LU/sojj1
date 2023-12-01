using Sojj.Dtos;

namespace Sojj.Services.Contracts
{
    public interface IValidatorService
    {
        ValidatorType Type { get; }

        Task<TestCaseResult> ValidateAsync(TestCase testCase, TestCaseResult testCaseResult);
    }
}
