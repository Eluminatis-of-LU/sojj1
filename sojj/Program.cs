using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Sojj;
using Sojj.HealthChecks;
using Sojj.Services;
using Sojj.Services.Contracts;


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        int numWorkers = context.Configuration.GetValue<int>("NumberOfWorkers", Environment.ProcessorCount);
        services.AddKeyedSingleton("workerCacheLock", new SemaphoreSlim(1, 1));
        for (int i = 0; i < numWorkers; i++)
        {
			services.AddSingleton<IHostedService, Worker>();
		}
        services.AddScoped<IJudgeService, JudgeService>();
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

        services.AddHealthChecks()
            .AddCheck<JudgeServiceHealthCheck>("JudgeServiceHealthCheck")
            .AddCheck<SandboxServiceHealthCheck>("SandboxServiceHealthCheck");

        services.AddHttpClient(Constants.HeartbeatCheckinClient, (client) =>
        {
            client.BaseAddress = new Uri(context.Configuration.GetValue<string>("HeartbeatCheckinUrl"));
        })
            .AddPolicyHandler(RetryPolicy.GetRetryPolicy());

        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(10);
            options.Period = TimeSpan.FromSeconds(context.Configuration.GetValue<long>("HeartbeatIntervalInSeconds", 60));
        });

        services.AddSingleton<IHealthCheckPublisher, HealthCheckPublisher>();

        services.AddMemoryCache();
    })
    .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
           .ReadFrom.Configuration(hostingContext.Configuration))
    .Build();

host.Run();
