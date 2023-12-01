using Sojj.Services.Contracts;

namespace Sojj.Services;

public class HeartbeatService : BackgroundService
{
    private readonly ILogger<HeartbeatService> logger;
    private readonly IJudgeService judgeService;
    private readonly ISandboxService sandboxService;

    public HeartbeatService(ILogger<HeartbeatService> logger, IJudgeService judgeService, ISandboxService sandboxService)
    {
        this.logger = logger;
        this.judgeService = judgeService;
        this.sandboxService = sandboxService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("HeartbeatService running at: {time}", DateTimeOffset.Now);

            await judgeService.EnsureLoggedinAsync();

            await sandboxService.CheckHealthAsync();

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
