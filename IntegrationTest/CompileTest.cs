using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sojj;
using Sojj.Services;

namespace IntegrationTests;

public class CompileTest: IAsyncLifetime 
{
    
    private IFutureDockerImage _image;
    private IContainer _container;
    private SandboxService _sandBoxSerivce;

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }

    public async Task InitializeAsync()
    {
        _image = new ImageFromDockerfileBuilder()
            .WithName("sojj1-integration-test")
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
            .WithDockerfile("Dockerfile")
            .Build();
        await _image.CreateAsync().ConfigureAwait(false);
        _container = new ContainerBuilder()
            .WithImage(_image)
            .WithPortBinding(5050, true)
            .WithEntrypoint("/bin/bash", "-c", "/usr/bin/sandbox -http-addr 0.0.0.0:5050 -dir ~/sandbox/ -release -file-timeout 30m")
            .WithPrivileged(true)
            .Build();
        await _container.StartAsync();
        
        int port = _container.GetMappedPublicPort(5050);
        var logger = NSubstitute.Substitute.For<ILogger<SandboxService>>();
        var inMemorySettings = new Dictionary<string, string> {
            {"SandboxUrl", $"http://localhost:{port}"},
            {"LanguageFilePath", "languages.json"},
            {"SandboxEnvironment:0", "PATH=/usr/local/bin:/usr/bin:/bin:/usr/local/bflat"},
            {"CpuLimitForRuns", "15"},
            {"MemoryLimitForRuns", "1024"},
            {"StackLimitForRuns", "256"},
            {"ProcessLimitForRuns", "20"},
            {"OutputLimitForRuns", "5"}
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        _sandBoxSerivce = new SandboxService(logger, config);
    }

    [Fact]
    public async Task CheckHealthAsync()
    {
        await _sandBoxSerivce.CheckHealthAsync();
        Assert.True(true);
    }

    [Fact]
    public async Task CompileC()
    {
        string sourceCode = "#include <stdio.h>\nint main() { printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "c");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }

    [Fact]
    public async Task CompileCpp()
    {
        string sourceCode = "#include <cstdio>\nint main() { printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "cpp");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }
}