using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class InsertLadleReversalWEB
    {
        public Guid ID { get; set; }
        public string userID { get; set; }
        public DateTime transactionDateTime { get; set; }
        public int locoID { get; set; }
        public List<ReversalDetailsWEB> revLadles { get; set; }
    }
    public class ReversalDetailsWEB
    {
        public string castID { get; set; }
        public string ladleNo { get; set; }
        public string movementType { get; set; }
        public string sourceLocationName { get; set; }
        public string destinationName { get; set; }
        public DateTime transactionDateTime { get; set; }
    }
}