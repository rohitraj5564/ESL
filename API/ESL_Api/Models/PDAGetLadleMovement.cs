using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PDAGetLadleMovement
    {
        public string tripID { get; set; }
        public string ladleNo { get; set; }
        public string type { get; set; }
        public string source { get; set; }
        public string destination { get; set; }
        public bool isActive { get; set; }
        public bool isTransferred { get; set; }
        public bool isAssigned { get; set; }
    }
    public class LadleMovementResponse
    {
        //public int completedCount { get; set; }
        public List<PDAGetLadleMovement> movementOrder { get; set; }
    }

}