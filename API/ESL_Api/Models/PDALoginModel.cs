using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PDALoginRequest
    {
        public string userName { get; set; }
        public string password { get; set; }
        public string clientDeviceID { get; set; }
        public string activationKey { get; set; }
    }
    public class PDALoginModel
    {
        public Guid userID { get; set; }
        public int userLocationId { get; set; }
        public string userLocationName { get; set; }
        public string userFirstName { get; set; }
        public string sessionToken { get; set; }
        public string jwtToken { get; set; }
        public List<ModuleDetail> modules { get; set; }
        public List<LocationDetail> locationDetails { get; set; }
    }

    public class ModuleDetail
    {
        public int moduleID { get; set; }
        public string moduleName { get; set; }
    }

}