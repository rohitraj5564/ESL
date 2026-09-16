using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLIMSData
    {
        public string SampleDate { get; set; }
        public string Shift { get; set; }
        public string SampleTime { get; set; }
        public string SampleName { get; set; }
        public string CastNo { get; set; }
        public string CastingTime { get; set; }
        public string ClosingTime { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string LadleNumbers { get; set; }
        public int LadleCount { get; set; }
        public string C { get; set; }
        public string Si { get; set; }
        public string Mn { get; set; }
        public string S { get; set; }
        public string P { get; set; }
        public string Ti { get; set; }
        public string Cr { get; set; }
        public string S_P { get; set; }
        public string PigIron_Grade { get; set; }
        public string SampleBy { get; set; }
        public string Analyst { get; set; }
        public string EnteredBy { get; set; }
        private DateTime _sampleDatetime = DateTime.MinValue;
        public DateTime SampleDateTime
        {
            get
            {
                return _sampleDatetime;
            }
            set
            {
                _sampleDatetime = value;
                SampleDateTimeSTR = _sampleDatetime.ToString("hh:mm:ss");
            }
        }
        public string SampleDateTimeSTR { get; set; }
        private DateTime _castingDateTime = DateTime.MinValue;
        public DateTime CastingDateTime
        { get
            {
                return _castingDateTime;
            }
            set
            {
                _castingDateTime = value;
                CastingDateTimeSTR = _castingDateTime.ToString("hh:mm:ss");
            }
        }
        public string CastingDateTimeSTR { get; set; }
        public string NewCastNumber { get; set; }
        public string LocationName { get; set; }
        
    }
}
