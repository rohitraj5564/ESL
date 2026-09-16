using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using PSL.Infinity.ESLLadleTracker.Model;
using PSL.Infinity.ESLLadleTracker.BO;

using System.Text;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using PSL.Infinity.Utils;

namespace PSL.Infinity.ESLLadleTracker
{
    public class Engine
    {
        #region Constructor

        public Engine(string ladleDBConnectionSting, string limsDBConnectionString, string wbDataConnectionString)
        {

            _connectionString = ladleDBConnectionSting;
            _limsConnectionString = limsDBConnectionString;
            _wbDataConnectionString = wbDataConnectionString;


            _locations = GetAllLocations();
            _ladles = GetAllLadles();
        }

        #endregion

        #region Private Members
        private string _connectionString = string.Empty;
        private string _limsConnectionString = string.Empty;
        private string _wbDataConnectionString = string.Empty;

        private string _connectionStringLDB = string.Empty;

        private List<ESLLocation> _locations = new List<ESLLocation>();
        private List<ESLLadle> _ladles = new List<ESLLadle>();
        private DateTime Efromdate;
        private DateTime Etodate;
        List<TranscationLadledetails> transcationLadledetails = null;

        #endregion

        #region Public Methods
        /// <summary>
        /// PROD This Method Returs the Current Location with the List of Ladles at each Location.
        /// Used for the Initial Dasboard which has to be created.
        /// </summary>
        /// <returns></returns>
        public ESLDashboardSummary GetLocationsOnlineDataOld()
        {
            ESLDashboardSummary dashboardSummary = new ESLDashboardSummary();
            List<ESLLocation> retValue = null;
            string valusee = string.Empty;//del
            string valusee1 = string.Empty;///del
            Dictionary<string, List<string>> castNumberDictionary = new Dictionary<string, List<string>>();

            try
            {
                List<ESLLadle> activeLadles = GetAllLadles();

                dashboardSummary.TotalActiveLadleCount = activeLadles.Count;

                retValue = GetAllLocations();

                //Get the Ladles moved in the last 24 hrs...
                List<ESLLadle> activeMovingLadles = GetAllLadleObjectInUse(DateTime.Now.Subtract(TimeSpan.FromDays(1)), DateTime.Now);


                //DateTime dateTime1 = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                //DateTime dateTime2 = DateTime.Now;

                dashboardSummary.TotalInUseLadleCount = activeMovingLadles.Count;


                foreach (ESLLadle l in activeMovingLadles)
                {
                    long serialNo = Int64.Parse(l.SerialNo);

                    List<ReadersTransactionLog> trackPoints = GetReaderTrsactionLogForAsset(serialNo);



                    ResolveLastValidLocationAndDirection(l, trackPoints);
                    //ResolveCurrentLadlePath(l);
                    if (l.CurrentLocationID != 0)
                    {
                        foreach (ESLLocation lc in retValue)
                        {
                            if (lc.LocationID == l.CurrentLocationID)
                            {
                                if (DateTime.Now.Subtract(l.LastLocationDateTime).TotalDays < 10) //Added Logic to Show only for current scope ladle within 2 days...
                                {
                                    lc.LadleList.Add(l);
                                    l.IsActive = true;
                                }
                                else
                                {
                                    l.IsActive = false;
                                }
                                //else
                                //{
                                //  int j = 500;
                                //}
                                break;
                            }
                        }
                    }

                    //int x = 10;


                }



                foreach (ESLLadle ll in activeMovingLadles)
                {
                    ESLLadleWeightData lwd = null;
                    try
                    {
                        if (ll.CastNoLocationQueryID != 0)
                        {
                            lwd = GetLadleWeighmentData(ll.LadleNo, ll.CastNoQueryMinDateTime);

                        }
                        else
                        {
                            lwd = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        lwd = null;
                    }

                    if (lwd != null && lwd.CastNumber != string.Empty)
                    {
                        //Fetch LiMS data..
                        //lc.HasLIMSData = true;
                        ll.CastNo = lwd.CastNumber;

                        ll.NetWeightSTR = lwd.NetWeight.ToString("00.00");
                        if (castNumberDictionary.ContainsKey(ll.CastNoLocationQueryID.ToString()))
                        {
                            List<string> casts = castNumberDictionary[ll.CastNoLocationQueryID.ToString()];
                            if (!casts.Contains(lwd.CastNumber))
                            {
                                casts.Add(lwd.CastNumber);
                            }
                        }
                        else
                        {
                            List<string> casts = new List<string>();
                            casts.Add(lwd.CastNumber);
                            castNumberDictionary.Add(ll.CastNoLocationQueryID.ToString(), casts);
                        }

                        /*ESLLIMSData liData = GetLIMSData(ll.CastNoLocationQueryID, lwd.CastNumber);
                        if (liData != null)
                        {
                            ll.LIMSData = liData;
                            //ll.HasLIMSData = true;
                        }*/
                    }
                    else
                    {
                        int nullWeight = 0;
                    }

                }

                //return locations;

                List<ESLLIMSData> totalLimsData = new List<ESLLIMSData>();
                //List<ESLLadle> totalladles = new List<ESLLadle>();


                foreach (string castNoLocation in castNumberDictionary.Keys)
                {

                    List<ESLLIMSData> currentLocationLimsData = null;
                    List<string> bf2CastNos = castNumberDictionary[castNoLocation];
                    currentLocationLimsData = GetLIMSData(Int32.Parse(castNoLocation), bf2CastNos);
                    if (currentLocationLimsData != null)
                        totalLimsData.AddRange(currentLocationLimsData);
                }

                foreach (ESLLadle l in activeMovingLadles)
                {
                    l.TimeSpentObj = DateTime.Now.Subtract(l.InTime);
                    if (l.LadleNo.Contains("34"))
                    {
                        int o = 100;
                    }
                    if (totalLimsData != null)
                    {
                        //if(totalLimsData.Exists(x => x.NewCastNumber == l.CastNo))
                        //{
                        ESLLIMSData limsData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo);
                        if (limsData != null)
                        {
                            l.LIMSData = limsData;
                        }
                        else
                        {
                            limsData = totalLimsData.Find(x => "0" + x.NewCastNumber == l.CastNo);
                            if (limsData != null)
                            {
                                l.LIMSData = limsData;
                            }
                        }
                        //l.LIMSData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo);

                        //totalladles.Add(l);
                        //}
                        //else
                        //{
                        //Not Found ....
                        int kk = 0;
                        //}

                    }

                }


                //List<ESLLIMSData> totalLimsData1 = new List<ESLLIMSData>();

                //foreach (string castNoLocation in castNumberDictionary.Keys)
                //{

                //    List<ESLLIMSData> currentLocationLimsData = null;
                //    List<string> bf3CastNos = castNumberDictionary[castNoLocation];
                //    currentLocationLimsData = GetLIMSData(Int32.Parse(castNoLocation), bf3CastNos);
                //    if (currentLocationLimsData != null)
                //        totalLimsData.AddRange(currentLocationLimsData);
                //}

                //foreach (ESLLadle l in activeMovingLadles)
                //{
                //    l.TimeSpentObj = DateTime.Now.Subtract(l.InTime);
                //    if (totalLimsData != null)
                //    {
                //        if (totalLimsData.Exists(x => x.NewCastNumber == l.CastNo))
                //        {
                //            l.LIMSData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo);
                //        }
                //        else
                //        {
                //            //Not Found ....
                //            int kk = 0;
                //        }

                //    }

                //}
                dashboardSummary.LocationData = retValue;

                //Ordering of the Ladle in Decending order of timespan.

                foreach (ESLLocation loc in retValue)
                {
                    var ladleComparer = new ESLLadleCompare();
                    loc.LadleList.Sort(ladleComparer);
                    loc.LadleList.Reverse();
                }

                DateTime todateTime = DateTime.Now;
                DateTime fromDateTime;
                if (todateTime.Hour >= 22)
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(1));
                }

                fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                ESLLadleTransactionSummary sum = GetTransactionSummary(todateTime, todateTime);
                dashboardSummary.LimsSummary = totalLimsData;

                //valusee= sum.LimsSummary.Count.ToString();


                int totalTrips = sum.LadleTransactionDetails.Count;

                int completedTrips = 0;
                int pendingTrips = 0;

                Dictionary<string, ESLLocation> talCalculationDictionary = new Dictionary<string, ESLLocation>();

