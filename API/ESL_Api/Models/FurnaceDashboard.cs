using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class FurnaceDashboard
    {
        public FurnaceLadlesModelResponse availableLadles { get; set; }
        public List<AvaialableEmptyLadles> emptyLadles { get; set; }
        public List<PDASummaryModel> summary { get; set; }
    }
    public class AvaialableEmptyLadles
    {
        public string ladleNo { get; set; }
        public string sourceName { get; set; }
        public string sourceId { get; set; }
    }
}