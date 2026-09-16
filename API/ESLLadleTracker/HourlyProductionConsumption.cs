using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker
{
    class HourlyProductionConsumption
    {
        public string HourSlot { get; set; }
        public string HourTimeSlot { get; set; }

        public decimal BF2Production { get; set; }
        public decimal BF3Production { get; set; }

        public decimal SMSConsumption { get; set; }
        public decimal PCMConsumption { get; set; }
        public decimal DIPConsumption { get; set; }
    }
}
