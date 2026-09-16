using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLPath
    {
        public int LocationID { get; set; }
        public string LocationName { get; set; }
        public int INTouchPointID { get; set; }
        public int OUTTouchPointID { get; set; }
        //public double Weight { get; set; }
        //public DateTime WeighmentDateTime { get; set; }
        public DateTime INTransactionDateTime { get; set; }
        public DateTime OUTTransactionDateTime { get; set; }
        public string Direction { get; set; }
        public double InTemperature { get; set; }
        public double OutTemperature { get; set; }
        public ESLLadleWeightData WeightData { get; set; }
        public bool IsWeighment { get; set; }
        public TimeSpan TimeSpent { get; set; }
    }
}
