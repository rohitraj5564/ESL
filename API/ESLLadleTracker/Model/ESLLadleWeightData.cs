using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleWeightData
    {
        public string TransID { get; set; }
        public DateTime GrossDateTime { get; set; }
        public DateTime TareDateTime { get; set; }
        public string LadleNo { get; set; }
        public decimal GrossWeight { get; set; }
        public decimal TareWeight { get; set; }
        public decimal NetWeight { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string Sender { get; set; }
        public string CastNumber { get; set; }
        public string Receiver { get; set; }
        public DateTime Gross_DT_Time { get; set; }
        public string FID { get; set; }

        public List<ESLLadleWeightDataConsumption> ConsumptionData = null;
    }
}
