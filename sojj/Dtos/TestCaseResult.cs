using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
    public class TestCaseResult
    {
        public JudgeStatus Status { get; set; }

        public long TimeInNs { get; set; }

        public long MemoryInByte { get; set; }

        public string Output { get; set; }
        public string Message { get; internal set; }
        public int Score { get; internal set; }
    }
}
