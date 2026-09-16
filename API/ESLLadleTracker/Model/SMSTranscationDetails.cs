using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class SMSTranscationDetails
    {
        public Guid TranscationId { get; set; }
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string Department { get; set; }
        public int LocationId { get; set; }
        public string LadleNo { get; set; }
        public string IdleTime { get; set; }
        public DateTime SMSDateTime { get; set; }
        public DateTime TransactionDateTime { get; set; }
    }
}
