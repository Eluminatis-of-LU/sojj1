using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Sojj.Services;
internal class JudgeService : IJudgeService
{
    private readonly ILogger<IJudgeService> logger;
    private readonly IConfiguration configuration;
    private readonly string username;
    private readonly string password;
    private readonly Uri baseUrl;
    private readonly HttpClient httpClient;
    private readonly Uri wsUrl;
    private readonly ClientWebSocket ws;

    public JudgeService(ILogger<JudgeService> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
        username = this.configuration.GetValue<string>("JudgeService:Username") ?? throw new ArgumentNullException("JudgeService:Username");
        password = this.configuration.GetValue<string>("JudgeService:Password") ?? throw new ArgumentNullException("JudgeService:Password");
        baseUrl = new Uri(this.configuration.GetValue<string>("JudgeService:BaseUrl") ?? throw new ArgumentNullException("JudgeService:BaseUrl"));
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
        wsUrl = new Uri("ws://" + httpClient.BaseAddress.Host + ":" + httpClient.BaseAddress.Port + "/judge/consume-conn/websocket");
        ws = new ClientWebSocket();
        ws.Options.Cookies = socketHandler.CookieContainer;
    }

    public async Task<ClientWebSocket> ConsumeWebSocketAsync(CancellationToken cancellationToken)
    {
        await ws.ConnectAsync(wsUrl, cancellationToken);
        logger.LogInformation("Connected to websocket");
        return ws;
    }

    public async Task EnsureLoggedinAsync()
    {
        logger.LogInformation("Ensure logged in");
        if (await NoopAsync())
        {
            logger.LogInformation("Already logged in");
            return;
        }
        await LoginAsync();
    }

    public async Task<DataList?> GetDataListAsync(int lastUpdatedAtTimeStamp)
    {
        var response = await httpClient.GetAsync($"/judge/datalist?last={lastUpdatedAtTimeStamp}");

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var dataList = JsonSerializer.Deserialize<DataList>(content);
            return dataList;
        }

        logger.LogError("Get datalist failed");
        return null;
    }

    public async IAsyncEnumerable<TestCase> GetPretestCasesAsync(string runId)
    {
        var response = await httpClient.GetAsync($"/records/{runId}/data");
        if (!response.IsSuccessStatusCode)
        {
            yield break;
        }

        ZipArchive zipArchive = new ZipArchive(await response.Content.ReadAsStreamAsync());

        var configEntry = zipArchive.GetEntry("Config.ini");

        var configStreamReader = new StreamReader(configEntry.Open());

        int numberOfCases = int.Parse(await configStreamReader.ReadLineAsync());

        for (int caseNumber = 0; caseNumber < numberOfCases; caseNumber++)
        {
            var splitted = (await configStreamReader.ReadLineAsync()).Split('|');

            var inputFile = splitted[0];

            var outputFile = splitted[1];

            var inputEntry = zipArchive.GetEntry($"Input/{inputFile}");

            var outputEntry = zipArchive.GetEntry($"Output/{outputFile}");

            var inputStreamReader = new StreamReader(inputEntry.Open());

            var outputStreamReader = new StreamReader(outputEntry.Open());

            yield return new TestCase
            {
                CaseNumber = caseNumber,
                Input = await inputStreamReader.ReadToEndAsync(),
                Output = await outputStreamReader.ReadToEndAsync(),
                TimeLimit = long.Parse(splitted[2]) * Constants.NanoSecondInSecond,
                Score = int.Parse(splitted[3]),
                MemoryLimit = long.Parse(splitted[4]) * Constants.ByteInKiloByte,
                TotalCase = numberOfCases,
                ValidatorType= ValidatorType.FileValidator,
            };
        }
    }

    public async Task<ZipArchive?> GetProblemDataAsync(int problemId, string domainId)
    {
        var response = await httpClient.GetAsync($"/d/{domainId}/p/{problemId}/data");
        if (response.IsSuccessStatusCode)
        {
            ZipArchive zipArchive = new ZipArchive(await response.Content.ReadAsStreamAsync());
            return zipArchive;
        }
        return null;
    }

    public async Task LoginAsync()
    {
        var formdata = new MultipartFormDataContent();
        formdata.Add(new StringContent(username), "uname");
        formdata.Add(new StringContent(password), "password");
        var respone = await httpClient.PostAsync("/login", formdata);
        if (respone.IsSuccessStatusCode)
        {
            logger.LogInformation("Login succesfull");
        }
        else
        {
            logger.LogError("Loging failed");
        }
    }

    public async Task<bool> NoopAsync()
    {
        var response = await httpClient.GetAsync("/judge/noop");
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Noop success");
        }
        else
        {
            logger.LogError("Noop failed");
        }

        return response.IsSuccessStatusCode;
    }
}
