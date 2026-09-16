using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class RequestAvailableLadleModel
    {
        public int requestLocationID { get; set; }
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public DateTime transactionDateTime { get; set; }
        public List<LadleRequestedDetails> ladles { get; set; }
    }
    public class LadleRequestedDetails
    {
        public int furnaceLocationID { get; set; }
        public string ladleNo { get; set; }
    }
}