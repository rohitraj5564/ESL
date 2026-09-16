using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class RequestReversalLadles
    {
        public int requestLocationID { get; set; }
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public DateTime transactionDateTime { get; set; }
        public List<ReversalLadleRequestedDetails> ladles { get; set; }
    }
    public class ReversalLadleRequestedDetails
    {
        public int sourceLocationID { get; set; }
        public string ladleNo { get; set; }
        public string movementType { get; set; }
    }
}