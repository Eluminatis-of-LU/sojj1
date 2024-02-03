using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.HealthChecks;
public class HealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<HealthCheckPublisher> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public HealthCheckPublisher(ILogger<HealthCheckPublisher> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        HttpClient? httpClient = _httpClientFactory.CreateClient(Constants.HeartbeatCheckinClient);

        if (httpClient == null)
        {
            _logger.LogError("HeartbeatCheckinClient is not configured");
            return;
        }

        if (report.Status != HealthStatus.Healthy)
        {
            return;
        }

        var response = await httpClient.GetAsync(string.Empty, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Heartbeat checkin success");
        }
        else
        {
            _logger.LogError("Heartbeat checkin failed");
        }
    }
}
