using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class OccupiedLocoDetailsModel
    {
        public int LocoID { get; set; }
        public string LocoName { get; set; }
        public int IsOccupied { get; set; }
        public int IsBooked { get; set; }
        public int LadleCount { get; set; }
        public string LastOccupiedDateTime { get; set; }
    }
}