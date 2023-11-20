using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
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
    }
}
