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
    private readonly ISandboxService sandboxService;
    private readonly IProblemService problemService;
    private readonly List<IValidatorService> validatorServices;

    public Worker(ILogger<Worker> logger,
        IJudgeService judgeService,
        ICacheService cacheService,
        ISandboxService sandboxService,
        IProblemService problemService,
        IEnumerable<IValidatorService> validatorServices)
    {
        this.logger = logger;
        this.judgeService = judgeService;
        this.cacheService = cacheService;
        this.sandboxService = sandboxService;
        this.problemService = problemService;
        this.validatorServices = [.. validatorServices.OrderBy(x => x.Type)];
        if (this.validatorServices.Count != (int)ValidatorType.CustomValidator)
        {
            throw new Exception("Not all validator registered");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

                await TryProcessMessageAsync(messageDto!, ws, stoppingToken);
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
            await ExecuteAndGradeAsync(messageDto, ws, compileResult, stoppingToken);
        }
        else
        {
            await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JudgeProcessResponse
            {
                Key = "next",
                Tag = messageDto.Tag,
                Status = JudgeStatus.STATUS_COMPILE_ERROR,
                CompilerText = compileResult.Message,
            })), WebSocketMessageType.Text, true, stoppingToken);
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

    private async Task ExecuteAndGradeAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CompileResult compileResult, CancellationToken stoppingToken)
    {
        int totalScore = 0;
        int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
        long totalMemory = 0;
        long totalTime = 0;
        await foreach (TestCase testCase in this.GetTestCasesAsync(messageDto))
        {
            logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

            var testCaseResult = await sandboxService.RunAsync(testCase, compileResult);

            if (testCaseResult.Status.Equals(JudgeStatus.STATUS_ACCEPTED))
            {
                this.logger.LogInformation("Validating test case {testCaseId} using {validatorType}", testCase.CaseNumber, testCase.ValidatorType);
                testCaseResult = await this.validatorServices[((int)testCase.ValidatorType) - 1].ValidateAsync(testCase, testCaseResult);
            }

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

        await this.sandboxService.DeleteFileAsync(compileResult.OutputFileId);

        logger.LogInformation("Procesed Compiled language for {runId} {language} {problemId} {domainId}",
                                   messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);
    }

    private IAsyncEnumerable<TestCase> GetTestCasesAsync(JudgeProcessRequest messageDto)
    {
        if (messageDto.Type == JudgeProcessRequestType.Pretest)
        {
            return this.judgeService.GetPretestCasesAsync(messageDto.RunId);
        }
        return problemService.GetTestCasesAsync(messageDto.ProblemId, messageDto.DomainId);
    }

    private static bool TryParseMessageDto(string message, out JudgeProcessRequest? messageDto)
    {
        try
        {
            messageDto = JsonSerializer.Deserialize<JudgeProcessRequest>(message);
            return messageDto != null;
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
        if (dataList == null || dataList.Problems == null)
        {
            logger.LogInformation("No problem data updated");
            return;
        }
        foreach (var problem in dataList.Problems)
        {
            string problemId = problem.ProblemId.ToString()!;
            logger.LogInformation("Problem {problemId} updated at {dommainId}", problemId, problem.DomainId);
            var zipData = await judgeService.GetProblemDataAsync(problemId, problem.DomainId);
            if (zipData == null)
            {
                logger.LogError("Problem data not found for {problemid}, {dommainId}", problemId, problem.DomainId);
                continue;
            }

            await cacheService.InvalidateCacheAsync(problem.DomainId, problemId);

            await cacheService.WriteCacheAsync(zipData, problem.DomainId, problemId, dataList.UnixTimestamp);
        }
    }
}
