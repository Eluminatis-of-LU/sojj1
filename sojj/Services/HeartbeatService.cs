using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Services.Contracts;

namespace Sojj.Services;

public class HeartbeatService : BackgroundService
{
    private readonly ILogger<HeartbeatService> logger;
    private readonly IJudgeService judgeService;
    private readonly ISandboxService sandboxService;
    private readonly IConfiguration configuration;
    private readonly int heartbeatIntervalInSeconds;
    private readonly TimeSpan heartbeatInterval;
    private HttpClient? heartBeatCheckinClient = null;

    public HeartbeatService(ILogger<HeartbeatService> logger, IJudgeService judgeService, ISandboxService sandboxService, IConfiguration configuration)
    {
        this.logger = logger;
        this.judgeService = judgeService;
        this.sandboxService = sandboxService;
        this.configuration = configuration;
        this.heartbeatIntervalInSeconds = this.configuration.GetValue<int>("HeartbeatIntervalInSeconds");
        this.heartbeatInterval = TimeSpan.FromSeconds(this.heartbeatIntervalInSeconds);
        if (this.heartbeatIntervalInSeconds <= 0)
        {
            throw new ArgumentException("HeartbeatIntervalInSeconds must be greater than 0");
        }

        var heartbeatCheckinUrlString = this.configuration.GetValue<string>("HeartbeatCheckinUrl");

        if (!string.IsNullOrWhiteSpace(heartbeatCheckinUrlString))
        {
            var pollyHandler = new PolicyHttpMessageHandler(RetryPolicy.GetRetryPolicy());
            this.heartBeatCheckinClient = new HttpClient(pollyHandler);
            var heartbeatCheckinUrl = new Uri(heartbeatCheckinUrlString);
            this.heartBeatCheckinClient.BaseAddress = heartbeatCheckinUrl;
        }

        logger.LogInformation("HeartbeatIntervalInSeconds: {heartbeatIntervalInSeconds}", heartbeatIntervalInSeconds);

        logger.LogInformation("HeartbeatService started");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("HeartbeatService running at: {time}", DateTimeOffset.Now);

            await judgeService.EnsureLoggedinAsync();

            await sandboxService.CheckHealthAsync();

            await CheckinAsync();

            await Task.Delay(heartbeatInterval, stoppingToken);
        }
    }

    private async Task CheckinAsync()
    {
        if (heartBeatCheckinClient is null)
        {
            return;
        }

        var response = await heartBeatCheckinClient.GetAsync(string.Empty);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Heartbeat checkin success");
        }
        else
        {
            logger.LogError("Heartbeat checkin failed");
        }
    }
}
