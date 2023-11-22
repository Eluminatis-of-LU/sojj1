using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sojj.Dtos
{
    public class TestCaseConfig
    {
        public long TimeLimit { get; set; }

        public long MemoryLimit { get; set; }

        public ValidatorType ValidatorType { get; set; }

        public TestCase[] TestCases { get; set; }
    }
}
