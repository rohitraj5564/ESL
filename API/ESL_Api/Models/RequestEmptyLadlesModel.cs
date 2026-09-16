using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class RequestEmptyLadlesModel
    {
        public string clientDeviceID { get; set; }
        public string userID { get; set; }
        public int requestLocationID { get; set; }
        public DateTime transactionDateTime { get; set; }
        public List<EmptyLadleItem> ladles { get; set; }
    }
    public class EmptyLadleItem
    {
        public int sourceLocationID { get; set; }
        public string ladleNo { get; set; }
    }
}