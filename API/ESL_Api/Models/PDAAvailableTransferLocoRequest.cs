using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PDAAvailableTransferLocoRequest
    {
        public string clientDeviceID { get; set; }
        public string locoID { get; set; }
    }
}