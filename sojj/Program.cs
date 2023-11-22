using Microsoft.Extensions.Configuration;
using Sojj;
using Sojj.Services;
using Sojj.Services.Abstractions;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<IJudgeService, JudgeService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IProblemService, ProblemServices>();
        services.AddSingleton<ISandboxService, SandboxService>();

        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
