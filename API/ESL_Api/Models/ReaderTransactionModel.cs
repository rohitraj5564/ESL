using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class ReaderTransactionModel
    {
        public string LadleNo { get; set; }
        public int LocationID { get; set; }
        public string LocationName { get; set; }
        public string CastNo { get; set; }
        public DateTime? ServerDatetime { get; set; }
        public DateTime? TransDatetime { get; set; }
    }
}