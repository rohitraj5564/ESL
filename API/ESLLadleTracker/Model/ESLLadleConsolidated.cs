using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleConsolidated
    {
        public int SerialNumber { get; set; } = 0;
        public int TXNo { get; set; } // TX Number 
        public string LadleNo { get; set; }
        
        public string SourceLocation { get; set; }
        private DateTime _sourceInDateTime = DateTime.MinValue;
        public DateTime SourceInDateTime
        {
            get
            {
                return _sourceInDateTime;
            }
            set
            {
                _sourceInDateTime = value;
                if (_sourceInDateTime.Year != 1)
                {
                    SourceInDateTimeSTR = _sourceInDateTime.ToString("yyyy-MM-dd H:mm:ss");
                    SourceINDateSTR = _sourceInDateTime.ToString("yyyy-MM-dd");
                    SourceINTimeSTR = _sourceInDateTime.ToString("H:mm:ss");
                }
                else
                {
                    SourceInDateTimeSTR = string.Empty;
                }
    
            }
        }

        public string SourceInDateTimeSTR { get; set; }

        public string SourceINDateSTR { get; set; }

        public string SourceINTimeSTR { get; set; }

        private DateTime _sourceOutDateTime = DateTime.MinValue;

        public DateTime SourceOutDateTime
        {
            get
            {
                return _sourceOutDateTime;
            }
            set
            {
                _sourceOutDateTime = value;
                if(_sourceOutDateTime.Year == 1)
                {
                    SourceOutDateTimeSTR = string.Empty;
                }
                else
                {
                    SourceOutDateTimeSTR = _sourceOutDateTime.ToString("yyyy-MM-dd H:mm:ss");

                    SourceOUTDateSTR = _sourceOutDateTime.ToString("yyyy-MM-dd");
                    SourceOUTTimeSTR = _sourceOutDateTime.ToString("H:mm:ss");
                }
            }
        }

        public string SourceOutDateTimeSTR { get; set; }

        public string SourceOUTDateSTR { get; set; }

        public string SourceOUTTimeSTR { get; set; }


        public string SourceWeight { get; set; }
        public string SourceWeightDateTime { get; set; }

        public string CastNo { get; set; }

        public string TareWeight { get; set; }
        public string TareWeightDateTime { get; set; }

        public string NetWeight { get; set; }

        public String TAT { get; set; }

        public TimeSpan TATValue { get; set; }

        public string DestinationLocation { get; set; }

        private DateTime _destinationInDateTime = DateTime.MinValue;
        public DateTime DestinationInDateTime
        {
            get
            {
                return _destinationInDateTime;
            }
            set
            {
                _destinationInDateTime = value;

                if (_destinationInDateTime.Year == 1)
                {
                    DestinationInDateTimeSTR = string.Empty;
                }
                else
                {
                    DestinationInDateTimeSTR = _destinationInDateTime.ToString("yyyy-MM-dd H:mm:ss");

                    DestinationINDateSTR = _destinationInDateTime.ToString("yyyy-MM-dd");
                    DestinationINTimeSTR = _destinationInDateTime.ToString("H:mm:ss");
                }
            }
        }

        public string DestinationInDateTimeSTR { get; set; }

        public string DestinationINDateSTR { get; set; }

        public string DestinationINTimeSTR { get; set; }

        
        private DateTime _destinationOutDateTime = DateTime.MinValue;
        public DateTime DestinationOutDateTime
        {
            get
            {
                return _destinationOutDateTime;
            }
            set
            {
                _destinationOutDateTime = value;

                if (_destinationOutDateTime.Year == 1)
                {
                    DestinationOutDateTimeSTR = string.Empty;
                }
                else
                {
                    DestinationOutDateTimeSTR = _destinationOutDateTime.ToString("yyyy-MM-dd H:mm:ss");
                    DestinationOUTDateSTR = _destinationOutDateTime.ToString("yyyy-MM-dd");
                    DestinationOUTTimeSTR = _destinationOutDateTime.ToString("H:mm:ss");
                }
            }
        }

        public string DestinationOutDateTimeSTR { get; set; }

        public string DestinationOUTDateSTR { get; set; }

        public string DestinationOUTTimeSTR { get; set; }


        public bool HasMultipleDestination { get; set; }

        public bool HasLIMSData { get; set; }

        private ESLLIMSData _limsData = null;

        public ESLLIMSData LIMSData
        {
            get
            {
                return _limsData;
            }
            set
            {
                _limsData = value;
            }
        }

        private DateTime _lrsInDateTime = DateTime.MinValue;
        public DateTime LRSInDateTime
        {
            get
            {
                return _lrsInDateTime;
            }
            set
            {
                _lrsInDateTime = value;
                if(_lrsInDateTime.Year ==  1)
                {
                    LRSInDateTimeSTR = string.Empty;
                }
                else
                {
                    LRSInDateTimeSTR = _lrsInDateTime.ToString("yyyy-MM-dd H:mm:ss");
                }
            }
        }

        public string LRSInDateTimeSTR { get; set; }

        private DateTime _lrsOutdateTime = DateTime.MinValue;

        public DateTime LRSOutDateTime
        {
            get
            {
                return _lrsOutdateTime;
            }
            set
            {
                _lrsOutdateTime = value;
                if(_lrsOutdateTime.Year == 1)
                {
                    LRSOutDateTimeSTR = string.Empty;
                }
                else
                {
                    LRSOutDateTimeSTR = _lrsOutdateTime.ToString("yyyy-MM-dd H:mm:ss");
                }
            }
        }

        public string LRSOutDateTimeSTR { get; set; }

        public int State { get; set; } //0 - Empty , 1 Loaded , 2 Partially Unloaded , 3 Completely Unloaded. Sample Data for 24134 Cast No
        public string DisplayText { get; set; }
        /*public DateTime LRSInTime { get; set; }
        public DateTime LRSOutTime { get; set; }*/

        public List<ESLLadleDestination> Destinations = new List<ESLLadleDestination>();
        public TimeSpan HoldingValue { get; set; }
        public string HoldingTimeValue { get; set; }
    }
}
