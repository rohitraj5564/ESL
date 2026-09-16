using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class FullReportModel
    {
        public string CastNo { get; set; }
        public string TripNo { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
        public string LocoName { get; set; }
        public string LadleNo { get; set; }
        public string MovementType { get; set; }
        public string Status { get; set; }
        public string C { get; set; }
        public string Si { get; set; }
        public string Mn { get; set; }
        public string S { get; set; }
        public string P { get; set; }
        public string Ti { get; set; }
        public string Cr { get; set; }
        public string SP { get; set; }
        public DateTime? AssignedDateTime { get; set; }
        public DateTime? RequestedDateTime { get; set; }
        public DateTime? CompletedDateTime { get; set; }
        public string ReportType { get; set; }
    }
}