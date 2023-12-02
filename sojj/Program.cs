using Sojj;
using Sojj.Services;
using Sojj.Services.Contracts;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddHostedService<HeartbeatService>();
        services.AddSingleton<IJudgeService, JudgeService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IProblemService, ProblemServices>();
        services.AddSingleton<ISandboxService, SandboxService>();
        services.AddSingleton<IValidatorService, FileValidatorService>();
        services.AddSingleton<IValidatorService, WordValidatorService>();
        services.AddSingleton<IValidatorService, LineValidatorService>();
        services.AddSingleton<IValidatorService, CustomValidatorService>();

        services.AddApplicationInsightsTelemetryWorkerService();
    })
    .Build();

host.Run();
