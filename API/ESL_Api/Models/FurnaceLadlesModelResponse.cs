using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class FurnaceLadlesModelResponse
    {
        public List<string> ladles { get; set; }
        public string prevCastNo { get; set; }
        public string C { get; set; }
        public string Si { get; set; }
        public string Mn { get; set; }
        public string S { get; set; }
        public string P { get; set; }
        public string Ti { get; set; }
        public string Cr { get; set; }
        public string SP { get; set; }
    }
}