using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

public class HeartbeatService : BackgroundService
{
    private ILogger<HeartbeatService> logger;
    private IJudgeService judgeService;
    private ISandboxService sandboxService;

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
            this.logger.LogInformation("HeartbeatService running at: {time}", DateTimeOffset.Now);

            await this.judgeService.EnsureLoggedinAsync();

            await this.sandboxService.CheckHealthAsync();

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
