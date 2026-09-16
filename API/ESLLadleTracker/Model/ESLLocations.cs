using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLocation
    {
        public short LocationID { get; set; }
        public string LocationName { get; set; }
        /// <summary>
        /// Values would be FURNACE,WEIGHMENT,PROD,MAINTANANCE
        /// </summary>
        public string LocationType { get; set; }

        public List<ESLLadle> LadleList = new List<ESLLadle>();

        public int TotalProduction { get; set; }

        public int LadleCount { get; set; } = 12;

        public List<ESLLocation> ProductionUnits = new List<ESLLocation>();

        public string AverageTATSTR = string.Empty; //Average Turn Around

        public string AverageHoldTimeSTR = string.Empty; //Holding Time....

        public TimeSpan totalTAT;

        public int totalTATCount;

        public TimeSpan totalHoldingTime;

        public int totalHoldCount;

    }
}
