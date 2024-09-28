using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sojj;
using Sojj.Services;

namespace IntegrationTests;

[Collection("ContainerCollection")]
public class CompileTest 
{
    private IFutureDockerImage _image;
    private IContainer _container;
    private SandboxService _sandBoxSerivce;

    public CompileTest(ContainerFixture fixture)
    {
        _sandBoxSerivce = fixture.SandBoxSerivce;
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
    public async Task CompileC11()
    {
        string sourceCode = "#include <stdio.h>\nint main() { printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "c11");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }

    [Fact]
    public async Task CompileCC()
    {
        string sourceCode = "#include <cstdio>\nint main() { printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "cc");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }

    [Fact]
    public async Task CompileCC11()
    {
        string sourceCode = "#include <cstdio>\nint main() { auto st = \"c++11\"; printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "cc11");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }

    [Fact]
    public async Task CompileCC20()
    {
        string sourceCode = "#include <cstdio>\n#include <compare>\nint main() { int a = 91, b = 110; auto ans1 = a <=> b; printf(\"Hello, World!\"); return 0; }";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "cc20");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }

    [Fact]
    public async Task CompilePython3()
    {
        string sourceCode = "print('Hello, World!')";
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), "py3");
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }
}