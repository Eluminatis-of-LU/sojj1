using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class JudgeProcessRequest
{
    [JsonPropertyName("rid")]
    public string RunId { get; set; }

    [JsonPropertyName("tag")]
    public int Tag { get; set; }

    [JsonPropertyName("pid")]
    public string ProblemId { get; set; }

    [JsonPropertyName("domain_id")]
    public string DomainId { get; set; }

    [JsonPropertyName("lang")]
    public string Language { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("type")]
    public JudgeProcessRequestType Type { get; set; }
}

public enum JudgeProcessRequestType
{
    Submission = 0,
    Pretest = 1,
}
