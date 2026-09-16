using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLocationData
    {
        public short LocationID { get; set; }
        public string LocationName { get; set; }
       
        public string LocationType { get; set; }

        public List<ESLLadleConsolidated> LadleList = new List<ESLLadleConsolidated>();

        public int TotalProduction { get; set; }

        public int LadleCount { get; set; } = 12;

    

        public string AverageTATSTR = string.Empty; //Average Turn Around

        public string AverageHoldTimeSTR = string.Empty; //Holding Time....

        public TimeSpan totalTAT;

        public int totalTATCount;

        public TimeSpan totalHoldingTime;

        public int totalHoldCount;
    }
}
