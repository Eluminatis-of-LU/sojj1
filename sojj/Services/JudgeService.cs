using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sojj.Services
{
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
            this.username = this.configuration.GetValue<string>("JudgeService:Username") ?? throw new ArgumentNullException("JudgeService:Username");
            this.password = this.configuration.GetValue<string>("JudgeService:Password") ?? throw new ArgumentNullException("JudgeService:Password");
            this.baseUrl = new Uri(this.configuration.GetValue<string>("JudgeService:BaseUrl") ?? throw new ArgumentNullException("JudgeService:BaseUrl"));
            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var socketHandler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) };
            socketHandler.CookieContainer = new CookieContainer();
            var pollyHandler = new PolicyHttpMessageHandler(retryPolicy)
            {
                InnerHandler = socketHandler,
            };

            this.httpClient = new HttpClient(pollyHandler);
            this.httpClient.BaseAddress = this.baseUrl;
            this.wsUrl = new Uri("ws://" + this.httpClient.BaseAddress.Host + ":" + this.httpClient.BaseAddress.Port + "/judge/consume-conn/websocket");
            this.ws = new ClientWebSocket();
            this.ws.Options.Cookies = socketHandler.CookieContainer;
        }

        public async Task<ClientWebSocket> ConsumeWebSocketAsync(CancellationToken cancellationToken)
        {
            await this.ws.ConnectAsync(this.wsUrl, cancellationToken);
            this.logger.LogInformation("Connected to websocket");
            return this.ws;
        }

        public async Task EnsureLoggedinAsync()
        {
            this.logger.LogInformation("Ensure logged in");
            if (await this.NoopAsync())
            {
                this.logger.LogInformation("Already logged in");
                return;
            }
            await this.LoginAsync();
        }

        public async Task<DataList?> GetDataListAsync(int lastUpdatedAtTimeStamp)
        {
            var response = await this.httpClient.GetAsync($"/judge/datalist?last={lastUpdatedAtTimeStamp}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var dataList = JsonSerializer.Deserialize<DataList>(content);
                return dataList;
            }
            
            this.logger.LogError("Get datalist failed");
            return null;
        }

        public async Task<ZipArchive?> GetProblemDataAsync(int problemId, string domainId)
        {
            var response = await this.httpClient.GetAsync($"/d/{domainId}/p/{problemId}/data");
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
            var respone = await this.httpClient.PostAsync("/login", formdata);
            if (respone.IsSuccessStatusCode)
            {
                this.logger.LogInformation("Login succesfull");
            }
            else
            {
                this.logger.LogError("Loging failed");
            }
        }

        public async Task<bool> NoopAsync()
        {
            var response = await this.httpClient.GetAsync("/judge/noop");
            if (response.IsSuccessStatusCode)
            {
                this.logger.LogInformation("Noop success");
            }
            else
            {
                this.logger.LogError("Noop failed");
            }

            return response.IsSuccessStatusCode;
        }
    }
}
