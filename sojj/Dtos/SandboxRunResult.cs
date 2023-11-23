using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class SandboxRunResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("exitStatus")]
    public int ExitStatus { get; set; }

    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("memory")]
    public long Memory { get; set; }

    [JsonPropertyName("runTime")]
    public long RunTime { get; set; }

    [JsonPropertyName("files")]
    public Dictionary<string, string> Files { get; set; }

    [JsonPropertyName("fileIds")]
    public Dictionary<string, string> FileIds { get; set; }
}
