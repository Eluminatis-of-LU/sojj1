using Serilog;
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
        services.AddKeyedSingleton<IValidatorService, FileValidatorService>(ValidatorType.FileValidator);
        services.AddKeyedSingleton<IValidatorService, WordValidatorService>(ValidatorType.WordValidator);
        services.AddKeyedSingleton<IValidatorService, LineValidatorService>(ValidatorType.LineValidator);
        services.AddKeyedSingleton<IValidatorService, FloatValidatorService>(ValidatorType.FloatValidator);
        services.AddKeyedSingleton<IValidatorService, CustomValidatorService>(ValidatorType.CustomValidator);
        services.AddSingleton<IValidatorService, FileValidatorService>();
        services.AddSingleton<IValidatorService, WordValidatorService>();
        services.AddSingleton<IValidatorService, LineValidatorService>();
        services.AddSingleton<IValidatorService, FloatValidatorService>();
        services.AddSingleton<IValidatorService, CustomValidatorService>();

        services.AddMemoryCache();
    })
    .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
           .ReadFrom.Configuration(hostingContext.Configuration))
    .Build();

host.Run();
