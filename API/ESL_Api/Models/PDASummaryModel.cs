using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PDASummaryModel
    {
        public Guid? ID { get; set; }
        public string ladleNo { get; set; }
        public string castNo { get; set; }
        public string chemicalComp { get; set; }
        public string type { get; set; }
        public string revType { get; set; }
        public DateTime? sortDate { get; set; }           
        public DateTime? completedDateTime { get; set; }
        public string bookingStatus { get; set; }
    }
}