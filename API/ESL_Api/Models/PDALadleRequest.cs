using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
     public class PDALadleRequestModel
    {
        public Guid requestID { get; set; }
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public int locationID { get; set; }
        public int nosOfLadle { get; set; }
        public string C { get; set; }
        public string Si { get; set; }
        public string Mn { get; set; }
        public string S { get; set; }
        public string P { get; set; }
        public string Ti { get; set; }
        public string Cr { get; set; }
        public string SP { get; set; }
        public DateTime transactionDateTime { get; set; }
    }

}