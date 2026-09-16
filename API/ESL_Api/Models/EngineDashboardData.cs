using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class EngineDashboardData
    {
        public List<ESLLocation> LocationData = new List<ESLLocation>();
        public List<ESLLocationConsolidate> ProductionSummary = new List<ESLLocationConsolidate>();
        public List<ESLLIMSData> LimsSummary = new List<ESLLIMSData>();
        public List<ESLLadle> UnusedLadles = new List<ESLLadle>();
        public int TotalActiveLadleCount = 0;
        public int TotalInUseLadleCount = 0;
        public int CompletedTrips = 0;
        public int PendingTrips = 0;
    }

    public class ESLLocation
    {
        public short LocationID { get; set; }
        public string LocationName { get; set; }
        public string LocationType { get; set; }
        public List<ESLLadle> LadleList = new List<ESLLadle>();
        public int TotalProduction { get; set; }
        public int LadleCount { get; set; } = 12;
        public List<ESLLocation> ProductionUnits = new List<ESLLocation>();
        public string AverageTATSTR = string.Empty;
        public string AverageHoldTimeSTR = string.Empty;
        public TimeSpan totalTAT;
        public int totalTATCount;
        public TimeSpan totalHoldingTime;
        public int totalHoldCount;
    }

    public class ESLLadle
    {
        public Guid ID { get; set; }
        public int TXNo { get; set; }
        public string LadleNo { get; set; }
        private string _name = string.Empty;
        public string Name
        {
            get
            { return _name; }
            set
            {
                _name = value;
                SetDisplayText();
            }
        }
        public string TagID { get; set; }
        public string SerialNo { get; set; }
        public double TareWeight { get; set; }
        public DateTime TareDateTime { get; set; }
        public double GrossWeight { get; set; }
        public string NetWeightSTR { get; set; }
        public DateTime GrossDateTime { get; set; }
        public double NetWeight { get; set; }
        public string CastNo { get; set; }
        public string Direction { get; set; } //IN,OUT,NA
        public int LastFurnaceID { get; set; } //This gets the last furnace ID for the Ladle from where it was loaded.
        public DateTime LastFurnaceLoadedDateTime { get; set; }
        public int LastLocationID { get; set; }
        public string LastLocationName { get; set; }
        private DateTime _lastLocationDateTime;
        public DateTime LastLocationDateTime
        {
            get
            {
                return _lastLocationDateTime;
            }
            set
            {
                _lastLocationDateTime = value;
                if (_lastLocationDateTime.Year != 1)
                {
                    LastLocationDateTimeSTR = _lastLocationDateTime.ToString("yyyy-MM-dd H:mm:ss");

                }
                else
                {
                    LastLocationDateTimeSTR = string.Empty;
                }

            }
        }
        public string LastLocationDateTimeSTR { get; set; }
        public int CurrentLocationID { get; set; }
        public int LastTouchpointID { get; set; }
        public int CurrentTouchpointID { get; set; }
        public DateTime InTime { get; set; }
        public DateTime OutTime { get; set; }
        private ESLLIMSData _limsData = null;
        public ESLLIMSData LIMSData
        {
            get
            { return _limsData; }
            set
            {
                _limsData = value;
                SetDisplayText();
            }
        }
        public string AcceptedLocationName { get; set; }
        public string AcceptedDateTime { get; set; }
        public List<ESLPath> Paths { get; set; } = new List<ESLPath>();
        public int State { get; set; } //0 - Empty , 1 Loaded , 2 Partially Unloaded , 3 Completely Unloaded. Sample Data for 24134 Cast No
        public string DisplayText { get; set; }
        public DateTime LRSInTime { get; set; }
        public DateTime LRSOutTime { get; set; }
        public DateTime CastNoQueryMinDateTime { get; set; }
        public int CastNoLocationQueryID { get; set; }
        public ESLPath GetPathForLocation(int locationID)
        {
            ESLPath retValue = null;
            foreach (ESLPath p in Paths)
            {
                if (p.LocationID == locationID)
                {
                    retValue = p;
                }
            }

            return retValue;
        }
        public TimeSpan TimeSpent { get; set; } = TimeSpan.MinValue;
        private TimeSpan _timeSpentObj = TimeSpan.MinValue;
        public TimeSpan TimeSpentObj
        {
            get
            {
                return _timeSpentObj;
            }
            set
            {
                _timeSpentObj = value;
                TimeSpent = _timeSpentObj;
                TimeSpentSTR = _timeSpentObj.ToString("d\\.hh\\:mm");
            }
        }
        public string TimeSpentSTR { get; set; } = "";
        public void SetDisplayText()
        {
            string retValue = string.Empty;
            if (this.Name != string.Empty)
            {
                retValue = retValue + this.Name;
            }

            if (this.LIMSData != null)
            {
                retValue = retValue + ",C=" + LIMSData.C + ",Cr=" + LIMSData.Cr + ",Mn=" + LIMSData.Mn + ",P=" + LIMSData.P + ",S=" + LIMSData.S + ",Si=" + LIMSData.Si + ",S_P=" + LIMSData.S_P + ",Ti=" + LIMSData.Ti;
            }

            DisplayText = retValue;

        }
        public bool IsActive { get; set; } = true;
        public bool FoundSourceToSource { get; set; } = false;
        public string PreviousLocation { get; set; }
    }

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
        {
            get
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

    public class ESLPath
    {
        public int LocationID { get; set; }
        public string LocationName { get; set; }
        public int INTouchPointID { get; set; }
        public int OUTTouchPointID { get; set; }
        public DateTime INTransactionDateTime { get; set; }
        public DateTime OUTTransactionDateTime { get; set; }
        public string Direction { get; set; }
        public double InTemperature { get; set; }
        public double OutTemperature { get; set; }
        public ESLLadleWeightData WeightData { get; set; }
        public bool IsWeighment { get; set; }
        public TimeSpan TimeSpent { get; set; }
    }

    public class ESLLadleWeightData
    {
        public string TransID { get; set; }
        public DateTime GrossDateTime { get; set; }
        public DateTime TareDateTime { get; set; }
        public string LadleNo { get; set; }
        public decimal GrossWeight { get; set; }
        public decimal TareWeight { get; set; }
        public decimal NetWeight { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string Sender { get; set; }
        public string CastNumber { get; set; }
        public string Receiver { get; set; }
        public DateTime Gross_DT_Time { get; set; }
        public string FID { get; set; }
        public List<ESLLadleWeightDataConsumption> ConsumptionData = null;
    }

    public class ESLLadleWeightDataConsumption
    {
        public string TransID { get; set; }
        public string ConsumptionID { get; set; }
        public string LadleNo { get; set; }
        public decimal LaddleNumber_SL { get; set; }
        public DateTime GrossDateTime { get; set; }
        public DateTime TareDateTime { get; set; }
        public decimal GrossWeight { get; set; }
        public decimal TareWeight { get; set; }
        public decimal NetWeight { get; set; }
        public DateTime TransactionDateTime { get; set; }
        public string Sender { get; set; }
        public string CastNumber { get; set; }
        public string Receiver { get; set; }
        public string FID { get; set; }
    }

    public class ESLLocationConsolidate
    {
        public short LocationID { get; set; }
        public string LocationName { get; set; }
        public decimal TotalProduction { get; set; }
        public int LadleCount { get; set; } = 0;
        public List<ESLLocationConsolidate> ProductionUnits = new List<ESLLocationConsolidate>();
    }
}