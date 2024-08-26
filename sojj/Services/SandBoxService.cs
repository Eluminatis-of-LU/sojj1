using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Dtos;
using Sojj.Services.Contracts;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sojj.Services;

public class SandboxService : ISandboxService
{
    private readonly ILogger<SandboxService> logger;
    private readonly IConfiguration configuration;
    private readonly Uri baseUrl;
    private readonly HttpClient httpClient;
    private readonly string languageFilePath;
    private readonly Dictionary<string, Language> languages;
    private readonly string[] sandboxEnvironment;
    private readonly int processLimitForRuns;
    private readonly long memoryLimitForRuns;
    private readonly long cpuLimitForRuns;
    private readonly long outputLimitForRuns;

    private readonly long stackLimitForRuns;

    public SandboxService(ILogger<SandboxService> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        baseUrl = new Uri(this.configuration.GetValue<string>("SandboxUrl") ?? throw new MissingFieldException("SandboxUrl"));

        var socketHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            CookieContainer = new CookieContainer()
        };
        var pollyHandler = new PolicyHttpMessageHandler(RetryPolicy.GetRetryPolicy())
        {
            InnerHandler = socketHandler,
        };

        httpClient = new HttpClient(pollyHandler)
        {
            BaseAddress = baseUrl
        };
        languageFilePath = this.configuration.GetValue<string>("LanguageFilePath") ?? throw new MissingFieldException("LanguageFilePath");
        using var stream = File.OpenRead(languageFilePath);
        languages = JsonSerializer.Deserialize<Dictionary<string, Language>>(stream)!;
        this.sandboxEnvironment = this.configuration.GetSection("SandboxEnvironment").Get<string[]>() ?? throw new MissingFieldException("SandboxEnvironment");
        this.processLimitForRuns = this.configuration.GetValue<int>("ProcessLimitForRuns");
        this.memoryLimitForRuns = this.configuration.GetValue<long>("MemoryLimitForRuns");
        this.cpuLimitForRuns = this.configuration.GetValue<long>("CpuLimitForRuns");
        this.outputLimitForRuns = this.configuration.GetValue<long>("OutputLimitForRuns");
        this.stackLimitForRuns = this.configuration.GetValue<long>("StackLimitForRuns");
    }

    public async Task CheckHealthAsync()
    {
        logger.LogInformation("Checking sandbox service health");

        var response = await httpClient.GetAsync("/version");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Sandbox service is not healthy: {statusCode}", response.StatusCode);
            throw new Exception("Sandbox service is not healthy");
        }

        logger.LogInformation("Sandbox service is healthy");
    }

    public async Task<CompileResult> CompileAsync(string sourceCode, string runId, string language)
    {
        if (!languages.TryGetValue(language, out var languageInfo))
        {
            logger.LogError("Language {language} not found", language);
            return new CompileResult
            {
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Message = "Language not found",
            };
        }

        logger.LogInformation("Language {language} found", language);
        long timeLimitInNs = (languageInfo.CpuLimit ?? this.cpuLimitForRuns) * Constants.NanoSecondInSecond;
        var request = new SandboxRunRequest
        {
            Commands =
            [
                    new Command
                    {
                        Args = languageInfo.Compile,
                        Env = this.sandboxEnvironment,
                        Files =
                        [
                            new SandboxMemoryFile
                            {
                                Content = string.Empty,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stdout,
                                Max = this.outputLimitForRuns * Constants.ByteInMegaByte,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stderr,
                                Max = this.outputLimitForRuns * Constants.ByteInKiloByte,
                            },
                        ],
                        CpuLimit = timeLimitInNs,
                        ClockLimit = timeLimitInNs * 3,
                        MemoryLimit = this.memoryLimitForRuns * Constants.ByteInMegaByte,
                        StackLimit = this.stackLimitForRuns * Constants.ByteInMegaByte,
                        ProcessLimit = this.processLimitForRuns,
                        CopyIn = new Dictionary<string, SandboxFile>
                        {
                            { languageInfo.CodeFile, new SandboxMemoryFile { Content = sourceCode } },
                        },
                        CopyOut = [Constants.Stdout, Constants.Stderr],
                        CopyOutCached = [languageInfo.CodeFile, languageInfo.OutputFile],
                    }
            ],
        };
        logger.LogDebug("Compile request: {request}", JsonSerializer.Serialize(request));
        logger.LogInformation("Compiling {language}", language);
        var response = await httpClient.PostAsJsonAsync("/run", request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Compile failed: {statusCode}", response.StatusCode);
            return new CompileResult
            {
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Message = "Compile failed",
            };
        }

        var content = await response.Content.ReadAsStringAsync();

        logger.LogDebug("Compile result: {content}", content);

        logger.LogInformation("Compiling {runId} done", runId);
        var result = JsonSerializer.Deserialize<SandboxRunResult[]>(content);
        if (result == null || result.Length != 1)
        {
            logger.LogError("Compile result length is not 1");
            return new CompileResult
            {
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Message = "Compile result length is not 1",
            };
        }

        var compileResult = result[0];

        logger.LogInformation("Compilation used, cpu: {compileTime}ms wall: {compileRunTime}ms using memory: {compileMemory}mb for {language}", 
            compileResult.Time / Constants.NanoSecondInMillisecond, compileResult.RunTime / Constants.NanoSecondInMillisecond, compileResult.Memory / Constants.ByteInMegaByte, language);

        if (compileResult.Status != Constants.Accepted)
        {
            logger.LogError("Compile failed: {status}", compileResult.Status);
            return new CompileResult
            {
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Message = compileResult.Files[Constants.Stderr],
            };
        }

        logger.LogInformation("Compile success");        

        foreach (var file in compileResult.FileIds)
        {
            if (file.Key != languageInfo.OutputFile)
            {
                await DeleteFileAsync(file.Value);
            }
        }

        return new CompileResult
        {
            Status = JudgeStatus.STATUS_ACCEPTED,
            Message = compileResult.Files[Constants.Stderr],
            ExecuteArgs = languageInfo.Execute,
            Language = language,
            RunId = runId,
            OutputFileId = compileResult.FileIds[languageInfo.OutputFile],
            OutputFile = languageInfo.OutputFile,
        };
    }

    public async Task DeleteFileAsync(string fileId)
    {
        try
        {
            await httpClient.DeleteAsync($"/file/{fileId}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while deleting file {fileId}", fileId);
        }
    }

    public async Task<TestCaseResult> RunAsync(TestCase testCase, CompileResult compileResult, Dictionary<string, SandboxFile>? copyIn)
    {
        copyIn ??= [];

        copyIn.Add(compileResult.OutputFile, new SandboxPreparedFile { FileId = compileResult.OutputFileId });
        var request = new SandboxRunRequest
        {
            Commands =
            [
                    new() {
                        Args = compileResult.ExecuteArgs,
                        Env = this.sandboxEnvironment,
                        Files =
                        [
                            new SandboxMemoryFile
                            {
                                Content = testCase.Input,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stdout,
                                Max = this.outputLimitForRuns * Constants.ByteInMegaByte,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stderr,
                                Max = this.outputLimitForRuns * Constants.ByteInKiloByte,
                            },
                        ],
                        CpuLimit = testCase.TimeLimit,
                        ClockLimit = testCase.TimeLimit * 3,
                        StackLimit = this.stackLimitForRuns * Constants.ByteInMegaByte,
                        MemoryLimit = testCase.MemoryLimit,
                        ProcessLimit = this.processLimitForRuns,
                        CopyIn = copyIn,
                        CopyOut = [Constants.Stdout, Constants.Stderr],
                    }
            ],
        };

        logger.LogDebug("Run request: {request}", JsonSerializer.Serialize(request));

        logger.LogInformation("Running test case {TestCaseNumber}", testCase.CaseNumber);

        var response = await httpClient.PostAsJsonAsync("/run", request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Run failed: sandbox returned statusCode-> {statusCode}", response.StatusCode);
            return new TestCaseResult
            {
                Status = JudgeStatus.STATUS_SYSTEM_ERROR,
                Message = "Run failed",
                Score = 0,
            };
        }

        var content = await response.Content.ReadAsStringAsync();

        logger.LogDebug("Run result: {content}", content);

        logger.LogInformation("Running finished test case {TestCaseNumber} done", testCase.CaseNumber);

        var result = JsonSerializer.Deserialize<SandboxRunResult[]>(content);

        if (result == null || result.Length != 1)
        {
            logger.LogError("Run result length is not 1");
            return new TestCaseResult
            {
                Status = JudgeStatus.STATUS_SYSTEM_ERROR,
                Message = "Run result length is not 1",
                Score = 0,
            };
        }

        var runResult = result[0];

        if (runResult.Status != Constants.Accepted)
        {
            logger.LogInformation("Run failed: {status}", runResult.Status);
            return new TestCaseResult
            {
                Status = runResult.Status.ToJudgeStatus(),
                Message = runResult.Files[Constants.Stderr],
                Score = 0,
                MemoryInByte = runResult.Memory,
                TimeInNs = runResult.Time,
            };
        }

        logger.LogInformation("Run success");

        return new TestCaseResult
        {
            Status = JudgeStatus.STATUS_ACCEPTED,
            Message = runResult.Files[Constants.Stderr],
            TimeInNs = runResult.Time,
            MemoryInByte = runResult.Memory,
            Score = testCase.Score,
            Output = runResult.Files[Constants.Stdout],
        };
    }

    public async Task<string?> UploadFileAsync(string fileName, string fileContent)
    {
        var formData = new MultipartFormDataContent
        {
            { new StringContent(fileContent), "file", fileName }
        };
        var response = await httpClient.PostAsync("/file", formData);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Upload file failed: {statusCode}", response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();

        content = content.Replace("\"", "");

        logger.LogDebug("Upload file result: {content}", content);

        return content;
    }
}