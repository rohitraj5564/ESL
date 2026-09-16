using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class TransferLadleMovementModel
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public string tripID { get; set; }
        public string ladleNos { get; set; }
        public string currentLocation { get; set; }
        public string destination { get; set; }
        public string transactionDateTime { get; set; }
    }
}