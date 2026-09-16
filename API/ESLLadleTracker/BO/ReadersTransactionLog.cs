using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.BO
{
    public class ReadersTransactionLog
    {
        public Guid ID { get; set; }
        public long AssetSerialNo { get; set; }
        public string AssetDesc { get; set; }
        public string ReaderIP { get; set; }
        public int AntennaID { get; set; }
        public int RSSI { get; set; }
        public int LocationID { get; set; }
        public int TouchPointID { get; set; }
        public string TouchPointType { get; set; }
        public DateTime ServerDateTime { get; set; }
        public DateTime TransDateTime { get; set; }
        public string AssignedCastNo { get; set; }
        public DateTime CastAssignmentDateTime { get; set; }
        public int CastAssignmentLocationID { get; set; }
        public bool isPickedAlready { get; set; }
    }
}
