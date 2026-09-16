using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class KPIModel
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Color { get; set; }
        public string Icon { get; set; }
        public string Trend { get; set; }
    }
}