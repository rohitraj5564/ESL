using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class TranscationLadledetails
    {
        public string ladleNo { get; set; }
        public string TagID { get; set; }
        public string SerialNo { get; set; }
        public string sourceLocation { get; set; }
        public int sourceLocationId { get; set; }
        public DateTime sourceINtime { get; set; }
        public DateTime sourceOUTtime { get; set; }
        public string weighmentLocation { get; set; }
        public DateTime weightmentDatetime { get; set; }
        public string destinationLocation { get; set; }
        public int destinationLocationId { get; set; }
        public DateTime destinationINtime { get; set; }
        public DateTime destinationOUTtime { get; set; }
    }
}
