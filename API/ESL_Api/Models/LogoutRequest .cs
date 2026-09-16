using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class LogoutRequest
    {
        public string userID { get; set; }
        public string userName { get; set; }
        public string reason { get; set; }
        public string activationKey { get; set; }
    }
}