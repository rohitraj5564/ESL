using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class ProductionOrderModel
    {
        public int Pro_id { get; set; }
        public string Production_Unit { get; set; }
        public string Production_Order { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime Todate { get; set; }
        public string Order_Month { get; set; }
        public string Order_Year { get; set; }
        public string CastNo_Predecessor { get; set; }
    }
}