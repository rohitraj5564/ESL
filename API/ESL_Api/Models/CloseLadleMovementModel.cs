using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class CloseLadleMovementModel
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public string tripID { get; set; }
        public string ladleNos { get; set; }
        public string source { get; set; }
        public string destination { get; set; }
        public DateTime transactionDateTime { get; set; }
    }
}