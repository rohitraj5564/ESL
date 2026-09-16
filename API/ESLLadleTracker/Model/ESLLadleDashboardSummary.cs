using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLDashboardSummary
    {
        public List<ESLLocation> LocationData = new List<ESLLocation>();
        public List<ESLLocationConsolidate> ProductionSummary = new List<ESLLocationConsolidate>();

        public List<ESLLIMSData> LimsSummary = new List<ESLLIMSData>();

        public List<ESLLadle> UnusedLadles = new List<ESLLadle>();

        public int TotalActiveLadleCount = 0;

        public int TotalInUseLadleCount = 0;

        public int CompletedTrips = 0;

        public int PendingTrips = 0;
    }
}
