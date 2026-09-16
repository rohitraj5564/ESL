using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class DashboardLoginRequest
    {
        public string userName { get; set; }
        public string password { get; set; }
        public string activationKey { get; set; }
    }

    public class WEBDashboardLogin
    {
        public Guid userID { get; set; }
        public int userLocationId { get; set; }
        public string userLocationName { get; set; }
        public string userFirstName { get; set; }
        public string sessionToken { get; set; }
        public string jwtToken { get; set; }
    }
}