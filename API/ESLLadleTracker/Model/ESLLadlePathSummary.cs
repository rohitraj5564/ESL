using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadlePathSummary
    {
        public List<ESLLadleConsolidated> LadleTransactionDetails = new List<ESLLadleConsolidated>();

        public decimal TotalTonnage { get; set; }

        public string TotalTonnageStr { get; set; }

        public TimeSpan TotalTAT { get; set; } = new TimeSpan(5, 10, 10);

        public string TotalTATSTR { get; set; } = "05:10:10";

        public string ExportString { get; set; }

    }
        
}
