using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleAssignment
    {
        public string ID { get; set; }
        public string LadleNo { get; set; }
        public string AssignedProductionUit { get; set; }
        public int AssignedProductionUnitID { get; set; }
        public DateTime AssignedDateTime { get; set; }
        public int SourceLocationID { get; set; }
        public string SourceLocationName { get; set; }
        public int State { get; set; }
        public DateTime SourceInDateTime { get; set; }
        public string UID { get; set; }

    }
}
