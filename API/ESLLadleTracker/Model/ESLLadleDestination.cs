using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadleDestination
    {
        public String TAT { get; set; }

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
                if(_destinationOutDateTime.Year == 1)
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

        public string GrossWeight { get; set; }

        private DateTime _grossWeightDateTime = DateTime.MinValue;
        public DateTime GrossWeightDateTime
        {
            get
            {
                return _grossWeightDateTime;
            }
            set
            {
                _grossWeightDateTime = value;

                if (_grossWeightDateTime.Year == 1)
                {
                    GrossWeightDateTimeSTR = string.Empty;
                }
                else
                {
                    GrossWeightDateTimeSTR = _grossWeightDateTime.ToString("yyyy-MM-dd H:mm:ss");
                }
            }
        }
                

        public string GrossWeightDateTimeSTR { get; set; }

        public string TareWeight { get; set; }

        private DateTime _tareWeightDateTime = DateTime.MinValue;
        public DateTime TareWeightDateTime
        {
            get
            {
                return _tareWeightDateTime;
            }
            set
            {
                _tareWeightDateTime = value;
                if (_tareWeightDateTime.Year == 1)
                {
                    TareWeightDateTimeSTR = string.Empty;
                }
                else
                {
                    TareWeightDateTimeSTR = _tareWeightDateTime.ToString("yyyy-MM-dd H:mm:ss");
                }
            }
        }

        public string TareWeightDateTimeSTR { get; set; }


    }
}
