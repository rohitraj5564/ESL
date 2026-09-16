using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class LadleReversalWEB
    {
        public Guid ID { get; set; }
        public string castID { get; set; }
        public string castNo { get; set; }
        public string ladleNo { get; set; }
        public string movementType { get; set; }
        public string sourceName { get; set; }
        public string destinationName { get; set; }
        public string isBooked { get; set; }
    }
}