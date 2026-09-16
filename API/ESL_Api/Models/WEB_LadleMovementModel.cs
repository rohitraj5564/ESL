using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class LadleMovementListResponse
    {
        public List<LadleMovementModel> intransitMovement { get; set; }
        public List<LadleMovementModel> completedmovement { get; set; }
    }
    public class LadleMovementModel
    {
        public string tripID { get; set; }
        public string locoName { get; set; }
        public int isActive { get; set; }
        public int isAssigned { get; set; }
        public List<LadleMovementList> movementData { get; set; }
    }
    public class LadleMovementList
    {
        public string ladleNo { get; set; }
        public string sourceName { get; set; }
        public string destinationName { get; set; }
    }
}