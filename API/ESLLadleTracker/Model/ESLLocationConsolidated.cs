using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLocationConsolidate
    {
        public short LocationID { get; set; }
        public string LocationName { get; set; }

        public decimal TotalProduction { get; set; }

        public int LadleCount { get; set; } = 0;

        public List<ESLLocationConsolidate> ProductionUnits = new List<ESLLocationConsolidate>();
    }
}
