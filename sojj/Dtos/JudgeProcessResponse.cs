using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class JudgeProcessResponse
{
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonPropertyName("tag")]
    public int Tag { get; set; }

    [JsonPropertyName("status")]
    public JudgeStatus Status { get; set; }

    [JsonPropertyName("case")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JudgeProcessResponseCase? Case { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; internal set; }

    [JsonPropertyName("time_ms")]
    public float TimeInMilliseconds { get; internal set; }

    [JsonPropertyName("memory_kb")]
    public float MemoryInKiloBytes { get; internal set; }

    [JsonPropertyName("judge_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JudgeText { get; internal set; }

    [JsonPropertyName("compiler_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CompilerText { get; internal set; }
}

public class JudgeProcessResponseCase
{
    [JsonPropertyName("status")]
    public JudgeStatus Status { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("time_ms")]
    public float TimeInMilliseconds { get; set; }

    [JsonPropertyName("memory_kb")]
    public float MemoryInKiloBytes { get; set; }

    [JsonPropertyName("judge_text")]
    public string? JudgeText { get; set; }
}
