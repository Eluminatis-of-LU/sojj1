using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class Problem
{
    [JsonPropertyName("domain_id")]
    public string DomainId { get; set; }
    [JsonPropertyName("pid")]
    public object ProblemId { get; set; }
}
