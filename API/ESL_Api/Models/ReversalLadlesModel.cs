using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class ReversalLadlesModel
    {
        public string ladleNo { get; set; }
        public string locoName { get; set; }
        public string chemicalComp { get; set; }
        public Guid castID { get; set; }
    }
}