using System.Text.Json.Serialization;

namespace Sojj.Dtos;

public class DataList
{
    [JsonPropertyName("pids")]
    public Problem[] Problems { get; set; }
    [JsonPropertyName("time")]
    public int UnixTimestamp { get; set; }
}

