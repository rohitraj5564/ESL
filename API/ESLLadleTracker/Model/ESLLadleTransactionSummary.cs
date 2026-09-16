using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleTransactionSummary
    {
        public List<ESLLadleConsolidated> LadleTransactionDetails = new List<ESLLadleConsolidated>();
        public List<ESLLocationConsolidate> ProductionSummary = new List<ESLLocationConsolidate>();
        public List<ESLLIMSData> LimsSummary = new List<ESLLIMSData>();
    }
}
