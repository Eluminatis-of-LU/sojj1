using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
    public class TestCase
    {
        public int CaseNumber { get; set; }

        public string Input { get; set; }

        public string Output { get; set; }

        public int Score { get; set; }

        public long TimeLimit { get; set; }

        public long MemoryLimit { get; set; }
        public int TotalCase { get; set; }
    }
}
