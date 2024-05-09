using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class Language
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("compile")]
    public string[]? Compile { get; set; }

    [JsonPropertyName("codeFile")]
    public string CodeFile { get; set; }

    [JsonPropertyName("outputFile")]
    public string OutputFile { get; set; }

    [JsonPropertyName("execute")]
    public string[]? Execute { get; set; }

    [JsonPropertyName("cpuLimit")]
    public long? CpuLimit { get; set; }
}
