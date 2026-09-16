using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class CreateWEBMovementModel
    {
        public Guid ID { get; set; }
        public string userID { get; set; }
        public DateTime transactionDateTime { get; set; }
        public int locoID { get; set; }
        public List<MovementDetails> movementDetails { get; set; }
    }
    public class MovementDetails
    {
        public string ladleNo { get; set; }
        public string movementTypeID { get; set; }
        public string castID { get; set; }
        public string castNo { get; set; }
        public string sourceLocationName { get; set; }
        public string destinationName { get; set; }
        public DateTime transactionDateTime { get; set; }
    }
}