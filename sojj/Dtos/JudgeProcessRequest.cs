using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
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
        public int Type { get; set; }
    }
}
