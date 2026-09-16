using System;

namespace ESL_Api.Models
{
    public class ManualVsAutoAssignmentModel
    {
        public Guid MappingID { get; set; }
        public short? TouchPointID { get; set; }
        public string TouchPointType { get; set; }
        public string LocoName { get; set; }
        public string LocoSerialNo { get; set; }
        public string LocoTagId { get; set; }
        public string LadleName { get; set; }
        public string LadleNumber { get; set; }
        public string LadleRfidTag { get; set; }
        public Guid? LocoEventID { get; set; }
        public Guid? LadleRfidEventID { get; set; }
        public Guid? CameraEventID { get; set; }
        public DateTime? LocoEventTime { get; set; }
        public DateTime? LadleRfidEventTime { get; set; }
        public DateTime? CameraEventTime { get; set; }
        public DateTime? MappingStartTime { get; set; }
        public DateTime? MappingEndTime { get; set; }
        public int? ConfidenceScore { get; set; }
        public string MappingStatus { get; set; }
        public string MappingReason { get; set; }
        public DateTime CreatedOn { get; set; }
        public bool IsManual { get; set; }
        public string AssignmentType { get; set; }
    }
}
