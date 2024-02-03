using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sojj.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.HealthChecks;

public class SandboxServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<SandboxServiceHealthCheck> _logger;
    private readonly ISandboxService _sandboxService;

    public SandboxServiceHealthCheck(ILogger<SandboxServiceHealthCheck> logger, ISandboxService sandboxService)
    {
        _logger = logger;
        _sandboxService = sandboxService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sandboxService.CheckHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SandboxServiceHealthCheck failed");
            return HealthCheckResult.Unhealthy("SandboxServiceHealthCheck failed", ex);
        }
        
        return HealthCheckResult.Healthy("SandboxServiceHealthCheck succeeded");
    }
}

