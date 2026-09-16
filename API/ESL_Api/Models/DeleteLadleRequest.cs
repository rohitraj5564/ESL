using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class DeleteLadleRequest
    {
        public string TripID { get; set; }
        public string LadleNo { get; set; }
    }
}