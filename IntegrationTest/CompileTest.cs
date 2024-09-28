using DotNet.Testcontainers.Builders;
using Sojj;
using Sojj.Services;

namespace IntegrationTests;

[Collection("ContainerCollection")]
public class CompileTest 
{
    private SandboxService _sandBoxSerivce;

    public CompileTest(ContainerFixture fixture)
    {
        _sandBoxSerivce = fixture.SandBoxSerivce;
    }

    public static IEnumerable<object[]> TestData()
    {
        string path = Path.Combine(CommonDirectoryPath.GetSolutionDirectory().DirectoryPath, "testdata", "compile-test");
        foreach (string file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).Substring(1);
            yield return new object[] { ext, File.ReadAllText(file) };
        }
    }
    
    [Fact]
    public async Task CheckHealthAsync()
    {
        await _sandBoxSerivce.CheckHealthAsync();
        Assert.True(true);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task CompileCodeAsync(string lang, string sourceCode)
    {
        var result = await _sandBoxSerivce.CompileAsync(sourceCode, Guid.NewGuid().ToString(), lang);
        Assert.Equal(JudgeStatus.STATUS_ACCEPTED, result.Status);
    }
}