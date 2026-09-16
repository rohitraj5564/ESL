using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class ProductionDataRes
    {
        public int requestCount { get; set; }
        public List<ReversalLadlesModel> reversalLadles { get; set; }
        public List<AvailableLadlesInFurnace> availableLadles { get; set; }
        public List<PDASummaryModel> summary { get; set; }
    }
    public class AvailableLadlesInFurnace
    {
        public string ladleNo { get; set; }
        public string chemicalComp { get; set; }
        public string sourceLocation { get; set; }
        public int locationID { get; set; }
        public string movementType { get; set; }
    }
}