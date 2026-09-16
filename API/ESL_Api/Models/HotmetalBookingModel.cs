using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class HotmetalBookingModel
    {
        public long Tran_ID { get; set; }

        public DateTime? GrossDateTime { get; set; }

        public DateTime? TaredateTime { get; set; }

        public int? LaddleNumber { get; set; }

        public decimal? GrossWT { get; set; }

        public decimal? TareWT { get; set; }

        public decimal? NetWT { get; set; }

        public DateTime? TransactionDateTime { get; set; }

        public string SenderPlant { get; set; }

        public string CastNumber { get; set; }

        public string ReceiverPlant { get; set; }

        public string Gross_DT_Time { get; set; }

        public long? FID { get; set; }
    }
}