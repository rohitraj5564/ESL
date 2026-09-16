using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class LadleRequestListResponse
    {
        public List<LadleRequestModel> pendingRequest { get; set; }
        public List<LadleRequestModel> completedRequest { get; set; }
    }

    public class LadleRequestModel
    {
        public Guid ID { get; set; }
        public string requestNo { get; set; }
        public string locationName { get; set; }
        public int nosOfLadle { get; set; }
        public string ladleNo { get; set; }
        public string C { get; set; }
        public string Si { get; set; }
        public string Mn { get; set; }
        public string S { get; set; }
        public string P { get; set; }
        public string Ti { get; set; }
        public string Cr { get; set; }
        public string SP { get; set; }
        public DateTime createdDateTime { get; set; }
        public DateTime? completedDateTime { get; set; } 
        public int isActive { get; set; }
    }
}