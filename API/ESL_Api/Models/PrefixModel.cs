using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class PrefixModel
    {
        public string ID { get; set; }

        public int LocationId { get; set; }

        public string LocationName { get; set; }

        public string Prefix { get; set; }

        public DateTime CreatedDateTime { get; set; }
    }
}