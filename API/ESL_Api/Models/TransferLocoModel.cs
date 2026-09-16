using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class TransferLocoModel
    {
        public string tripID { get; set; }
        public string currentLoco { get; set; }
        public string newLoco { get; set; }
    }
}