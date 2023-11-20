using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj
{
    public class HeartBeatBackgroundService : BackgroundService
    {
        private ILogger<HeartBeatBackgroundService> logger;

        public HeartBeatBackgroundService(ILogger<HeartBeatBackgroundService> logger)
        {
            this.logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Heartbeat");
                
                
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}