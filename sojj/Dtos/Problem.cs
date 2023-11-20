using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
    public class Problem
    {
        [JsonPropertyName("domain_id")]
        public string DomainId { get; set; }
        [JsonPropertyName("pid")]
        public int ProblemId { get; set; }
    }
}
