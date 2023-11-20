using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        await this.UpdateProblemDataAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            this.logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await this.judgeService.EnsureLoggedinAsync();

            var ws = await this.judgeService.ConsumeWebSocketAsync(stoppingToken);

            var buffer = new byte[1024 * 4];

            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, stoppingToken);
                }
                
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                this.logger.LogInformation("Message received: {message}", message);

                if (!TryParseMessageDto(message, out var messageDto))
                {
                    await this.UpdateProblemDataAsync();
                    continue;
                }

                await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new 
                {
                    key = "next",
                    tag = messageDto.Tag,
                    status = JudgeStatus.STATUS_COMPILING,
                })), WebSocketMessageType.Text, true, stoppingToken);
                var compileResult = await this.sandboxService.CompileAsync(messageDto.Code, messageDto.RunId, messageDto.Language);
                if (compileResult.Status == JudgeStatus.STATUS_ACCEPTED)
                {
                    int totalScore = 0;
                    int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
                    long totalMemory = 0;
                    long totalTime = 0;
                    await foreach (TestCase testCase in this.problemService.GetTestCasesAsync(messageDto.ProblemId.ToString(), messageDto.DomainId))
                    {
                        this.logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);
                        
                        var testCaseResult = await this.sandboxService.RunAsync(testCase, compileResult);

                        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                        {
                            key = "next",
                            tag = messageDto.Tag,
                            status = JudgeStatus.STATUS_JUDGING,
                            Case = new
                            {
                                status = testCaseResult.Status,
                                score = testCaseResult.Score,
                                time_ms = testCaseResult.TimeInNs / 1000000.0f,
                                memory_kb = testCaseResult.MemoryInByte / 1024.0f,
                                judge_text = testCaseResult.Message,
                            },
                            progress = ((testCase.CaseNumber + 1) * 100.0f / testCase.TotalCase),
                        })), WebSocketMessageType.Text, true, stoppingToken);

                        totalScore += testCaseResult.Score;
                        problemStatus = Math.Max(problemStatus, (int)testCaseResult.Status);
                        totalMemory = Math.Max(totalMemory, testCaseResult.MemoryInByte);
                        totalTime = Math.Max(totalTime, testCaseResult.TimeInNs);
                    }

                    await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        key = "end",
                        tag = messageDto.Tag,
                        status = problemStatus,
                        score = totalScore,
                        time_ms = totalTime / 1000000.0f,
                        memory_kb = totalMemory / 1024.0f,
                    })), WebSocketMessageType.Text, true, stoppingToken);
                }
                else if (compileResult.Status == JudgeStatus.STATUS_INTEPRETED_LANGUAGE)
                {
                    int totalScore = 0;
                    int problemStatus = (int)JudgeStatus.STATUS_ACCEPTED;
                    long totalMemory = 0;
                    long totalTime = 0;
                    await foreach (TestCase testCase in this.problemService.GetTestCasesAsync(messageDto.ProblemId.ToString(), messageDto.DomainId))
                    {
                        this.logger.LogInformation("Running test case {testCase.CaseNumber}", testCase.CaseNumber);

                        var testCaseResult = await this.sandboxService.RunInterpreterAsync(testCase, messageDto.Code, messageDto.RunId, messageDto.Language);

                        await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                        {
                            key = "next",
                            tag = messageDto.Tag,
                            status = JudgeStatus.STATUS_JUDGING,
                            Case = new
                            {
                                status = testCaseResult.Status,
                                score = testCaseResult.Score,
                                time_ms = testCaseResult.TimeInNs / 1000000.0f,
                                memory_kb = testCaseResult.MemoryInByte / 1024.0f,
                                judge_text = testCaseResult.Message,
                            },
                            progress = ((testCase.CaseNumber + 1) * 100.0f / testCase.TotalCase),
                        })), WebSocketMessageType.Text, true, stoppingToken);

                        totalScore += testCaseResult.Score;
                        problemStatus = Math.Max(problemStatus, (int)testCaseResult.Status);
                        totalMemory = Math.Max(totalMemory, testCaseResult.MemoryInByte);
                        totalTime = Math.Max(totalTime, testCaseResult.TimeInNs);
                    }

                    await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        key = "end",
                        tag = messageDto.Tag,
                        status = problemStatus,
                        score = totalScore,
                        time_ms = totalTime / 1000000.0f,
                        memory_kb = totalMemory / 1024.0f,
                    })), WebSocketMessageType.Text, true, stoppingToken);
                }
                else
                {
                    await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                    {
                        key = "end",
                        tag = messageDto.Tag,
                        status = JudgeStatus.STATUS_COMPILE_ERROR,
                        score = 0,
                        time_ms = 0,
                        memory_kb = 0,
                    })), WebSocketMessageType.Text, true, stoppingToken);
                }

            }

            await Task.Delay(1000, stoppingToken);
        }
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
        await this.judgeService.EnsureLoggedinAsync();
        int lastUpdateAt = await this.cacheService.GetCacheUpdateTimeAsync();
        var dataList = await this.judgeService.GetDataListAsync(0);
        foreach (var problem in dataList.Problems)
        {
            this.logger.LogInformation($"Problem {problem.ProblemId} updated at {problem.DomainId}");
            var zipData = await this.judgeService.GetProblemDataAsync(problem.ProblemId, problem.DomainId);
            if (zipData == null)
            {
                this.logger.LogError("Problem data not found for {problemid}, {dommainId}", problem.ProblemId, problem.DomainId);
                continue;
            }

            await this.cacheService.InvalidateCacheAsync();

            await this.cacheService.WriteCacheAsync(zipData, problem.DomainId, problem.ProblemId.ToString(), dataList.UnixTimestamp);
        }
    }
}
