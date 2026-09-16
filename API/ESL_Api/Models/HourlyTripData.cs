using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class HourlyTripData
    {
        public string Time { get; set; }
        public int CompletedTrips { get; set; }
        public int TargetTrips { get; set; }
    }

    public class HourlyTripSummaryResponse
    {
        public int TotalTrips { get; set; }
        public int CompletedTrips { get; set; }
        public int PendingTrips { get; set; }

        public List<HourlyTripData> HourlyTrips { get; set; }
    }
}