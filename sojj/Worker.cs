using Sojj.Dtos;
using Sojj.Services.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Sojj;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly IJudgeService judgeService;
    private readonly ICacheService cacheService;
    private readonly IConfiguration configuration;
    private readonly ISandboxService sandboxService;
    private readonly IProblemService problemService;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IJudgeService judgeService, ICacheService cacheService, ISandboxService sandboxService, IProblemService problemService)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.judgeService = judgeService;
        this.cacheService = cacheService;
        this.sandboxService = sandboxService;
        this.problemService = problemService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await cacheService.InvalidateCacheAsync();

        await UpdateProblemDataAsync();
        
        var buffer = WebSocket.CreateClientBuffer(1024 * 4, 1024 * 4);

        WebSocketReceiveResult webSocketReceiveResult;

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await judgeService.EnsureLoggedinAsync();

            var ws = await judgeService.ConsumeWebSocketAsync(stoppingToken);

            while (ws.State == WebSocketState.Open)
            {
                string message = string.Empty;

                do {
                    webSocketReceiveResult = await ws.ReceiveAsync(buffer, stoppingToken);
                    message += Encoding.UTF8.GetString(buffer.Array, 0, webSocketReceiveResult.Count);
                } while (webSocketReceiveResult.MessageType != WebSocketMessageType.Close && !webSocketReceiveResult.EndOfMessage);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken);
                }

                logger.LogInformation("Message received: {message}", message);

                if (!TryParseMessageDto(message, out var messageDto))
                {
                    await UpdateProblemDataAsync();
                    continue;
                }

                await TryProcessMessageAsync(messageDto, ws, stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task TryProcessMessageAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CancellationToken stoppingToken)
    {
        try
        {
            await ProcessMessageAsync(messageDto, ws, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing for {runId} {language} {problemId} {domainId}",
                messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);

            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JudgeProcessResponse
            {
                Key = "end",
                Tag = messageDto.Tag,
                Status = JudgeStatus.STATUS_SYSTEM_ERROR,
                Score = 0,
                TimeInMilliseconds = 0,
                MemoryInKiloBytes = 0,
            })), WebSocketMessageType.Text, true, stoppingToken);
        }
    }

    private async Task ProcessMessageAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CancellationToken stoppingToken)
    {
        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JudgeProcessResponse
        {
            Key = "next",
            Tag = messageDto.Tag,
            Status = JudgeStatus.STATUS_COMPILING,
        })), WebSocketMessageType.Text, true, stoppingToken);

        var compileResult = await sandboxService.CompileAsync(messageDto.Code, messageDto.RunId, messageDto.Language);

        if (compileResult.Status == JudgeStatus.STATUS_ACCEPTED)
        {
            await ProcessCompiledLanguageAsync(messageDto, ws, stoppingToken, compileResult);
        }
        else if (compileResult.Status == JudgeStatus.STATUS_INTERPRETED_LANGUAGE)
        {
            await ProcessInterpretedLanguageAsync(messageDto, ws, stoppingToken);
        }
        else
        {
            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JudgeProcessResponse
            {
                Key = "end",
                Tag = messageDto.Tag,
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                Score = 0,
                TimeInMilliseconds = 0,
                MemoryInKiloBytes = 0,
            })), WebSocketMessageType.Text, true, stoppingToken);

            logger.LogInformation("Compile error for {runId} {language} {problemId} {domainId}",
                messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);
        }
    }

    private async Task ProcessInterpretedLanguageAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CancellationToken stoppingToken)
    {
        int totalScore = 0;
        int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
        long totalMemory = 0;
        long totalTime = 0;
        await foreach (TestCase testCase in this.GetTestCasesAsync(messageDto))
        {
            logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

            var testCaseResult = await sandboxService.RunInterpreterAsync(testCase, messageDto.Code, messageDto.RunId, messageDto.Language);

            var testCaseResponse = new JudgeProcessResponse
            {
                Key = "next",
                Tag = messageDto.Tag,
                Status = JudgeStatus.STATUS_JUDGING,
                Case = new JudgeProcessResponseCase
                {
                    Status = testCaseResult.Status,
                    Score = testCaseResult.Score,
                    TimeInMilliseconds = testCaseResult.TimeInNs / 1000000.0f,
                    MemoryInKiloBytes = testCaseResult.MemoryInByte / 1024.0f,
                    JudgeText = testCaseResult.Message,
                },
            };

            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testCaseResponse)), WebSocketMessageType.Text, true, stoppingToken);

            totalScore += testCaseResult.Score;
            problemStatus = Math.Max(problemStatus, (int)testCaseResult.Status);
            totalMemory = Math.Max(totalMemory, testCaseResult.MemoryInByte);
            totalTime = Math.Max(totalTime, testCaseResult.TimeInNs);
        }

        var judgeProcessResponse = new JudgeProcessResponse
        {
            Key = "end",
            Tag = messageDto.Tag,
            Status = (JudgeStatus)problemStatus,
            Score = totalScore,
            TimeInMilliseconds = totalTime / 1000000.0f,
            MemoryInKiloBytes = totalMemory / 1024.0f,
        };

        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(judgeProcessResponse)), WebSocketMessageType.Text, true, stoppingToken);

        logger.LogInformation("Procesed Interpreted language for {runId} {language} {problemId} {domainId}",
                                   messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);
    }

    private async Task ProcessCompiledLanguageAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CancellationToken stoppingToken, CompileResult compileResult)
    {
        int totalScore = 0;
        int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
        long totalMemory = 0;
        long totalTime = 0;
        await foreach (TestCase testCase in this.GetTestCasesAsync(messageDto))
        {
            logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

            var testCaseResult = await sandboxService.RunAsync(testCase, compileResult);

            var testCaseResponse = new JudgeProcessResponse
            {
                Key = "next",
                Tag = messageDto.Tag,
                Status = JudgeStatus.STATUS_JUDGING,
                Case = new JudgeProcessResponseCase
                {
                    Status = testCaseResult.Status,
                    Score = testCaseResult.Score,
                    TimeInMilliseconds = testCaseResult.TimeInNs / 1000000.0f,
                    MemoryInKiloBytes = testCaseResult.MemoryInByte / 1024.0f,
                    JudgeText = testCaseResult.Message,
                },
            };

            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testCaseResponse)), WebSocketMessageType.Text, true, stoppingToken);

            totalScore += testCaseResult.Score;
            problemStatus = Math.Max(problemStatus, (int)testCaseResult.Status);
            totalMemory = Math.Max(totalMemory, testCaseResult.MemoryInByte);
            totalTime = Math.Max(totalTime, testCaseResult.TimeInNs);
        }

        var judgeProcessResponse = new JudgeProcessResponse
        {
            Key = "end",
            Tag = messageDto.Tag,
            Status = (JudgeStatus)problemStatus,
            Score = totalScore,
            TimeInMilliseconds = totalTime / 1000000.0f,
            MemoryInKiloBytes = totalMemory / 1024.0f,
        };

        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(judgeProcessResponse)), WebSocketMessageType.Text, true, stoppingToken);

        logger.LogInformation("Procesed Compiled language for {runId} {language} {problemId} {domainId}",
                                   messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);
    }

    private IAsyncEnumerable<TestCase> GetTestCasesAsync(JudgeProcessRequest messageDto)
    {
        if (messageDto.Type == JudgeProcessRequestType.Pretest)
        {
            return this.judgeService.GetPretestCasesAsync(messageDto.RunId);
        }
        return problemService.GetTestCasesAsync(messageDto.ProblemId.ToString(), messageDto.DomainId);
    }

    private bool TryParseMessageDto(string message, out JudgeProcessRequest messageDto)
    {
        try
        {
            messageDto = JsonSerializer.Deserialize<JudgeProcessRequest>(message);
            return true;
        }
        catch (Exception)
        {
            messageDto = null;
            return false;
        }
    }

    private async Task UpdateProblemDataAsync()
    {
        await judgeService.EnsureLoggedinAsync();
        int lastUpdateAt = await cacheService.GetCacheUpdateTimeAsync();
        var dataList = await judgeService.GetDataListAsync(lastUpdateAt);
        foreach (var problem in dataList.Problems)
        {
            logger.LogInformation($"Problem {problem.ProblemId} updated at {problem.DomainId}");
            var zipData = await judgeService.GetProblemDataAsync(problem.ProblemId, problem.DomainId);
            if (zipData == null)
            {
                logger.LogError("Problem data not found for {problemid}, {dommainId}", problem.ProblemId, problem.DomainId);
                continue;
            }

            await cacheService.InvalidateCacheAsync(problem.DomainId, problem.ProblemId.ToString());

            await cacheService.WriteCacheAsync(zipData, problem.DomainId, problem.ProblemId.ToString(), dataList.UnixTimestamp);
        }
    }
}
