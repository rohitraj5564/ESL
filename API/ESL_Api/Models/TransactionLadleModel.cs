using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class TransactionLadleModel
    {
        public string ladleNo { get; set; }
        public string sourceLocation { get; set; }
        public string status { get; set; }
        public string transferDateTime { get; set; }
    }
}