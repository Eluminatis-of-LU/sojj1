using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Dtos;
using Sojj.Services.Contracts;
using System.Net;
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

    public SandboxService(ILogger<SandboxService> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        baseUrl = new Uri(this.configuration.GetValue<string>("SandboxUrl") ?? throw new ArgumentNullException("SandboxUrl"));
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        var socketHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) };
        socketHandler.CookieContainer = new CookieContainer();
        var pollyHandler = new PolicyHttpMessageHandler(retryPolicy)
        {
            InnerHandler = socketHandler,
        };

        httpClient = new HttpClient(pollyHandler);
        httpClient.BaseAddress = baseUrl;
        languageFilePath = this.configuration.GetValue<string>("LanguageFilePath") ?? throw new ArgumentNullException("LanguageFilePath");
        using var stream = File.OpenRead(languageFilePath);
        languages = JsonSerializer.Deserialize<Dictionary<string, Language>>(stream);
        this.sandboxEnvironment = this.configuration.GetSection("SandboxEnvironment").Get<string[]>() ?? throw new ArgumentNullException("SandboxEnvironment");
    }

    public async Task CheckHealthAsync()
    {
        logger.LogInformation("Checking sandbox service health");

        var response = await httpClient.GetAsync("/config");

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

        logger.LogInformation("Compiling {runId}", runId);
        var request = new SandboxRunRequest
        {
            Commands = new Command[]
            {
                    new Command
                    {
                        Args = languageInfo.Compile,
                        Env = this.sandboxEnvironment,
                        Files = new SandboxFile[]
                        {
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stdout,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stderr,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                        },
                        CpuLimit = 15 * Constants.NanoSecondInSecond,
                        MemoryLimit = 256 * Constants.ByteInMegaByte,
                        ProcessLimit = 10,
                        CopyIn = new Dictionary<string, SandboxFile>
                        {
                            { languageInfo.CodeFile, new SandboxMemoryFile { Content = sourceCode } },
                        },
                        CopyOut = new string[] { Constants.Stdout, Constants.Stderr },
                        CopyOutCached = new string[] { languageInfo.CodeFile, languageInfo.OutputFile },
                    }
            },
        };
        logger.LogDebug("Compile request: {request}", JsonSerializer.Serialize(request));
        logger.LogInformation("Compiling {runId}", runId);
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

        return new CompileResult
        {
            Status = JudgeStatus.STATUS_ACCEPTED,
            Message = compileResult.Files[Constants.Stdout],
            ExecuteArgs = languageInfo.Execute,
            Language = language,
            RunId = runId,
            OutputFileId = compileResult.FileIds[languageInfo.OutputFile],
            OutputFile = languageInfo.OutputFile,
        };
    }

    public async Task<TestCaseResult> RunAsync(TestCase testCase, CompileResult compileResult)
    {
        var request = new SandboxRunRequest
        {
            Commands = new Command[]
            {
                    new Command
                    {
                        Args = compileResult.ExecuteArgs,
                        Env = this.sandboxEnvironment,
                        Files = new SandboxFile[]
                        {
                            new SandboxMemoryFile
                            {
                                Content = testCase.Input,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stdout,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stderr,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                        },
                        CpuLimit = testCase.TimeLimit,
                        MemoryLimit = testCase.MemoryLimit,
                        ProcessLimit = 10,
                        CopyIn = new Dictionary<string, SandboxFile>
                        {
                            { compileResult.OutputFile, new SandboxPreparedFile { FileId = compileResult.OutputFileId } },
                        },
                        CopyOut = new string[] { Constants.Stdout, Constants.Stderr },
                    }
            },
        };

        logger.LogDebug("Run request: {request}", JsonSerializer.Serialize(request));

        logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

        var response = await httpClient.PostAsJsonAsync("/run", request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Run failed: {statusCode}", response.StatusCode);
            return new TestCaseResult
            {
                Status = JudgeStatus.STATUS_SYSTEM_ERROR,
                Message = "Run failed",
                Score = 0,
            };
        }

        var content = await response.Content.ReadAsStringAsync();

        logger.LogDebug("Run result: {content}", content);

        logger.LogInformation("Running finished test case {testCase.CaseNumber} done", testCase.CaseNumber);

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
            logger.LogError("Run failed: {status}", runResult.Status);
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

    public async Task<TestCaseResult> RunInterpreterAsync(TestCase testCase, string code, string runId, string language)
    {
        if (!languages.TryGetValue(language, out var languageInfo))
        {
            logger.LogError("Language {language} not found", language);
            return new TestCaseResult
            {
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Message = "Language not found",
            };
        }

        var request = new SandboxRunRequest
        {
            Commands = new Command[]
            {
                    new Command
                    {
                        Args = languageInfo.Execute,
                        Env = this.sandboxEnvironment,
                        Files = new SandboxFile[]
                        {
                            new SandboxMemoryFile
                            {
                                Content = testCase.Input,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stdout,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                            new SandboxCollectorFile
                            {
                                Name = Constants.Stderr,
                                Max = 4 * Constants.ByteInMegaByte,
                            },
                        },
                        CpuLimit = testCase.TimeLimit,
                        MemoryLimit = testCase.MemoryLimit,
                        ProcessLimit = 10,
                        CopyIn = new Dictionary<string, SandboxFile>
                        {
                            { languageInfo.CodeFile, new SandboxMemoryFile { Content = code } },
                        },
                        CopyOut = new string[] { Constants.Stdout, Constants.Stderr },
                    }
            },
        };

        logger.LogDebug("Run request: {request}", JsonSerializer.Serialize(request));

        logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

        var response = await httpClient.PostAsJsonAsync("/run", request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Run failed: {statusCode}", response.StatusCode);
            return new TestCaseResult
            {
                Status = JudgeStatus.STATUS_SYSTEM_ERROR,
                Message = "Run failed",
                Score = 0,
            };
        }

        var content = await response.Content.ReadAsStringAsync();

        logger.LogDebug("Run result: {content}", content);

        logger.LogInformation("Running finished test case {testCase.CaseNumber} done", testCase.CaseNumber);

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
            logger.LogError("Run failed: {status}", runResult.Status);
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
}