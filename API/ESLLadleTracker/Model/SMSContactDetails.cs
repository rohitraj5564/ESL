using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class SMSContactDetails
    {
        public Guid UserID { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string MobileNo { get; set; }
        public DateTime LastSMSDateTime { get; set; }
        public bool SMSAccess { get; set; }
        public bool AllLocationSMSAccess { get; set; }
        public string Department { get; set; }
        public int LocationId { get; set; }
        public bool IsActive { get; set; }
        public DateTime TransactionDateTime { get; set; }

    }
}
