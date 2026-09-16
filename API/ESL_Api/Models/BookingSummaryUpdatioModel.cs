using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class BookingSummaryUpdatioModel
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public string ladleNo { get; set; }
        public string castNo { get; set; }
    }
    public class BookingSummaryUpdatioModel1
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public string ladleNo { get; set; }
        public string movementType { get; set; }
    }
}