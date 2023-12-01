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

        public Task<TestCaseResult> ValidateAsync(TestCase testCase, TestCaseResult testCaseResult)
        {
            throw new NotImplementedException();
        }
    }
}
