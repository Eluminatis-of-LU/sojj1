using Microsoft.Extensions.DependencyInjection;
using Serilog.Context;
using Sojj.Dtos;
using Sojj.Services.Contracts;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Sojj;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
	private readonly IServiceScope _serviceScope;
	private readonly IJudgeService _judgeService;
    private readonly ICacheService _cacheService;
    private readonly ISandboxService _sandboxService;
    private readonly IProblemService _problemService;
    private readonly List<IValidatorService> _validatorServices;
	private readonly SemaphoreSlim _workerCacheLock;
	private readonly Guid _workerId;
    private readonly int _testCaseFailLimit;

	public Worker(ILogger<Worker> logger,
        ICacheService cacheService,
        ISandboxService sandboxService,
        IProblemService problemService,
        IEnumerable<IValidatorService> validatorServices,
        [FromKeyedServices("workerCacheLock")] SemaphoreSlim workerCacheLock,
		IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)

	{
        _logger = logger;
        _serviceScope = serviceScopeFactory.CreateScope();
		_judgeService = _serviceScope.ServiceProvider.GetRequiredService<IJudgeService>();
        _cacheService = cacheService;
        _sandboxService = sandboxService;
        _problemService = problemService;
        _validatorServices = [.. validatorServices.OrderBy(x => x.Type)];
        if (_validatorServices.Count != (int)ValidatorType.CustomValidator)
        {
            throw new Exception("Not all validator registered");
        }
        _testCaseFailLimit = configuration.GetValue<int>("TestCaseFailLimit");
        _workerCacheLock = workerCacheLock;
        LogContext.PushProperty("WorkerId", _workerId = Guid.NewGuid());
        _logger.LogInformation("Worker created with id {WorkerId}", _workerId);
	}

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateProblemDataAsync();
        
        var buffer = WebSocket.CreateClientBuffer(1024 * 4, 1024 * 4);

        WebSocketReceiveResult webSocketReceiveResult;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _judgeService.EnsureLoggedinAsync();

            var ws = await _judgeService.ConsumeWebSocketAsync(stoppingToken);

            while (ws.State == WebSocketState.Open)
            {
                string message = string.Empty;

                do {
                    webSocketReceiveResult = await ws.ReceiveAsync(buffer, stoppingToken);
                    message += Encoding.UTF8.GetString(buffer.Array!, 0, webSocketReceiveResult.Count);
                } while (webSocketReceiveResult.MessageType != WebSocketMessageType.Close && !webSocketReceiveResult.EndOfMessage);

                if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken);
                }

                _logger.LogInformation("Message received: {message}", message);

                if (!TryParseMessageDto(message, out var messageDto))
                {
                    await UpdateProblemDataAsync();
                    continue;
                }

                LogContext.PushProperty("CorrelationId", Guid.NewGuid());
                LogContext.PushProperty("runId", messageDto!.RunId);
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
            _logger.LogError(ex, "Error processing for {runId} {language} {problemId} {domainId}",
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

        var compileResult = await _sandboxService.CompileAsync(messageDto.Code, messageDto.RunId, messageDto.Language);

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

            _logger.LogInformation("Compile error for {runId} {language} {problemId} {domainId}",
                messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId);
        }
    }

    private async Task ExecuteAndGradeAsync(JudgeProcessRequest messageDto, ClientWebSocket ws, CompileResult compileResult, CancellationToken stoppingToken)
    {
        int totalScore = 0;
        int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
        long totalMemory = 0;
        long totalTime = 0;
        int failCount = 0;
        await foreach (TestCase testCase in this.GetTestCasesAsync(messageDto))
        {
            _logger.LogInformation("Running test case {TestCaseNumber}", testCase.CaseNumber);

            var testCaseResult = await _sandboxService.RunAsync(testCase, compileResult);

            if (testCaseResult.Status.Equals(JudgeStatus.STATUS_ACCEPTED))
            {
                _logger.LogInformation("Validating test case {testCaseId} using {validatorType}", testCase.CaseNumber, testCase.ValidatorType);
                testCaseResult.Message = string.Empty;
                testCaseResult = await _validatorServices[((int)testCase.ValidatorType) - 1].ValidateAsync(testCase, testCaseResult);
                _logger.LogInformation("Validated test case {testCaseId} using {validatorType} {ProcessedTestCase}", testCase.CaseNumber, testCase.ValidatorType, true);
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

            failCount += testCaseResult.Status.Equals(JudgeStatus.STATUS_ACCEPTED) ? 0 : 1;

            if (failCount > _testCaseFailLimit)
            {
                break;
            }
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

        await _sandboxService.DeleteFileAsync(compileResult.OutputFileId);

        _logger.LogInformation("Graded problem for: {runId} {language} {problemId} {domainId} {ProcessedProblemSubmission}",
                                   messageDto.RunId, messageDto.Language, messageDto.ProblemId, messageDto.DomainId, true);
    }

    private IAsyncEnumerable<TestCase> GetTestCasesAsync(JudgeProcessRequest messageDto)
    {
        if (messageDto.Type == JudgeProcessRequestType.Pretest)
        {
            return _judgeService.GetPretestCasesAsync(messageDto.RunId);
        }
        return _problemService.GetTestCasesAsync(messageDto.ProblemId, messageDto.DomainId);
    }

    private static bool TryParseMessageDto(string message, out JudgeProcessRequest? messageDto)
    {
        try
        {
            messageDto = JsonSerializer.Deserialize<JudgeProcessRequest>(message);
            return messageDto is not null;
        }
        catch (Exception)
        {
            messageDto = null;
            return false;
        }
    }

    private async Task UpdateProblemDataAsync()
    {
        _logger.LogInformation("Inside UpdateProblemDataAsync");
        _logger.LogInformation("Waiting for workerCacheLock");
        await _workerCacheLock.WaitAsync();
        try
        {
            await _judgeService.EnsureLoggedinAsync();
            int lastUpdateAt = await _cacheService.GetCacheUpdateTimeAsync();
            var dataList = await _judgeService.GetDataListAsync(lastUpdateAt);
            if (dataList == null || dataList.Problems == null)
            {
                _logger.LogInformation("No problem data updated");
                return;
            }
            foreach (var problem in dataList.Problems)
            {
                string problemId = problem.ProblemId.ToString()!;
                _logger.LogInformation("Problem {problemId} updated at {domainId}", problemId, problem.DomainId);
                var zipData = await _judgeService.GetProblemDataAsync(problemId, problem.DomainId);
                if (zipData == null)
                {
                    _logger.LogError("Problem data not found for {problemid}, {domainId}", problemId, problem.DomainId);
                    continue;
                }

                await _cacheService.InvalidateCacheAsync(problem.DomainId, problemId);

                _cacheService.WriteCache(zipData, problem.DomainId, problemId);
            }

            await _cacheService.UpdateCacheTimeAsync(dataList.UnixTimestamp);
        }
        finally
        {
            _logger.LogInformation("Releasing workerCacheLock");
            _workerCacheLock.Release();
        }
    }
}
