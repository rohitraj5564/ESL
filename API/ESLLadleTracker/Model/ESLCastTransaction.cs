using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLCastTransaction
    {
        public Guid CTID { get; set; }
        public string CastNo { get; set; }
        public decimal Temperature { get; set; }
        public int LocationID { get; set; }
        public string LocationName { get; set; }
        public DateTime CreateDateTime { get; set; }
        public List<ESLLadle> LadleList = new List<ESLLadle>();
        public DateTime LIMSDateTime { get; set; }
    }
}
