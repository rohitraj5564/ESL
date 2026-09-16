using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSL.Infinity.ESLLadleTracker.Model
{
    public class ESLLadle
    {
        public Guid ID { get; set; }
        public int TXNo { get; set; }
        public string LadleNo { get; set; }
        private string _name = string.Empty;
        public string Name
        { get
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
            foreach(ESLPath p in Paths)
            {
                if(p.LocationID == locationID)
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
            if(this.Name != string.Empty)
            {
                retValue = retValue + this.Name;
            }

            if(this.LIMSData != null)
            {
                retValue = retValue + ",C=" + LIMSData.C + ",Cr=" + LIMSData.Cr + ",Mn=" + LIMSData.Mn + ",P=" + LIMSData.P + ",S=" + LIMSData.S + ",Si=" + LIMSData.Si + ",S_P=" + LIMSData.S_P + ",Ti=" + LIMSData.Ti;  
            }

            DisplayText = retValue;
            
        }

        public bool IsActive { get; set; } = true;

        public bool FoundSourceToSource { get; set; } = false;

        public string PreviousLocation { get; set; }
    }
}
