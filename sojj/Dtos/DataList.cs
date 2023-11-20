using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
    public class DataList
    {
        [JsonPropertyName("pids")]
        public Problem[] Problems { get; set; }
        [JsonPropertyName("time")]
        public  int UnixTimestamp { get; set; }
    }
}