                foreach (ESLLadleConsolidated con in sum.LadleTransactionDetails)
                {
                    if (!string.IsNullOrEmpty(con.DestinationLocation))
                    {
                        completedTrips++;
                        if (talCalculationDictionary.ContainsKey(con.DestinationLocation))
                        {
                            ESLLocation loc = talCalculationDictionary[con.DestinationLocation];
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }
                        }
                        else
                        {
                            ESLLocation loc = new ESLLocation();
                            loc.LocationName = con.DestinationLocation;
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }

                            talCalculationDictionary.Add(con.DestinationLocation, loc);
                        }

                    }
                    else
                    {
                        pendingTrips++;
                    }
                }

                dashboardSummary.CompletedTrips = completedTrips;
                dashboardSummary.PendingTrips = pendingTrips;

                //int jack = 500;


                if (sum.ProductionSummary != null && sum.ProductionSummary.Count == 0)
                {
                    dashboardSummary.LimsSummary.Clear();
                }

                dashboardSummary.ProductionSummary = sum.ProductionSummary;

                //Get Unsued ladle Information...

                foreach (ESLLadle ldl in activeLadles)
                {
                    if (!activeMovingLadles.Exists(x => x.LadleNo == ldl.LadleNo))
                    {
                        //Get Ladles Last Location from the system and add to the Unused Collection...
                        //ldl.LastLocationID
                        DateTime transDateTime;
                        string LocationName = string.Empty;
                        if (GetLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName))
                        {
                            if (LocationName != string.Empty)
                            {
                                ldl.LastLocationDateTime = transDateTime;
                                ldl.LastLocationName = LocationName;
                                ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                            }

                        }
                        dashboardSummary.UnusedLadles.Add(ldl);
                    }
                    else
                    {
                        ESLLadle fl = activeMovingLadles.Find(x => x.LadleNo == ldl.LadleNo);

                        if (!FindLadleInOnlineLocations(dashboardSummary.LocationData, ldl.LadleNo))
                        {
                            int j = 9000;
                            string LocationName = string.Empty;
                            DateTime transDateTime;
                            GetLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName);
                            if (LocationName != string.Empty)
                            {
                                ldl.LastLocationDateTime = transDateTime;
                                ldl.LastLocationName = LocationName;
                                ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                                //loc.LadleList.Add(ldl);
                                ESLLocation loc = dashboardSummary.LocationData.Find(x => x.LocationName == LocationName);
                                if (loc != null)
                                {
                                    loc.LadleList.Add(ldl);
                                    var ladleComparer = new ESLLadleCompare();
                                    loc.LadleList.Sort(ladleComparer);
                                    //loc.LadleList.Reverse();
                                }
                            }




                        }

                        if (fl != null)
                        {
                            if (fl.CastNoQueryMinDateTime.Year != 1)
                            {
                                ESLLadleAssignment la = GetLadleAssignment(fl.LadleNo, fl.CastNoQueryMinDateTime);

                                if (la != null)
                                {
                                    fl.AcceptedLocationName = la.AssignedProductionUit;
                                    fl.State = 4;
                                }
                            }
                        }
                    }
                }



                foreach (ESLLocation fl in dashboardSummary.LocationData)
                {
                    if (talCalculationDictionary.ContainsKey(fl.LocationName))
                    {

                        ESLLocation r1 = talCalculationDictionary[fl.LocationName];
                        //valusee = r1.LadleCount.ToString();
                        //valusee1 = r1.totalTATCount.ToString();
                        fl.AverageTATSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Hours, TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Minutes);
                        fl.AverageHoldTimeSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Hours, TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Minutes);
                        //fl.AverageTATSTR = TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).ToString("d\\.hh\\:mm");
                        //fl.AverageHoldTimeSTR = TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).ToString("d\\.hh\\:mm");

                    }
                }


                //Logic For InTransit Box
                #region InTransit
                TimeSpan totaltimewaited = new TimeSpan();
                DateTime invaliddatetime = DateTime.MinValue;
                ESLLocation Intranslocation = new ESLLocation();
                Intranslocation.LocationName = "InTransit";
                dashboardSummary.LocationData.Add(Intranslocation);
                //dashboardSummary.LocationData[dashboardSummary.LocationData.Count + 1].LocationName = "InTransit";



                for (int i = 0; i < dashboardSummary.LocationData.Count; i++)
                {
                    if (dashboardSummary.LocationData[i].LocationID == 3)
                    {
                        for (int h = 0; h < dashboardSummary.LocationData[i].LadleList.Count; h++)
                        {
                            string LadleNo = dashboardSummary.LocationData[i].LadleList[h].LadleNo;
                            int Currentlocation = dashboardSummary.LocationData[i].LocationID;
                            dashboardSummary.LocationData[i].LadleList[h].PreviousLocation = GetLadlePreviousLocation(LadleNo, Currentlocation);

                            if (dashboardSummary.LocationData[i].LadleList[h].OutTime == invaliddatetime)
                            {
                                totaltimewaited = DateTime.Now.Subtract(dashboardSummary.LocationData[i].LadleList[h].InTime);
                            }
                            else
                            {

                                totaltimewaited = DateTime.Now.Subtract(dashboardSummary.LocationData[i].LadleList[h].OutTime);
                            }

                            if (totaltimewaited.TotalMinutes > 1)
                            {

                                if (dashboardSummary.LocationData[i].LadleList.Count != 0)
                                {
                                    dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleList.Add(dashboardSummary.LocationData[i].LadleList[h]);
                                    dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleCount = dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleList.Count;

                                }
                                dashboardSummary.LocationData[i].LadleList.RemoveAt(h);
                                dashboardSummary.LocationData[i].LadleCount = dashboardSummary.LocationData[i].LadleList.Count;
                                h--;
                            }


                        }
                    }
                }
                #endregion

                #region SMS Service
                List<SMSContactDetails> UserContactDetails = new List<SMSContactDetails>();
                UserContactDetails = GetDepartmentContactDetails();
                //for()
                #endregion

                return dashboardSummary;


            }
            catch (Exception ex)
            {
                throw new Exception(valusee, ex);
                string msg = ex.Message + ex.InnerException.StackTrace + ex.StackTrace;

                retValue = null;
            }

            /*(if(dashboardSummary.LocationData != null && dashboardSummary.LocationData.Count > 0)
            {
                if(dashboardSummary.LocationData[0].LadleList != null && dashboardSummary.LocationData[0].LadleList.Count > 0)
                {
                    dashboardSummary.LocationData[0].LadleList[0].TimeSpent = new TimeSpan(2, 2, 3);
                }
                if (dashboardSummary.LocationData[0].LadleList != null && dashboardSummary.LocationData[0].LadleList.Count > 1)
                {
                    dashboardSummary.LocationData[0].LadleList[1].TimeSpent = new TimeSpan(2, 2, 3);
                }
            }*/

            return dashboardSummary;
        }



        /// <summary>
        /// PROD This Method Returs the Current Location with the List of Ladles at each Location.
        /// Used for the Initial Dasboard which has to be created.
        /// </summary>
        /// <returns></returns>
        public ESLDashboardSummary GetLocationsOnlineData()
        {
            ESLDashboardSummary dashboardSummary = new ESLDashboardSummary();
            List<ESLLocation> retValue = null;
            string valusee = string.Empty;//del
            string valusee1 = string.Empty;///del
            Dictionary<string, List<string>> castNumberDictionary = new Dictionary<string, List<string>>();

            try
            {
                List<ESLLadle> activeLadles = GetAllLadles();

                dashboardSummary.TotalActiveLadleCount = activeLadles.Count;

                retValue = GetAllLocations();

                //Get the Ladles moved in the last 24 hrs...
                List<ESLLadle> activeMovingLadles = GetAllLadleObjectInUse(DateTime.Now.Subtract(TimeSpan.FromDays(1)), DateTime.Now);


                //DateTime dateTime1 = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                //DateTime dateTime2 = DateTime.Now;

                dashboardSummary.TotalInUseLadleCount = activeMovingLadles.Count;

                //This section gets the last location of the Ladle to show in the dashboard....
                foreach (ESLLadle l in activeMovingLadles)
                {
                    long serialNo = Int64.Parse(l.SerialNo);

                    List<ReadersTransactionLog> trackPoints = GetReaderTrsactionLogForAsset(serialNo);



                    ResolveLastValidLocationAndDirection(l, trackPoints);
                    //ResolveCurrentLadlePath(l);
                    if (l.CurrentLocationID != 0)
                    {
                        foreach (ESLLocation lc in retValue)
                        {
                            if (lc.LocationID == l.CurrentLocationID)
                            {
                                if (DateTime.Now.Subtract(l.LastLocationDateTime).TotalDays < 10) //Added Logic to Show only for current scope ladle within 2 days...
                                {
                                    lc.LadleList.Add(l);
                                    l.IsActive = true;
                                }
                                else
                                {
                                    l.IsActive = false;
                                }
                                //else
                                //{
                                //  int j = 500;
                                //}
                                break;
                            }
                        }
                    }

                    //int x = 10;


                }



                foreach (ESLLadle ll in activeMovingLadles)
                {
                    ESLLadleWeightData lwd = null;
                    try
                    {
                        if (ll.CastNoLocationQueryID != 0)
                        {
                            if (ll.LadleNo.Contains("30"))
                            {
                                int y = 40;
                            }
                            lwd = GetLadleWeighmentData(ll.LadleNo, ll.CastNoQueryMinDateTime);

                        }
                        else
                        {
                            lwd = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        lwd = null;
                    }

                    if (lwd != null && lwd.CastNumber != string.Empty)
                    {
                        //Fetch LiMS data..
                        //lc.HasLIMSData = true;
                        ll.CastNo = lwd.CastNumber;

                        ll.NetWeightSTR = lwd.NetWeight.ToString("00.00");
                        if (castNumberDictionary.ContainsKey(ll.CastNoLocationQueryID.ToString()))
                        {
                            List<string> casts = castNumberDictionary[ll.CastNoLocationQueryID.ToString()];
                            if (!casts.Contains(lwd.CastNumber))
                            {
                                casts.Add(lwd.CastNumber);
                            }
                        }
                        else
                        {
                            List<string> casts = new List<string>();
                            casts.Add(lwd.CastNumber);
                            castNumberDictionary.Add(ll.CastNoLocationQueryID.ToString(), casts);
                        }

                        /*ESLLIMSData liData = GetLIMSData(ll.CastNoLocationQueryID, lwd.CastNumber);
                        if (liData != null)
                        {
                            ll.LIMSData = liData;
                            //ll.HasLIMSData = true;
                        }*/
                    }
                    else
                    {
                        int nullWeight = 0;
                    }

                }

                //return locations;

                List<ESLLIMSData> totalLimsData = new List<ESLLIMSData>();
                //List<ESLLadle> totalladles = new List<ESLLadle>();


                foreach (string castNoLocation in castNumberDictionary.Keys)
                {

                    List<ESLLIMSData> currentLocationLimsData = null;
                    List<string> bf2CastNos = castNumberDictionary[castNoLocation];
                    currentLocationLimsData = GetLIMSData(Int32.Parse(castNoLocation), bf2CastNos);
                    if (currentLocationLimsData != null)
                        totalLimsData.AddRange(currentLocationLimsData);
                }

                foreach (ESLLadle l in activeMovingLadles)
                {
                    l.TimeSpentObj = DateTime.Now.Subtract(l.InTime);
                    if (l.LadleNo.Contains("30"))
                    {
                        int o = 100;
                    }
                    if (totalLimsData != null)
                    {
                        //if(totalLimsData.Exists(x => x.NewCastNumber == l.CastNo))
                        //{
                        ESLLIMSData limsData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo && x.LadleNumbers.Contains(l.LadleNo));
                        if (limsData != null)
                        {
                            if (l.LadleNo.Contains("30"))
                            {
                                int tth = 90;
                            }
                            l.LIMSData = limsData;
                        }
                        else
                        {
                            limsData = totalLimsData.Find(x => "0" + x.NewCastNumber == l.CastNo);
                            if (limsData != null)
                            {
                                if (l.LadleNo.Contains("30"))
                                {
                                    int th = 90;
                                }
                                l.LIMSData = limsData;
                            }
                        }
                        //l.LIMSData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo);

                        //totalladles.Add(l);
                        //}
                        //else
                        //{
                        //Not Found ....
                        int kk = 0;
                        //}

                    }

                }


                //List<ESLLIMSData> totalLimsData1 = new List<ESLLIMSData>();

                //foreach (string castNoLocation in castNumberDictionary.Keys)
                //{

                //    List<ESLLIMSData> currentLocationLimsData = null;
                //    List<string> bf3CastNos = castNumberDictionary[castNoLocation];
                //    currentLocationLimsData = GetLIMSData(Int32.Parse(castNoLocation), bf3CastNos);
                //    if (currentLocationLimsData != null)
                //        totalLimsData.AddRange(currentLocationLimsData);
                //}

                //foreach (ESLLadle l in activeMovingLadles)
                //{
                //    l.TimeSpentObj = DateTime.Now.Subtract(l.InTime);
                //    if (totalLimsData != null)
                //    {
                //        if (totalLimsData.Exists(x => x.NewCastNumber == l.CastNo))
                //        {
                //            l.LIMSData = totalLimsData.Find(x => x.NewCastNumber == l.CastNo);
                //        }
                //        else
                //        {
                //            //Not Found ....
                //            int kk = 0;
                //        }

                //    }

                //}
                dashboardSummary.LocationData = retValue;

                //Ordering of the Ladle in Decending order of timespan.

                foreach (ESLLocation loc in retValue)
                {
                    var ladleComparer = new ESLLadleCompare();
                    loc.LadleList.Sort(ladleComparer);
                    loc.LadleList.Reverse();
                }

                DateTime todateTime = DateTime.Now;
                DateTime fromDateTime;
                if (todateTime.Hour >= 22)
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(1));
                }

                fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                ESLLadleTransactionSummary sum = GetTransactionSummary(todateTime, todateTime);
                dashboardSummary.LimsSummary = totalLimsData;

                //valusee= sum.LimsSummary.Count.ToString();


                int totalTrips = sum.LadleTransactionDetails.Count;

                int completedTrips = 0;
                int pendingTrips = 0;

                Dictionary<string, ESLLocation> talCalculationDictionary = new Dictionary<string, ESLLocation>();

                foreach (ESLLadleConsolidated con in sum.LadleTransactionDetails)
                {
                    if (!string.IsNullOrEmpty(con.DestinationLocation))
                    {
                        completedTrips++;
                        if (talCalculationDictionary.ContainsKey(con.DestinationLocation))
                        {
                            ESLLocation loc = talCalculationDictionary[con.DestinationLocation];
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }
                        }
                        else
                        {
                            ESLLocation loc = new ESLLocation();
                            loc.LocationName = con.DestinationLocation;
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }

                            talCalculationDictionary.Add(con.DestinationLocation, loc);
                        }

                    }
                    else
                    {
                        pendingTrips++;
                    }
                }

                dashboardSummary.CompletedTrips = completedTrips;
                dashboardSummary.PendingTrips = pendingTrips;

                //int jack = 500;


                if (sum.ProductionSummary != null && sum.ProductionSummary.Count == 0)
                {
                    dashboardSummary.LimsSummary.Clear();
                }

                dashboardSummary.ProductionSummary = sum.ProductionSummary;

                //Get Unsued ladle Information...

                foreach (ESLLadle ldl in activeLadles)
                {
                    if (!activeMovingLadles.Exists(x => x.LadleNo == ldl.LadleNo))
                    {
                        //Get Ladles Last Location from the system and add to the Unused Collection...
                        //ldl.LastLocationID
                        DateTime transDateTime;
                        string LocationName = string.Empty;
                        if (GetLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName))
                        {
                            if (LocationName != string.Empty)
                            {
                                ldl.LastLocationDateTime = transDateTime;
                                ldl.LastLocationName = LocationName;
                                ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                            }

                        }
                        dashboardSummary.UnusedLadles.Add(ldl);
                    }
                    else
                    {
                        ESLLadle fl = activeMovingLadles.Find(x => x.LadleNo == ldl.LadleNo);

                        if (!FindLadleInOnlineLocations(dashboardSummary.LocationData, ldl.LadleNo))
                        {
                            int j = 9000;
                            string LocationName = string.Empty;
                            DateTime transDateTime;
                            GetLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName);
                            if (LocationName != string.Empty)
                            {
                                ldl.LastLocationDateTime = transDateTime;
                                ldl.LastLocationName = LocationName;
                                ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                                //loc.LadleList.Add(ldl);
                                ESLLocation loc = dashboardSummary.LocationData.Find(x => x.LocationName == LocationName);
                                if (loc != null)
                                {
                                    loc.LadleList.Add(ldl);
                                    var ladleComparer = new ESLLadleCompare();
                                    loc.LadleList.Sort(ladleComparer);
                                    //loc.LadleList.Reverse();
                                }
                            }




                        }

                        if (fl != null)
                        {
                            if (fl.CastNoQueryMinDateTime.Year != 1)
                            {
                                ESLLadleAssignment la = GetLadleAssignment(fl.LadleNo, fl.CastNoQueryMinDateTime);

                                if (la != null)
                                {
                                    fl.AcceptedLocationName = la.AssignedProductionUit;
                                    fl.State = 4;
                                }
                            }
                        }
                    }
                }

                #region //new method to calculate lrs tat
                //transcationLadledetails = destinationlocationCalculation();

                //if (transcationLadledetails!=null && transcationLadledetails.Count!=0)
                //{
                //    ESLLocation loc = new ESLLocation();
                //    loc.LocationName ="LRS";
                //    foreach (var lrs in transcationLadledetails)
                //    {
                //        if (lrs.destinationLocation == loc.LocationName)
                //        {
                //            if (lrs.destinationOUTtime.Subtract(lrs.destinationINtime).TotalMinutes > 15)
                //            {
                //                //Above consition checks for total duration more than 15 mins.....
                //                loc.totalHoldingTime = loc.totalHoldingTime + lrs.destinationOUTtime.Subtract(lrs.destinationINtime);
                //                loc.totalHoldCount++;
                //            }
                //            TimeSpan TATValue = lrs.destinationINtime.Subtract(lrs.sourceINtime);
                //            if (TATValue.TotalMinutes > 30)
                //            {
                //                loc.totalTAT = loc.totalTAT + TATValue;
                //                loc.totalTATCount++;
                //            }
                //        }
                //    }

                //    talCalculationDictionary.Add(loc.LocationName, loc);
                //}


                //if(!talCalculationDictionary.ContainsKey("PCM"))
                //{
                //    if (transcationLadledetails != null && transcationLadledetails.Count != 0)
                //    {
                //        ESLLocation loc = new ESLLocation();
                //        loc.LocationName = "PCM";
                //        foreach (var lrs in transcationLadledetails)
                //        {
                //            if (lrs.destinationLocation == loc.LocationName)
                //            {
                //                if (lrs.destinationOUTtime.Subtract(lrs.destinationINtime).TotalMinutes > 15)
                //                {
                //                    //Above consition checks for total duration more than 15 mins.....
                //                    loc.totalHoldingTime = loc.totalHoldingTime + lrs.destinationOUTtime.Subtract(lrs.destinationINtime);
                //                    loc.totalHoldCount++;
                //                }
                //                TimeSpan TATValue = lrs.destinationINtime.Subtract(lrs.sourceINtime);
                //                if (TATValue.TotalMinutes > 30)
                //                {
                //                    loc.totalTAT = loc.totalTAT + TATValue;
                //                    loc.totalTATCount++;
                //                }
                //            }
                //        }

                //        talCalculationDictionary.Add(loc.LocationName, loc);
                //    }

                //}

                #endregion//method end


                foreach (ESLLocation fl in dashboardSummary.LocationData)
                {
                    if (talCalculationDictionary.ContainsKey(fl.LocationName))
                    {
                        //New Added for Location TAT 
                        ESLLocationData l1 = GetLocationTATData(fl.LocationName);
                        //End
                        fl.AverageTATSTR = l1.AverageTATSTR;
                        fl.AverageHoldTimeSTR = l1.AverageHoldTimeSTR;

                        //ESLLocation r1 = talCalculationDictionary[fl.LocationName];

                        //if (r1.totalTATCount != 0)
                        //{
                        //    fl.AverageTATSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Hours, TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Minutes);
                        //}

                        //if (r1.totalHoldCount != 0)
                        //{
                        //    //valusee = r1.LadleCount.ToString();
                        //    //valusee1 = r1.totalTATCount.ToString();

                        //    fl.AverageHoldTimeSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Hours, TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Minutes);
                        //    //fl.AverageTATSTR = TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).ToString("d\\.hh\\:mm");
                        //    //fl.AverageHoldTimeSTR = TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).ToString("d\\.hh\\:mm");
                        //}
                    }
                }


                //Logic For InTransit Box
                #region InTransit
                TimeSpan totaltimewaited = new TimeSpan();
                DateTime invaliddatetime = DateTime.MinValue;
                ESLLocation Intranslocation = new ESLLocation();
                Intranslocation.LocationName = "InTransit";
                dashboardSummary.LocationData.Add(Intranslocation);
                //dashboardSummary.LocationData[dashboardSummary.LocationData.Count + 1].LocationName = "InTransit";



                for (int i = 0; i < dashboardSummary.LocationData.Count; i++)
                {
                    if (dashboardSummary.LocationData[i].LocationID == 3)
                    {
                        for (int h = 0; h < dashboardSummary.LocationData[i].LadleList.Count; h++)
                        {

                            string LadleNo = dashboardSummary.LocationData[i].LadleList[h].LadleNo;
                            int Currentlocation = dashboardSummary.LocationData[i].LocationID;
                            dashboardSummary.LocationData[i].LadleList[h].PreviousLocation = GetLadlePreviousLocation(LadleNo, Currentlocation);

                            if (dashboardSummary.LocationData[i].LadleList[h].OutTime == invaliddatetime)
                            {
                                totaltimewaited = DateTime.Now.Subtract(dashboardSummary.LocationData[i].LadleList[h].InTime);
                            }
                            else
                            {
                                totaltimewaited = DateTime.Now.Subtract(dashboardSummary.LocationData[i].LadleList[h].OutTime);
                            }

                            if (totaltimewaited.TotalMinutes > 1)
                            {

                                if (dashboardSummary.LocationData[i].LadleList.Count != 0)
                                {
                                    dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleList.Add(dashboardSummary.LocationData[i].LadleList[h]);
                                    dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleCount = dashboardSummary.LocationData[dashboardSummary.LocationData.Count - 1].LadleList.Count;

                                }
                                dashboardSummary.LocationData[i].LadleList.RemoveAt(h);
                                dashboardSummary.LocationData[i].LadleCount = dashboardSummary.LocationData[i].LadleList.Count;
                                h--;
                            }


                        }
                    }
                }
                #endregion

                ////Code For SMS Service
                #region SMS Service
                //List<SMSContactDetails> UserContactDetails = new List<SMSContactDetails>();
                //string MsgLadleNo = string.Empty;
                //double TimeSpentOnLocation = 0;
                //int CurrentLadlelocation = 0;
                //string CurrentLadleLocationName = string.Empty;
                //string FromDate = string.Empty;
                //string TotalTimeSpent = string.Empty;
                //DateTime CurrentDateTime = DateTime.Now;
                //Guid perviousUserGuid = Guid.NewGuid();

                //List<SMSContactDetails> UpdateMsgUserDateTimeList = new List<SMSContactDetails>();

                //UserContactDetails = GetDepartmentContactDetails();

                //for (int s = 0; s < dashboardSummary.LocationData.Count; s++)
                //{
                //    bool SentMsg = false;
                //    for (int s1 = 0; s1 < dashboardSummary.LocationData[s].LadleList.Count; s1++)
                //    {
                //        MsgLadleNo = dashboardSummary.LocationData[s].LadleList[s1].Name;
                //        CurrentLadlelocation = dashboardSummary.LocationData[s].LocationID;
                //        TimeSpentOnLocation = dashboardSummary.LocationData[s].LadleList[s1].TimeSpent.TotalHours;
                //        FromDate = dashboardSummary.LocationData[s].LadleList[s1].InTime.ToString("dd/MM/yyyy");
                //        CurrentLadleLocationName = dashboardSummary.LocationData[s].LocationName;
                //        TotalTimeSpent = dashboardSummary.LocationData[s].LadleList[s1].TimeSpentSTR;

                //        if (TimeSpentOnLocation > 2)
                //        {
                //            for (int uc = 0; uc < UserContactDetails.Count; uc++)
                //            {
                //                double CheckTimeInHours = CurrentDateTime.Subtract(UserContactDetails[uc].LastSMSDateTime).TotalHours;


                //                if ((UserContactDetails[uc].LocationId == CurrentLadlelocation && CheckTimeInHours>1) || (UserContactDetails[uc].AllLocationSMSAccess==true && CheckTimeInHours > 1))
                //                {
                //                    if (perviousUserGuid != UserContactDetails[uc].UserID)
                //                    {
                //                        //UserContactDetails[uc].LastSMSDateTime = CurrentDateTime;
                //                        UpdateMsgUserDateTimeList.Add(UserContactDetails[uc]);
                //                    }

                //                    perviousUserGuid = UserContactDetails[uc].UserID;
                //                    string PhoneNumber = UserContactDetails[uc].MobileNo;
                //                    try
                //                    {
                //                        WebClient client = new WebClient();
                //                        string baseURL = "http://sms.innuvissolutions.com/api/mt/SendSMS?APIKey=hKF17EQmKEC4SyhmGemDpg&senderid=ESLLTS&channel=Trans&DCS=0&flashsms=0&number="+PhoneNumber+"&text=Ladle%20No:"+ MsgLadleNo + "%20FromDate:"+FromDate+"%20Location:"+ CurrentLadleLocationName + "%20IdleTime::"+ TotalTimeSpent + "%20(ESLLTS)&route=2&peid=1701161715882193369&DLTTemplateId=1707169701612698655"; 
                //                        client.DownloadString(baseURL);
                //                        SentMsg = true;

                //                    }
                //                    catch (Exception exp)
                //                    {
                //                        throw new Exception("Error In SMS Message Service:", exp);
                //                    }
                //                }
                //            }


                //        }
                //    }

                //}

                //UpdateUserSMSDateTime(UpdateMsgUserDateTimeList);

                #endregion



                return dashboardSummary;


            }
            catch (Exception ex)
            {
                throw new Exception(valusee, ex);
                string msg = ex.Message + ex.InnerException.StackTrace + ex.StackTrace;

                retValue = null;
            }

            /*(if(dashboardSummary.LocationData != null && dashboardSummary.LocationData.Count > 0)
            {
                if(dashboardSummary.LocationData[0].LadleList != null && dashboardSummary.LocationData[0].LadleList.Count > 0)
                {
                    dashboardSummary.LocationData[0].LadleList[0].TimeSpent = new TimeSpan(2, 2, 3);
                }
                if (dashboardSummary.LocationData[0].LadleList != null && dashboardSummary.LocationData[0].LadleList.Count > 1)
                {
                    dashboardSummary.LocationData[0].LadleList[1].TimeSpent = new TimeSpan(2, 2, 3);
                }
            }*/

            return dashboardSummary;
        }
        /// <summary>
        /// Gets All The Ladels List which are active with the Current Online Data.
        /// </summary>
        /// <returns>List of Active Ladles</returns>
        public List<ESLLadle> GetLadlesOnlineData()
        {
            List<ESLLadle> retValue = null;
            try
            {

                retValue = GetAllLadles();

                foreach (ESLLadle l in retValue)
                {
                    ResolveCurrentLadlePath(l);
                }
            }
            catch (Exception ex)
            {
                retValue = null;
            }

            return retValue;
        }


        /// <summary>
        /// Gets all the Cast Transactions which have happend in the specified date time range and populates
        /// Ladle information with LIMS and Weighment information aswell in the paths data.
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
        public List<ESLCastTransaction> GetTransactions(DateTime fromDate, DateTime toDate)
        {
            /*List<ESLCastTransaction> _testData = new List<ESLCastTransaction>();

            ESLCastTransaction c1 = new ESLCastTransaction();
            c1.LocationName = "BF2";
            c1.CastNo = "BF2-C00001";
            c1.CreateDateTime = DateTime.Parse("30-04-2022 11:40:00");
            c1.LIMSDateTime = DateTime.Parse("30-04-2022 14:40:00");

            _testData.Add(c1);

            ESLCastTransaction c2 = new ESLCastTransaction();
            c2.LocationName = "BF3";
            c2.CastNo = "BF3-C00001";
            c2.CreateDateTime = DateTime.Parse("30-04-2022 15:40:00");
            c2.LIMSDateTime = DateTime.Parse("30-04-2022 17:40:00");

            _testData.Add(c2);

            return _testData;*/

            List<ESLCastTransaction> _transactions = null;
            try
            {
                _transactions = GetCastTransactions(fromDate, toDate);

                for (int i = 0; i < _transactions.Count; i++)
                {
                    ESLCastTransaction ct = _transactions[i];
                    ESLLIMSData limsData = GetLIMSData(ct.LocationID, ct.CastNo);
                    List<ESLLadle> list = GetLadleListForCastTransaction(ct.CTID);
                    foreach (ESLLadle l in list)
                    {
                        ResolveCurrentLadlePath(l, ct.CreateDateTime);

                    }
                    ct.LadleList = list;
                }
            }
            catch (Exception ex)
            {
                _transactions = null;
                throw ex;
            }
            return _transactions;
        }

        /// <summary>
        /// Currently Returns Demo Data...
        /// </summary>
        /// <returns></returns>
        public List<ESLLocation> GetOnlineProductionData()
        {
            List<ESLLocation> _onlineProduction = new List<ESLLocation>();

            ESLLocation l = new ESLLocation();
            l.LocationID = 1;
            l.LocationName = "BF2";
            l.TotalProduction = 3500;

            ESLLocation s = new ESLLocation();
            s.LocationName = "SMS";
            s.TotalProduction = 1500;

            ESLLocation p = new ESLLocation();
            p.LocationName = "PCM";
            p.TotalProduction = 1000;

            ESLLocation d = new ESLLocation();
            d.LocationName = "DIP";
            d.TotalProduction = 1000;

            l.ProductionUnits.Add(s);
            l.ProductionUnits.Add(p);
            l.ProductionUnits.Add(d);

            _onlineProduction.Add(l);

            ESLLocation l1 = new ESLLocation();
            l1.LocationID = 2;
            l1.LocationName = "BF3";
            l1.TotalProduction = 1500;

            ESLLocation s1 = new ESLLocation();
            s1.LocationName = "SMS";
            s1.TotalProduction = 1000;

            ESLLocation p1 = new ESLLocation();
            p1.LocationName = "PCM";
            p1.TotalProduction = 200;

            ESLLocation d1 = new ESLLocation();
            d1.LocationName = "DIP";
            d1.TotalProduction = 300;

            l1.ProductionUnits.Add(s1);
            l1.ProductionUnits.Add(p1);
            l1.ProductionUnits.Add(d1);

            _onlineProduction.Add(l1);

            return _onlineProduction;

        }

        public ESLLadleTransactionSummary GetTransactionSummaryOld(DateTime fromDate, DateTime toDate)
        {
            //bool retValue = false;
            DateTime fromDateTime;
            DateTime toDateTime;
            bool isDashboardCall = false;
            Dictionary<string, ESLLIMSData> _limsData = new Dictionary<string, ESLLIMSData>();
            if (fromDate.Equals(toDate))
            {
                //Dashboard Calll
                isDashboardCall = true;
                if (toDate.Hour >= 22)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                }
            }
            else
            {
                if (fromDate.Year == toDate.Year && fromDate.Day == toDate.Day && fromDate.Month == toDate.Month)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                    //fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                }
                else
                {
                    fromDateTime = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                }
            }

            ESLLadleTransactionSummary ladleTransactionSummry = null;

            try
            {
                DateTime qfromDate = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

                List<ReadersTransactionLog> logs = GetSourceReaderTrsactionLogForDateTime(qfromDate, qToDate);

                List<ESLLocation> _sourceLocationData = new List<ESLLocation>();
                Dictionary<string, ESLLadle> _currentSourceLadles = new Dictionary<string, ESLLadle>();
                Dictionary<string, ESLLadle> _currentAllLadlesLastLocation = new Dictionary<string, ESLLadle>(); //Stores the last Location and Time of the Ladle for further reference...
                string successMessage = string.Empty;
                string errorMessages = string.Empty;
                int TXNO = 0;

                for (int i = 0; i < logs.Count; i++)
                {


                    ReadersTransactionLog l = logs[i];
                    if (l.LocationID == 1)
                    {
                        //BF2
                        if (!_sourceLocationData.Exists(x => x.LocationID == 1))
                        {
                            ESLLocation bf2 = new ESLLocation();
                            bf2.LocationID = 1;
                            bf2.LocationName = "BF2";
                            _sourceLocationData.Add(bf2);
                        }
                    }
                    else if (l.LocationID == 2)
                    {
                        //BF3
                        if (!_sourceLocationData.Exists(x => x.LocationID == 2))
                        {
                            ESLLocation bf3 = new ESLLocation();
                            bf3.LocationID = 2;
                            bf3.LocationName = "BF3";
                            _sourceLocationData.Add(bf3);
                        }
                    }


                    //Get Source Location Collection..
                    ESLLocation currentLocationObj = null;

                    foreach (ESLLocation l1 in _sourceLocationData)
                    {
                        if (l1.LocationID == l.LocationID)
                        {
                            currentLocationObj = l1;
                            break;
                        }
                    }

                    if (currentLocationObj != null)
                    {
                        //Enteres when the Ladle is found at the source location.
                        //Transaction ID for this Ladle Needs to be created...
                        //Set the State to 0....
                        //Check if the Ladle Exists and update its Time Slot...
                        //This Section is only for SOURCE Ladles....
                        if (currentLocationObj.LocationID == 2)
                        {
                            int k = 100;
                        }
                        ESLLadle currentladle = null;
                        /*foreach(ESLLadle ld in currentLocationObj.LadleList)
                        {
                            
                            if(ld.SerialNo == l.AssetSerialNo.ToString() && ld.State != 3)
                            {
                                currentladle = ld;
                            }
                        }*/

                        for (int x = 0; x < currentLocationObj.LadleList.Count; x++) //ESLLadle ld in currentLocationObj.LadleList)
                        {
                            ESLLadle lc = currentLocationObj.LadleList[x];
                            if (lc != null)
                            {
                                if (lc.SerialNo == l.AssetSerialNo.ToString())
                                {
                                    currentladle = lc;
                                }
                            }
                            //if(l.AssetSerialNo == currentLocationObj.LadleList[x])
                        }

                        if (currentladle == null)
                        {
                            //New Laddle tobe added
                            currentladle = new ESLLadle();
                            currentladle.ID = Guid.NewGuid(); //New TX ID for this ladle....
                            currentladle.TXNo = ++TXNO;
                            currentladle.CurrentLocationID = l.LocationID;
                            currentladle.Direction = "IN";
                            currentladle.InTime = l.ServerDateTime;
                            //currentladle.OutTime = l.ServerDateTime;
                            currentladle.SerialNo = l.AssetSerialNo.ToString();
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            currentladle.LadleNo = lad.LadleNo;
                            currentladle.LastLocationID = l.LocationID;
                            currentladle.State = 0; //New Loading State.
                            currentLocationObj.LadleList.Add(currentladle);
                            //Stores the Latest Source Ladles in the Dictionary...
                            if (!_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                _currentSourceLadles.Add(currentladle.LadleNo, currentladle);

                            //Stores the Last Location of the ladle in the Dictionary....
                            if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = currentladle.LadleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                            }
                            else
                            {
                                if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                {
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                }
                            }
                        }
                        else
                        {
                            //Check the last location of the ladle if its not as current its a new tramsaction..
                            int curladlelastLocation = 0;
                            if (_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                            {
                                curladlelastLocation = _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID;
                            }

                            if (l.LocationID != curladlelastLocation)
                            {
                                errorMessages = errorMessages + "\r\n Found Ladle from One Source to Other Missing Transactions...\r\n";
                                //Insert New 
                                currentladle = new ESLLadle();


                                currentladle.ID = Guid.NewGuid();
                                currentladle.TXNo = ++TXNO;
                                currentladle.CurrentLocationID = l.LocationID;
                                currentladle.Direction = "IN";
                                currentladle.InTime = l.ServerDateTime;
                                //currentladle.OutTime = l.ServerDateTime;
                                currentladle.SerialNo = l.AssetSerialNo.ToString();
                                ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                                if (lad == null)
                                    continue;
                                currentladle.LadleNo = lad.LadleNo;
                                currentladle.LastLocationID = l.LocationID;

                                if (curladlelastLocation == 1 || curladlelastLocation == 2) //Last Location was a Source and Come Again to Another Source...
                                {
                                    currentladle.FoundSourceToSource = true;
                                    ESLLocation previousLadleSource = null;
                                    foreach (ESLLocation l1 in _sourceLocationData)
                                    {
                                        if (l1.LocationID == curladlelastLocation)
                                        {
                                            previousLadleSource = l1;
                                            break;
                                        }
                                    }

                                    if (previousLadleSource != null)
                                    {
                                        for (int x = previousLadleSource.LadleList.Count - 1; x >= 0; x--) //ESLLadle ld in currentLocationObj.LadleList)
                                        {
                                            ESLLadle lc = previousLadleSource.LadleList[x];
                                            if (lc != null)
                                            {
                                                if (lc.SerialNo == l.AssetSerialNo.ToString())
                                                {
                                                    //currentladle = lc;
                                                    lc.FoundSourceToSource = true;
                                                }
                                            }
                                            //if(l.AssetSerialNo == currentLocationObj.LadleList[x])
                                        }
                                    }
                                }

                                currentLocationObj.LadleList.Add(currentladle);
                                if (!_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                    _currentSourceLadles.Add(currentladle.LadleNo, currentladle);
                                else
                                    _currentSourceLadles[currentladle.LadleNo] = currentladle;

                                if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                {
                                    ESLLadle curLocLadle = new ESLLadle();
                                    curLocLadle.LastLocationID = l.LocationID;
                                    curLocLadle.LadleNo = currentladle.LadleNo;
                                    curLocLadle.InTime = l.ServerDateTime;
                                    _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                }
                                else
                                {
                                    if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                    }
                                    else
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                    }
                                }
                            }
                            else if (l.LocationID == curladlelastLocation)
                            {
                                //Update In time.
                                //if (l.ServerDateTime.Subtract(currentladle.InTime).TotalMinutes > 25)
                                //{
                                currentladle.OutTime = l.ServerDateTime; // Mark as Out Time...
                                                                         //}
                                                                         //else
                                                                         //{
                                                                         //  currentladle.InTime = l.ServerDateTime; //Found Multiple Entries for the source at short duration...
                                                                         //errorMessages = errorMessages + "\r\n Found Laddle at other Source without Intermedia Transactions.\r\n";
                                                                         //}

                                if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                {
                                    _currentSourceLadles[currentladle.LadleNo] = currentladle;
                                }

                                if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                {
                                    ESLLadle curLocLadle = new ESLLadle();
                                    curLocLadle.LastLocationID = l.LocationID;
                                    curLocLadle.LadleNo = currentladle.LadleNo;
                                    curLocLadle.InTime = l.ServerDateTime;
                                    _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                }
                                else
                                {
                                    if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                    }
                                    else
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if ((l.LocationID == 4) || (l.LocationID == 5) || (l.LocationID == 6))
                        {
                            //Identified as Production Units.
                            //string LadleNo = GetLadleByID(l.AssetSerialNo).LadleNo;
                            string LadleNo = string.Empty;
                            ESLLadle eld = GetLadleByID(l.AssetSerialNo);
                            if (eld != null)
                            {
                                LadleNo = eld.LadleNo;
                            }
                            else
                            {
                                continue;
                            }
                            if (!string.IsNullOrEmpty(LadleNo) && _currentSourceLadles.ContainsKey(LadleNo))
                            {
                                ESLLadle recLadle = _currentSourceLadles[LadleNo];
                                if (recLadle != null && recLadle.State != 3)
                                {

                                    foreach (ESLLocation l1 in _sourceLocationData)
                                    {
                                        if (l1.LocationID == recLadle.LastLocationID)
                                        {
                                            currentLocationObj = l1;
                                            break;
                                        }
                                    }


                                }


                                //currentLocationObj.LadleList.Add(currentladle);
                                //Check if the production unit exist and add to the production unit....
                                ESLLocation ladleLoc = GetLocation(l.LocationID);
                                if (currentLocationObj.ProductionUnits.Exists(x => x.LocationID == l.LocationID))
                                {
                                    ESLLocation existingProdLocation = null;
                                    foreach (ESLLocation el in currentLocationObj.ProductionUnits)
                                    {
                                        if (el.LocationID == l.LocationID)
                                        {
                                            existingProdLocation = el;
                                            break;
                                        }
                                    }

                                    if (existingProdLocation != null) //Addtional Check....
                                    {
                                        ESLLadle currentladle = new ESLLadle();
                                        currentladle.CurrentLocationID = l.LocationID;
                                        currentladle.Direction = "IN";
                                        currentladle.InTime = l.ServerDateTime;
                                        currentladle.SerialNo = l.AssetSerialNo.ToString();
                                        ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                                        if (lad == null)
                                            continue;
                                        currentladle.LadleNo = lad.LadleNo;
                                        currentladle.LastLocationID = l.LocationID;
                                        if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                        {
                                            currentladle.ID = _currentSourceLadles[currentladle.LadleNo].ID;
                                            currentladle.TXNo = _currentSourceLadles[currentladle.LadleNo].TXNo;
                                        }
                                        else
                                        {
                                            //Do Nothing...
                                        }

                                        if (existingProdLocation.LadleList.Exists(x => x.ID == currentladle.ID))
                                        {
                                            foreach (ESLLadle pl in existingProdLocation.LadleList)
                                            {
                                                if (pl.ID == currentladle.ID)
                                                {
                                                    pl.OutTime = l.ServerDateTime;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                            {
                                                currentladle.ID = _currentSourceLadles[currentladle.LadleNo].ID;
                                                currentladle.TXNo = _currentSourceLadles[currentladle.LadleNo].TXNo;
                                                existingProdLocation.LadleList.Add(currentladle);
                                            }
                                            else
                                            {
                                                //Not Found in Source....
                                                int k = 0;
                                            }

                                        }

                                        if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                        {
                                            ESLLadle curLocLadle = new ESLLadle();
                                            curLocLadle.LastLocationID = l.LocationID;
                                            curLocLadle.LadleNo = currentladle.LadleNo;
                                            curLocLadle.InTime = l.ServerDateTime;
                                            _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                        }
                                        else
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                        }

                                        //_currentAllLadles.Add(currentladle.LadleNo, currentladle);
                                    }
                                }
                                else
                                {
                                    //Add Location and Then the Ladle....
                                    ESLLocation proUnit = new ESLLocation();
                                    proUnit.LocationID = (short)l.LocationID;
                                    proUnit.LocationName = GetLocation(l.LocationID).LocationName;

                                    ESLLadle currentladle = new ESLLadle();
                                    currentladle.CurrentLocationID = l.LocationID;
                                    currentladle.Direction = "IN";
                                    currentladle.InTime = l.ServerDateTime;
                                    currentladle.SerialNo = l.AssetSerialNo.ToString();
                                    ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                                    if (lad == null)
                                        continue;
                                    currentladle.LadleNo = lad.LadleNo;
                                    currentladle.LastLocationID = l.LocationID;

                                    if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                    {
                                        currentladle.ID = _currentSourceLadles[currentladle.LadleNo].ID;
                                        currentladle.TXNo = _currentSourceLadles[currentladle.LadleNo].TXNo;
                                    }

                                    proUnit.LadleList.Add(currentladle);
                                    currentLocationObj.ProductionUnits.Add(proUnit);

                                    if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                    {
                                        ESLLadle curLocLadle = new ESLLadle();
                                        curLocLadle.LastLocationID = l.LocationID;
                                        curLocLadle.LadleNo = currentladle.LadleNo;
                                        curLocLadle.InTime = l.ServerDateTime;
                                        _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                    }
                                    else
                                    {
                                        if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                        }
                                        else
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                        }
                                    }
                                }

                            }


                        }
                        else if (l.LocationID == 3)
                        {
                            //Weighment Information...
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            string ladleNo = lad.LadleNo;
                            if (!_currentAllLadlesLastLocation.ContainsKey(ladleNo))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = ladleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(ladleNo, curLocLadle);
                            }
                            else
                            {
                                //_currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                //_currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                if (_currentAllLadlesLastLocation[ladleNo].LastLocationID == l.LocationID)
                                {
                                    _currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                    _currentAllLadlesLastLocation[ladleNo].InTime = l.ServerDateTime;
                                }
                            }
                            //if()
                        }
                        else if (l.LocationID == 7)
                        {
                            //LRS.....

                            //string ladleNo = GetLadleByID(l.AssetSerialNo).LadleNo;
                            string ladleNo = string.Empty;
                            ESLLadle eld = GetLadleByID(l.AssetSerialNo);

                            if (eld != null)
                                ladleNo = eld.LadleNo;
                            else
                                continue;

                            if (!string.IsNullOrEmpty(ladleNo) && (!_currentAllLadlesLastLocation.ContainsKey(ladleNo)))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = ladleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(ladleNo, curLocLadle);
                            }
                            else
                            {
                                if (_currentAllLadlesLastLocation[ladleNo].LastLocationID == l.LocationID)
                                {
                                    _currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                    _currentAllLadlesLastLocation[ladleNo].InTime = l.ServerDateTime;
                                }
                            }

                            //Also Set the Property of the source ladles as completed.
                        }

                    }

                }

                ladleTransactionSummry = new ESLLadleTransactionSummary();

                int internalSerialNumber = 0;

                List<string> castLadlehash = new List<string>();

                foreach (ESLLocation source in _sourceLocationData)
                {
                    ESLLocationConsolidate cLocationSource = null;

                    if (!ladleTransactionSummry.ProductionSummary.Exists(x => x.LocationID == source.LocationID))
                    {
                        cLocationSource = new ESLLocationConsolidate();
                        cLocationSource.LocationID = source.LocationID;
                        cLocationSource.LocationName = GetLocation(source.LocationID).LocationName;
                        cLocationSource.TotalProduction = 0;
                        ladleTransactionSummry.ProductionSummary.Add(cLocationSource);
                    }


                    foreach (ESLLadle sl in source.LadleList)
                    {
                        //Prepare ladle Consolidated
                        ESLLadleConsolidated lc = new ESLLadleConsolidated();
                        lc.SerialNumber = ++internalSerialNumber;
                        lc.TXNo = sl.TXNo;
                        lc.SourceLocation = GetLocation(sl.LastLocationID).LocationName;
                        lc.SourceInDateTime = sl.InTime;
                        lc.SourceOutDateTime = sl.OutTime;
                        lc.LadleNo = sl.LadleNo;

                        if (sl.FoundSourceToSource)
                        {
                            int j = 0;
                        }
                        //Added on 30-07-2022

                        if (cLocationSource != null)
                            cLocationSource.LadleCount = cLocationSource.LadleCount + 1;
                        ESLLadleWeightData lwd1 = null;
                        try
                        {
                            lwd1 = GetLadleWeighmentData(sl.LadleNo, sl.InTime);
                        }
                        catch (Exception ex)
                        {
                            lwd1 = null;
                        }
                        //ESLLadleWeightData lwd1 = GetLadleWeighmentData(sl.LadleNo, sl.InTime);

                        if (lwd1 != null)
                        {
                            cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd1.NetWeight;
                        }

                        //Find the Ladle in Destination...
                        foreach (ESLLocation p in source.ProductionUnits)
                        {
                            foreach (ESLLadle pdl in p.LadleList)
                            {
                                if (pdl.TXNo == sl.TXNo && pdl.LadleNo == sl.LadleNo)
                                {
                                    //Found the ladle
                                    lc.DestinationLocation = GetLocation(p.LocationID).LocationName;
                                    lc.DestinationInDateTime = pdl.InTime;
                                    lc.DestinationOutDateTime = pdl.OutTime;

                                    //Populate WB Data...
                                    ESLLadleWeightData lwd = null;
                                    try
                                    {
                                        lwd = GetLadleWeighmentData(pdl.LadleNo, lc.SourceInDateTime, lc.DestinationInDateTime);
                                    }
                                    catch (Exception ex)
                                    {

                                    }


                                    if (lwd != null)
                                    {
                                        //Count the ladle only if it is weighed for the prodcution total.
                                        //Comented on 30-07-2022 
                                        //if (cLocationSource != null)
                                        //  cLocationSource.LadleCount = cLocationSource.LadleCount + 1;

                                        lc.SourceWeight = lwd.GrossWeight.ToString("000.00");
                                        lc.SourceWeightDateTime = lwd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss");

                                        lc.TareWeight = lwd.TareWeight.ToString("000.00");
                                        lc.TareWeightDateTime = lwd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss");

                                        lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");

                                        lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);

                                        lc.NetWeight = lwd.NetWeight.ToString("000.00");

                                        if (cLocationSource != null)
                                        {
                                            //Commented on 30-07-2022
                                            //cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd.NetWeight;

                                            if (!cLocationSource.ProductionUnits.Exists(x => x.LocationID == p.LocationID))
                                            {
                                                ESLLocationConsolidate cprodLocation = new ESLLocationConsolidate();
                                                cprodLocation.LocationID = p.LocationID;
                                                cprodLocation.LocationName = p.LocationName;
                                                cprodLocation.TotalProduction = 0;
                                                cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                                cLocationSource.ProductionUnits.Add(cprodLocation);
                                                castLadlehash.Add(lwd.CastNumber + "|" + lc.LadleNo);
                                            }
                                            else
                                            {
                                                ESLLocationConsolidate cprodLocation = cLocationSource.ProductionUnits.Find(x => x.LocationID == p.LocationID);

                                                if (!castLadlehash.Contains(lwd.CastNumber + "|" + lc.LadleNo))
                                                {
                                                    cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                    cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                                    castLadlehash.Add(lwd.CastNumber + "|" + lc.LadleNo);
                                                }
                                            }
                                        }

                                        lc.CastNo = lwd.CastNumber;

                                        if (lc.CastNo != string.Empty)
                                        {
                                            //Fetch LiMS data..
                                            //lc.HasLIMSData = true;

                                            ESLLIMSData liData = null;

                                            if (!isDashboardCall)
                                            {
                                                if (_limsData.ContainsKey(lc.CastNo))
                                                {
                                                    liData = _limsData[lc.CastNo];
                                                }
                                                else
                                                {
                                                    ESLLIMSData ld = null;
                                                    try
                                                    {
                                                        ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        ld = null;
                                                    }
                                                    //ESLLIMSData ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                    if (ld != null)
                                                    {
                                                        _limsData.Add(lc.CastNo, ld);
                                                        liData = ld;
                                                    }
                                                }
                                                //liData = GetLIMSData(source.LocationID, lc.CastNo);
                                            }

                                            if (liData != null)
                                            {
                                                liData.LocationName = GetLocation(source.LocationID).LocationName;
                                                lc.LIMSData = liData;
                                                lc.HasLIMSData = true;

                                                if (!ladleTransactionSummry.LimsSummary.Exists(x => x.CastNo == lc.CastNo))
                                                {
                                                    ladleTransactionSummry.LimsSummary.Add(liData);
                                                }

                                            }
                                        }
                                    }


                                }
                                else
                                {
                                    //Did not find the ladle....
                                    //int jack = 500;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(lc.DestinationLocation))
                        {
                            //int jj = 500;
                            //Get The Weight from the Source time.
                            if (lc.SourceOutDateTime.Year != 1)
                            {
                                ESLLadleWeightData ewd = null;

                                try
                                {
                                    ewd = GetLadleWeighmentData(lc.LadleNo, lc.SourceOutDateTime);
                                }
                                catch (Exception ex)
                                {
                                    ewd = null;
                                }
                                //if(ewd != null)
                                //{
                                if (ewd != null)
                                {
                                    lc.SourceWeight = ewd.GrossWeight.ToString("000.00");
                                    lc.SourceWeightDateTime = ewd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss");

                                    lc.TareWeight = ewd.TareWeight.ToString("000.00");
                                    lc.TareWeightDateTime = ewd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss");

                                    //lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");


                                    lc.NetWeight = ewd.NetWeight.ToString("000.00");

                                    if (cLocationSource != null)
                                    {
                                        //cLocationSource.TotalProduction = cLocationSource.TotalProduction + ewd.NetWeight;
                                    }

                                    lc.CastNo = ewd.CastNumber;

                                    if (lc.CastNo != string.Empty)
                                    {
                                        //Fetch LiMS data..
                                        //lc.HasLIMSData = true;

                                        ESLLIMSData liData = null;

                                        if (!isDashboardCall)
                                        {
                                            if (_limsData.ContainsKey(lc.CastNo))
                                            {
                                                liData = _limsData[lc.CastNo];
                                            }
                                            else
                                            {
                                                ESLLIMSData ld = null;
                                                try
                                                {
                                                    ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                }
                                                catch (Exception ex)
                                                {
                                                    ld = null;
                                                }
                                                //ESLLIMSData 
                                                if (ld != null)
                                                {
                                                    _limsData.Add(lc.CastNo, ld);
                                                    liData = ld;
                                                }

                                            }
                                            //liData = GetLIMSData(source.LocationID, lc.CastNo);
                                        }

                                        if (liData != null)
                                        {
                                            liData.LocationName = GetLocation(source.LocationID).LocationName;
                                            lc.LIMSData = liData;
                                            lc.HasLIMSData = true;

                                            if (!ladleTransactionSummry.LimsSummary.Exists(x => x.CastNo == lc.CastNo))
                                            {
                                                ladleTransactionSummry.LimsSummary.Add(liData);
                                            }

                                        }
                                    }
                                }
                            }

                            //}
                        }

                        ladleTransactionSummry.LadleTransactionDetails.Add(lc);

                        if (lc.SourceOutDateTime.Subtract(lc.SourceInDateTime).TotalMinutes < 30)
                        {
                            //Get the Actual Source In Time due to boundry condition....
                            ESLLocation loc = GetLocation(lc.SourceLocation);
                            ESLLadle lad = GetLadleByID(lc.LadleNo);
                            DateTime lastSourceEntryDatetime;
                            short lastLocationID = 0;

                            if (loc != null && lad != null)
                            {
                                if (GetBoundryConditionSourceInTime(lc.SourceInDateTime, loc.LocationID.ToString(), lad.SerialNo, out lastSourceEntryDatetime, out lastLocationID))
                                {
                                    if (lastLocationID == loc.LocationID)
                                    {
                                        lc.SourceOutDateTime = lc.SourceInDateTime;
                                        lc.SourceInDateTime = lastSourceEntryDatetime;
                                        //Update TAT as new New Source...
                                        if (!string.IsNullOrEmpty(lc.DestinationLocation))
                                        {
                                            lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");
                                            lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);
                                        }
                                    }
                                }

                            }
                            int k = 0;
                        }

                    }
                }



                int x1 = 50;

            }
            catch (Exception ex)
            {
                int j = 0;
                throw ex;
            }
            finally
            {

            }
            return ladleTransactionSummry;

        }


        public ESLLadleTransactionSummary GetTransactionSummary(DateTime fromDate, DateTime toDate)
        {
            //bool retValue = false;
            DateTime fromDateTime;
            DateTime toDateTime;
            bool isDashboardCall = false;
            Dictionary<string, ESLLIMSData> _limsData = new Dictionary<string, ESLLIMSData>();
            if (fromDate.Equals(toDate))
            {
                //Dashboard Calll
                isDashboardCall = true;
                if (toDate.Hour >= 22)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                }
            }
            else
            {
                if (fromDate.Year == toDate.Year && fromDate.Day == toDate.Day && fromDate.Month == toDate.Month)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                    //fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                }
                else
                {
                    fromDateTime = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                }
            }

            ESLLadleTransactionSummary ladleTransactionSummry = null;

            try
            {
                DateTime qfromDate = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

                Efromdate = qfromDate;
                Etodate = qToDate;

                List<ReadersTransactionLog> logs = GetSourceReaderTrsactionLogForDateTimeNew(qfromDate, qToDate);
                List<ESLLadleWeightData> wbData = GetWeighmentData(qfromDate, qToDate);

                List<ESLLocation> _sourceLocationData = new List<ESLLocation>();
                Dictionary<string, ESLLadle> _currentSourceLadles = new Dictionary<string, ESLLadle>();
                Dictionary<string, ESLLadle> _currentAllLadlesLastLocation = new Dictionary<string, ESLLadle>(); //Stores the last Location and Time of the Ladle for further reference...
                string successMessage = string.Empty;
                string errorMessages = string.Empty;
                Dictionary<string, short> locationMap = new Dictionary<string, short>();

                locationMap.Add("BF2", 1);
                locationMap.Add("BF3", 2);
                locationMap.Add("SMS", 4);
                locationMap.Add("DIP", 5);
                locationMap.Add("PCM", 6);
                locationMap.Add("LRS", 7);

                int TXNO = 0;

                ladleTransactionSummry = new ESLLadleTransactionSummary();

                int internalSerialNumber = 0;

                //var l = (from lg in logs where lg.TransDateTime > '2023-09-14' orderby lg.TransDateTime).FirstOrDefault(); 

                //for (int i = 0; i < logs.Count; i++)

                for (int i = 0; i < wbData.Count; i++) //Changed to WB Data..
                {
                    ESLLadleWeightData ewd = wbData[i];
                    //ReadersTransactionLog l = logs[i];
                    string currentWBSourceLocation = ewd.Sender;
                    currentWBSourceLocation = currentWBSourceLocation.Replace("-", ""); //Replace the - as WB Data has - in the BF Names
                    short currentSourceLocationID = 0;

                    currentSourceLocationID = locationMap[currentWBSourceLocation];


                    ESLLocationConsolidate cLocationSource = null;

                    if (!ladleTransactionSummry.ProductionSummary.Exists(x => x.LocationName == currentWBSourceLocation))
                    {
                        cLocationSource = new ESLLocationConsolidate();
                        cLocationSource.LocationID = currentSourceLocationID;
                        cLocationSource.LocationName = currentWBSourceLocation;
                        cLocationSource.TotalProduction = 0;
                        cLocationSource.LadleCount = 0;
                        ladleTransactionSummry.ProductionSummary.Add(cLocationSource);

                    }
                    else
                    {
                        cLocationSource = ladleTransactionSummry.ProductionSummary.Find(x => x.LocationName == currentWBSourceLocation);
                    }

                    DateTime sourceOutTime = DateTime.MinValue;// = log.ServerDateTime;
                    DateTime sourceINTime = DateTime.MinValue;

                    //if (currentWBSourceLocation == "BF2")
                    //{
                    //Current WB Tx Sender is BF2..
                    var log = (from lg in logs where (lg.TransDateTime < ewd.Gross_DT_Time) && (lg.LocationID == currentSourceLocationID) && (lg.AssetDesc == "Ladle" + ewd.LadleNo) && (lg.isPickedAlready == false) orderby lg.TransDateTime descending select lg).FirstOrDefault();
                    //This Transaction will give the out time ..
                    if (log != null)
                    {
                        sourceOutTime = log.ServerDateTime;

                        log.isPickedAlready = true;

                        var log1 = (from lg in logs where (lg.TransDateTime < sourceOutTime) && (lg.LocationID == 1) && (lg.AssetDesc == "Ladle" + ewd.LadleNo) && (lg.isPickedAlready == false) orderby lg.TransDateTime ascending select lg).FirstOrDefault();
                        //We have got the Out Time from the BF so get the In-time...

                        if (log1 != null)
                        {
                            //
                            sourceINTime = log1.ServerDateTime;
                        }
                        else
                        {
                            // could not get the source in time so we need to fill in.. 
                            sourceINTime = sourceOutTime.AddHours(-1);
                        }

                        //int x2 = 50;

                    }
                    else
                    {
                        //Could not get the source OUT time so we need to fill in...
                        sourceOutTime = ewd.Gross_DT_Time.AddHours(-1);
                    }



                    ESLLadleConsolidated lc = new ESLLadleConsolidated();
                    lc.SerialNumber = ++internalSerialNumber;
                    lc.TXNo = Int32.Parse(ewd.TransID);
                    lc.SourceLocation = currentWBSourceLocation;
                    lc.SourceInDateTime = sourceINTime;
                    if (lc.SourceInDateTime == DateTime.MinValue)
                    {
                        lc.SourceInDateTime = sourceOutTime.AddHours(-1);
                    }
                    lc.SourceOutDateTime = sourceOutTime;
                    lc.LadleNo = "Ladle" + ewd.LadleNo;

                    //Added on 30-07-2022

                    if (cLocationSource != null)
                        cLocationSource.LadleCount = cLocationSource.LadleCount + 1;

                    cLocationSource.TotalProduction = cLocationSource.TotalProduction + ewd.NetWeight;

                    //Now check to the WB Data for the Ladle Movement...
                    string initialReceiver = ewd.Receiver;

                    if (ewd.ConsumptionData != null && ewd.ConsumptionData.Count > 0)
                    {

                        for (int tcount = 0; tcount < ewd.ConsumptionData.Count; tcount++)
                        {
                            if (tcount == 0)
                            {
                                short destinationLocationID = locationMap[ewd.Receiver];
                                lc.DestinationLocation = GetLocation(destinationLocationID).LocationName;

                                DateTime destinationInTime = DateTime.MinValue;
                                DateTime destinationOutTime = DateTime.MinValue;

                                var destLogIn = (from dlg in logs where (dlg.TransDateTime > ewd.Gross_DT_Time) && (dlg.LocationID == destinationLocationID) && (dlg.AssetDesc == "Ladle" + ewd.LadleNo) && (dlg.isPickedAlready == false) orderby dlg.TransDateTime ascending select dlg).FirstOrDefault();

                                if (destLogIn != null)
                                {
                                    destinationInTime = destLogIn.ServerDateTime;
                                    destLogIn.isPickedAlready = true;
                                    var destLogOut = (from dlg in logs where (dlg.TransDateTime > destinationInTime) && (dlg.LocationID == destinationLocationID) && (dlg.AssetDesc == "Ladle" + ewd.LadleNo) && (dlg.isPickedAlready == false) orderby dlg.TransDateTime ascending select dlg).FirstOrDefault();

                                    if (destLogOut != null)
                                    {
                                        destLogOut.isPickedAlready = true;
                                        destinationOutTime = destLogOut.ServerDateTime;
                                    }
                                    else
                                    {
                                        destinationOutTime = destinationInTime.AddHours(1);
                                    }
                                }
                                else
                                {
                                    destinationInTime = ewd.GrossDateTime.AddHours(1);
                                }
                                lc.DestinationInDateTime = destinationInTime;
                                lc.DestinationOutDateTime = destinationOutTime;

                                lc.SourceWeight = ewd.GrossWeight.ToString("000.00");
                                lc.SourceWeightDateTime = ewd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss");

                                lc.TareWeight = ewd.TareWeight.ToString("000.00");
                                lc.TareWeightDateTime = ewd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss");

                                lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");

                                lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);

                                lc.NetWeight = ewd.NetWeight.ToString("000.00");

                                lc.CastNo = ewd.CastNumber;


                                //currentLocationObj.TotalProduction = currentLocationObj.TotalProduction + ewd.NetWeight;
                                //currentLocationObj.
                                //tcount++;
                                continue;
                            }
                            else
                            {

                            }
                        }
                    }

                    if (cLocationSource != null)
                    {
                        //Commented on 30-07-2022
                        //cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd.NetWeight;

                        if (!cLocationSource.ProductionUnits.Exists(x => x.LocationName == initialReceiver))
                        {
                            ESLLocationConsolidate cprodLocation = new ESLLocationConsolidate();
                            cprodLocation.LocationID = locationMap[initialReceiver];
                            cprodLocation.LocationName = initialReceiver;
                            cprodLocation.TotalProduction = 0;
                            cprodLocation.TotalProduction = cprodLocation.TotalProduction + ewd.NetWeight;
                            cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                            cLocationSource.ProductionUnits.Add(cprodLocation);
                            //castLadlehash.Add(lwd.CastNumber + "|" + lc.LadleNo);
                        }
                        else
                        {
                            ESLLocationConsolidate cprodLocation = cLocationSource.ProductionUnits.Find(x => x.LocationName == initialReceiver);

                            cprodLocation.TotalProduction = cprodLocation.TotalProduction + ewd.NetWeight;
                            cprodLocation.LadleCount = cprodLocation.LadleCount + 1;

                        }
                    }


                    ladleTransactionSummry.LadleTransactionDetails.Add(lc);

                }

                //ladleTransactionSummry = new ESLLadleTransactionSummary();

                //int internalSerialNumber = 0;

                List<string> castLadlehash = new List<string>();

                /*foreach (ESLLocation source in _sourceLocationData)
                {
                    ESLLocationConsolidate cLocationSource = null;

                    if (!ladleTransactionSummry.ProductionSummary.Exists(x => x.LocationID == source.LocationID))
                    {
                        cLocationSource = new ESLLocationConsolidate();
                        cLocationSource.LocationID = source.LocationID;
                        cLocationSource.LocationName = GetLocation(source.LocationID).LocationName;
                        cLocationSource.TotalProduction = 0;
                        ladleTransactionSummry.ProductionSummary.Add(cLocationSource);
                    }


                    foreach (ESLLadle sl in source.LadleList)
                    {
                        //Prepare ladle Consolidated
                        ESLLadleConsolidated lc = new ESLLadleConsolidated();
                        lc.SerialNumber = ++internalSerialNumber;
                        lc.TXNo = sl.TXNo;
                        lc.SourceLocation = GetLocation(sl.LastLocationID).LocationName;
                        lc.SourceInDateTime = sl.InTime;
                        lc.SourceOutDateTime = sl.OutTime;
                        lc.LadleNo = sl.LadleNo;

                        if (sl.FoundSourceToSource)
                        {
                            int j = 0;
                        }
                        //Added on 30-07-2022

                        if (cLocationSource != null)
                            cLocationSource.LadleCount = cLocationSource.LadleCount + 1; //Done..
                        ESLLadleWeightData lwd1 = null;
                        try
                        {
                            lwd1 = GetLadleWeighmentData(sl.LadleNo, sl.InTime);
                        }
                        catch (Exception ex)
                        {
                            lwd1 = null;
                        }
                        //ESLLadleWeightData lwd1 = GetLadleWeighmentData(sl.LadleNo, sl.InTime);

                        if (lwd1 != null)
                        {
                            cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd1.NetWeight; //Done
                        }

                        //Find the Ladle in Destination...
                        foreach (ESLLocation p in source.ProductionUnits)
                        {
                            foreach (ESLLadle pdl in p.LadleList)
                            {
                                if (pdl.TXNo == sl.TXNo && pdl.LadleNo == sl.LadleNo)
                                {
                                    //Found the ladle
                                    lc.DestinationLocation = GetLocation(p.LocationID).LocationName; //Done
                                    lc.DestinationInDateTime = pdl.InTime; //Done
                                    lc.DestinationOutDateTime = pdl.OutTime; //Done

                                    //Populate WB Data...
                                    ESLLadleWeightData lwd = null;
                                    try
                                    {
                                        lwd = GetLadleWeighmentData(pdl.LadleNo, lc.SourceInDateTime, lc.DestinationInDateTime);
                                    }
                                    catch (Exception ex)
                                    {

                                    }


                                    if (lwd != null)
                                    {
                                        //Count the ladle only if it is weighed for the prodcution total.
                                        //Comented on 30-07-2022 
                                        //if (cLocationSource != null)
                                        //  cLocationSource.LadleCount = cLocationSource.LadleCount + 1;

                                        lc.SourceWeight = lwd.GrossWeight.ToString("000.00"); //Done
                                        lc.SourceWeightDateTime = lwd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss"); //Done

                                        lc.TareWeight = lwd.TareWeight.ToString("000.00"); //Done
                                        lc.TareWeightDateTime = lwd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss"); //Done

                                        lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm"); //Done

                                        lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime); //Done

                                        lc.NetWeight = lwd.NetWeight.ToString("000.00"); //Done

                                        if (cLocationSource != null)
                                        {
                                            //Commented on 30-07-2022
                                            //cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd.NetWeight;

                                            if (!cLocationSource.ProductionUnits.Exists(x => x.LocationID == p.LocationID))
                                            {
                                                ESLLocationConsolidate cprodLocation = new ESLLocationConsolidate();
                                                cprodLocation.LocationID = p.LocationID;
                                                cprodLocation.LocationName = p.LocationName;
                                                cprodLocation.TotalProduction = 0;
                                                cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                                cLocationSource.ProductionUnits.Add(cprodLocation);
                                                castLadlehash.Add(lwd.CastNumber + "|" + lc.LadleNo);
                                            }
                                            else
                                            {
                                                ESLLocationConsolidate cprodLocation = cLocationSource.ProductionUnits.Find(x => x.LocationID == p.LocationID);

                                                if (!castLadlehash.Contains(lwd.CastNumber + "|" + lc.LadleNo))
                                                {
                                                    cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                    cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                                    castLadlehash.Add(lwd.CastNumber + "|" + lc.LadleNo);
                                                }
                                            }
                                        }

                                        lc.CastNo = lwd.CastNumber;

                                        if (lc.CastNo != string.Empty)
                                        {
                                            //Fetch LiMS data..
                                            //lc.HasLIMSData = true;

                                            ESLLIMSData liData = null;

                                            if (!isDashboardCall)
                                            {
                                                if (_limsData.ContainsKey(lc.CastNo))
                                                {
                                                    liData = _limsData[lc.CastNo];
                                                }
                                                else
                                                {
                                                    ESLLIMSData ld = null;
                                                    try
                                                    {
                                                        ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        ld = null;
                                                    }
                                                    //ESLLIMSData ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                    if (ld != null)
                                                    {
                                                        _limsData.Add(lc.CastNo, ld);
                                                        liData = ld;
                                                    }
                                                }
                                                //liData = GetLIMSData(source.LocationID, lc.CastNo);
                                            }

                                            if (liData != null)
                                            {
                                                liData.LocationName = GetLocation(source.LocationID).LocationName;
                                                lc.LIMSData = liData;
                                                lc.HasLIMSData = true;

                                                if (!ladleTransactionSummry.LimsSummary.Exists(x => x.CastNo == lc.CastNo))
                                                {
                                                    ladleTransactionSummry.LimsSummary.Add(liData);
                                                }

                                            }
                                        }
                                    }


                                }
                                else
                                {
                                    //Did not find the ladle....
                                    //int jack = 500;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(lc.DestinationLocation))
                        {
                            //int jj = 500;
                            //Get The Weight from the Source time.
                            if (lc.SourceOutDateTime.Year != 1)
                            {
                                ESLLadleWeightData ewd = null;

                                try
                                {
                                    ewd = GetLadleWeighmentData(lc.LadleNo, lc.SourceOutDateTime);
                                }
                                catch (Exception ex)
                                {
                                    ewd = null;
                                }
                                //if(ewd != null)
                                //{
                                if (ewd != null)
                                {
                                    lc.SourceWeight = ewd.GrossWeight.ToString("000.00");
                                    lc.SourceWeightDateTime = ewd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss");

                                    lc.TareWeight = ewd.TareWeight.ToString("000.00");
                                    lc.TareWeightDateTime = ewd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss");

                                    //lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");


                                    lc.NetWeight = ewd.NetWeight.ToString("000.00");

                                    if (cLocationSource != null)
                                    {
                                        //cLocationSource.TotalProduction = cLocationSource.TotalProduction + ewd.NetWeight;
                                    }

                                    lc.CastNo = ewd.CastNumber;

                                    if (lc.CastNo != string.Empty)
                                    {
                                        //Fetch LiMS data..
                                        //lc.HasLIMSData = true;

                                        ESLLIMSData liData = null;

                                        if (!isDashboardCall)
                                        {
                                            if (_limsData.ContainsKey(lc.CastNo))
                                            {
                                                liData = _limsData[lc.CastNo];
                                            }
                                            else
                                            {
                                                ESLLIMSData ld = null;
                                                try
                                                {
                                                    ld = GetLIMSData(source.LocationID, lc.CastNo);
                                                }
                                                catch (Exception ex)
                                                {
                                                    ld = null;
                                                }
                                                //ESLLIMSData 
                                                if (ld != null)
                                                {
                                                    _limsData.Add(lc.CastNo, ld);
                                                    liData = ld;
                                                }

                                            }
                                            //liData = GetLIMSData(source.LocationID, lc.CastNo);
                                        }

                                        if (liData != null)
                                        {
                                            liData.LocationName = GetLocation(source.LocationID).LocationName;
                                            lc.LIMSData = liData;
                                            lc.HasLIMSData = true;

                                            if (!ladleTransactionSummry.LimsSummary.Exists(x => x.CastNo == lc.CastNo))
                                            {
                                                ladleTransactionSummry.LimsSummary.Add(liData);
                                            }

                                        }
                                    }
                                }
                            }

                            //}
                        }

                        ladleTransactionSummry.LadleTransactionDetails.Add(lc);

                        if (lc.SourceOutDateTime.Subtract(lc.SourceInDateTime).TotalMinutes < 30)
                        {
                            //Get the Actual Source In Time due to boundry condition....
                            ESLLocation loc = GetLocation(lc.SourceLocation);
                            ESLLadle lad = GetLadleByID(lc.LadleNo);
                            DateTime lastSourceEntryDatetime;
                            short lastLocationID = 0;

                            if (loc != null && lad != null)
                            {
                                if (GetBoundryConditionSourceInTime(lc.SourceInDateTime, loc.LocationID.ToString(), lad.SerialNo, out lastSourceEntryDatetime, out lastLocationID))
                                {
                                    if (lastLocationID == loc.LocationID)
                                    {
                                        lc.SourceOutDateTime = lc.SourceInDateTime;
                                        lc.SourceInDateTime = lastSourceEntryDatetime;
                                        //Update TAT as new New Source...
                                        if (!string.IsNullOrEmpty(lc.DestinationLocation))
                                        {
                                            lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");
                                            lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);
                                        }
                                    }
                                }

                            }
                            int k = 0;
                        }

                    }
                }*/



                int x1 = 50;

            }
            catch (Exception ex)
            {
                int j = 0;
                throw ex;
            }
            finally
            {

            }
            return ladleTransactionSummry;

        }

        public ESLLadleTransactionSummary GetTransactionSummaryPlusLims(DateTime fromDate, DateTime toDate)
        {
            //bool retValue = false;
            DateTime fromDateTime;
            DateTime toDateTime;
            if (fromDate.Equals(toDate))
            {
                //Dashboard Calll
                if (toDate.Hour >= 22)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                }
            }
            else
            {
                if (fromDate.Year == toDate.Year && fromDate.Day == toDate.Day && fromDate.Month == toDate.Month)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                    //fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                }
                else
                {
                    fromDateTime = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                }
            }

            ESLLadleTransactionSummary ladleTransactionSummry = null;

            try
            {
                DateTime qfromDate = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

                List<ReadersTransactionLog> logs = GetSourceReaderTrsactionLogForDateTime(qfromDate, qToDate);

                List<ESLLocation> _sourceLocationData = new List<ESLLocation>();
                Dictionary<string, ESLLadle> _currentSourceLadles = new Dictionary<string, ESLLadle>();
                Dictionary<string, ESLLadle> _currentAllLadlesLastLocation = new Dictionary<string, ESLLadle>(); //Stores the last Location and Time of the Ladle for further reference...
                string successMessage = string.Empty;
                string errorMessages = string.Empty;
                int TXNO = 0;

                for (int i = 0; i < logs.Count; i++)
                {
                    ReadersTransactionLog l = logs[i];
                    if (l.LocationID == 1)
                    {
                        //BF2
                        if (!_sourceLocationData.Exists(x => x.LocationID == 1))
                        {
                            ESLLocation bf2 = new ESLLocation();
                            bf2.LocationID = 1;
                            bf2.LocationName = "BF2";
                            _sourceLocationData.Add(bf2);
                        }
                    }
                    else if (l.LocationID == 2)
                    {
                        //BF3
                        if (!_sourceLocationData.Exists(x => x.LocationID == 2))
                        {
                            ESLLocation bf3 = new ESLLocation();
                            bf3.LocationID = 2;
                            bf3.LocationName = "BF3";
                            _sourceLocationData.Add(bf3);
                        }
                    }


                    //Get Source Location Collection..
                    ESLLocation currentLocationObj = null;

                    foreach (ESLLocation l1 in _sourceLocationData)
                    {
                        if (l1.LocationID == l.LocationID)
                        {
                            currentLocationObj = l1;
                            break;
                        }
                    }

                    if (currentLocationObj != null)
                    {
                        //Enteres when the Ladle is found at the source location.
                        //Transaction ID for this Ladle Needs to be created...
                        //Set the State to 0....
                        //Check if the Ladle Exists and update its Time Slot...
                        //This Section is only for SOURCE Ladles....
                        ESLLadle currentladle = null;
                        foreach (ESLLadle ld in currentLocationObj.LadleList)
                        {

                            if (ld.SerialNo == l.AssetSerialNo.ToString() && ld.State != 3)
                            {
                                currentladle = ld;
                            }
                        }

                        if (currentladle == null)
                        {
                            //New Laddle tobe added
                            currentladle = new ESLLadle();
                            currentladle.ID = Guid.NewGuid(); //New TX ID for this ladle....
                            currentladle.TXNo = ++TXNO;
                            currentladle.CurrentLocationID = l.LocationID;
                            currentladle.Direction = "IN";
                            currentladle.InTime = l.ServerDateTime;
                            currentladle.SerialNo = l.AssetSerialNo.ToString();
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            currentladle.LadleNo = lad.LadleNo;
                            currentladle.LastLocationID = l.LocationID;
                            currentladle.State = 0; //New Loading State.
                            currentLocationObj.LadleList.Add(currentladle);
                            //Stores the Latest Source Ladles in the Dictionary...
                            if (!_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                _currentSourceLadles.Add(currentladle.LadleNo, currentladle);

                            //Stores the Last Location of the ladle in the Dictionary....
                            if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = currentladle.LadleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                            }
                            else
                            {
                                if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                {
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                    _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                }
                            }
                        }
                        else
                        {
                            //Check the last location of the ladle if its not as current its a new tramsaction..
                            int curladlelastLocation = 0;
                            if (_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                            {
                                curladlelastLocation = _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID;
                            }

                            if (l.LocationID != curladlelastLocation)
                            {
                                errorMessages = errorMessages + "\r\n Found Ladle from One Source to Other Missing Transactions...\r\n";
                                //Insert New 
                                currentladle = new ESLLadle();
                                currentladle.ID = Guid.NewGuid();
                                currentladle.TXNo = ++TXNO;
                                currentladle.CurrentLocationID = l.LocationID;
                                currentladle.Direction = "IN";
                                currentladle.InTime = l.ServerDateTime;
                                currentladle.SerialNo = l.AssetSerialNo.ToString();
                                ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                                if (lad == null)
                                    continue;
                                currentladle.LadleNo = lad.LadleNo;
                                currentladle.LastLocationID = l.LocationID;
                                currentLocationObj.LadleList.Add(currentladle);
                                if (!_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                    _currentSourceLadles.Add(currentladle.LadleNo, currentladle);
                                else
                                    _currentSourceLadles[currentladle.LadleNo] = currentladle;

                                if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                {
                                    ESLLadle curLocLadle = new ESLLadle();
                                    curLocLadle.LastLocationID = l.LocationID;
                                    curLocLadle.LadleNo = currentladle.LadleNo;
                                    curLocLadle.InTime = l.ServerDateTime;
                                    _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                }
                                else
                                {
                                    if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                    }
                                    else
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                    }
                                }
                            }
                            else if (l.LocationID == curladlelastLocation)
                            {
                                //Update In time.
                                //if (l.ServerDateTime.Subtract(currentladle.InTime).TotalMinutes > 25)
                                //{
                                currentladle.OutTime = l.ServerDateTime; // Mark as Out Time...
                                                                         //}
                                                                         //else
                                                                         //{
                                                                         //  currentladle.InTime = l.ServerDateTime; //Found Multiple Entries for the source at short duration...
                                                                         //errorMessages = errorMessages + "\r\n Found Laddle at other Source without Intermedia Transactions.\r\n";
                                                                         //}

                                if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                {
                                    _currentSourceLadles[currentladle.LadleNo] = currentladle;
                                }

                                if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                {
                                    ESLLadle curLocLadle = new ESLLadle();
                                    curLocLadle.LastLocationID = l.LocationID;
                                    curLocLadle.LadleNo = currentladle.LadleNo;
                                    curLocLadle.InTime = l.ServerDateTime;
                                    _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                }
                                else
                                {
                                    if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                    }
                                    else
                                    {
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                        _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if ((l.LocationID == 4) || (l.LocationID == 5) || (l.LocationID == 6))
                        {
                            //Identified as Production Units.
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            string LadleNo = lad.LadleNo;
                            if (_currentSourceLadles.ContainsKey(LadleNo))
                            {
                                ESLLadle recLadle = _currentSourceLadles[LadleNo];
                                if (recLadle != null && recLadle.State != 3)
                                {

                                    foreach (ESLLocation l1 in _sourceLocationData)
                                    {
                                        if (l1.LocationID == recLadle.LastLocationID)
                                        {
                                            currentLocationObj = l1;
                                            break;
                                        }
                                    }


                                }


                                //currentLocationObj.LadleList.Add(currentladle);
                                //Check if the production unit exist and add to the production unit....
                                ESLLocation ladleLoc = GetLocation(l.LocationID);
                                if (currentLocationObj.ProductionUnits.Exists(x => x.LocationID == l.LocationID))
                                {
                                    ESLLocation existingProdLocation = null;
                                    foreach (ESLLocation el in currentLocationObj.ProductionUnits)
                                    {
                                        if (el.LocationID == l.LocationID)
                                        {
                                            existingProdLocation = el;
                                            break;
                                        }
                                    }

                                    if (existingProdLocation != null) //Addtional Check....
                                    {
                                        ESLLadle currentladle = new ESLLadle();
                                        currentladle.CurrentLocationID = l.LocationID;
                                        currentladle.Direction = "IN";
                                        currentladle.InTime = l.ServerDateTime;
                                        currentladle.SerialNo = l.AssetSerialNo.ToString();
                                        ESLLadle lad1 = GetLadleByID(l.AssetSerialNo);
                                        if (lad1 == null)
                                            continue;
                                        currentladle.LadleNo = lad1.LadleNo;
                                        currentladle.LastLocationID = l.LocationID;
                                        if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                        {
                                            currentladle.ID = _currentSourceLadles[currentladle.LadleNo].ID;
                                            currentladle.TXNo = _currentSourceLadles[currentladle.LadleNo].TXNo;
                                        }
                                        else
                                        {
                                            //Do Nothing...
                                        }

                                        if (existingProdLocation.LadleList.Exists(x => x.ID == currentladle.ID))
                                        {
                                            foreach (ESLLadle pl in existingProdLocation.LadleList)
                                            {
                                                if (pl.ID == currentladle.ID)
                                                {
                                                    pl.OutTime = l.ServerDateTime;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            existingProdLocation.LadleList.Add(currentladle);
                                        }

                                        if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                        {
                                            ESLLadle curLocLadle = new ESLLadle();
                                            curLocLadle.LastLocationID = l.LocationID;
                                            curLocLadle.LadleNo = currentladle.LadleNo;
                                            curLocLadle.InTime = l.ServerDateTime;
                                            _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                        }
                                        else
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                        }

                                        //_currentAllLadles.Add(currentladle.LadleNo, currentladle);
                                    }
                                }
                                else
                                {
                                    //Add Location and Then the Ladle....
                                    ESLLocation proUnit = new ESLLocation();
                                    proUnit.LocationID = (short)l.LocationID;
                                    proUnit.LocationName = GetLocation(l.LocationID).LocationName;

                                    ESLLadle currentladle = new ESLLadle();
                                    currentladle.CurrentLocationID = l.LocationID;
                                    currentladle.Direction = "IN";
                                    currentladle.InTime = l.ServerDateTime;
                                    currentladle.SerialNo = l.AssetSerialNo.ToString();
                                    ESLLadle lad1 = GetLadleByID(l.AssetSerialNo);
                                    if (lad1 == null)
                                        continue;
                                    currentladle.LadleNo = lad1.LadleNo;
                                    currentladle.LastLocationID = l.LocationID;

                                    if (_currentSourceLadles.ContainsKey(currentladle.LadleNo))
                                    {
                                        currentladle.ID = _currentSourceLadles[currentladle.LadleNo].ID;
                                        currentladle.TXNo = _currentSourceLadles[currentladle.LadleNo].TXNo;
                                    }

                                    proUnit.LadleList.Add(currentladle);
                                    currentLocationObj.ProductionUnits.Add(proUnit);

                                    if (!_currentAllLadlesLastLocation.ContainsKey(currentladle.LadleNo))
                                    {
                                        ESLLadle curLocLadle = new ESLLadle();
                                        curLocLadle.LastLocationID = l.LocationID;
                                        curLocLadle.LadleNo = currentladle.LadleNo;
                                        curLocLadle.InTime = l.ServerDateTime;
                                        _currentAllLadlesLastLocation.Add(curLocLadle.LadleNo, curLocLadle);
                                    }
                                    else
                                    {
                                        if (_currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID == currentladle.LastLocationID)
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].OutTime = l.ServerDateTime;
                                        }
                                        else
                                        {
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].LastLocationID = currentladle.LastLocationID;
                                            _currentAllLadlesLastLocation[currentladle.LadleNo].InTime = l.ServerDateTime;
                                        }
                                    }
                                }

                            }


                        }
                        else if (l.LocationID == 3)
                        {
                            //Weighment Information...
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            string ladleNo = lad.LadleNo;
                            if (!_currentAllLadlesLastLocation.ContainsKey(ladleNo))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = ladleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(ladleNo, curLocLadle);
                            }
                            else
                            {
                                //_currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                //_currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                if (_currentAllLadlesLastLocation[ladleNo].LastLocationID == l.LocationID)
                                {
                                    _currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                    _currentAllLadlesLastLocation[ladleNo].InTime = l.ServerDateTime;
                                }
                            }
                            //if()
                        }
                        else if (l.LocationID == 7)
                        {
                            //LRS.....
                            ESLLadle lad = GetLadleByID(l.AssetSerialNo);
                            if (lad == null)
                                continue;
                            string ladleNo = lad.LadleNo;
                            if (!_currentAllLadlesLastLocation.ContainsKey(ladleNo))
                            {
                                ESLLadle curLocLadle = new ESLLadle();
                                curLocLadle.LastLocationID = l.LocationID;
                                curLocLadle.LadleNo = ladleNo;
                                curLocLadle.InTime = l.ServerDateTime;
                                _currentAllLadlesLastLocation.Add(ladleNo, curLocLadle);
                            }
                            else
                            {
                                if (_currentAllLadlesLastLocation[ladleNo].LastLocationID == l.LocationID)
                                {
                                    _currentAllLadlesLastLocation[ladleNo].OutTime = l.ServerDateTime;
                                }
                                else
                                {
                                    _currentAllLadlesLastLocation[ladleNo].LastLocationID = l.LocationID;
                                    _currentAllLadlesLastLocation[ladleNo].InTime = l.ServerDateTime;
                                }
                            }

                            //Also Set the Property of the source ladles as completed.
                        }

                    }

                }

                ladleTransactionSummry = new ESLLadleTransactionSummary();

                int internalSerialNumber = 0;

                foreach (ESLLocation source in _sourceLocationData)
                {
                    ESLLocationConsolidate cLocationSource = null;

                    if (!ladleTransactionSummry.ProductionSummary.Exists(x => x.LocationID == source.LocationID))
                    {
                        cLocationSource = new ESLLocationConsolidate();
                        cLocationSource.LocationID = source.LocationID;
                        cLocationSource.LocationName = GetLocation(source.LocationID).LocationName;
                        cLocationSource.TotalProduction = 0;
                        ladleTransactionSummry.ProductionSummary.Add(cLocationSource);
                    }


                    foreach (ESLLadle sl in source.LadleList)
                    {
                        //Prepare ladle Consolidated
                        ESLLadleConsolidated lc = new ESLLadleConsolidated();
                        lc.SerialNumber = ++internalSerialNumber;
                        lc.TXNo = sl.TXNo;
                        lc.SourceLocation = GetLocation(sl.LastLocationID).LocationName;
                        lc.SourceInDateTime = sl.InTime;
                        lc.SourceOutDateTime = sl.OutTime;
                        lc.LadleNo = sl.LadleNo;
                        if (cLocationSource != null)
                            cLocationSource.LadleCount = cLocationSource.LadleCount + 1;

                        //Find the Ladle in Destination...
                        foreach (ESLLocation p in source.ProductionUnits)
                        {
                            foreach (ESLLadle pdl in p.LadleList)
                            {
                                if (pdl.TXNo == sl.TXNo && pdl.LadleNo == sl.LadleNo)
                                {
                                    //Found the ladle
                                    lc.DestinationLocation = GetLocation(p.LocationID).LocationName;
                                    lc.DestinationInDateTime = pdl.InTime;
                                    lc.DestinationOutDateTime = pdl.OutTime;
                                    //Populate WB Data...
                                    ESLLadleWeightData lwd = null;
                                    try
                                    {
                                        lwd = GetLadleWeighmentData(pdl.LadleNo, lc.SourceInDateTime, lc.DestinationInDateTime);
                                    }
                                    catch (Exception ex)
                                    {
                                        lwd = null;
                                    }
                                    if (lwd != null)
                                    {
                                        lc.SourceWeight = lwd.GrossWeight.ToString("000.00");
                                        lc.SourceWeightDateTime = lwd.Gross_DT_Time.ToString("yyyy-MM-dd H:mm:ss");

                                        lc.TareWeight = lwd.TareWeight.ToString("000.00");
                                        lc.TareWeightDateTime = lwd.TareDateTime.ToString("yyyy-MM-dd H:mm:ss");

                                        lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");

                                        lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);


                                        lc.NetWeight = lwd.NetWeight.ToString("000.00");

                                        if (cLocationSource != null)
                                        {
                                            cLocationSource.TotalProduction = cLocationSource.TotalProduction + lwd.NetWeight;

                                            if (!cLocationSource.ProductionUnits.Exists(x => x.LocationID == p.LocationID))
                                            {
                                                ESLLocationConsolidate cprodLocation = new ESLLocationConsolidate();
                                                cprodLocation.LocationID = p.LocationID;
                                                cprodLocation.LocationName = p.LocationName;
                                                cprodLocation.TotalProduction = 0;
                                                cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                                cLocationSource.ProductionUnits.Add(cprodLocation);
                                            }
                                            else
                                            {
                                                ESLLocationConsolidate cprodLocation = cLocationSource.ProductionUnits.Find(x => x.LocationID == p.LocationID);
                                                cprodLocation.TotalProduction = cprodLocation.TotalProduction + lwd.NetWeight;
                                                cprodLocation.LadleCount = cprodLocation.LadleCount + 1;
                                            }
                                        }

                                        lc.CastNo = lwd.CastNumber;

                                        if (lc.CastNo != string.Empty)
                                        {
                                            //Fetch LiMS data..
                                            //lc.HasLIMSData = true;

                                            ESLLIMSData liData = null;

                                            //liData = GetLIMSData(source.LocationID, lc.CastNo);

                                            if (liData != null)
                                            {
                                                liData.LocationName = GetLocation(source.LocationID).LocationName;
                                                lc.LIMSData = liData;
                                                lc.HasLIMSData = true;

                                                if (!ladleTransactionSummry.LimsSummary.Exists(x => x.CastNo == lc.CastNo))
                                                {
                                                    ladleTransactionSummry.LimsSummary.Add(liData);
                                                }

                                            }
                                        }
                                    }


                                }
                            }
                        }

                        ladleTransactionSummry.LadleTransactionDetails.Add(lc);

                    }
                }



                int x1 = 50;

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {

            }
            return ladleTransactionSummry;

        }


        public ESLLadleTransactionSummary GetTransactionSummaryForLadle(DateTime fromDate, DateTime toDate, string ladleNo)
        {
            //bool retValue = false;

            ESLLadleTransactionSummary ladleTransactionSummry = null;

            try
            {
                DateTime qfromDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

                ESLLadle li = GetAllLadles().Find(x => x.LadleNo == ladleNo);

                if (li == null)
                    return null;

                ResolveCurrentLadlePathWithDateRange(qfromDate, qToDate, li);

                int x1 = 60;


                ladleTransactionSummry = new ESLLadleTransactionSummary();

                int x2 = 50;

            }
            catch
            {

            }
            finally
            {

            }
            return ladleTransactionSummry;

        }

        /// <summary>
        /// Method Used to Assign Ladles....
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public bool AssignLadles(List<ESLLadleAssignment> list)
        {
            List<ESLLadleAssignment> finalList = new List<ESLLadleAssignment>();
            foreach (ESLLadleAssignment la in list)
            {
                if (la.AssignedProductionUit != string.Empty)
                {
                    la.ID = Guid.NewGuid().ToString();
                    finalList.Add(la);
                }
            }
            return InsertLadleAssgnments(finalList);
        }

        /// <summary>
        /// To Get The Assigned Ladles from the Database....
        /// </summary>
        /// <returns></returns>
        List<ESLLadleAssignment> GetAssignedLadles()
        {
            List<ESLLadleAssignment> retValue = new List<ESLLadleAssignment>();
            return retValue;
        }

        public ESLLadlePathSummary GetLadleDataforDateRange(DateTime fromDate, DateTime toDate, string ladleNo)
        {

            if (ladleNo == "0")
            {
                ESLLadlePathSummary allLadlesPathSummary = null;

                bool isDashboardCall = false;

                DateTime fromDateTime;

                if (fromDate.Equals(toDate))
                {
                    //Dashboard Calll
                    isDashboardCall = true;
                    if (toDate.Hour >= 22)
                    {
                        fromDateTime = toDate.Subtract(TimeSpan.FromDays(0));
                    }
                    else
                    {
                        fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                    }
                }
                else
                {
                    if (fromDate.Year == toDate.Year && fromDate.Day == toDate.Day && fromDate.Month == toDate.Month)
                    {
                        fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                        //fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                    }
                    else
                    {
                        fromDateTime = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                    }
                }

                DateTime qfromDate = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

                List<string> allLadles = GetAllLadlesInUse(qfromDate, qToDate);
                allLadlesPathSummary = new ESLLadlePathSummary();
                foreach (string lad in allLadles)
                {
                    ESLLadlePathSummary pathsum = GetLadleDataforDateRangeInternal(fromDate, toDate, lad);
                    if (allLadlesPathSummary.LadleTransactionDetails == null)
                        allLadlesPathSummary.LadleTransactionDetails = new List<ESLLadleConsolidated>();
                    if (pathsum != null)
                    {
                        allLadlesPathSummary.LadleTransactionDetails.AddRange(pathsum.LadleTransactionDetails);
                        allLadlesPathSummary.TotalTonnage = allLadlesPathSummary.TotalTonnage + pathsum.TotalTonnage;
                    }
                }
                allLadlesPathSummary.ExportString = ToClassString(allLadlesPathSummary.LadleTransactionDetails);
                //allLadlesPathSummary.LadleTransactionDetails.Sort();
                return allLadlesPathSummary;
            }
            else
            {
                return GetLadleDataforDateRangeInternal(fromDate, toDate, ladleNo);
            }

        }

        public ESLLadlePathSummary GetLadleDataforDateRangeInternal(DateTime fromDate, DateTime toDate, string ladleNo)
        {
            //return null;
            bool retValue = false;
            int lastLocationID = 0;
            int lastTouchPointID = 0;
            string lastTouchPointType = string.Empty;
            DateTime lastDateTime = DateTime.Now;
            bool directionIdentified = false;

            ESLLadlePathSummary summary = new ESLLadlePathSummary();

            ESLLadleConsolidated currentLadleConsolidate = null;

            int internalSerialNumber = 0;

            decimal totalTonnage = 0;
            bool isDashboardCall = false;
            DateTime fromDateTime;

            if (fromDate.Equals(toDate))
            {
                //Dashboard Calll
                isDashboardCall = true;
                if (toDate.Hour >= 22)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                }
            }
            else
            {
                if (fromDate.Year == toDate.Year && fromDate.Day == toDate.Day && fromDate.Month == toDate.Month)
                {
                    fromDateTime = toDate.Subtract(TimeSpan.FromDays(1));
                    //fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                }
                else
                {
                    fromDateTime = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day, 22, 00, 00);
                }
            }

            DateTime qfromDate = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
            DateTime qToDate = new DateTime(toDate.Year, toDate.Month, toDate.Day, 21, 59, 59);

            //bool addPath = false;
            //direction = "IN";
            try
            {
                ESLLadle ladle = _ladles.Find(x => x.LadleNo == ladleNo);

                if (ladle == null)
                    return null;

                long serialNo = Int64.Parse(ladle.SerialNo);

                List<ReadersTransactionLog> trans = GetSourceReaderTrsactionLogForDateTime(qfromDate, qToDate, serialNo.ToString());

                if (trans != null)
                {
                    for (int i = 0; i < trans.Count; i++)
                    {
                        ReadersTransactionLog l = trans[i];

                        if ((l.LocationID == 1 || l.LocationID == 2) && (l.LocationID != lastLocationID))
                        {
                            //Found Source Transaction....
                            currentLadleConsolidate = new ESLLadleConsolidated();
                            summary.LadleTransactionDetails.Add(currentLadleConsolidate);
                        }

                        if (l != null)
                        {
                            if (i == 0)
                            {
                                lastLocationID = l.LocationID;
                                lastTouchPointID = l.TouchPointID;
                                lastTouchPointType = l.TouchPointType;
                                lastDateTime = l.ServerDateTime;
                                ladle.InTime = l.ServerDateTime;
                                ladle.Direction = "IN";
                                ladle.CurrentLocationID = l.LocationID;
                                ladle.CurrentTouchpointID = l.TouchPointID;
                                ladle.InTime = l.ServerDateTime;
                                ESLPath p = new ESLPath();
                                p.Direction = "IN";
                                p.LocationID = l.LocationID;
                                p.LocationName = GetLocation(l.LocationID).LocationName;
                                p.INTouchPointID = l.TouchPointID;
                                p.INTransactionDateTime = l.ServerDateTime;
                                if (GetLocationType(l.LocationID) == LocationType.Weight)
                                {
                                    //p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                }
                                ladle.Paths.Add(p);

                                directionIdentified = true;

                                if (currentLadleConsolidate != null && (l.LocationID == 1 || l.LocationID == 2))
                                {
                                    //currentLadleConsolidate.SourceLocation = l.LocationID;
                                    currentLadleConsolidate.SerialNumber = ++internalSerialNumber;
                                    currentLadleConsolidate.TXNo = internalSerialNumber;
                                    currentLadleConsolidate.SourceLocation = GetLocation(l.LocationID).LocationName;
                                    currentLadleConsolidate.SourceInDateTime = l.ServerDateTime;
                                    currentLadleConsolidate.SourceOutDateTime = l.ServerDateTime;
                                    currentLadleConsolidate.LadleNo = ladle.LadleNo;

                                    ESLLadleWeightData lwd = GetLadleWeighmentData(ladle.LadleNo, l.ServerDateTime);

                                    if (lwd != null)
                                    {
                                        currentLadleConsolidate.SourceWeight = lwd.GrossWeight.ToString("000.00");
                                        currentLadleConsolidate.SourceWeightDateTime = lwd.Gross_DT_Time.ToString("yyyy-MM-dd hh:mm:ss");
                                        currentLadleConsolidate.TareWeight = lwd.TareWeight.ToString("000.00");
                                        currentLadleConsolidate.TareWeightDateTime = lwd.TareDateTime.ToString("yyyy-MM-dd hh:mm:ss");
                                        currentLadleConsolidate.NetWeight = lwd.NetWeight.ToString("000.00");
                                        currentLadleConsolidate.CastNo = lwd.CastNumber;
                                        totalTonnage = totalTonnage + lwd.NetWeight;

                                    }

                                }

                            }
                            else
                            {
                                int currentLocationID = l.LocationID;
                                int currentTouchpointID = l.TouchPointID;


                                if (l.ServerDateTime.Subtract(lastDateTime).TotalMinutes > LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES) //10 Minutest
                                {
                                    if (currentLocationID == lastLocationID)
                                    {
                                        //This is a direction indication 
                                        ladle.Direction = "OUT";
                                        ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;
                                        ladle.LastLocationID = l.LocationID;
                                        ladle.LastTouchpointID = l.TouchPointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        directionIdentified = true;
                                        if (currentLadleConsolidate != null && (currentLocationID == 1 || currentLocationID == 2))
                                            currentLadleConsolidate.SourceOutDateTime = l.ServerDateTime;

                                        if (currentLocationID != 1 && currentLocationID != 2 && currentLadleConsolidate != null)
                                        {
                                            if (currentLadleConsolidate.Destinations != null && currentLadleConsolidate.Destinations.Count > 0)
                                            {


                                                ESLLadleDestination dest1 = currentLadleConsolidate.Destinations[currentLadleConsolidate.Destinations.Count - 1];
                                                if (dest1 != null)
                                                {
                                                    if (dest1.DestinationLocation == GetLocation(currentLocationID).LocationName)
                                                        dest1.DestinationOutDateTime = l.ServerDateTime;
                                                }

                                                // currentLadleConsolidate.Destinations.Add(dest1);
                                            }

                                        }
                                        //Get The Esisting Path to Update the Out Time and details

                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);

                                        if (p != null)
                                        {
                                            p.Direction = "OUT";
                                            p.LocationID = l.LocationID;
                                            p.OUTTouchPointID = l.TouchPointID;
                                            p.OUTTransactionDateTime = l.ServerDateTime;
                                        }
                                        //ladle.Paths.Add(p);
                                        //break;
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        //if(ladle.Direction != "OUT")
                                        if (currentLocationID != 1 && currentLocationID != 2 && currentLadleConsolidate != null)
                                        {
                                            ESLLadleDestination dest1 = new ESLLadleDestination();
                                            dest1.DestinationLocation = GetLocation(l.LocationID).LocationName;
                                            dest1.DestinationInDateTime = l.ServerDateTime;
                                            dest1.DestinationOutDateTime = l.ServerDateTime;

                                            ESLLadleWeightData lwd = null;

                                            if (currentLadleConsolidate.Destinations.Count == 0)
                                            {
                                                lwd = GetLadleWeighmentData(currentLadleConsolidate.LadleNo, currentLadleConsolidate.SourceOutDateTime, l.ServerDateTime);
                                            }
                                            else
                                            {
                                                lwd = GetLadleWeighmentData(ladleNo, currentLadleConsolidate.Destinations[currentLadleConsolidate.Destinations.Count - 1].DestinationOutDateTime, l.ServerDateTime);
                                            }

                                            if (lwd != null)
                                            {
                                                dest1.GrossWeight = lwd.GrossWeight.ToString("000.00");
                                                dest1.GrossWeightDateTime = lwd.GrossDateTime;
                                                dest1.TareWeight = lwd.TareWeight.ToString("000.00");
                                                dest1.TareWeightDateTime = lwd.TareDateTime;
                                                dest1.TAT = "0";
                                            }

                                            currentLadleConsolidate.Destinations.Add(dest1);

                                        }
                                        else if (currentLocationID == 1 || currentLocationID == 2 && currentLadleConsolidate != null)
                                        {
                                            currentLadleConsolidate.SerialNumber = ++internalSerialNumber;
                                            currentLadleConsolidate.TXNo = internalSerialNumber;
                                            currentLadleConsolidate.SourceLocation = GetLocation(l.LocationID).LocationName;
                                            currentLadleConsolidate.SourceInDateTime = l.ServerDateTime;
                                            currentLadleConsolidate.SourceOutDateTime = l.ServerDateTime;
                                            currentLadleConsolidate.LadleNo = ladle.LadleNo;

                                            ESLLadleWeightData lwd = GetLadleWeighmentData(ladle.LadleNo, l.ServerDateTime);

                                            if (lwd != null)
                                            {
                                                currentLadleConsolidate.SourceWeight = lwd.GrossWeight.ToString("000.00");
                                                currentLadleConsolidate.SourceWeightDateTime = lwd.Gross_DT_Time.ToString("yyyy-MM-dd hh:mm:ss");
                                                currentLadleConsolidate.TareWeight = lwd.TareWeight.ToString("000.00");
                                                currentLadleConsolidate.TareWeightDateTime = lwd.TareDateTime.ToString("yyyy-MM-dd hh:mm:ss");
                                                currentLadleConsolidate.NetWeight = lwd.NetWeight.ToString("000.00");
                                                currentLadleConsolidate.CastNo = lwd.CastNumber;
                                                totalTonnage = totalTonnage + lwd.NetWeight;
                                            }
                                        }

                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            //p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }
                                        ladle.Paths.Add(p);

                                    }
                                }
                                else
                                {
                                    //Less then 1 Minute repeat Transaction..
                                    if (currentLocationID == lastLocationID)
                                    {
                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        //p.OUTTouchPointID = l.TouchPointID;
                                        //p.OUTTransactionDateTime = l.ServerDateTime;

                                        //This is a direction indication 
                                        ladle.Direction = "IN";
                                        //ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            //p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }

                                        if (currentLocationID != 1 && currentLocationID != 2 && currentLadleConsolidate != null)
                                        {
                                            if (currentLadleConsolidate.Destinations != null && currentLadleConsolidate.Destinations.Count > 0)
                                            {


                                                ESLLadleDestination dest1 = currentLadleConsolidate.Destinations[currentLadleConsolidate.Destinations.Count - 1];
                                                if (dest1 != null)
                                                {
                                                    if (dest1.DestinationLocation == GetLocation(currentLocationID).LocationName)
                                                        dest1.DestinationOutDateTime = l.ServerDateTime;
                                                }

                                                // currentLadleConsolidate.Destinations.Add(dest1);
                                            }

                                        }

                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        ladle.Direction = "IN";
                                        ladle.LastLocationID = currentLocationID;
                                        ladle.LastTouchpointID = currentTouchpointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;
                                        directionIdentified = true;
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        ladle.Paths.Add(p);

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            //p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }

                                        if (currentLocationID != 1 && currentLocationID != 2 && currentLadleConsolidate != null)
                                        {
                                            ESLLadleDestination dest1 = new ESLLadleDestination();
                                            dest1.DestinationLocation = GetLocation(l.LocationID).LocationName;
                                            dest1.DestinationInDateTime = l.ServerDateTime;
                                            dest1.DestinationOutDateTime = l.ServerDateTime;
                                            ESLLadleWeightData lwd = null;
                                            if (currentLadleConsolidate.Destinations.Count == 0)
                                            {
                                                lwd = GetLadleWeighmentData(currentLadleConsolidate.LadleNo, currentLadleConsolidate.SourceOutDateTime, l.ServerDateTime);
                                            }
                                            else
                                            {
                                                lwd = GetLadleWeighmentData(ladleNo, currentLadleConsolidate.Destinations[currentLadleConsolidate.Destinations.Count - 1].DestinationOutDateTime, l.ServerDateTime);
                                            }
                                            if (lwd != null)
                                            {
                                                dest1.GrossWeight = lwd.GrossWeight.ToString("000.00");
                                                dest1.GrossWeightDateTime = lwd.GrossDateTime;
                                                dest1.TareWeight = lwd.TareWeight.ToString("000.00");
                                                dest1.TareWeightDateTime = lwd.TareDateTime;
                                                dest1.TAT = "0";
                                            }

                                            currentLadleConsolidate.Destinations.Add(dest1);

                                        }

                                        //break;
                                    }
                                }


                            }

                            lastLocationID = l.LocationID;
                            lastTouchPointID = l.TouchPointID;
                        }

                        /*if (currentLadleConsolidate != null)
                            summary.LadleTransactionDetails.Add(currentLadleConsolidate);*/
                    }
                }

                if (directionIdentified)
                    retValue = true;
                else
                    retValue = false;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                retValue = false;
            }

            string outputStringObject = string.Empty;

            foreach (ESLLadleConsolidated lc in summary.LadleTransactionDetails)
            {
                int cnt = lc.Destinations.Count;

                if (cnt != 0)
                {
                    lc.DestinationInDateTime = lc.Destinations[cnt - 1].DestinationInDateTime;
                    lc.DestinationLocation = lc.Destinations[cnt - 1].DestinationLocation;
                    lc.DestinationOutDateTime = lc.Destinations[cnt - 1].DestinationOutDateTime;
                }


            }

            for (int i = 0; i < summary.LadleTransactionDetails.Count; i++)
            {
                ESLLadleConsolidated lc = summary.LadleTransactionDetails[i];

                if (i < (summary.LadleTransactionDetails.Count - 1))
                {
                    ESLLadleConsolidated lc1 = summary.LadleTransactionDetails[i + 1];
                    lc.TAT = lc1.SourceInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");
                    lc.TATValue = lc1.SourceInDateTime.Subtract(lc.SourceInDateTime);
                }

                if (lc.Destinations != null)
                {
                    for (int j = 0; j < lc.Destinations.Count; j++)
                    {
                        ESLLadleDestination dest = lc.Destinations[j];
                        if (j < (lc.Destinations.Count - 1))
                        {
                            ESLLadleDestination dest1 = lc.Destinations[j + 1];
                            if (dest.DestinationInDateTimeSTR != string.Empty && dest1.DestinationInDateTimeSTR != string.Empty)
                                dest.TAT = dest1.DestinationInDateTime.Subtract(dest.DestinationInDateTime).ToString("d\\.hh\\:mm");
                        }
                    }
                }

                //Added Boundry Condition Code...
                if (lc.SourceOutDateTime.Subtract(lc.SourceInDateTime).TotalMinutes < 30)
                {
                    //Get the Actual Source In Time due to boundry condition....
                    ESLLocation loc = GetLocation(lc.SourceLocation);
                    ESLLadle lad = GetLadleByID(lc.LadleNo);
                    DateTime lastSourceEntryDatetime;
                    short lastLocationIDBoundry = 0;

                    if (loc != null && lad != null)
                    {
                        if (GetBoundryConditionSourceInTimeLadleReport(lc.SourceInDateTime, loc.LocationID.ToString(), lad.SerialNo, out lastSourceEntryDatetime, out lastLocationIDBoundry))
                        {
                            if (lastLocationIDBoundry == loc.LocationID)
                            {
                                lc.SourceOutDateTime = lc.SourceInDateTime;
                                lc.SourceInDateTime = lastSourceEntryDatetime;
                                //Update TAT as new New Source...
                                if (!string.IsNullOrEmpty(lc.DestinationLocation))
                                {
                                    lc.TAT = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime).ToString("d\\.hh\\:mm");
                                    lc.TATValue = lc.DestinationInDateTime.Subtract(lc.SourceInDateTime);
                                }
                            }
                        }

                    }
                    int k = 0;
                }


            }

            summary.TotalTonnageStr = totalTonnage.ToString("000.00");
            summary.TotalTonnage = totalTonnage;
            //string retValue23 =  Newtonsoft.Json.JsonConvert.SerializeObject(summary.LadleTransactionDetails);
            summary.ExportString = ToClassString(summary.LadleTransactionDetails);
            //object ret = ToClassString1(summary.LadleTransactionDetails);
            //summary.TotalTATSTR = 
            return summary;

        }


        public ESLLadlePathSummary GetLadleDataforDateRangeDEMO(DateTime fromDate, DateTime toDate, string ladleNo)
        {
            ESLLadlePathSummary summary = new ESLLadlePathSummary();

            ESLLadleConsolidated con1 = new ESLLadleConsolidated();
            con1.CastNo = "1234";
            con1.DestinationInDateTime = DateTime.Now.AddHours(5);
            con1.DestinationLocation = "SMS"; //SMS
            con1.DestinationOutDateTime = DateTime.Now.AddHours(4);
            con1.HasLIMSData = true;
            con1.HasMultipleDestination = true;
            con1.LadleNo = "1";
            con1.LIMSData = null;
            con1.NetWeight = "55.34";
            con1.SerialNumber = 1;
            con1.SourceInDateTime = DateTime.Now.Subtract(TimeSpan.FromMinutes(30));
            con1.SourceOutDateTime = DateTime.Now;
            con1.SourceWeight = "105.44";
            con1.SourceWeightDateTime = DateTime.Now.AddMinutes(10).ToLongDateString();
            con1.State = 1;
            con1.TareWeight = (105.44 - 55.34).ToString();
            con1.TareWeightDateTime = DateTime.Now.AddMinutes(10).ToLongDateString();
            con1.TXNo = 1;

            ESLLadleDestination dest1 = new ESLLadleDestination();
            dest1.DestinationLocation = "SMS";
            dest1.DestinationInDateTime = DateTime.Now;
            dest1.DestinationOutDateTime = DateTime.Now;
            dest1.GrossWeight = "105.4";
            dest1.GrossWeightDateTime = DateTime.Now;
            dest1.TareWeight = "50.45";
            dest1.TareWeightDateTime = DateTime.Now;
            dest1.TAT = "0";
            con1.Destinations.Add(dest1);

            ESLLadleDestination dest2 = new ESLLadleDestination();
            dest2.DestinationLocation = "DIP";
            dest2.DestinationInDateTime = DateTime.Now;
            dest2.DestinationOutDateTime = DateTime.Now;
            dest2.GrossWeight = "105.4";
            dest2.GrossWeightDateTime = DateTime.Now;
            dest2.TareWeight = "50.45";
            dest2.TareWeightDateTime = DateTime.Now;
            dest2.TAT = "0";

            con1.Destinations.Add(dest2);

            summary.LadleTransactionDetails.Add(con1);

            ESLLadleConsolidated con2 = new ESLLadleConsolidated();
            con2.CastNo = "6789";
            con2.DestinationInDateTime = DateTime.Now.AddHours(5);
            con2.DestinationLocation = "SMS"; //SMS
            con2.DestinationOutDateTime = DateTime.Now.AddHours(4);
            con2.HasLIMSData = true;
            con2.HasMultipleDestination = true;
            con2.LadleNo = "1";
            con2.LIMSData = null;
            con2.NetWeight = "55.34";
            con2.SerialNumber = 1;
            con2.SourceInDateTime = DateTime.Now.Subtract(TimeSpan.FromMinutes(30));
            con2.SourceOutDateTime = DateTime.Now;
            con2.SourceWeight = "105.44";
            con2.SourceWeightDateTime = DateTime.Now.AddMinutes(10).ToLongDateString();
            con2.State = 1;
            con2.TareWeight = (105.44 - 55.34).ToString();
            con2.TareWeightDateTime = DateTime.Now.AddMinutes(10).ToLongDateString();
            con2.TXNo = 2;

            dest1 = new ESLLadleDestination();
            dest1.DestinationLocation = "PCM";
            dest1.DestinationInDateTime = DateTime.Now;
            dest1.DestinationOutDateTime = DateTime.Now;
            dest1.GrossWeight = "105.4";
            dest1.GrossWeightDateTime = DateTime.Now;
            dest1.TareWeight = "50.45";
            dest1.TareWeightDateTime = DateTime.Now;
            dest1.TAT = "0";
            con2.Destinations.Add(dest1);

            dest2 = new ESLLadleDestination();
            dest2.DestinationLocation = "SMS";
            dest2.DestinationInDateTime = DateTime.Now;
            dest2.DestinationOutDateTime = DateTime.Now;
            dest2.GrossWeight = "105.4";
            dest2.GrossWeightDateTime = DateTime.Now;
            dest2.TareWeight = "50.45";
            dest2.TareWeightDateTime = DateTime.Now;
            dest2.TAT = "0";

            con2.Destinations.Add(dest2);

            summary.LadleTransactionDetails.Add(con2);

            return summary;

        }

        public List<ESLLadle> GetAllActiveLadle()
        {
            return GetAllLadles();
        }



        public List<TranscationLadledetails> destinationlocationCalculation()
        {
            List<TranscationLadledetails> transcationLadledetail = new List<TranscationLadledetails>();
            try
            {
                foreach (var la in _ladles)
                {
                    List<ReadersTransactionLog> eachLadlelogs = GetReaderTrsactionLogForEachLadle(Efromdate, Etodate, la.SerialNo);

                    DateTime sourceINdatetime = DateTime.MinValue;
                    DateTime sourceOUTdatetime = DateTime.MinValue;
                    DateTime destinationINdatetime = DateTime.MinValue;
                    DateTime destinationOUTdatetime = DateTime.MinValue;
                    DateTime Weighmentdatetime = DateTime.MinValue;
                    int sourcelocationId = 0;
                    string sourceLocation = string.Empty;
                    TranscationLadledetails ld = new TranscationLadledetails();

                    int previouslocationId = 0;

                    for (int i = 0; i < eachLadlelogs.Count; i++)
                    {
                        if (i == eachLadlelogs.Count - 1)
                        {
                            if (eachLadlelogs[i].LocationID != previouslocationId && eachLadlelogs[i].LocationID != 1 && eachLadlelogs[i].LocationID != 2 && eachLadlelogs[i].LocationID != 3)
                            {
                                destinationINdatetime = eachLadlelogs[i].ServerDateTime;
                                ld = new TranscationLadledetails();
                                ld.ladleNo = la.LadleNo;
                                ld.SerialNo = la.SerialNo;
                                ld.sourceLocation = sourceLocation;
                                ld.sourceINtime = sourceINdatetime;
                                ld.sourceOUTtime = sourceOUTdatetime;
                                ld.weightmentDatetime = Weighmentdatetime;
                                if (eachLadlelogs[i].LocationID == 4)
                                {
                                    ld.destinationLocation = "SMS";

                                }
                                else if (eachLadlelogs[i].LocationID == 5)
                                {
                                    ld.destinationLocation = "DIP";
                                }
                                else if (eachLadlelogs[i].LocationID == 6)
                                {
                                    ld.destinationLocation = "PCM";
                                }
                                else if (eachLadlelogs[i].LocationID == 7)
                                {
                                    ld.destinationLocation = "LRS";
                                }

                                ld.destinationLocationId = eachLadlelogs[i].LocationID;
                                ld.destinationINtime = destinationINdatetime;

                            }
                            else if (eachLadlelogs[i].LocationID != 1 && eachLadlelogs[i].LocationID != 2 && eachLadlelogs[i].LocationID != 3)
                            {
                                destinationOUTdatetime = eachLadlelogs[i].ServerDateTime;
                                ld.destinationOUTtime = destinationOUTdatetime;
                                transcationLadledetail.Add(ld);
                                previouslocationId = eachLadlelogs[i].LocationID;
                            }
                        }
                        else if ((eachLadlelogs[i].LocationID == 1 || eachLadlelogs[i].LocationID == 2) && previouslocationId != eachLadlelogs[i].LocationID)
                        {
                            if (eachLadlelogs[i].LocationID == 1)
                            {
                                sourceLocation = "BF2";
                            }
                            else if (eachLadlelogs[i].LocationID == 2)
                            {
                                sourceLocation = "BF3";
                            }

                            sourcelocationId = eachLadlelogs[i].LocationID;
                            sourceINdatetime = eachLadlelogs[i].ServerDateTime;
                            previouslocationId = eachLadlelogs[i].LocationID;
                        }
                        else if (eachLadlelogs[i].LocationID != previouslocationId && sourceINdatetime != DateTime.MinValue)
                        {

                            if (previouslocationId == 1 || previouslocationId == 2)
                            {
                                sourceOUTdatetime = eachLadlelogs[i - 1].ServerDateTime;
                            }

                            if (eachLadlelogs[i].LocationID == 3)
                            {
                                Weighmentdatetime = eachLadlelogs[i].ServerDateTime;
                            }
                            else
                            {
                                destinationINdatetime = eachLadlelogs[i].ServerDateTime;
                                ld = new TranscationLadledetails();
                                ld.ladleNo = la.LadleNo;
                                ld.SerialNo = la.SerialNo;
                                ld.sourceLocation = sourceLocation;
                                ld.sourceLocationId = sourcelocationId;
                                ld.sourceINtime = sourceINdatetime;
                                ld.sourceOUTtime = sourceOUTdatetime;
                                ld.weightmentDatetime = Weighmentdatetime;
                                if (eachLadlelogs[i].LocationID == 4)
                                {
                                    ld.destinationLocation = "SMS";
                                }
                                else if (eachLadlelogs[i].LocationID == 5)
                                {
                                    ld.destinationLocation = "DIP";
                                }
                                else if (eachLadlelogs[i].LocationID == 6)
                                {
                                    ld.destinationLocation = "PCM";
                                }
                                else if (eachLadlelogs[i].LocationID == 7)
                                {
                                    ld.destinationLocation = "LRS";
                                }

                                ld.destinationLocationId = eachLadlelogs[i].LocationID;
                                ld.destinationINtime = destinationINdatetime;


                            }

                            previouslocationId = eachLadlelogs[i].LocationID;
                        }
                        else if (eachLadlelogs[i].LocationID != eachLadlelogs[i + 1].LocationID && sourceINdatetime != DateTime.MinValue)
                        {
                            if (eachLadlelogs[i].LocationID != 1 && eachLadlelogs[i].LocationID != 2 && eachLadlelogs[i].LocationID != 3)
                            {
                                destinationOUTdatetime = eachLadlelogs[i].ServerDateTime;
                                ld.destinationOUTtime = destinationOUTdatetime;
                                transcationLadledetail.Add(ld);
                                previouslocationId = eachLadlelogs[i].LocationID;
                            }
                        }
                        else
                        {
                            previouslocationId = eachLadlelogs[i].LocationID;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                int j = 0;
            }
            return transcationLadledetail;
        }






        List<TranscationLadledetails> transLadledetails = null;
        public void EachlocationCalculation()
        {

            DateTime todateTime = DateTime.Now;
            DateTime fromDateTime;
            try
            {
                if (todateTime.Hour >= 22)
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(0));
                }
                else
                {
                    fromDateTime = todateTime.Subtract(TimeSpan.FromDays(1));
                }

                fromDateTime = new DateTime(fromDateTime.Year, fromDateTime.Month, fromDateTime.Day, 22, 00, 00);
                ESLLadleTransactionSummary sum = GetTransactionSummary(todateTime, todateTime);


                //valusee= sum.LimsSummary.Count.ToString();


                int totalTrips = sum.LadleTransactionDetails.Count;

                int completedTrips = 0;
                int pendingTrips = 0;

                Dictionary<string, ESLLocation> talCalculationDictionary = new Dictionary<string, ESLLocation>();

                foreach (ESLLadleConsolidated con in sum.LadleTransactionDetails)
                {
                    if (!string.IsNullOrEmpty(con.DestinationLocation))
                    {
                        completedTrips++;
                        if (talCalculationDictionary.ContainsKey(con.DestinationLocation))
                        {
                            ESLLocation loc = talCalculationDictionary[con.DestinationLocation];
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }
                        }
                        else
                        {
                            ESLLocation loc = new ESLLocation();
                            loc.LocationName = con.DestinationLocation;
                            if (con.DestinationOutDateTime.Subtract(con.DestinationInDateTime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + con.DestinationOutDateTime.Subtract(con.DestinationInDateTime);
                                loc.totalHoldCount++;
                            }

                            if (con.TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + con.TATValue;
                                loc.totalTATCount++;
                            }

                            talCalculationDictionary.Add(con.DestinationLocation, loc);
                        }

                    }
                    else
                    {
                        pendingTrips++;
                    }
                }








                DateTime prevSourcedatetime = DateTime.MinValue;
                int previouslocationId = 0;
                string lastSerialNo = string.Empty;
                transLadledetails = destinationlocationCalculation();

                for (int i = 0; i < transLadledetails.Count; i++)
                {
                    if (lastSerialNo == transLadledetails[i].SerialNo)
                    {
                        if (prevSourcedatetime == transLadledetails[i].sourceINtime)
                        {



                            transLadledetails[i].sourceINtime = transLadledetails[i - 1].destinationOUTtime;
                            transLadledetails[i].sourceLocation = transLadledetails[i - 1].destinationLocation;
                            transLadledetails[i].sourceLocationId = transLadledetails[i - 1].destinationLocationId;

                        }
                    }
                    prevSourcedatetime = transLadledetails[i].sourceINtime;
                    lastSerialNo = transLadledetails[i].SerialNo;


                }





                if (transLadledetails != null && transLadledetails.Count != 0)
                {
                    ESLLocation loc = new ESLLocation();
                    loc.LocationName = "LRS";
                    foreach (var lrs in transLadledetails)
                    {
                        if (lrs.destinationLocation == loc.LocationName)
                        {
                            if (lrs.destinationOUTtime.Subtract(lrs.destinationINtime).TotalMinutes > 15)
                            {
                                //Above consition checks for total duration more than 15 mins.....
                                loc.totalHoldingTime = loc.totalHoldingTime + lrs.destinationOUTtime.Subtract(lrs.destinationINtime);
                                loc.totalHoldCount++;
                            }
                            TimeSpan TATValue = lrs.destinationINtime.Subtract(lrs.sourceINtime);
                            if (TATValue.TotalMinutes > 30)
                            {
                                loc.totalTAT = loc.totalTAT + TATValue;
                                loc.totalTATCount++;
                            }
                        }
                    }

                    talCalculationDictionary.Add(loc.LocationName, loc);
                }
                List<ESLLocation> LocationData = GetAllLocations();

                foreach (ESLLocation fl in LocationData)
                {
                    if (talCalculationDictionary.ContainsKey(fl.LocationName))
                    {
                        if (transLadledetails != null && transLadledetails.Count != 0)
                        {
                            ESLLocation loc = new ESLLocation();
                            loc.LocationName = fl.LocationName;
                            foreach (var lrs in transLadledetails)
                            {
                                if (lrs.destinationLocation == loc.LocationName)
                                {
                                    if (lrs.destinationOUTtime.Subtract(lrs.destinationINtime).TotalMinutes > 15)
                                    {
                                        //Above consition checks for total duration more than 15 mins.....
                                        loc.totalHoldingTime = loc.totalHoldingTime + lrs.destinationOUTtime.Subtract(lrs.destinationINtime);
                                        loc.totalHoldCount++;
                                    }
                                    TimeSpan TATValue = lrs.destinationINtime.Subtract(lrs.sourceINtime);
                                    if (TATValue.TotalMinutes > 30)
                                    {
                                        loc.totalTAT = loc.totalTAT + TATValue;
                                        loc.totalTATCount++;
                                    }
                                }
                            }

                            talCalculationDictionary.Add(loc.LocationName, loc);
                        }

                    }
                }




                foreach (ESLLocation fl in LocationData)
                {
                    if (talCalculationDictionary.ContainsKey(fl.LocationName))
                    {

                        ESLLocation r1 = talCalculationDictionary[fl.LocationName];
                        if (r1.totalTATCount != 0)
                        {
                            fl.AverageTATSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Hours, TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).Minutes);
                        }

                        if (r1.totalHoldCount != 0)
                        {
                            //valusee = r1.LadleCount.ToString();
                            //valusee1 = r1.totalTATCount.ToString();

                            fl.AverageHoldTimeSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Hours, TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).Minutes);
                            //fl.AverageTATSTR = TimeSpan.FromTicks(r1.totalTAT.Ticks / r1.totalTATCount).ToString("d\\.hh\\:mm");
                            //fl.AverageHoldTimeSTR = TimeSpan.FromTicks(r1.totalHoldingTime.Ticks / r1.totalHoldCount).ToString("d\\.hh\\:mm");
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }

        }


        //public ESLLocationData LocationwiseClarification(string locationName)
        //{
        //    ESLLocationData lData = new ESLLocationData();
        //    DateTime frmdate = DateTime.Now.AddDays(-1);
        //    DateTime todate = DateTime.Now;
        //    string ladleNo = "0";

        //    try
        //    {
        //        ESLLadlePathSummary lwc = GetLadleDataforDateRange(frmdate, todate, ladleNo);

        //        Parallel.ForEach(lwc.LadleTransactionDetails, ldle =>
        //        {
        //            for (int i = 0; i < ldle.Destinations.Count - 1; i++)
        //            {
        //                ESLLadleConsolidated es = new ESLLadleConsolidated
        //                {
        //                    LadleNo = ldle.LadleNo,
        //                    SourceOutDateTime = ldle.Destinations[i].DestinationOutDateTime,
        //                    SourceLocation = ldle.Destinations[i].DestinationLocation,
        //                    SourceOUTDateSTR = ldle.Destinations[i].DestinationOUTDateSTR,
        //                    SourceOUTTimeSTR = ldle.Destinations[i].DestinationOUTTimeSTR,
        //                    SourceOutDateTimeSTR = ldle.Destinations[i].DestinationOutDateTimeSTR,
        //                    NetWeight = ldle.NetWeight,
        //                    DestinationInDateTime = ldle.Destinations[i + 1].DestinationInDateTime,
        //                    DestinationLocation = ldle.Destinations[i + 1].DestinationLocation,
        //                    DestinationINDateSTR = ldle.Destinations[i + 1].DestinationINDateSTR,
        //                    DestinationINTimeSTR = ldle.Destinations[i + 1].DestinationINTimeSTR,
        //                    DestinationInDateTimeSTR = ldle.Destinations[i + 1].DestinationInDateTimeSTR,
        //                    DestinationOutDateTime = ldle.Destinations[i + 1].DestinationOutDateTime,
        //                    DestinationOUTDateSTR = ldle.Destinations[i + 1].DestinationOUTDateSTR,
        //                    DestinationOUTTimeSTR = ldle.Destinations[i + 1].DestinationOUTTimeSTR,
        //                    DestinationOutDateTimeSTR = ldle.Destinations[i + 1].DestinationOutDateTimeSTR
        //                };

        //                es.TATValue = es.DestinationInDateTime.Subtract(es.SourceOutDateTime);
        //                es.TAT = string.Format("{0:D2}:{1:D2}", es.TATValue.Hours, es.TATValue.Minutes);
        //                es.HoldingValue = es.DestinationOutDateTime.Subtract(es.DestinationInDateTime);
        //                es.HoldingTimeValue = string.Format("{0:D2}:{1:D2}", es.HoldingValue.Hours, es.HoldingValue.Minutes);

        //                lock (lData.LadleList)
        //                {
        //                    lData.LadleList.Add(es);
        //                }
        //            }
        //        });

        //        var ladlesAtLocation = lData.LadleList.AsParallel()
        //            .Where(fl => fl.DestinationLocation == locationName)
        //            .ToList();

        //        foreach (var fl in ladlesAtLocation)
        //        {
        //            if (fl.TATValue.TotalMinutes > 30)
        //            {
        //                lData.totalTAT += fl.TATValue;
        //                lData.totalTATCount++;
        //            }

        //            if (fl.HoldingValue.TotalMinutes > 15)
        //            {
        //                lData.totalHoldingTime += fl.HoldingValue;
        //                lData.totalHoldCount++;
        //            }
        //        }

        //        if (lData != null)
        //        {
        //            if (lData.totalTATCount != 0)
        //            {
        //                lData.AverageTATSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(lData.totalTAT.Ticks / lData.totalTATCount).Hours, TimeSpan.FromTicks(lData.totalTAT.Ticks / lData.totalTATCount).Minutes);
        //            }

        //            if (lData.totalHoldCount != 0)
        //            {
        //                lData.AverageHoldTimeSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(lData.totalHoldingTime.Ticks / lData.totalHoldCount).Hours, TimeSpan.FromTicks(lData.totalHoldingTime.Ticks / lData.totalHoldCount).Minutes);
        //            }

        //            lData.LadleCount = lData.LadleList.Count;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle exception
        //    }

        //    return lData;
        //}


        public ESLLocationData LocationwiseClarification(string locationName)
        {
            List<ESLLadleConsolidated> eSLLadlelist = new List<ESLLadleConsolidated>();
            ESLLocationData lData = new ESLLocationData();
            DateTime frmdate = DateTime.Now.AddDays(-1);
            DateTime todate = DateTime.Now;
            string ladleNo = "0";
            string prevladleno = string.Empty;
            DateTime prevSourceDatetime = DateTime.MinValue;
            try
            {
                ESLLadlePathSummary lwc = GetLadleDataforDateRange(frmdate, todate, ladleNo);

                foreach (var ldle in lwc.LadleTransactionDetails)
                {
                    prevladleno = ldle.LadleNo;
                    prevSourceDatetime = ldle.SourceOutDateTime;

                    for (int i = 0; i < ldle.Destinations.Count; i++)
                    {
                        ESLLadleConsolidated es = new ESLLadleConsolidated();
                        if (i != ldle.Destinations.Count - 1)
                        {
                            es.LadleNo = ldle.LadleNo;
                            es.SourceOutDateTime = ldle.Destinations[i].DestinationOutDateTime;
                            es.SourceLocation = ldle.Destinations[i].DestinationLocation;
                            es.SourceOUTDateSTR = ldle.Destinations[i].DestinationOUTDateSTR;
                            es.SourceOUTTimeSTR = ldle.Destinations[i].DestinationOUTTimeSTR;
                            es.SourceOutDateTimeSTR = ldle.Destinations[i].DestinationOutDateTimeSTR;
                            es.NetWeight = ldle.NetWeight;
                            es.DestinationInDateTime = ldle.Destinations[i + 1].DestinationInDateTime;
                            es.DestinationLocation = ldle.Destinations[i + 1].DestinationLocation;
                            es.DestinationINDateSTR = ldle.Destinations[i + 1].DestinationINDateSTR;
                            es.DestinationINTimeSTR = ldle.Destinations[i + 1].DestinationINTimeSTR;
                            es.DestinationInDateTimeSTR = ldle.Destinations[i + 1].DestinationInDateTimeSTR;
                            es.DestinationOutDateTime = ldle.Destinations[i + 1].DestinationOutDateTime;
                            es.DestinationOUTDateSTR = ldle.Destinations[i + 1].DestinationOUTDateSTR;
                            es.DestinationOUTTimeSTR = ldle.Destinations[i + 1].DestinationOUTTimeSTR;
                            es.DestinationOutDateTimeSTR = ldle.Destinations[i + 1].DestinationOutDateTimeSTR;

                            es.TATValue = es.DestinationInDateTime.Subtract(es.SourceOutDateTime);
                            es.TAT = string.Format("{0:D2}:{1:D2}", es.TATValue.Hours, es.TATValue.Minutes);
                            es.HoldingValue = es.DestinationOutDateTime.Subtract(es.DestinationInDateTime);
                            es.HoldingTimeValue = string.Format("{0:D2}:{1:D2}", es.HoldingValue.Hours, es.HoldingValue.Minutes);
                            eSLLadlelist.Add(es);
                        }

                    }
                }

                foreach (var fl in eSLLadlelist)
                {
                    if (fl.DestinationLocation == locationName)
                    {
                        if (fl.TATValue.TotalMinutes > 30)
                        {
                            lData.totalTAT = fl.TATValue + lData.totalTAT;
                            lData.totalTATCount++;
                        }

                        if (fl.HoldingValue.TotalMinutes > 15)
                        {
                            lData.totalHoldingTime = lData.totalHoldingTime + fl.HoldingValue;
                            lData.totalHoldCount++;
                        }

                        lData.LadleList.Add(fl);
                    }
                }

                if (lData != null)
                {
                    if (lData.totalTATCount != 0)
                    {
                        lData.AverageTATSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(lData.totalTAT.Ticks / lData.totalTATCount).Hours, TimeSpan.FromTicks(lData.totalTAT.Ticks / lData.totalTATCount).Minutes);
                    }

                    if (lData.totalHoldCount != 0)
                    {


                        lData.AverageHoldTimeSTR = string.Format("{0:D2}:{1:D2}", TimeSpan.FromTicks(lData.totalHoldingTime.Ticks / lData.totalHoldCount).Hours, TimeSpan.FromTicks(lData.totalHoldingTime.Ticks / lData.totalHoldCount).Minutes);

                    }

                    lData.LadleCount = lData.LadleList.Count;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error at method LocationwiseClarification.", ex);
            }

            lData.LocationType = "PROD";

            if (locationName == "SMS")
            {
                lData.LocationID = 4;
                lData.LocationName = locationName;

            }
            else if (locationName == "DIP")
            {
                lData.LocationID = 5;
                lData.LocationName = locationName;
            }
            else if (locationName == "PCM")
            {
                lData.LocationID = 6;
                lData.LocationName = locationName;
            }
            else if (locationName == "LRS")
            {
                lData.LocationID = 7;
                lData.LocationName = locationName;
                lData.LocationType = "MAINTANANCE";
            }

            return lData;
        }


        public List<ESLLadle> GetIdleladlesDataByDate(DateTime fromDatetime, DateTime toDatetime)
        {
            List<ESLLadle> idleLadles = new List<ESLLadle>();
            try
            {
                List<ESLLadle> activeLadles = GetAllLadles();
                List<ESLLadle> activeMovingLadles = GetAllLadleObjectInUse(fromDatetime, toDatetime);

                foreach (ESLLadle ldl in activeLadles)
                {
                    if (!activeMovingLadles.Exists(x => x.LadleNo == ldl.LadleNo))
                    {
                        //Get Ladles Last Location from the system and add to the Unused Collection...
                        //ldl.LastLocationID
                        DateTime transDateTime;
                        string LocationName = string.Empty;
                        if (GetIdleLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName, fromDatetime, toDatetime))
                        {
                            if (LocationName != string.Empty)
                            {
                                ldl.LastLocationDateTime = transDateTime;
                                ldl.LastLocationName = LocationName;
                                ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                            }

                        }
                        idleLadles.Add(ldl);
                    }
                    //else
                    //{
                    //    ESLLadle fl = activeMovingLadles.Find(x => x.LadleNo == ldl.LadleNo);

                    //    if (!FindLadleInOnlineLocations(dashboardSummary.LocationData, ldl.LadleNo))
                    //    {
                    //        int j = 9000;
                    //        string LocationName = string.Empty;
                    //        DateTime transDateTime;
                    //        GetLadleLastLocationAndTime(ldl.SerialNo, out transDateTime, out LocationName);
                    //        if (LocationName != string.Empty)
                    //        {
                    //            ldl.LastLocationDateTime = transDateTime;
                    //            ldl.LastLocationName = LocationName;
                    //            ldl.TimeSpentObj = DateTime.Now.Subtract(ldl.LastLocationDateTime);
                    //            //loc.LadleList.Add(ldl);
                    //            ESLLocation loc = dashboardSummary.LocationData.Find(x => x.LocationName == LocationName);
                    //            if (loc != null)
                    //            {
                    //                loc.LadleList.Add(ldl);
                    //                var ladleComparer = new ESLLadleCompare();
                    //                loc.LadleList.Sort(ladleComparer);
                    //                //loc.LadleList.Reverse();
                    //            }
                    //        }




                    //    }

                    //    if (fl != null)
                    //    {
                    //        if (fl.CastNoQueryMinDateTime.Year != 1)
                    //        {
                    //            ESLLadleAssignment la = GetLadleAssignment(fl.LadleNo, fl.CastNoQueryMinDateTime);

                    //            if (la != null)
                    //            {
                    //                fl.AcceptedLocationName = la.AssignedProductionUit;
                    //                fl.State = 4;
                    //            }
                    //        }
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return idleLadles;
        }


        #endregion

        #region ESL Ladle Trackng Constants

        private int LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES = 1;
        //private string GET_READERS_TRANSACTION_LOGS_FOR_ASSET = "SELECT top 10 [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime] FROM [ReadersTransactionLog] WHERE AssetSerialNo=@AssetSerialNo order by ServerDatetime desc";
        private string GET_READERS_TRANSACTION_LOGS_FOR_ASSET_ONLINE = "select [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] from ReadersTransactionLog where ServerDatetime >= (select Top 1 ServerDatetime from ReadersTransactionLog where LocationID in (select LocationID from Locations where LocationType='FURNACE') and AssetSerialNo = @AssetSerialNo  and AssetteType = 1 order by ServerDatetime desc) and AssetSerialNo = @AssetSerialNo and AssetteType = 1 order by ServerDatetime desc";

        private string GET_READERS_TRANSACTION_LOGS_FOR_ASSET_FOR_DATE = "select [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] from ReadersTransactionLog where ServerDatetime >= (select Top 1 ServerDatetime from ReadersTransactionLog where LocationID in (select LocationID from Locations where LocationType='FURNACE') and AssetSerialNo = @AssetSerialNo  and AssetteType = 1 order by ServerDatetime asc) and AssetSerialNo = @AssetSerialNo and AssetteType = 1 order by ServerDatetime desc";

        private string GET_READERS_TRANSACTION_LOGS_FOR_ASSET_BY_CAST_TX_TIME = "SELECT[ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] from ReadersTransactionLog where " +
                                                                                "ServerDatetime >= (select Top 1 ServerDateTime from ReadersTransactionLog where LocationID in (select LocationID from Locations where LocationType='FURNACE') and AssetSerialNo = @AssetSerialNo and AssetteType = 1 AND CastNo is NOT NULL AND ServerDateTime < '@CastOpenTime' order by ServerDatetime desc) AND " +
                                                                                "ServerDateTime < (select Top 1 ServerDatetime from ReadersTransactionLog where LocationID in (select LocationID from Locations where LocationType='FURNACE') and AssetSerialNo = @AssetSerialNo and AssetteType = 1 AND CastNo is NOT NULL AND ServerDateTime > '@CastOpenTime' order by ServerDatetime asc)  " +
                                                                                "AND AssetSerialNo = 3 AND AssetteType = 1 order by ServerDatetime desc ";

        //Query Changed for Debugging...
        private string GET_READER_TRANSACTION_LOGS_FROM_TO_DATE = "select [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType]," +
                                                                  "[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] " +
                                                                   "from ReadersTransactionLog where ServerDatetime >= '@FROMDATE' AND " +
                                                                   "ServerDatetime <= '@TODATE' and AssetteType = 1 order by ServerDateTime asc ";


        private string GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_NEW = "select R.[ID],A.[ADescription],R.[AssetSerialNo],R.[ReaderIP],R.[AntennaID],R.[RSSI],R.[LocationID],R.[TouchPointID],R.[TouchPointType]," +
                                                                  "R.[ServerDatetime],R.[TransDatetime],R.[CastNo],R.[CastNoDateTime],R.[CastNoLocationID] " +
                                                                   "from ReadersTransactionLog R inner join AssetMaster A on R.TagId=A.ATagID where A.ATypeID=1 and R.ServerDatetime >= '@FROMDATE' AND " +
                                                                   "R.ServerDatetime <= '@TODATE' and R.AssetteType = 1 order by ServerDateTime asc ";


        private string GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_FOR_LADLE = "select [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType]," +
                                                                  "[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] " +
                                                                   "from ReadersTransactionLog where ServerDatetime >= '@FROMDATE' AND " +
                                                                   "ServerDatetime <= '@TODATE' and AssetteType = 1 AND AssetSerialNo = @SerialNumber  order by ServerDateTime asc ";

        private string GET_ESL_LADLES = "SELECT [AssetID],[AName],[ASerialNo],[ATagID],[ADescription] FROM [AssetMaster] WHERE ATypeID=1 AND IsActive=1";

        private string GET_LADLES_IN_USE = "select distinct A.AName as LadleNo from ReadersTransactionLog R " +
                                           "left Join Locations L on L.LocationId = R.LocationID " +
                                           "Left Join AssetMaster A on A.ATagID= R.TagId " +
                                           "where ServerDatetime between '@FROMDATE' and '@TODATE' and " +
                                           "A.ATypeID= '1'";

        private string GET_LADLE_LAST_LOCATION_DATA = "select Top 1 R.ServerDatetime,L.LocationName from ReadersTransactionLog R " +
                                                      "left Join Locations L on L.LocationId = R.LocationID Left Join AssetMaster A on A.ATagID= R.TagId " +
                                                      "where A.ASerialNo = @SerialNo and " +
                                                      "A.ATypeID='1'order by R.ServerDatetime desc ";


        private string GET_BOUNDRY_SRC_LOC_DT = "select top 1 ServerDatetime,LocationID from ReadersTransactionLog R where R.AssetSerialNo = @SerialNo " +
                                                 "and LocationID = @LOCATIONID and ServerDatetime< '@QUERYDT' and ServerDatetime > (select top 1 ServerDatetime " +
                                                 "from ReadersTransactionLog R1 where R1.AssetSerialNo = @SerialNo and LocationID != @LOCATIONID and " +
                                                 "ServerDatetime < '@QUERYDT' order by ServerDatetime desc) and R.AssetteType ='1' order by R.ServerDatetime asc ";

        private string GET_BOUNDRY_SRC_LOC_DT_LADLE = "select top 1 ServerDatetime,LocationID from ReadersTransactionLog R where R.AssetSerialNo = @SerialNo " +
                                                 "and LocationID = @LOCATIONID and ServerDatetime< '@QUERYDT' and ServerDatetime > (select top 1 ServerDatetime " +
                                                 "from ReadersTransactionLog R1 where R1.AssetSerialNo = @SerialNo and LocationID != @LOCATIONID and R1.AssetteType ='1' and " +
                                                 "ServerDatetime < '@QUERYDT' order by ServerDatetime desc) and R.AssetteType ='1' order by R.ServerDatetime asc ";


        private string GET_LADLES_OBJECT_IN_USE = "SELECT [AssetID],[AName],[ASerialNo],[ATagID],[ADescription] FROM [AssetMaster] where AName in (select distinct A.AName as LadleNo from ReadersTransactionLog R " +
                                           "left Join Locations L on L.LocationId = R.LocationID " +
                                           "Left Join AssetMaster A on A.ATagID= R.TagId " +
                                           "where ServerDatetime between '@FROMDATE' and '@TODATE' and " +
                                           "A.ATypeID= '1') AND ATypeID=1 AND IsActive=1";


        private string GET_ESL_LOCATIONS = "SELECT [LocationId],[LocationName],[CreatedOn],[ModifiedOn],[IsActive],[LocationType] FROM [Locations]";

        private string GET_LIMS_DATA_BF2 = "SELECT [SampleDate],[Shift],[SampleTime],[SampleName],[CastNo],[Casting_time_HR],[Closing_time_HR],[Ladle_no],[Ladle_Count],[C%],[Si],[Mn],[S],[P],[Ti],[Cr],[S+P],[Pig_Iron_Grade],[SampleBy],[Analyst],[EnteredBy],[SampleDateTime],[CastingDateTime],[NewCastNo] FROM[dbo].[HotMetal_ChemistryBF2_LTS] where [NewCastNo]='@CastNo'";

        private string GET_LIMS_DATA_BF2_LIST = "SELECT [SampleDate],[Shift],[SampleTime],[SampleName],[CastNo],[Casting_time_HR],[Closing_time_HR],[Ladle_no],[Ladle_Count],[C%],[Si],[Mn],[S],[P],[Ti],[Cr],[S+P],[Pig_Iron_Grade],[SampleBy],[Analyst],[EnteredBy],[SampleDateTime],[CastingDateTime],[NewCastNo] FROM[dbo].[HotMetal_ChemistryBF2_LTS] where TRIM('-' FROM [NewCastNo]) IN (@LISTDATA) order by SampleDateTime desc";

        private string GET_LIMS_DATA_BF3 = "SELECT [SampleDate],[Shift],[SampleTime],[SampleName],[Cast No],[Casting_time_HR],[Closing_time_HR],[Ladle_no],[Ladle count],[C%],[Si],[Mn],[S],[P],[Ti],[Cr],[S+P],[Pig Iron Grade],[SampleBy],[Analyst],[EnterdBy],[SampleDateTime],[NewCastNo] FROM[dbo].[HotMetal_ChemistryBF3_LTS] where [NewCastNo]='@CastNo'";

        private string GET_LIMS_DATA_BF3_LIST = "SELECT [SampleDate],[Shift],[SampleTime],[SampleName],[Cast No],[Casting_time_HR],[Closing_time_HR],[Ladle_no],[Ladle count],[C%],[Si],[Mn],[S],[P],[Ti],[Cr],[S+P],[Pig Iron Grade],[SampleBy],[Analyst],[EnterdBy],[SampleDateTime],[NewCastNo] FROM[dbo].[HotMetal_ChemistryBF3_LTS] where TRIM('-' FROM [NewCastNo]) IN (@LISTDATA) order by SampleDateTime desc";

        private string GET_RTL_CAST_DTL_FOR_LADLE_ONLINE = "SELECT TOP 1 [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] from ReadersTransactionLog where AssetSerialNo = @AssetSerialNo AND CastNo IS NOT NULL AND CastNoDateTime < (select Top 1 ServerDatetime from ReadersTransactionLog where LocationID in (select LocationID from Locations where LocationType='FURNACE') AND AssetSerialNo = @AssetSerialNo order by ServerDatetime desc) " +
                                                    "AND LocationID in (select LocationID from Locations where LocationType='FURNACE')";

        private string GET_RTL_CAST_DTL_FOR_LADLE_DATE_RANGE = "SELECT [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType],[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] from ReadersTransactionLog where AssetSerialNo = @AssetSerialNo AND CastNo IS NOT NULL AND CastNoDateTime < '@ToCastNoDateTime'  AND CastNoDateTime > '@FromCastNoDateTime' " +
                                                    "AND LocationID in (select LocationID from Locations where LocationType='FURNACE')";

        private string GET_LADLE_WEIGHMENT_DATA = "SELECT TOP 1 [Tran_ID],CONVERT(datetime,[GrossDateTime],126) as [GrossDateTime],CONVERT(datetime,[TaredateTime],126) as [TaredateTime],[LaddleNumber],[GrossWT],[TareWT],[NetWT],[TransactionDateTime],[SenderPlant],[CastNumber],[ReceiverPlant],[Gross_DT_Time],[FID] from tbl_Laddle_WT where TransactionDateTime between (select DATEADD(mi,-2,'@QueryDateTime')) AND (select DATEADD(mi, 2, '@QueryDateTime')) AND LaddleNumber = '@LaddleNumber' order by TransactionDateTime desc ";

        private string GET_LADLE_WEIGHMENT_DATA_DATE_RANGE = "SELECT TOP 1 [Tran_ID],CONVERT(datetime,[GrossDateTime],126) as [GrossDateTime],CONVERT(datetime,[TaredateTime],126) as [TaredateTime],[LaddleNumber],[GrossWT],[TareWT],[NetWT],[TransactionDateTime],[SenderPlant],[CastNumber],[ReceiverPlant],[Gross_DT_Time],[FID] from tbl_Laddle_WT where Gross_DT_Time between '@FROMDATE' AND '@TODATE' AND LaddleNumber = '@LaddleNumber' order by Gross_DT_Time desc ";

        private string GET_LADLE_WEIGHMENT_DATA_DATE_RANGE_FROM = "SELECT TOP 1 [Tran_ID],CONVERT(datetime,[GrossDateTime],126) as [GrossDateTime],CONVERT(datetime,[TaredateTime],126) as [TaredateTime],[LaddleNumber],[GrossWT],[TareWT],[NetWT],[TransactionDateTime],[SenderPlant],[CastNumber],[ReceiverPlant],[Gross_DT_Time],[FID] from tbl_Laddle_WT where Gross_DT_Time > '@FROMDATE' AND LaddleNumber = '@LaddleNumber' order by Gross_DT_Time asc";

        private string GET_WEIGHMENT_DATA_DATE_RANGE_FROM = "SELECT [Tran_ID],CONVERT(datetime,[GrossDateTime],126) as [GrossDateTime],CONVERT(datetime,[TaredateTime],126) as [TaredateTime],[LaddleNumber],[GrossWT],[TareWT],[NetWT],[TransactionDateTime],[SenderPlant],[CastNumber],[ReceiverPlant],[Gross_DT_Time],[FID] from tbl_Laddle_WT where Gross_DT_Time between '@FROMDATE' AND '@TODATE' order by Gross_DT_Time asc";

        private string GET_WEIGHMENT_DATA_CONSUMP_BY_ID = "SELECT [Tran_ID],[Consumption_Id],[LaddleNumber],[LaddleNumber_SL],CONVERT(datetime,[GrossDateTime],126) as [GrossDateTime],CONVERT(datetime,[TaredateTime],126) as [TaredateTime],[GrossWT],[TareWT],[NetWT],[TransactionDateTime],[SenderPlant],[CastNumber],[ReceiverPlant],[FID] from tbl_Laddle_Consumption where Consumption_Id = '@Consumption_Id' order by LaddleNumber_SL,TransactionDateTime asc";

        private string GET_CAST_TRANSACTIONS = "SELECT [CTID],[CastNo],[Temperature],[LocationID],[CreatedDateTime],[TID],[UID],[AssetID] FROM [CastTransaction] WHERE CreatedDateTime between '@fromDate' AND '@toDate' ORDER BY CreatedDateTime desc";

        private string GET_LADLES_FOR_CAST_TRANSACTION = "SELECT [AssetID],[AName],[ASerialNo],[ATagID],[ADescription] FROM [AssetMaster] WHERE ATypeID=1 AND IsActive=1 " +
                                                        " AND ASerialNo IN(SELECT AssetSerialNo from CastTransactionDetails where CTID = '@CTID')";


        private string INSERT_LADLE_ASSIGNMENT = "INSERT INTO LadleAssignments(ID,LadleNo,SerialNumber,AssignmentDateTime,AssignedLocation,AssignedLocationID,SourceLocation,SourceLocationName,SourceLocationDateTime) VALUES(@ID,@LadleNo,@SerialNumber,@AssignmentDateTime,@AssignedLocation,@AssignedLocationID,@SourceLocation,@SourceLocationName,@SourceLocationDateTime)";

        private string GET_LADLE_ASSIGNMENT = "SELECT TOP 1 ID,LadleNo,SerialNumber,AssignmentDateTime,AssignedLocation,AssignedLocationID,SourceLocation,SourceLocationName,SourceLocationDateTime FROM [LadleAssignments] WHERE LadleNo=@LadleNo AND AssignmentDateTime > '@QUERYDATETIME'  order by AssignmentDateTime desc";

        private string GET_LADLE_PREV_LOCATION = "SELECT TOP 1 R.LocationID,A.AName,L.LocationName FROM AssetMaster A INNER JOIN ReadersTransactionLog R ON R.TagId=A.ATagID INNER JOIN Locations L ON L.LocationId=R.LocationID WHERE R.LocationID!=3 AND A.AName='@LadleNo' ORDER BY R.ServerDatetime DESC";

        private string GET_CONTACT_DETAILS = "Select UserID,UserName,FirstName,MobileNo,LastSMSDateTime,SMSAccess,AllLocationSMSAccess,Department,LocationId,IsActive,TransactionDateTime  FROM [ESLLadleDB_Prod].[dbo].[User] where SMSAccess=1 and IsActive=1";

        private string UPDATE_LAST_SMS_DATETIME = "UPDATE  [ESLLadleDB_Prod].[dbo].[User] set LastSMSDateTime=@LastSMSDateTime where UserID=@UserID";

        private string INSERT_SMS_TRANSCATION_LOG = "INSERT INTO SMSTranscationLog (TranscationId,UserID,Username,FirstName,Department,LocationId,LadleNo,IdleTime,SMSDatetime,TransactionDateTime) VALUES (@TranscationId,@UserID,@Username,@FirstName,@Department,@LocationId,@LadleNo,@IdleTime,@SMSDatetime,@TransactionDateTime)";

        private string GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_FOR_EACH_LADLE = "select [ID],[AssetSerialNo],[ReaderIP],[AntennaID],[RSSI],[LocationID],[TouchPointID],[TouchPointType]," +
                                                                "[ServerDatetime],[TransDatetime],[CastNo],[CastNoDateTime],[CastNoLocationID] " +
                                                                 "from ReadersTransactionLog where ServerDatetime >= '@FROMDATE' AND " +
                                                                 "ServerDatetime <= '@TODATE' AND AssetSerialNo = @SerialNumber  order by ServerDateTime asc ";


        private string LOCATION_CLARIFCATION = "INSERT INTO LocationClarificationData ([ID],[TranscationNo],[LocationID],[LocationName],[LocationType],[LadleCount]," +
                "[AverageTATSTR],[AverageHoldTimeSTR],[ServerDatetime] ) VALUES(@ID, @TranscationNo,@LocationID, @LocationName,@LocationType,@LadleCount,@AverageTATSTR, @AverageHoldTimeSTR,@ServerDatetime)";

        private string CLEAR_LOCATION_CLARIFCATION_TABLE = "Delete LocationClarificationData where TranscationNo=@transNo and LocationName=@LocationName";

        private string GET_LOCATION_TAT = "Select top 1 * from LocationClarificationData where LocationName='@LocationName' order by ServerDatetime desc";

        private string INSERT_IDLE_LADLE = @"INSERT INTO [ESLLadleDB_Prod].[dbo].[IdleLadleRecords] ([ID], [LadleName], [LastLocationDateTime],[LastLocationDateTimeSTR],[LastLocationName],[LastLocationID], [TimeSpentSTR],[Serverdatetime] ) 
                                            VALUES (@ID,@LadleName,@LastLocationDateTime,@LastLocationDateTimeSTR, @LastLocationName,@LastLocationID,@TimeSpentSTR, @Serverdatetime)";

        private string GET_IDLE_LADLE_LAST_LOCATION_DATA = "select Top 1 R.ServerDatetime,L.LocationName from ReadersTransactionLog R " +
                                                     "left Join Locations L on L.LocationId = R.LocationID Left Join AssetMaster A on A.ATagID= R.TagId " +
                                                     "where A.ASerialNo = @SerialNo and " +
                                                     "A.ATypeID='1' and R.ServerDatetime between '@FROMDATE' and '@TODATE' order by R.ServerDatetime desc ";

        #endregion

        #region ESL Ladle Track Private Methods

        private List<ReadersTransactionLog> GetReaderTrsactionLogForAsset(long serialNo)
        {

            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READERS_TRANSACTION_LOGS_FOR_ASSET_ONLINE;
                        strCommand = strCommand.Replace("@AssetSerialNo", serialNo.ToString());
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }


        private List<ReadersTransactionLog> GetSourceReaderTrsactionLogForDateTime(DateTime fromDate, DateTime toDate)
        {
            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READER_TRANSACTION_LOGS_FROM_TO_DATE;
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }


        private List<ReadersTransactionLog> GetSourceReaderTrsactionLogForDateTimeNew(DateTime fromDate, DateTime toDate)
        {
            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_NEW;
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.AssetDesc = dr.GetString("ADescription");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }


        private List<ReadersTransactionLog> GetSourceReaderTrsactionLogForDateTime(DateTime fromDate, DateTime toDate, string assetSerialNumber)
        {
            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_FOR_LADLE;
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@SerialNumber", assetSerialNumber);

                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }

        private List<ReadersTransactionLog> GetReaderTrsactionLogForAsset(long serialNo, DateTime castOpenTime)
        {

            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READERS_TRANSACTION_LOGS_FOR_ASSET_BY_CAST_TX_TIME;
                        strCommand = strCommand.Replace("@CastOpenTime", castOpenTime.ToString("yyyy-MM-dd H:mm:ss"));
                        strCommand = strCommand.Replace("@AssetSerialNo", serialNo.ToString());
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }

        private List<ESLLadle> GetAllLadles()
        {
            List<ESLLadle> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = GET_ESL_LADLES;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ESLLadle>();
                            while (dr.Read())
                            {
                                ESLLadle obj = new ESLLadle();
                                obj.ID = dr.GetGuid("AssetID");
                                obj.LadleNo = dr.GetString("AName");
                                obj.SerialNo = dr.GetString("ASerialNo").ToString();
                                obj.TagID = dr.GetString("ATagID");
                                obj.Name = dr.GetString("ADescription");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetAssettes.", ex);
            }
        }

        private List<string> GetAllLadlesInUse(DateTime fromDate, DateTime toDate)
        {
            List<string> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLES_IN_USE;

                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<string>();
                            while (dr.Read())
                            {
                                string lNo = dr.GetString("LadleNo");
                                returnValue.Add(lNo);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetAllLadlesInUse.", ex);
            }
        }

        public List<ESLLadle> GetAllLadleObjectInUse(DateTime fromDate, DateTime toDate)
        {
            List<ESLLadle> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLES_OBJECT_IN_USE;

                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ESLLadle>();
                            while (dr.Read())
                            {
                                ESLLadle obj = new ESLLadle();
                                obj.ID = dr.GetGuid("AssetID");
                                obj.LadleNo = dr.GetString("AName");
                                obj.SerialNo = dr.GetString("ASerialNo").ToString();
                                obj.TagID = dr.GetString("ATagID");
                                obj.Name = dr.GetString("ADescription");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetAllLadleObjectInUse.", ex);
            }
        }

        public List<ESLLocation> GetAllLocations()
        {
            List<ESLLocation> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = GET_ESL_LOCATIONS;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ESLLocation>();
                            while (dr.Read())
                            {
                                ESLLocation obj = new ESLLocation();
                                obj.LocationID = dr.GetInt16("LocationId");
                                obj.LocationName = dr.GetString("LocationName");
                                obj.LocationType = dr.GetString("LocationType");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLocations.", ex);
            }
        }

        private ESLLIMSData GetLIMSData(int locationID, string castNo)
        {
            ESLLIMSData limsData = null;

            try
            {
                using (SqlConnection conn = new SqlConnection(_limsConnectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = string.Empty;
                        if (locationID == 1)
                        {
                            sqlStr = GET_LIMS_DATA_BF2;
                        }
                        else
                        {
                            sqlStr = GET_LIMS_DATA_BF3;
                        }

                        sqlStr = sqlStr.Replace("@CastNo", castNo);
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new List<ESLLocation>();
                            while (dr.Read())
                            {
                                limsData = new ESLLIMSData();
                                limsData.Analyst = dr.GetString("Analyst");
                                //object val = dr.GetValue("C%");
                                limsData.C = dr.GetValue("C%").ToString();
                                limsData.CastingDateTime = dr.GetDateTime("CastingDateTime");
                                limsData.CastingTime = dr.GetString("Casting_time_HR");
                                if (locationID == 1)
                                {

                                    limsData.CastNo = dr.GetString("CastNo");
                                }
                                else
                                {
                                    limsData.CastNo = dr.GetString("Cast No");
                                }
                                limsData.ClosingTime = dr.GetString("Closing_time_HR");
                                limsData.Cr = dr.GetValue("Cr").ToString();
                                limsData.EnteredBy = dr.GetString("EnteredBy");
                                limsData.LadleCount = dr.GetInt32("Ladle_Count");
                                limsData.LadleNumbers = dr.GetString("Ladle_no");
                                limsData.Mn = dr.GetValue("Mn").ToString();
                                limsData.NewCastNumber = dr.GetString("NewCastNo");
                                limsData.P = dr.GetValue("P").ToString();
                                limsData.PigIron_Grade = dr.GetString("Pig_Iron_Grade");
                                limsData.S = dr.GetValue("S").ToString();
                                limsData.SampleBy = dr.GetString("SampleBy");
                                limsData.SampleDate = dr.GetString("SampleDate");
                                limsData.SampleDateTime = dr.GetDateTime("SampleDateTime");
                                limsData.SampleName = dr.GetString("SampleName");
                                limsData.SampleTime = dr.GetString("SampleTime");
                                limsData.Shift = dr.GetString("Shift");
                                limsData.Si = dr.GetValue("Si").ToString();
                                limsData.S_P = dr.GetValue("S+P").ToString();
                                limsData.Ti = dr.GetValue("Ti").ToString();

                            }
                        }
                    }
                }
                return limsData;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLocations.", ex);
            }

        }

        private List<ESLLIMSData> GetLIMSData(int locationID, List<string> castNumbers)
        {
            List<ESLLIMSData> retList = new List<ESLLIMSData>();
            string instring = string.Empty;


            for (int i = 0; i < castNumbers.Count; i++)
            {
                instring = instring + "'" + castNumbers[i].TrimStart('0') + "'";
                if (i < (castNumbers.Count - 1))
                {
                    instring = instring + ",";
                }
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_limsConnectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = string.Empty;
                        if (locationID == 1)
                        {
                            sqlStr = GET_LIMS_DATA_BF2_LIST;
                        }
                        else
                        {
                            sqlStr = GET_LIMS_DATA_BF3_LIST;
                        }

                        sqlStr = sqlStr.Replace("@LISTDATA", instring);
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new List<ESLLocation>();
                            while (dr.Read())
                            {
                                ESLLIMSData limsData = new ESLLIMSData();
                                if (locationID == 1)
                                {
                                    limsData.LocationName = "BF2";
                                }
                                else
                                {
                                    limsData.LocationName = "BF3";
                                }

                                limsData.Analyst = dr.GetString("Analyst");
                                //object val = dr.GetValue("C%");
                                try
                                {
                                    limsData.C = dr.GetValue("C%").ToString();
                                }
                                catch (Exception ex)
                                {
                                    limsData.C = string.Empty;
                                }
                                limsData.CastingDateTime = dr.GetDateTime("SampleDateTime");
                                limsData.CastingTime = dr.GetString("Casting_time_HR");
                                if (locationID == 1)
                                {
                                    limsData.CastNo = dr.GetString("CastNo");
                                    limsData.EnteredBy = dr.GetString("EnteredBy");
                                    limsData.LadleCount = dr.GetInt32("Ladle_Count");
                                    limsData.PigIron_Grade = dr.GetString("Pig_Iron_Grade");
                                }
                                else
                                {
                                    limsData.CastNo = dr.GetString("Cast No");
                                    limsData.EnteredBy = dr.GetString("EnterdBy");
                                    limsData.LadleCount = dr.GetInt32("Ladle count");
                                    limsData.PigIron_Grade = dr.GetString("Pig Iron Grade");
                                }
                                limsData.ClosingTime = dr.GetString("Closing_time_HR");
                                try
                                {
                                    limsData.Cr = dr.GetValue("Cr").ToString();
                                }
                                catch (Exception ex)
                                {
                                    limsData.Cr = string.Empty;
                                }

                                limsData.LadleNumbers = dr.GetString("Ladle_no");
                                try
                                {

                                }
                                catch (Exception ex)
                                {
                                    limsData.Mn = string.Empty;
                                }

                                limsData.NewCastNumber = dr.GetString("NewCastNo");
                                limsData.NewCastNumber = limsData.NewCastNumber.Trim(new Char[] { '-' }); //edited fr dash
                                try
                                {
                                    limsData.P = dr.GetValue("P").ToString();
                                }
                                catch (Exception ex)
                                {
                                    limsData.P = string.Empty;
                                }


                                try
                                {
                                    limsData.S = dr.GetValue("S").ToString();
                                }
                                catch
                                {
                                    limsData.S = string.Empty;
                                }

                                limsData.SampleBy = dr.GetString("SampleBy");
                                limsData.SampleDate = dr.GetString("SampleDate");
                                limsData.SampleDateTime = dr.GetDateTime("SampleDateTime");
                                limsData.SampleName = dr.GetString("SampleName");
                                limsData.SampleTime = dr.GetString("SampleTime");
                                limsData.Shift = dr.GetString("Shift");
                                try
                                {
                                    limsData.Si = dr.GetValue("Si").ToString();
                                }
                                catch (Exception ex)
                                {
                                    limsData.Si = string.Empty;
                                }
                                try
                                {
                                    limsData.S_P = dr.GetValue("S+P").ToString();
                                }
                                catch (Exception ex)
                                {
                                    limsData.S_P = string.Empty;
                                }
                                try
                                {
                                    limsData.Ti = dr.GetValue("Ti").ToString();
                                }
                                catch
                                {
                                    limsData.Ti = string.Empty;
                                }

                                retList.Add(limsData);
                            }
                        }
                    }
                }
                return retList;
            }
            catch (SqlException ex)
            {
                throw ex;
                // throw new Exception("Error at method GetLIMSData.", ex);
                //LogManager.Logger.LogError(ex.Message+"***"+ex.InnerException.StackTrace);
                //string strPath = "Logs/ErrorLog.txt";
                //if (!File.Exists(strPath))
                //{
                //    File.Create(strPath).Dispose();
                //}
                //using (StreamWriter sw = File.AppendText(strPath))
                //{
                //    sw.WriteLine("=============Error Logging ===========");
                //    sw.WriteLine("===========Start============= " + DateTime.Now);
                //    sw.WriteLine("Error Message: " + ex.Message + ":" + ex.InnerException?.Message);
                //    sw.WriteLine("Stack Trace: " + ex.StackTrace);
                //    sw.WriteLine("===========End============= " + DateTime.Now);
                //}

                if (retList.Count > 0)
                {
                    return retList;
                }
                else
                {
                    return null;
                }
            }

            catch (Exception ex)
            {
                throw ex;
                //throw new Exception("Error at method GetLIMSData.", ex);

                //string strPath = "Logs/ErrorLog.txt";
                //if (!File.Exists(strPath))
                //{
                //    File.Create(strPath).Dispose();
                //}
                //using (StreamWriter sw = File.AppendText(strPath))
                //{
                //    sw.WriteLine("=============Error Logging ===========");
                //    sw.WriteLine("===========Start============= " + DateTime.Now);
                //    sw.WriteLine("Error Message: " + ex.Message + ":" + ex.InnerException?.Message);
                //    sw.WriteLine("Stack Trace: " + ex.StackTrace);
                //    sw.WriteLine("===========End============= " + DateTime.Now);
                //}
                //    LogManager.Logger.LogError(ex.Message + "***" + ex.InnerException.StackTrace);
                if (retList.Count > 0)
                {
                    return retList;
                }
                else
                {
                    return null;
                }
            }

        }

        /// <summary>
        /// Gets the Current Cast No Assigned for the current Ladle...
        /// </summary>
        /// <param name="serialNo"></param>
        /// <returns></returns>
        private ReadersTransactionLog GetCurrentAssignedCastDetailforLadle(long serialNo)
        {
            ReadersTransactionLog obj = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_RTL_CAST_DTL_FOR_LADLE_ONLINE;
                        strCommand = strCommand.Replace("@AssetSerialNo", serialNo.ToString());
                        //strCommand = strCommand.Replace("@CastNoDateTime", DateTime.Now.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                //returnValue.Add(obj);
                            }
                        }
                    }
                }
                return obj;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetAssignedCastDetailforLadle.", ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialNo"></param>
        /// <param name="toDate"></param>
        /// <param name="fromDate"></param>
        /// <returns></returns>
        private List<ReadersTransactionLog> GetAssignedCastDetailforLadleInDateRange(long serialNo, DateTime toDate, DateTime fromDate)
        {
            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_RTL_CAST_DTL_FOR_LADLE_DATE_RANGE;
                        strCommand = strCommand.Replace("@AssetSerialNo", serialNo.ToString());
                        strCommand = strCommand.Replace("@ToCastNoDateTime", toDate.ToString("yyyy-MM-dd H:mm:ss"));
                        strCommand = strCommand.Replace("@FromCastNoDateTime", fromDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetAssignedCastDetailforLadle.", ex);
            }
        }

        /// <summary>
        /// Get Ladle Weighment 
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="transactionDateTime"></param>
        /// <returns></returns>
        private ESLLadleWeightData GetLadleWeighmentData(long serialNumber, DateTime transactionDateTime)
        {
            ESLLadleWeightData obj = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLE_WEIGHMENT_DATA;
                        strCommand = strCommand.Replace("@LaddleNumber", serialNumber.ToString());
                        strCommand = strCommand.Replace("@QueryDateTime", transactionDateTime.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                obj = new ESLLadleWeightData();
                                obj.TransID = dr.GetDecimal("Tran_ID").ToString();
                                obj.GrossDateTime = dr.GetDateTime("GrossDateTime");
                                obj.TareDateTime = dr.GetDateTime("TaredateTime");
                                obj.LadleNo = dr.GetString("LaddleNumber");
                                obj.GrossWeight = dr.GetDecimal("GrossWT");
                                obj.TareWeight = dr.GetDecimal("TareWT");
                                obj.NetWeight = dr.GetDecimal("NetWT");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                obj.Sender = dr.GetString("SenderPlant");
                                obj.CastNumber = dr.GetString("CastNumber");
                                obj.Receiver = dr.GetString("ReceiverPlant");
                                obj.Gross_DT_Time = dr.GetDateTime("Gross_DT_Time");
                                obj.FID = dr.GetDecimal("FID").ToString();

                            }
                        }
                    }
                }
                return obj;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleWeighmentData.", ex);
            }
        }


        private ESLLadleWeightData GetLadleWeighmentData(string ladleNo, DateTime fromDate, DateTime toDate)
        {
            ESLLadleWeightData obj = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_wbDataConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLE_WEIGHMENT_DATA_DATE_RANGE;
                        strCommand = strCommand.Replace("@LaddleNumber", ladleNo);
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd H:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                obj = new ESLLadleWeightData();
                                obj.TransID = dr.GetDecimal("Tran_ID").ToString();
                                obj.GrossDateTime = dr.GetDateTime("GrossDateTime");
                                obj.TareDateTime = dr.GetDateTime("TaredateTime");
                                obj.LadleNo = dr.GetString("LaddleNumber");
                                obj.GrossWeight = dr.GetDecimal("GrossWT");
                                obj.TareWeight = dr.GetDecimal("TareWT");
                                obj.NetWeight = dr.GetDecimal("NetWT");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                obj.Sender = dr.GetString("SenderPlant");
                                obj.CastNumber = dr.GetString("CastNumber");
                                obj.Receiver = dr.GetString("ReceiverPlant");
                                obj.Gross_DT_Time = dr.GetDateTime("Gross_DT_Time");
                                obj.FID = dr.GetDecimal("FID").ToString();

                            }
                        }
                    }
                }
                return obj;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleWeighmentData.", ex);
            }
        }


        private ESLLadleWeightData GetLadleWeighmentData(string ladleNo, DateTime fromDate)
        {
            ESLLadleWeightData obj = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_wbDataConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLE_WEIGHMENT_DATA_DATE_RANGE_FROM;
                        strCommand = strCommand.Replace("@LaddleNumber", ladleNo);
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd H:mm:ss"));
                        //strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                obj = new ESLLadleWeightData();
                                obj.TransID = dr.GetDecimal("Tran_ID").ToString();
                                obj.GrossDateTime = dr.GetDateTime("GrossDateTime");
                                obj.TareDateTime = dr.GetDateTime("TaredateTime");
                                obj.LadleNo = dr.GetString("LaddleNumber");
                                obj.GrossWeight = dr.GetDecimal("GrossWT");
                                obj.TareWeight = dr.GetDecimal("TareWT");
                                obj.NetWeight = dr.GetDecimal("NetWT");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                obj.Sender = dr.GetString("SenderPlant");
                                obj.CastNumber = dr.GetString("CastNumber");
                                obj.Receiver = dr.GetString("ReceiverPlant");
                                obj.Gross_DT_Time = dr.GetDateTime("Gross_DT_Time");
                                obj.FID = dr.GetDecimal("FID").ToString();

                            }
                        }
                    }
                }
                return obj;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleWeighmentData.", ex);
                //return null;
            }
        }


        public List<ESLLadleWeightData> GetWeighmentData(DateTime fromDate, DateTime toDate)
        {
            List<ESLLadleWeightData> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_wbDataConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_WEIGHMENT_DATA_DATE_RANGE_FROM;
                        //strCommand = strCommand.Replace("@LaddleNumber", ladleNo);
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd H:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            if (returnValue == null)
                                returnValue = new List<ESLLadleWeightData>();
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                ESLLadleWeightData obj = new ESLLadleWeightData();
                                obj.TransID = dr.GetDecimal("Tran_ID").ToString();
                                obj.GrossDateTime = dr.GetDateTime("GrossDateTime");
                                obj.TareDateTime = dr.GetDateTime("TaredateTime");
                                obj.LadleNo = dr.GetString("LaddleNumber");
                                obj.GrossWeight = dr.GetDecimal("GrossWT");
                                obj.TareWeight = dr.GetDecimal("TareWT");
                                obj.NetWeight = dr.GetDecimal("NetWT");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                obj.Sender = dr.GetString("SenderPlant");
                                obj.CastNumber = dr.GetString("CastNumber");
                                obj.Receiver = dr.GetString("ReceiverPlant");
                                obj.Gross_DT_Time = dr.GetDateTime("Gross_DT_Time");
                                obj.FID = dr.GetDecimal("FID").ToString();

                                //Get Consumption Data..

                                obj.ConsumptionData = GetWeighmentDataConsumption(obj.TransID);

                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetWeighmentData.", ex);
                //return null;
            }
        }

        private List<ESLLadleWeightDataConsumption> GetWeighmentDataConsumption(string consumptionID)
        {
            List<ESLLadleWeightDataConsumption> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_wbDataConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_WEIGHMENT_DATA_CONSUMP_BY_ID;
                        strCommand = strCommand.Replace("@Consumption_Id", consumptionID);
                        //strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd H:mm:ss"));
                        //strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            if (returnValue == null)
                                returnValue = new List<ESLLadleWeightDataConsumption>();
                            //returnValue = new ReadersTransactionLog();
                            while (dr.Read())
                            {
                                ESLLadleWeightDataConsumption obj = new ESLLadleWeightDataConsumption();
                                obj.TransID = dr.GetDecimal("Tran_ID").ToString();
                                obj.LadleNo = dr.GetString("LaddleNumber");
                                obj.LaddleNumber_SL = dr.GetDecimal("LaddleNumber_SL");
                                obj.GrossDateTime = dr.GetDateTime("GrossDateTime");
                                obj.TareDateTime = dr.GetDateTime("TaredateTime");
                                obj.GrossWeight = dr.GetDecimal("GrossWT");
                                obj.TareWeight = dr.GetDecimal("TareWT");
                                obj.NetWeight = dr.GetDecimal("NetWT");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                obj.Sender = dr.GetString("SenderPlant");
                                obj.CastNumber = dr.GetString("CastNumber");
                                obj.Receiver = dr.GetString("ReceiverPlant");
                                //obj.Gross_DT_Time = dr.GetDateTime("Gross_DT_Time");
                                obj.FID = dr.GetDecimal("FID").ToString();
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetWeighmentData.", ex);
                //return null;
            }
        }



        private List<ESLCastTransaction> GetCastTransactions(DateTime startDate, DateTime endDate)
        {
            List<ESLCastTransaction> castTransactions = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_CAST_TRANSACTIONS;
                        strCommand = strCommand.Replace("@fromDate", startDate.ToString("yyyy-MM-dd H:mm:ss"));
                        strCommand = strCommand.Replace("@toDate", endDate.ToString("yyyy-MM-dd H:mm:ss"));
                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            castTransactions = new List<ESLCastTransaction>();

                            while (dr.Read())
                            {
                                ESLCastTransaction obj = new ESLCastTransaction();
                                obj.CTID = dr.GetGuid("CTID");
                                obj.CastNo = dr.GetString("CastNo");
                                obj.Temperature = dr.GetDecimal("Temperature");
                                obj.LocationID = dr.GetInt32("LocationID");
                                obj.CreateDateTime = dr.GetDateTime("CreatedDateTime");
                                castTransactions.Add(obj);
                            }
                        }
                    }
                }
                return castTransactions;
            }
            catch (SqlException ex)
            {
                castTransactions = null;
                throw new Exception("Error at method GetCastTransactions.", ex);
            }
            return castTransactions;
        }

        private List<ESLLadle> GetLadleListForCastTransaction(Guid castTransactionID)
        {
            List<ESLLadle> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = GET_LADLES_FOR_CAST_TRANSACTION;
                        sqlStr = sqlStr.Replace("@CTID", castTransactionID.ToString());
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ESLLadle>();
                            while (dr.Read())
                            {
                                ESLLadle obj = new ESLLadle();
                                obj.ID = dr.GetGuid("AssetID");
                                obj.LadleNo = dr.GetString("AName");
                                obj.SerialNo = dr.GetString("ASerialNo").ToString();
                                obj.TagID = dr.GetString("ATagID");
                                obj.Name = dr.GetString("ADescription");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleListForCastTransaction.", ex);
            }
        }


        private string ToClassString(List<ESLLadleConsolidated> summary)
        {
            if (summary == null)
                return null;
            var builder = new StringBuilder();

            builder.Append("[ ");

            string HEADERSTR = "";
            string DYNAHEADERSTR = "";
            string DATASTR = "";
            int j = 0;
            int dynaHeaderCount = 0;
            int reccount = 0;

            foreach (ESLLadleConsolidated lc in summary)
            {
                reccount++;
                if (j == 0)
                {
                    HEADERSTR = HEADERSTR + "SrNO,Ladle No,Source Location,Source In Date,Source In Time," +
                                "Source Out Date,Source Out Time,Gross Weight,Gross Weight DateTime," +
                                "Cast No,Tare Weight,Tare Weight DateTime,Net Weight,TAT";
                }

                DATASTR = DATASTR + lc.SerialNumber + "," + lc.LadleNo + "," + lc.SourceLocation + ","
                        + lc.SourceINDateSTR + "," + lc.SourceINTimeSTR + "," + lc.SourceOUTDateSTR + ","
                        + lc.SourceOUTTimeSTR + "," + lc.SourceWeight + "," + lc.SourceWeightDateTime + ","
                        + lc.CastNo + "," + lc.TareWeight + "," + lc.TareWeightDateTime + ","
                        + lc.NetWeight + "," + lc.TAT + ",";

                builder.Append("{ ");

                builder.Append("\"Srno\":\"" + lc.SerialNumber + "\",");
                builder.Append("\"Ladle No\":\"" + lc.LadleNo + "\",");
                builder.Append("\"Source Location\":\"" + lc.SourceLocation + "\",");
                if (lc.SourceINDateSTR == string.Empty)
                {
                    builder.Append("\"Source In Date\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Source In Date\":\"" + lc.SourceINDateSTR + "\",");
                }

                if (lc.SourceINTimeSTR == string.Empty)
                {
                    builder.Append("\"Source In Time\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Source In Time\":\"" + lc.SourceINTimeSTR + "\",");
                }

                if (lc.SourceOUTDateSTR == string.Empty)
                {
                    builder.Append("\"Source Out Date\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Source Out Date\":\"" + lc.SourceOUTDateSTR + "\",");
                }

                if (lc.SourceOUTTimeSTR == string.Empty)
                {
                    builder.Append("\"Source Out Time\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Source Out Time\":\"" + lc.SourceOUTTimeSTR + "\",");
                }

                if (lc.SourceWeight == string.Empty)
                {
                    builder.Append("\"Gross Weight\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Gross Weight\":\"" + lc.SourceWeight + "\",");
                }

                if (lc.SourceWeightDateTime == string.Empty)
                {
                    builder.Append("\"Gross Weight DateTime\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Gross Weight DateTime\":\"" + lc.SourceWeightDateTime + "\",");
                }

                if (lc.CastNo == string.Empty)
                {
                    builder.Append("\"Cast No\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Cast No\":\"" + lc.CastNo + "\",");
                }

                if (lc.TareWeight == string.Empty)
                {
                    builder.Append("\"Tare Weight\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Tare Weight\":\"" + lc.TareWeight + "\",");
                }

                if (lc.TareWeightDateTime == string.Empty)
                {
                    builder.Append("\"Tare Weight DateTime\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Tare Weight DateTime\":\"" + lc.TareWeightDateTime + "\",");
                }

                if (lc.NetWeight == string.Empty)
                {
                    builder.Append("\"Net Weight\":\"" + "null" + "\",");
                }
                else
                {
                    builder.Append("\"Net Weight\":\"" + lc.NetWeight + "\",");
                }

                if (lc.Destinations.Count > 0)
                {
                    if (lc.TAT == string.Empty)
                    {
                        builder.Append("\"TAT\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"TAT\":\"" + lc.TAT + "\",");
                    }
                }
                else
                {
                    if (lc.TAT == string.Empty)
                    {
                        builder.Append("\"TAT\":\"" + "null" + "\"");
                    }
                    else
                    {
                        builder.Append("\"TAT\":\"" + lc.TAT + "\"");
                    }
                }



                int counter = 0;
                for (int i = 0; i < lc.Destinations.Count; i++)
                {
                    if (dynaHeaderCount == 0)
                    {
                        //First Time Header
                        DYNAHEADERSTR = DYNAHEADERSTR + "Destination Location" + (i + 1).ToString() + "," +
                                        "Destination In Date" + (i + 1).ToString() + "," +
                                        "Destination In Time" + (i + 1).ToString() + "," +
                                        "Destination Out Date" + (i + 1).ToString() + "," +
                                        "Destination Out Time" + (i + 1).ToString() + "," +
                                        "Gross Weight" + (i + 1).ToString() + "," +
                                        "Tare Weight" + (i + 1).ToString() + "," +
                                        "Tare Weight DateTime" + (i + 1).ToString() + ",";




                        dynaHeaderCount = lc.Destinations.Count;
                    }
                    else
                    {
                        if (lc.Destinations.Count > dynaHeaderCount)
                        {
                            for (int k = dynaHeaderCount; k <= lc.Destinations.Count; k++)
                            {
                                DYNAHEADERSTR = DYNAHEADERSTR + "Destination Location" + (k).ToString() + "," +
                                        "Destination In Date" + (k).ToString() + "," +
                                        "Destination In Time" + (k).ToString() + "," +
                                        "Destination Out Date" + (k).ToString() + "," +
                                        "Destination Out Time" + (k).ToString() + "," +
                                        "Gross Weight" + (k).ToString() + "," +
                                        "Tare Weight" + (k).ToString() + "," +
                                        "Tare Weight DateTime" + (k).ToString() + ",";

                            }
                        }
                        dynaHeaderCount = lc.Destinations.Count;
                    }

                    DATASTR = DATASTR + lc.Destinations[i].DestinationLocation + "," + lc.Destinations[i].DestinationINDateSTR + "," +
                              lc.Destinations[i].DestinationINTimeSTR + "," + lc.Destinations[i].DestinationOUTDateSTR + "," +
                              lc.Destinations[i].DestinationOUTTimeSTR + "," + lc.Destinations[i].GrossWeight + "," + lc.Destinations[i].GrossWeightDateTime + "," +
                              lc.Destinations[i].TareWeight + "," + lc.Destinations[i].TareWeightDateTimeSTR + ",";

                    if (lc.Destinations[i].DestinationLocation == string.Empty)
                    {
                        builder.Append("\"Destination Location" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Destination Location" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].DestinationLocation + "\",");
                    }

                    if (lc.Destinations[i].DestinationINDateSTR == string.Empty)
                    {
                        builder.Append("\"Destination In Date" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Destination In Date" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].DestinationINDateSTR + "\",");
                    }

                    if (lc.Destinations[i].DestinationINTimeSTR == string.Empty)
                    {
                        builder.Append("\"Destination In Time" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Destination In Time" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].DestinationINTimeSTR + "\",");
                    }

                    if (lc.Destinations[i].DestinationOUTDateSTR == string.Empty)
                    {
                        builder.Append("\"Destination Out Date" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Destination Out Date" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].DestinationOUTDateSTR + "\",");
                    }

                    if (lc.Destinations[i].DestinationOUTTimeSTR == string.Empty)
                    {
                        builder.Append("\"Destination Out Time" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Destination Out Time" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].DestinationOUTTimeSTR + "\",");
                    }

                    if (lc.Destinations[i].GrossWeight == string.Empty)
                    {
                        builder.Append("\"Gross Weight" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Gross Weight" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].GrossWeight + "\",");
                    }

                    if (lc.Destinations[i].GrossWeightDateTimeSTR == string.Empty)
                    {
                        builder.Append("\"Gross Weight DateTime" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Gross Weight DateTime" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].GrossWeightDateTimeSTR + "\",");
                    }

                    if (lc.Destinations[i].TareWeight == string.Empty)
                    {
                        builder.Append("\"Tare Weight" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                    }
                    else
                    {
                        builder.Append("\"Tare Weight" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].TareWeight + "\",");
                    }

                    if (i != (lc.Destinations.Count - 1))
                    {
                        if (lc.Destinations[i].TareWeightDateTimeSTR == string.Empty)
                        {
                            builder.Append("\"Tare Weight DateTime" + (i + 1).ToString() + "\":\"" + "null" + "\",");
                        }
                        else
                        {
                            builder.Append("\"Tare Weight DateTime" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].TareWeightDateTimeSTR + "\",");
                        }
                    }
                    else
                    {
                        if (lc.Destinations[i].TareWeightDateTimeSTR == string.Empty)
                        {
                            builder.Append("\"Tare Weight DateTime" + (i + 1).ToString() + "\":\"" + "null" + "\"");
                        }
                        else
                        {
                            builder.Append("\"Tare Weight DateTime" + (i + 1).ToString() + "\":\"" + lc.Destinations[i].TareWeightDateTimeSTR + "\"");
                        }

                    }

                }

                DATASTR = DATASTR + "\r\n";

                if (reccount < summary.Count)
                    builder.Append(" } ,");
                else
                    builder.Append("}");

            }

            builder.Append("]");
            return builder.ToString();
        }


        private object ToClassString1(List<ESLLadleConsolidated> summary)
        {
            if (summary == null)
                return null;
            var builder = new StringBuilder();

            List<ExpandoObject> expandoList = new List<ExpandoObject>();
            //dynamic expando = new ExpandoObject();


            //builder.Append("[ ");

            foreach (ESLLadleConsolidated lc in summary)
            {
                dynamic expando = new ExpandoObject();
                //builder.Append("{ ");
                expando.Srno = lc.SerialNumber;
                expando.LadleNo = lc.LadleNo;
                expando.SourceLocation = lc.SourceLocation;
                expando.SourceInDate = lc.SourceINDateSTR;
                expando.SourceInTime = lc.SourceINTimeSTR;
                expando.SourceOutDate = lc.SourceOUTDateSTR;
                expando.SourceOutTime = lc.SourceOUTTimeSTR;
                expando.GrossWeight = lc.SourceWeight;
                expando.GrossWeighDateTime = lc.SourceWeightDateTime;
                expando.CastNo = lc.CastNo;
                expando.TareWeight = lc.TareWeight;
                expando.TareWeightDateTime = lc.TareWeightDateTime;
                expando.NetWeight = lc.NetWeight;
                expando.TAT = lc.TAT;

                //expandoList.Add(expando);
                int counter = 0;
                for (int i = 0; i < lc.Destinations.Count; i++)
                {

                    if (i == 0)
                    {
                        expando.DestinationLocation1 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate1 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime1 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate1 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime1 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight1 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime1 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight1 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime1 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 1)
                    {
                        expando.DestinationLocation2 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate2 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime2 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate2 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime2 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight2 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime2 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight2 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime2 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 2)
                    {
                        expando.DestinationLocation3 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate3 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime3 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate3 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime3 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight3 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime3 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight3 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime3 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 3)
                    {
                        expando.DestinationLocation3 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate3 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime3 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate3 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime3 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight3 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime3 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight3 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime3 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 4)
                    {
                        expando.DestinationLocation4 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate4 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime4 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate4 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime4 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight4 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime4 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight4 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime4 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 5)
                    {
                        expando.DestinationLocation5 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate5 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime5 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate5 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime5 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight5 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime5 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight5 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime5 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 6)
                    {
                        expando.DestinationLocation6 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate6 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime6 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate6 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime6 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight6 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime6 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight6 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime6 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 7)
                    {
                        expando.DestinationLocation7 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate7 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime7 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate7 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime7 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight7 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime7 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight7 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime7 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                    else if (i == 8)
                    {
                        expando.DestinationLocation8 = lc.Destinations[i].DestinationLocation;
                        expando.DestinationInDate8 = lc.Destinations[i].DestinationINDateSTR;
                        expando.DestinationInTime8 = lc.Destinations[i].DestinationINTimeSTR;
                        expando.DestinationOutDate8 = lc.Destinations[i].DestinationOUTDateSTR;
                        expando.DestinationOutTime8 = lc.Destinations[i].DestinationOUTTimeSTR;
                        expando.GrossWeight8 = lc.Destinations[i].GrossWeight;
                        expando.GrossWeightDateTime8 = lc.Destinations[i].GrossWeightDateTime;
                        expando.TareWeight8 = lc.Destinations[i].TareWeight;
                        expando.TareWeightDateTime8 = lc.Destinations[i].TareWeightDateTimeSTR;
                    }
                }

                expandoList.Add(expando);
                builder.Append(" } ");

            }

            return expandoList;
            //builder.Append("]");
            //return builder.ToString();
        }


        private bool GetLadleLastLocationAndTime(string serialNo, out DateTime transactionDateTime, out string locationName)
        {
            bool returnValue = false;
            locationName = string.Empty;
            transactionDateTime = DateTime.MinValue;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_LADLE_LAST_LOCATION_DATA;

                        strCommand = strCommand.Replace("@SerialNo", serialNo);
                        //strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            while (dr.Read())
                            {
                                locationName = dr.GetString("LocationName");
                                transactionDateTime = dr.GetDateTime("ServerDatetime");
                                //returnValue.Add(lNo);
                                returnValue = true;
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleLastLocationAndTime.", ex);
            }
        }

        private bool GetBoundryConditionSourceInTime(DateTime currentSourceInTime, string locationID, string serialNo, out DateTime lastSourceEntryDateTime, out short lastEntryLocationID)
        {
            //bool retValue = false;
            bool returnValue = false;
            lastEntryLocationID = 0;
            lastSourceEntryDateTime = DateTime.MinValue;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_BOUNDRY_SRC_LOC_DT;

                        strCommand = strCommand.Replace("@SerialNo", serialNo);
                        strCommand = strCommand.Replace("@QUERYDT", currentSourceInTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@LOCATIONID", locationID);

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            while (dr.Read())
                            {
                                lastEntryLocationID = dr.GetInt16("LocationID");
                                lastSourceEntryDateTime = dr.GetDateTime("ServerDatetime");
                                returnValue = true;
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetBoundryConditionSourceInTime.", ex);
            }
        }

        private bool GetBoundryConditionSourceInTimeLadleReport(DateTime currentSourceInTime, string locationID, string serialNo, out DateTime lastSourceEntryDateTime, out short lastEntryLocationID)
        {
            //bool retValue = false;
            bool returnValue = false;
            lastEntryLocationID = 0;
            lastSourceEntryDateTime = DateTime.MinValue;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_BOUNDRY_SRC_LOC_DT_LADLE;

                        strCommand = strCommand.Replace("@SerialNo", serialNo);
                        strCommand = strCommand.Replace("@QUERYDT", currentSourceInTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@LOCATIONID", locationID);

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            while (dr.Read())
                            {
                                lastEntryLocationID = dr.GetInt16("LocationID");
                                lastSourceEntryDateTime = dr.GetDateTime("ServerDatetime");
                                returnValue = true;
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetBoundryConditionSourceInTime.", ex);
            }
        }

        private bool InsertLadleAssgnments(List<ESLLadleAssignment> assignedList)
        {
            bool retValue = false;
            try
            {
                if (assignedList != null && assignedList.Count != 0)
                {
                    using (SqlConnection conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = INSERT_LADLE_ASSIGNMENT;
                            cmd.CommandType = System.Data.CommandType.Text;
                            foreach (ESLLadleAssignment la in assignedList)
                            {
                                if (!string.IsNullOrEmpty(la.AssignedProductionUit))
                                {
                                    cmd.Parameters.AddWithValue("@ID", la.ID);
                                    cmd.Parameters.AddWithValue("@LadleNo", la.LadleNo);
                                    cmd.Parameters.AddWithValue("@SerialNumber", 0);
                                    cmd.Parameters.AddWithValue("@AssignmentDateTime", DateTime.Now);
                                    cmd.Parameters.AddWithValue("@AssignedLocation", la.AssignedProductionUit);
                                    cmd.Parameters.AddWithValue("@AssignedLocationID", la.AssignedProductionUnitID);
                                    cmd.Parameters.AddWithValue("@SourceLocation", la.SourceLocationID);
                                    cmd.Parameters.AddWithValue("@SourceLocationName", la.SourceLocationName);
                                    cmd.Parameters.AddWithValue("@SourceLocationDateTime", DateTime.Now);
                                    cmd.ExecuteNonQuery();
                                    cmd.Parameters.Clear();
                                    retValue = true;
                                }
                            }

                        }
                    }
                }

                return retValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Exception in InsertLadleAssgnments.", ex);
            }
        }


        private ESLLadleAssignment GetLadleAssignment(string ladleNo, DateTime queryDateTime)
        {
            ESLLadleAssignment returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = GET_LADLE_ASSIGNMENT;
                        sqlStr = sqlStr.Replace("@QUERYDATETIME", queryDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        sqlStr = sqlStr.Replace("@LadleNo", ladleNo);
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {

                            while (dr.Read())
                            {
                                returnValue = new ESLLadleAssignment();
                                returnValue.ID = dr.GetGuid("ID").ToString();
                                returnValue.AssignedProductionUit = dr.GetString("AssignedLocation");
                                returnValue.AssignedDateTime = dr.GetDateTime("AssignmentDateTime");
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleListForCastTransaction.", ex);
            }
        }


        private string GetLadlePreviousLocation(string ladleNo, int Currentlocation)
        {
            string ReturnPreviouslocation = string.Empty;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = GET_LADLE_PREV_LOCATION;

                        sqlStr = sqlStr.Replace("@LadleNo", ladleNo);
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            while (dr.Read())
                            {
                                ReturnPreviouslocation = dr.GetString("LocationName");
                            }
                        }
                    }
                }
                return ReturnPreviouslocation;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleListForCastTransaction.", ex);
            }


        }

        public List<SMSContactDetails> GetDepartmentContactDetails()
        {
            List<SMSContactDetails> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = GET_CONTACT_DETAILS;
                        //sqlStr = sqlStr.Replace("@CTID", castTransactionID.ToString());
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<SMSContactDetails>();
                            while (dr.Read())
                            {
                                SMSContactDetails obj = new SMSContactDetails();
                                obj.UserID = dr.GetGuid("UserID");
                                obj.Username = dr.GetString("UserName");
                                obj.FirstName = dr.GetString("FirstName").ToString();
                                obj.MobileNo = dr.GetString("MobileNo");
                                obj.LastSMSDateTime = dr.GetDateTime("LastSMSDateTime");
                                obj.SMSAccess = dr.GetBoolean("SMSAccess");
                                obj.AllLocationSMSAccess = dr.GetBoolean("AllLocationSMSAccess");
                                obj.Department = dr.GetString("Department");
                                obj.LocationId = dr.GetInt32("LocationId");
                                obj.IsActive = dr.GetBoolean("IsActive");
                                obj.TransactionDateTime = dr.GetDateTime("TransactionDateTime");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method  GetDepartmentContactDetails.", ex);
            }
        }

        public bool UpdateUserSMSDateTime(List<SMSContactDetails> UserMsgDateTimeList)
        {
            bool retValue = false;
            try
            {
                if (UserMsgDateTimeList != null && UserMsgDateTimeList.Count != 0)
                {
                    using (SqlConnection conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = UPDATE_LAST_SMS_DATETIME;
                            cmd.CommandType = System.Data.CommandType.Text;
                            foreach (SMSContactDetails la in UserMsgDateTimeList)
                            {

                                cmd.Parameters.AddWithValue("@UserID", la.UserID);
                                //cmd.Parameters.AddWithValue("@Username", la.Username);
                                //cmd.Parameters.AddWithValue("@FirstName", la.FirstName);
                                //cmd.Parameters.AddWithValue("@MobileNo", la.MobileNo);
                                cmd.Parameters.AddWithValue("@LastSMSDateTime", DateTime.Now);
                                //cmd.Parameters.AddWithValue("@SMSAccess", la.SMSAccess);
                                //cmd.Parameters.AddWithValue("@Department", la.Department);
                                //cmd.Parameters.AddWithValue("@LocationId", la.LocationId);
                                //cmd.Parameters.AddWithValue("@IsActive", la.IsActive);
                                //cmd.Parameters.AddWithValue("@TransactionDateTime", la.TransactionDateTime);
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                                retValue = true;

                            }

                        }
                    }
                }

                return retValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Exception in InsertLadleAssgnments.", ex);
            }
        }


        public void InsertLocationClarificationData(ESLLocationData eSLLocationData, string transNo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(LOCATION_CLARIFCATION, connection))
                    {
                        command.Parameters.AddWithValue("@ID", Guid.NewGuid());
                        command.Parameters.AddWithValue("@TranscationNo", transNo);
                        command.Parameters.AddWithValue("@LocationID", eSLLocationData.LocationID);
                        command.Parameters.AddWithValue("@LocationName", eSLLocationData.LocationName);
                        command.Parameters.AddWithValue("@LocationType", eSLLocationData.LocationType);
                        command.Parameters.AddWithValue("@LadleCount", eSLLocationData.LadleCount);
                        command.Parameters.AddWithValue("@AverageTATSTR", eSLLocationData.AverageTATSTR);
                        command.Parameters.AddWithValue("@AverageHoldTimeSTR", eSLLocationData.AverageHoldTimeSTR);
                        command.Parameters.AddWithValue("@ServerDatetime", DateTime.Now);
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error at method  InsertLocationClarificationData.", ex);
            }
        }

        public bool ClearLocationClarificationTable(string transNo, string locationName)
        {
            bool retval = false;
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();



                    using (SqlCommand command = new SqlCommand(CLEAR_LOCATION_CLARIFCATION_TABLE, connection))
                    {
                        command.Parameters.AddWithValue("@transNo", transNo);
                        command.Parameters.AddWithValue("@LocationName", locationName);
                        var result = command.ExecuteNonQuery();

                        if (result == 1 || result == 0)
                        {
                            retval = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error at method  ClearLocationClarificationTable.", ex);
            }
            return retval;
        }



        private ESLLocationData GetLocationTATData(string locationName)
        {
            ESLLocationData returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string sqlStr = GET_LOCATION_TAT;

                        sqlStr = sqlStr.Replace("@LocationName", locationName);
                        cmd.CommandText = sqlStr;
                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {

                            while (dr.Read())
                            {
                                returnValue = new ESLLocationData();

                                returnValue.LocationName = dr.GetString("LocationName");
                                returnValue.LocationID = dr.GetInt16("LocationID");
                                returnValue.AverageTATSTR = dr.GetString("AverageTATSTR");
                                returnValue.AverageHoldTimeSTR = dr.GetString("AverageHoldTimeSTR");
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLocationTATData.", ex);
            }
        }

        public bool InsertSMSTranscationLog(SMSTranscationDetails Smslog)
        {
            bool retValue = false;
            try
            {
                if (Smslog != null)
                {
                    using (SqlConnection conn = new SqlConnection(_connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = INSERT_SMS_TRANSCATION_LOG;
                            cmd.CommandType = System.Data.CommandType.Text;

                            cmd.Parameters.AddWithValue("@TranscationId", Smslog.TranscationId);
                            cmd.Parameters.AddWithValue("@UserID", Smslog.UserID);
                            cmd.Parameters.AddWithValue("@Username", Smslog.Username);
                            cmd.Parameters.AddWithValue("@FirstName", Smslog.FirstName);
                            cmd.Parameters.AddWithValue("@IdleTime", Smslog.IdleTime);
                            cmd.Parameters.AddWithValue("@Department", Smslog.Department);
                            cmd.Parameters.AddWithValue("@LocationId", Smslog.LocationId);
                            cmd.Parameters.AddWithValue("@LadleNo", Smslog.LadleNo);
                            cmd.Parameters.AddWithValue("@SMSDateTime", Smslog.SMSDateTime);
                            cmd.Parameters.AddWithValue("@TransactionDateTime", DateTime.Now);
                            cmd.ExecuteNonQuery();
                            cmd.Parameters.Clear();
                            retValue = true;



                        }
                    }
                }

                return retValue;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception in InsertLadleAssgnments.", ex);
            }
        }

        private List<ReadersTransactionLog> GetReaderTrsactionLogForEachLadle(DateTime fromDate, DateTime toDate, string assetSerialNumber)
        {
            List<ReadersTransactionLog> returnValue = null;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_READER_TRANSACTION_LOGS_FROM_TO_DATE_FOR_EACH_LADLE;
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@SerialNumber", assetSerialNumber);

                        cmd.CommandText = strCommand;
                        cmd.CommandType = System.Data.CommandType.Text;

                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            returnValue = new List<ReadersTransactionLog>();
                            while (dr.Read())
                            {
                                ReadersTransactionLog obj = new ReadersTransactionLog();
                                obj.ID = dr.GetGuid("ID");
                                obj.AssetSerialNo = dr.GetInt64("AssetSerialNo");
                                obj.ReaderIP = dr.GetString("ReaderIP");
                                obj.AntennaID = dr.GetInt16("AntennaID");
                                obj.RSSI = dr.GetInt16("RSSI");
                                obj.LocationID = dr.GetInt16("LocationID");
                                obj.TouchPointID = dr.GetInt16("TouchPointID");
                                obj.TouchPointType = dr.GetString("TouchPointType");
                                obj.ServerDateTime = dr.GetDateTime("ServerDatetime");
                                obj.TransDateTime = dr.GetDateTime("TransDatetime");
                                obj.AssignedCastNo = dr.GetString("CastNo");
                                obj.CastAssignmentDateTime = dr.GetDateTime("CastNoDateTime");
                                obj.CastAssignmentLocationID = dr.GetInt16("CastNoLocationID");
                                returnValue.Add(obj);
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetReadersTransactionLogs.", ex);
            }
        }

        #endregion

        #region ESL HelperFunctions

        private bool ResolveLastValidLocationAndDirection(ESLLadle ladle, List<ReadersTransactionLog> trans)
        {
            bool retValue = false;
            int lastLocationID = 0;
            int lastTouchPointID = 0;
            string lastTouchPointType = string.Empty;
            DateTime lastDateTime = DateTime.Now;
            bool directionIdentified = false;
            bool addPath = false;
            //direction = "IN";
            try
            {
                if (trans != null)
                {
                    for (int i = 0; i < trans.Count; i++)
                    {
                        ReadersTransactionLog l = trans[i];

                        if (l != null)
                        {
                            if (i == 0)
                            {
                                if (l.LocationID == 1 || l.LocationID == 2)
                                {
                                    //found source ...
                                    int j = 90;
                                    ladle.CastNoQueryMinDateTime = l.ServerDateTime;
                                    ladle.CastNoLocationQueryID = l.LocationID;
                                }
                                lastLocationID = l.LocationID;
                                lastTouchPointID = l.TouchPointID;
                                lastTouchPointType = l.TouchPointType;
                                lastDateTime = l.ServerDateTime;
                                ladle.InTime = l.ServerDateTime;
                                ladle.Direction = "IN";
                                ladle.CurrentLocationID = l.LocationID;
                                ladle.CurrentTouchpointID = l.TouchPointID;
                                ladle.InTime = l.ServerDateTime;
                                ladle.LastLocationDateTime = l.ServerDateTime;
                                if (trans.Count == 1)
                                {
                                    ESLPath p = new ESLPath();
                                    p.Direction = "IN";
                                    p.LocationID = l.LocationID;
                                    p.LocationName = GetLocation(l.LocationID).LocationName;
                                    if (GetLocationType(l.LocationID) == LocationType.Weight)
                                    {
                                        p.WeightData = GetLadleWeighmentData(l.AssetSerialNo, l.ServerDateTime);
                                    }
                                    p.INTouchPointID = l.TouchPointID;
                                    p.INTransactionDateTime = l.ServerDateTime;
                                    ladle.Paths.Add(p);
                                    directionIdentified = true;

                                    break;
                                }
                            }
                            else
                            {
                                int currentLocationID = l.LocationID;
                                int currentTouchpointID = l.TouchPointID;

                                if (addPath == false)
                                {
                                    if (lastDateTime.Subtract(l.ServerDateTime).TotalMinutes > LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES) //10 Minutest
                                    {
                                        if (currentLocationID == lastLocationID)
                                        {
                                            if (currentLocationID == 1 || currentLocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = l.ServerDateTime;
                                                ladle.CastNoLocationQueryID = currentLocationID;
                                            }
                                            //This is a direction indication 
                                            ladle.Direction = "OUT";
                                            ladle.OutTime = ladle.InTime;
                                            ladle.InTime = l.ServerDateTime;
                                            ladle.LastLocationID = l.LocationID;
                                            ladle.LastTouchpointID = l.TouchPointID;
                                            ladle.LastLocationDateTime = l.ServerDateTime;

                                            directionIdentified = true;
                                            //Get The Esisting Path to Update the Out Time and details

                                            ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                            if (p != null)
                                            {
                                                p.Direction = "OUT";
                                                p.LocationID = l.LocationID;
                                                p.OUTTouchPointID = l.TouchPointID;
                                                p.OUTTransactionDateTime = l.ServerDateTime;
                                            }
                                            //ladle.Paths.Add(p);
                                            int lastIndex = trans.Count - 1;
                                            ReadersTransactionLog lastTrans = trans[lastIndex];
                                            if (lastTrans.LocationID == 1 || lastTrans.LocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = lastTrans.ServerDateTime;
                                                ladle.CastNoLocationQueryID = lastTrans.LocationID;
                                            }
                                            break;
                                        }
                                        else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                        {
                                            //if(ladle.Direction != "OUT")
                                            ladle.Direction = "IN";
                                            ladle.LastLocationID = currentLocationID;
                                            ladle.LastTouchpointID = currentTouchpointID;
                                            ladle.LastLocationDateTime = l.ServerDateTime;
                                            directionIdentified = true;

                                            if (currentLocationID == 1 || currentLocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = l.ServerDateTime;
                                                ladle.CastNoLocationQueryID = currentLocationID;
                                            }

                                            int lastIndex = trans.Count - 1;
                                            ReadersTransactionLog lastTrans = trans[lastIndex];
                                            if (lastTrans.LocationID == 1 || lastTrans.LocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = lastTrans.ServerDateTime;
                                                ladle.CastNoLocationQueryID = lastTrans.LocationID;
                                            }



                                            break;
                                        }
                                    }
                                    else
                                    {
                                        //Less then 1 Minute repeat Transaction..
                                        if (currentLocationID == lastLocationID)
                                        {
                                            //This is a direction indication 
                                            ladle.Direction = "IN";
                                            //ladle.OutTime = ladle.InTime;
                                            ladle.InTime = l.ServerDateTime;
                                            ladle.LastLocationDateTime = l.ServerDateTime;
                                            ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                            if (p != null)
                                            {
                                                p.INTransactionDateTime = l.ServerDateTime;
                                            }

                                        }
                                        else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                        {
                                            ladle.Direction = "IN";
                                            ladle.LastLocationID = currentLocationID;
                                            ladle.LastTouchpointID = currentTouchpointID;
                                            ladle.LastLocationDateTime = l.ServerDateTime;
                                            directionIdentified = true;

                                            if (currentLocationID == 1 || currentLocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = l.ServerDateTime;
                                                ladle.CastNoLocationQueryID = currentLocationID;
                                            }

                                            int lastIndex = trans.Count - 1;
                                            ReadersTransactionLog lastTrans = trans[lastIndex];
                                            if (lastTrans.LocationID == 1 || lastTrans.LocationID == 2)
                                            {
                                                ladle.CastNoQueryMinDateTime = lastTrans.ServerDateTime;
                                                ladle.CastNoLocationQueryID = lastTrans.LocationID;
                                            }


                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    int g = 0;
                                    //Add Paths....
                                }



                            }

                            lastLocationID = l.LocationID;
                            lastTouchPointID = l.TouchPointID;
                        }


                    }
                }

                if (directionIdentified)
                    retValue = true;
                else
                    retValue = false;
            }
            catch (Exception ex)
            {

                retValue = false;
                throw ex;
            }
            return retValue;
        }

        private bool ResolveLastValidLocationAndDirectionWithPath(ESLLadle ladle, List<ReadersTransactionLog> trans)
        {
            bool retValue = false;
            int lastLocationID = 0;
            int lastTouchPointID = 0;
            string lastTouchPointType = string.Empty;
            DateTime lastDateTime = DateTime.Now;
            bool directionIdentified = false;
            bool addPath = false;
            //direction = "IN";
            try
            {
                if (trans != null)
                {
                    for (int i = 0; i < trans.Count; i++)
                    {
                        ReadersTransactionLog l = trans[i];

                        if (l != null)
                        {
                            if (i == 0)
                            {
                                lastLocationID = l.LocationID;
                                lastTouchPointID = l.TouchPointID;
                                lastTouchPointType = l.TouchPointType;
                                lastDateTime = l.ServerDateTime;
                                ladle.InTime = l.ServerDateTime;
                                ladle.Direction = "IN";
                                ladle.CurrentLocationID = l.LocationID;
                                ladle.CurrentTouchpointID = l.TouchPointID;
                                ladle.InTime = l.ServerDateTime;

                                ESLPath p = new ESLPath();
                                p.Direction = "IN";
                                p.LocationID = l.LocationID;
                                p.LocationName = GetLocation(l.LocationID).LocationName;
                                if (GetLocationType(l.LocationID) == LocationType.Weight)
                                {
                                    p.WeightData = GetLadleWeighmentData(l.AssetSerialNo, l.ServerDateTime);
                                    p.IsWeighment = true;
                                }
                                p.INTouchPointID = l.TouchPointID;
                                p.INTransactionDateTime = l.ServerDateTime;
                                ladle.Paths.Add(p);
                            }
                            else
                            {
                                int currentLocationID = l.LocationID;
                                int currentTouchpointID = l.TouchPointID;


                                if (lastDateTime.Subtract(l.ServerDateTime).TotalMinutes > LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES) //10 Minutest
                                {
                                    if (currentLocationID == lastLocationID)
                                    {
                                        //This is a direction indication 
                                        ladle.Direction = "OUT";
                                        ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;
                                        ladle.LastLocationID = l.LocationID;
                                        ladle.LastTouchpointID = l.TouchPointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        directionIdentified = true;

                                        lastLocationID = l.LocationID;
                                        lastTouchPointID = l.TouchPointID;
                                        lastDateTime = l.ServerDateTime;
                                        //Get The Esisting Path to Update the Out Time and details

                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                        if (p != null)
                                        {
                                            p.Direction = "OUT";
                                            p.LocationID = l.LocationID;
                                            p.LocationName = GetLocation(l.LocationID).LocationName;
                                            p.OUTTouchPointID = l.TouchPointID;
                                            p.OUTTransactionDateTime = p.INTransactionDateTime;
                                            p.INTransactionDateTime = l.ServerDateTime;
                                            p.TimeSpent = p.OUTTransactionDateTime.Subtract(p.INTransactionDateTime);
                                        }
                                        else
                                        {
                                            //Should Never Be Here..
                                            int k = 90;
                                        }
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        //if(ladle.Direction != "OUT")
                                        ladle.Direction = "IN";
                                        ladle.LastLocationID = currentLocationID;
                                        ladle.LastTouchpointID = currentTouchpointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.LocationName = GetLocation(l.LocationID).LocationName;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(l.AssetSerialNo, l.ServerDateTime);
                                            p.IsWeighment = true;
                                        }
                                        ladle.Paths.Add(p);

                                    }
                                }
                                else
                                {
                                    //Less then 1 Minute repeat Transaction..
                                    if (currentLocationID == lastLocationID)
                                    {
                                        //This is a direction indication 
                                        ladle.Direction = "IN";
                                        //ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;
                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                        if (p != null)
                                        {
                                            p.INTransactionDateTime = l.ServerDateTime;
                                        }

                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        ladle.Direction = "IN";
                                        ladle.LastLocationID = currentLocationID;
                                        ladle.LastTouchpointID = currentTouchpointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        //Add the Path..
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.LocationName = GetLocation(l.LocationID).LocationName;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(l.AssetSerialNo, l.ServerDateTime);
                                        }
                                        ladle.Paths.Add(p);
                                        //directionIdentified = true;
                                        //break;
                                    }
                                }

                            }

                            lastLocationID = l.LocationID;
                            lastTouchPointID = l.TouchPointID;
                        }


                    }
                }

                if (directionIdentified)
                    retValue = true;
                else
                    retValue = false;
            }
            catch (Exception ex)
            {
                retValue = false;
                throw ex;
            }
            return retValue;
        }

        private bool ResolveCurrentLadlePath(ESLLadle ladle)
        {
            bool retValue = false;
            int lastLocationID = 0;
            int lastTouchPointID = 0;
            string lastTouchPointType = string.Empty;
            DateTime lastDateTime = DateTime.Now;
            bool directionIdentified = false;
            //bool addPath = false;
            //direction = "IN";
            try
            {
                long serialNo = Int64.Parse(ladle.SerialNo);


                List<ReadersTransactionLog> trans = GetReaderTrsactionLogForAsset(serialNo);
                ResolveLastValidLocationAndDirectionWithPath(ladle, trans);
                ReadersTransactionLog castTrans = GetCurrentAssignedCastDetailforLadle(serialNo);
                if (castTrans != null)
                {
                    ladle.LIMSData = GetLIMSData(castTrans.CastAssignmentLocationID, castTrans.AssignedCastNo);
                }

                if (trans != null)
                {
                    for (int i = 0; i < trans.Count; i++)
                    {
                        ReadersTransactionLog l = trans[i];

                        if (l != null)
                        {
                            if (i == 0)
                            {
                                lastLocationID = l.LocationID;
                                lastTouchPointID = l.TouchPointID;
                                lastTouchPointType = l.TouchPointType;
                                lastDateTime = l.ServerDateTime;
                                ladle.InTime = l.ServerDateTime;
                                ladle.Direction = "IN";
                                ladle.CurrentLocationID = l.LocationID;
                                ladle.CurrentTouchpointID = l.TouchPointID;
                                ladle.InTime = l.ServerDateTime;
                                ESLPath p = new ESLPath();
                                p.Direction = "IN";
                                p.LocationID = l.LocationID;
                                p.LocationName = GetLocation(l.LocationID).LocationName;
                                p.INTouchPointID = l.TouchPointID;
                                p.INTransactionDateTime = l.ServerDateTime;
                                if (GetLocationType(l.LocationID) == LocationType.Weight)
                                {
                                    p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                }
                                ladle.Paths.Add(p);
                                directionIdentified = true;

                            }
                            else
                            {
                                int currentLocationID = l.LocationID;
                                int currentTouchpointID = l.TouchPointID;


                                if (lastDateTime.Subtract(l.ServerDateTime).TotalMinutes > LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES) //10 Minutest
                                {
                                    if (currentLocationID == lastLocationID)
                                    {
                                        //This is a direction indication 
                                        ladle.Direction = "OUT";
                                        ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;
                                        ladle.LastLocationID = l.LocationID;
                                        ladle.LastTouchpointID = l.TouchPointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        directionIdentified = true;
                                        //Get The Esisting Path to Update the Out Time and details

                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);

                                        if (p != null)
                                        {
                                            p.Direction = "OUT";
                                            p.LocationID = l.LocationID;
                                            p.OUTTouchPointID = l.TouchPointID;
                                            p.OUTTransactionDateTime = l.ServerDateTime;
                                        }
                                        //ladle.Paths.Add(p);
                                        //break;
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        //if(ladle.Direction != "OUT")
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }
                                        ladle.Paths.Add(p);

                                    }
                                }
                                else
                                {
                                    //Less then 1 Minute repeat Transaction..
                                    if (currentLocationID == lastLocationID)
                                    {
                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        //p.OUTTouchPointID = l.TouchPointID;
                                        //p.OUTTransactionDateTime = l.ServerDateTime;

                                        //This is a direction indication 
                                        ladle.Direction = "IN";
                                        //ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        ladle.Direction = "IN";
                                        ladle.LastLocationID = currentLocationID;
                                        ladle.LastTouchpointID = currentTouchpointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;
                                        directionIdentified = true;
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        ladle.Paths.Add(p);

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }

                                        //break;
                                    }
                                }


                            }

                            lastLocationID = l.LocationID;
                            lastTouchPointID = l.TouchPointID;
                        }


                    }
                }

                if (directionIdentified)
                    retValue = true;
                else
                    retValue = false;
            }
            catch (Exception ex)
            {

                string msg = ex.Message;
                retValue = false;
                throw ex;
            }
            return retValue;
        }


        private bool ResolveCurrentLadlePathWithDateRange(DateTime fromDate, DateTime toDate, ESLLadle ladle)
        {
            bool retValue = false;
            int lastLocationID = 0;
            int lastTouchPointID = 0;
            string lastTouchPointType = string.Empty;
            DateTime lastDateTime = DateTime.Now;
            bool directionIdentified = false;
            //bool addPath = false;
            //direction = "IN";
            try
            {
                long serialNo = Int64.Parse(ladle.SerialNo);

                //List<ReadersTransactionLog> logs = GetSourceReaderTrsactionLogForDateTime(qfromDate, qToDate, li.SerialNo);


                List<ReadersTransactionLog> trans = GetSourceReaderTrsactionLogForDateTime(fromDate, toDate, serialNo.ToString());

                //ResolveLastValidLocationAndDirectionWithPath(ladle, trans);
                /*ReadersTransactionLog castTrans = GetCurrentAssignedCastDetailforLadle(serialNo);
                if (castTrans != null)
                {
                    ladle.LIMSData = GetLIMSData(castTrans.CastAssignmentLocationID, castTrans.AssignedCastNo);
                }*/

                if (trans != null)
                {
                    for (int i = 0; i < trans.Count; i++)
                    {
                        ReadersTransactionLog l = trans[i];

                        if (l != null)
                        {
                            if (i == 0)
                            {
                                lastLocationID = l.LocationID;
                                lastTouchPointID = l.TouchPointID;
                                lastTouchPointType = l.TouchPointType;
                                lastDateTime = l.ServerDateTime;
                                ladle.InTime = l.ServerDateTime;
                                ladle.Direction = "IN";
                                ladle.CurrentLocationID = l.LocationID;
                                ladle.CurrentTouchpointID = l.TouchPointID;
                                ladle.InTime = l.ServerDateTime;
                                ESLPath p = new ESLPath();
                                p.Direction = "IN";
                                p.LocationID = l.LocationID;
                                p.LocationName = GetLocation(l.LocationID).LocationName;
                                p.INTouchPointID = l.TouchPointID;
                                p.INTransactionDateTime = l.ServerDateTime;
                                if (GetLocationType(l.LocationID) == LocationType.Weight)
                                {
                                    p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                }
                                ladle.Paths.Add(p);
                                directionIdentified = true;

                            }
                            else
                            {
                                int currentLocationID = l.LocationID;
                                int currentTouchpointID = l.TouchPointID;


                                if (l.ServerDateTime.Subtract(lastDateTime).TotalMinutes > LADLE_AFTERNATE_TRSACTION_QUALIFICATION_TIME_IN_MINUTES) //10 Minutest
                                {
                                    if (currentLocationID == lastLocationID)
                                    {
                                        //This is a direction indication 
                                        ladle.Direction = "OUT";
                                        ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;
                                        ladle.LastLocationID = l.LocationID;
                                        ladle.LastTouchpointID = l.TouchPointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;

                                        directionIdentified = true;
                                        //Get The Esisting Path to Update the Out Time and details

                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);

                                        if (p != null)
                                        {
                                            p.Direction = "OUT";
                                            p.LocationID = l.LocationID;
                                            p.OUTTouchPointID = l.TouchPointID;
                                            p.OUTTransactionDateTime = l.ServerDateTime;
                                        }
                                        //ladle.Paths.Add(p);
                                        //break;
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        //if(ladle.Direction != "OUT")
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }
                                        ladle.Paths.Add(p);

                                    }
                                }
                                else
                                {
                                    //Less then 1 Minute repeat Transaction..
                                    if (currentLocationID == lastLocationID)
                                    {
                                        ESLPath p = ladle.GetPathForLocation(l.LocationID);
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        //p.OUTTouchPointID = l.TouchPointID;
                                        //p.OUTTransactionDateTime = l.ServerDateTime;

                                        //This is a direction indication 
                                        ladle.Direction = "IN";
                                        //ladle.OutTime = ladle.InTime;
                                        ladle.InTime = l.ServerDateTime;

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }
                                    }
                                    else if (currentLocationID != lastLocationID) //Case When we Identified a move to another location
                                    {
                                        ladle.Direction = "IN";
                                        ladle.LastLocationID = currentLocationID;
                                        ladle.LastTouchpointID = currentTouchpointID;
                                        ladle.LastLocationDateTime = l.ServerDateTime;
                                        directionIdentified = true;
                                        ESLPath p = new ESLPath();
                                        p.Direction = "IN";
                                        p.LocationID = l.LocationID;
                                        p.INTouchPointID = l.TouchPointID;
                                        p.INTransactionDateTime = l.ServerDateTime;
                                        ladle.Paths.Add(p);

                                        if (GetLocationType(l.LocationID) == LocationType.Weight)
                                        {
                                            p.WeightData = GetLadleWeighmentData(serialNo, l.ServerDateTime);
                                        }

                                        //break;
                                    }
                                }


                            }

                            lastLocationID = l.LocationID;
                            lastTouchPointID = l.TouchPointID;
                        }


                    }
                }

                if (directionIdentified)
                    retValue = true;
                else
                    retValue = false;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                retValue = false;
                throw ex;
            }
            return retValue;
        }

        private bool ResolveCurrentLadlePath(ESLLadle ladle, DateTime castOpenTime)
        {
            bool retValue = false;
            //bool addPath = false;
            //direction = "IN";
            try
            {
                long serialNo = Int64.Parse(ladle.SerialNo);


                List<ReadersTransactionLog> trans = GetReaderTrsactionLogForAsset(serialNo, castOpenTime);
                retValue = ResolveLastValidLocationAndDirectionWithPath(ladle, trans);
                ReadersTransactionLog castTrans = GetCurrentAssignedCastDetailforLadle(serialNo);
                ladle.LIMSData = GetLIMSData(castTrans.CastAssignmentLocationID, castTrans.AssignedCastNo);

            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                retValue = false;
                throw ex;
            }
            return retValue;
        }

        private string GetLocationType(int locationID)
        {
            string retValue = "";

            if (_locations != null)
            {
                foreach (ESLLocation l in _locations)
                {
                    if (l.LocationID == locationID)
                    {
                        retValue = l.LocationType;
                        break;
                    }
                }
            }
            return retValue;
        }

        private ESLLocation GetLocation(int locationID)
        {
            ESLLocation retValue = null;

            if (_locations != null)
            {
                foreach (ESLLocation l in _locations)
                {
                    if (l.LocationID == locationID)
                    {
                        retValue = l;
                        break;
                    }
                }
            }
            return retValue;
        }

        private ESLLocation GetLocation(string locationName)
        {
            ESLLocation retValue = null;

            if (_locations != null)
            {
                foreach (ESLLocation l in _locations)
                {
                    if (l.LocationName == locationName)
                    {
                        retValue = l;
                        break;
                    }
                }
            }
            return retValue;
        }

        private ESLLadle GetLadleByID(long ladleID)
        {
            ESLLadle retValue = null;
            if (_ladles != null)
            {
                foreach (ESLLadle l in _ladles)
                {
                    if (l.SerialNo == ladleID.ToString())
                    {
                        retValue = l;
                        break;
                    }
                }
            }
            return retValue;
        }

        private ESLLadle GetLadleByID(string ladleNo)
        {
            ESLLadle retValue = null;
            if (_ladles != null)
            {
                foreach (ESLLadle l in _ladles)
                {
                    if (l.LadleNo == ladleNo)
                    {
                        retValue = l;
                        break;
                    }
                }
            }
            return retValue;
        }

        private DateTime GetDateTimeLimitForShift()
        {
            DateTime currentTime = DateTime.Now;
            if (currentTime.Hour >= 22)
            {
                currentTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 22, 0, 0);
            }
            else
            {
                currentTime = currentTime.Subtract(TimeSpan.FromDays(1));
                currentTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 22, 0, 0);
                //string val = currentTime.ToString();
            }
            return currentTime;
        }

        private DateTime GetFromToDateTimeLimitForShift(DateTime rangeDateTime)
        {
            DateTime currentTime = rangeDateTime;
            if (currentTime.Hour >= 22)
            {
                currentTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 22, 0, 0);
            }
            else
            {
                //currentTime = currentTime.Subtract(TimeSpan.FromDays(1));
                currentTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 22, 0, 0);
                //string val = currentTime.ToString();
            }
            return currentTime;
        }

        private bool FindLadleInOnlineLocations(List<ESLLocation> locationDataObject, string ladleNoToFind)
        {
            bool retValue = false;

            foreach (ESLLocation l in locationDataObject)
            {
                if (l.LadleList.Find(x => x.LadleNo == ladleNoToFind) != null)
                {
                    retValue = true;
                    break;
                }
            }
            return retValue;
        }



        public void InsertIdleLadleRecord(ESLLadle eSLIdleLadle)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();



                    using (SqlCommand command = new SqlCommand(INSERT_IDLE_LADLE, connection))
                    {
                        // Add parameters with values from the eSLIdleLadle object
                        command.Parameters.AddWithValue("@ID", Guid.NewGuid());
                        command.Parameters.AddWithValue("@LadleName", eSLIdleLadle.Name);
                        command.Parameters.AddWithValue("@LastLocationDateTime", eSLIdleLadle.LastLocationDateTime);
                        command.Parameters.AddWithValue("@LastLocationDateTimeSTR", eSLIdleLadle.LastLocationDateTimeSTR);
                        command.Parameters.AddWithValue("@LastLocationName", eSLIdleLadle.LastLocationName);
                        command.Parameters.AddWithValue("@LastLocationID", eSLIdleLadle.LastLocationID);
                        command.Parameters.AddWithValue("@TimeSpentSTR", eSLIdleLadle.TimeSpentSTR);
                        command.Parameters.AddWithValue("@Serverdatetime", DateTime.Now);

                        // Execute the command
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error at method InsertIdleLadleRecords.", ex);
            }
        }

        private bool GetIdleLadleLastLocationAndTime(string serialNo, out DateTime transactionDateTime, out string locationName, DateTime fromDate, DateTime toDate)
        {
            bool returnValue = false;
            locationName = string.Empty;
            transactionDateTime = DateTime.MinValue;
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        string strCommand = GET_IDLE_LADLE_LAST_LOCATION_DATA;

                        strCommand = strCommand.Replace("@SerialNo", serialNo);
                        strCommand = strCommand.Replace("@FROMDATE", fromDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));
                        //strCommand = strCommand.Replace("@TODATE", toDate.ToString("yyyy-MM-dd HH:mm:ss"));

                        cmd.CommandText = strCommand;

                        cmd.CommandType = System.Data.CommandType.Text;
                        using (PSLDataReader dr = new PSLDataReader(cmd.ExecuteReader()))
                        {
                            while (dr.Read())
                            {
                                locationName = dr.GetString("LocationName");
                                transactionDateTime = dr.GetDateTime("ServerDatetime");
                                //returnValue.Add(lNo);
                                returnValue = true;
                            }
                        }
                    }
                }
                return returnValue;
            }
            catch (SqlException ex)
            {
                throw new Exception("Error at method GetLadleLastLocationAndTime.", ex);
            }
        }


        #endregion
    }

    public static class LocationType
    {
        public static string Source = "FURNACE";
        public static string Weight = "WEIGHMENT";
        public static string Production = "PROD";
        public static string Maintainance = "MAINTANANCE";
    }
}
