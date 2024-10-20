using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sojj.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.HealthChecks;

public class JudgeServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<JudgeServiceHealthCheck> _logger;
    private readonly IServiceScope _serviceScope;
    private readonly IJudgeService _judgeService;

    public JudgeServiceHealthCheck(ILogger<JudgeServiceHealthCheck> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScope = serviceScopeFactory.CreateScope();
		_judgeService = _serviceScope.ServiceProvider.GetRequiredService<IJudgeService>();;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _judgeService.EnsureLoggedinAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JudgeServiceHealthCheck failed");
            return HealthCheckResult.Unhealthy("JudgeServiceHealthCheck failed", ex);
        }

        return HealthCheckResult.Healthy("JudgeServiceHealthCheck succeeded");
    }
}
