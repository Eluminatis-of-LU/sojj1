using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Sojj.Dtos;
using Sojj.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sojj.Services
{
    public class SandboxService : ISandboxService
    {
        private readonly ILogger<SandboxService> logger;
        private readonly IConfiguration configuration;
        private readonly Uri baseUrl;
        private readonly HttpClient httpClient;
        private readonly string languageFilePath;
        private readonly Dictionary<string, Language> languages;

        public SandboxService(ILogger<SandboxService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.baseUrl = new Uri(this.configuration.GetValue<string>("SandboxUrl") ?? throw new ArgumentNullException("SandboxUrl"));
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
            this.languageFilePath = this.configuration.GetValue<string>("LanguageFilePath") ?? throw new ArgumentNullException("LanguageFilePath");
            using var stream = File.OpenRead(this.languageFilePath);
            this.languages = JsonSerializer.Deserialize<Dictionary<string, Language>>(stream);
        }

        public async Task<CompileResult> CompileAsync(string sourceCode, string runId, string language)
        {
            if (!this.languages.TryGetValue(language, out var languageInfo))
            {
                this.logger.LogError("Language {language} not found", language);
                return new CompileResult 
                { 
                    Status = Constants.CompilationError,
                    Message = "Language not found",
                };
            }
            if (languageInfo.Type != "compiler")
            {
                this.logger.LogInformation("Language {language} is not a compile language", language);
                return new CompileResult
                {
                    Status = Constants.InterpretedLanguage,
                    Message = "Language is not a compiled language",
                };
            }
            this.logger.LogInformation("Compiling {runId}", runId);
            var request = new SandboxRunRequest
            {
                Commands = new Command[]
                {
                    new Command
                    {
                        Args = languageInfo.Compile.Split(' '),
                        Env = new string[] {"PATH=/usr/bin:/bin"},
                        Files = new SandboxFile[]
                        {
                            new SandboxCollectorFile
                            {
                                Name = "stdout",
                                Max = 10240,
                            },
                            new SandboxCollectorFile
                            {
                                Name = "stderr",
                                Max = 10240,
                            },
                        },
                        CpuLimit = 10000000000,
                        MemoryLimit = 1024 * 1024 * 1024,
                        ProcessLimit = 50,
                        CopyIn = new Dictionary<string, SandboxFile>
                        {
                            { languageInfo.CodeFile, new SandboxMemoryFie { Content = sourceCode } },
                        },
                        CopyOut = new string[] { "stdout", "stderr" },
                        CopyOutCached = new string[] { languageInfo.CodeFile, languageInfo.OutputFile },
                    }
                },
            };
            this.logger.LogInformation("Compile request: {request}", JsonSerializer.Serialize(request));
            var response = await this.httpClient.PostAsJsonAsync("/run", request);

            if (!response.IsSuccessStatusCode)
            {
                this.logger.LogError("Compile failed: {statusCode}", response.StatusCode);
                return new CompileResult
                {
                    Status = Constants.CompilationError,
                    Message = "Compile failed",
                };
            }
            
            var content = await response.Content.ReadAsStringAsync();
            this.logger.LogInformation("Compile result: {content}", content);
            var result = JsonSerializer.Deserialize<SandboxRunResult[]>(content);
            if (result == null || result.Length != 1)
            {
                this.logger.LogError("Compile result length is not 1");
                return new CompileResult
                {
                    Status = Constants.CompilationError,
                    Message = "Compile result length is not 1",
                };
            }

            var compileResult = result[0];

            if (compileResult.Status != Constants.Accepted)
            {
                this.logger.LogError("Compile failed: {status}", compileResult.Status);
                return new CompileResult
                {
                    Status = Constants.CompilationError,
                    Message = compileResult.Files[Constants.Stderr],
                };
            }

            this.logger.LogInformation("Compile success");

            return new CompileResult
            {
                Status = Constants.CompileSuccess,
                Message = compileResult.Files[Constants.Stdout],
                ExecuteArgs = languageInfo.Execute.Split(' '),
                Language = language,
                RunId = runId,
                OutputFileId = compileResult.FileIds[languageInfo.OutputFile],
            };
        }
    }
}
