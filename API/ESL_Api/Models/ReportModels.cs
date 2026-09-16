using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class TransactionReportModel
    {
        public string TripNo { get; set; }
        public string CastNo { get; set; }
        public string LocoName { get; set; }
        public string LadleNo { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
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
        public DateTime? CompletedDateTime { get; set; }
    }

    public class RequestReportModel
    {
        public string TripNo { get; set; }
        public string CastNo { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
        public string Department { get; set; }
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
    }

    public class ReportFilterModel
    {
        public DateTime? fromDate { get; set; }
        public DateTime? toDate { get; set; }
    }

    public class LadleWeighmentReportModel
    {
        public long? SLNo { get; set; }
        public int? ID { get; set; }
        public Guid? TripID { get; set; }
        public DateTime? TripDateTime { get; set; }
        public string TripNumber { get; set; }
        public string LadleNo { get; set; }
        public string TransactionNo { get; set; }
        public string ConsumptionNo { get; set; }
        public string CastNo { get; set; }
        public string SenderLocation { get; set; }
        public string ReceiverLocation { get; set; }
        public decimal? TareWeight { get; set; }
        public DateTime? TareWeightDateTime { get; set; }
        public decimal? GrossWeight { get; set; }
        public DateTime? GrossWeightDateTime { get; set; }
        public decimal? NetWeight { get; set; }
        public decimal? Weight { get; set; }
        public DateTime? WeightDateTime { get; set; }
        public int? WeighbridgeID { get; set; }
        public string TransactionType { get; set; }
        public bool? IsManual { get; set; }
        public int? ProcessState { get; set; }
        public DateTime? ServerDateTime { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public string ConsumptionType { get; set; }
    }
}