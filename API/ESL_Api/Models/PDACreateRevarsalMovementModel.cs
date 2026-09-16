using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PDACreateRevarsalMovementModel
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public string source { get; set; }
        public DateTime transactionDateTime { get; set; }
        public List<ReversalDetails> reversalDetails { get; set; }
    }
    public class ReversalDetails
    {
        public Guid ID { get; set; }
        public string ladleNo { get; set; }
        public string movementType { get; set; }
        public string destination { get; set; }
        public string castID { get; set; }
    }
}