using log4net;
using System.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using PSL.Infinity.ESLLadleTracker;

namespace ESL_Api.Global
{
    public static class Appsetting
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string ConnectionString { get; private set; }
        public static string esslLadleTrackDBConnString { get; private set; }
        public static string DomainName { get; private set; } = "ESL01.VEDANTARESOURCE.LOCAL";
        public static string ActivationKey { get; private set; } = "78536E5557697A637453794168454A354F53434F54773D3D";
        public static bool UseADAuthentication { get; private set; } = false;

        public static string AD_SERVER { get; private set; } = "172.17.20.121";
        public static int AD_PORT { get; private set; } = 389;
        public static string AD_BASE_DN { get; private set; } = "OU=Users,OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local";
        public static string AD_DOMAIN_SUFFIX { get; private set; } = "ESL01";
        public static string AD_BIND_DN { get; private set; } = "CN=ESL01 LDAP,OU=Service Account,OU=Generic Users,OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local";
        public static string AD_BIND_PASSWORD { get; private set; } = "Secure@es1";
        public static int AD_TIMEOUT_MS { get; private set; } = 8000;

        public static string limsLadleTrackDB { get; private set; }
        public static string wbDatabase { get; private set; }
        public static Engine _laddleTrackerEngine = null;

        public static void loadAppsetting()
        {
            // 1. Initialize SQL Stored Procedure command names FIRST so they are ALWAYS available
            SQLQueryCommand.loadSQLQueryCommands();

            // 2. Read AppSettings
            try
            {
                DomainName = GetAppSetting("domainName", "ESL01.VEDANTARESOURCE.LOCAL");
                ActivationKey = GetAppSetting("ActivationKey", "78536E5557697A637453794168454A354F53434F54773D3D");
                
                string useAd = GetAppSetting("UseADAuthentication", "false");
                bool isAd;
                UseADAuthentication = bool.TryParse(useAd, out isAd) ? isAd : false;

                AD_SERVER = GetAppSetting("AD_SERVER", "172.17.20.121");
                int port;
                AD_PORT = int.TryParse(GetAppSetting("AD_PORT", "389"), out port) ? port : 389;
                AD_BASE_DN = GetAppSetting("AD_BASE_DN", "OU=Users,OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local");
                AD_DOMAIN_SUFFIX = GetAppSetting("AD_DOMAIN_SUFFIX", "ESL01");
                AD_BIND_DN = GetAppSetting("AD_BIND_DN", "CN=ESL01 LDAP,OU=Service Account,OU=Generic Users,OU=Electro Steels Limited,DC=ESL01,DC=vedantaresource,DC=local");
                AD_BIND_PASSWORD = GetAppSetting("AD_BIND_PASSWORD", "Secure@es1");
                int timeoutMs;
                AD_TIMEOUT_MS = int.TryParse(GetAppSetting("AD_TIMEOUT_MS", "8000"), out timeoutMs) ? timeoutMs : 8000;
            }
            catch (Exception ex)
            {
                Log.Warn("[loadAppsetting] Warning reading AppSettings: " + ex.Message);
            }

            // 3. Read Database Connection Strings
            try
            {
                ConnectionString = ConfigurationManager.ConnectionStrings["ConnString"]?.ConnectionString?.Trim();
                esslLadleTrackDBConnString = ConfigurationManager.ConnectionStrings["ESLDB"]?.ConnectionString?.Trim();
                limsLadleTrackDB = ConfigurationManager.ConnectionStrings["LIMSDB"]?.ConnectionString?.Trim();
                wbDatabase = ConfigurationManager.ConnectionStrings["WBDB"]?.ConnectionString?.Trim();

                if (!string.IsNullOrEmpty(esslLadleTrackDBConnString))
                {
                    _laddleTrackerEngine = new Engine(esslLadleTrackDBConnString, limsLadleTrackDB, wbDatabase);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[loadAppsetting] Error initializing database engine: " + ex.Message, ex);
            }
        }

        private static string GetAppSetting(string key, string defaultValue = "")
        {
            try
            {
                var val = ConfigurationManager.AppSettings[key];
                return val != null ? val.Trim() : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static class SQLQueryCommand
        {
            #region PDA
            public static string PDA_SP_LoginData { get; private set; } = "ESL_PDA_SP_LoginData";
            public static string PDA_SP_Logout { get; private set; } = "ESL_PDA_SP_Logout";
            public static string PDA_SP_SyncData { get; private set; } = "ESL_PDA_SP_SyncData";
            public static string PDA_SP_GetLadleByFurnace { get; private set; } = "ESL_PDA_SP_GetLadleByFurnace_V1";
            public static string PDA_SP_CreateCastAssignment { get; private set; } = "ESL_PDA_SP_CreateCastAssignment_V1";
            public static string PDA_SP_CreateLadleRequest { get; private set; } = "ESL_PDA_SP_CreateLadleRequest";
            public static string PDA_SP_GetLadleRequest { get; private set; } = "ESL_PDA_SP_GetLadleRequest_V1";
            public static string PDA_SP_UpdateLadleRequest { get; private set; } = "ESL_PDA_SP_UpdateLadleRequest";
            public static string PDA_SP_GetReversalLadles { get; private set; } = "ESL_PDA_SP_GetReversalLadles";
            public static string PDA_SP_CreateRevarsalMovement { get; private set; } = "ESL_PDA_SP_CreateRevarsalMovement_V1";
            public static string PDA_SP_GetLadleMovement { get; private set; } = "ESL_PDA_SP_GetLadleMovement_V1";
            public static string PDA_SP_CloseLadleMovement { get; private set; } = "ESL_PDA_SP_CloseLadleMovement_V2";
            public static string PDA_SP_TransferLadleMovement { get; private set; } = "ESL_PDA_SP_TransferLadleMovement";
            public static string PDA_SP_GetAvailableLadles { get; private set; } = "ESL_PDA_SP_GetAvailableLadles";
            public static string PDA_SP_RequestAvailableLadles { get; private set; } = "ESL_PDA_SP_RequestAvailableLadles";
            public static string PDA_SP_GetSummary { get; private set; } = "ESL_PDA_SP_GetSummary_V2";
            public static string PDA_SP_UpdateBookingLadles { get; private set; } = "ESL_PDA_SP_UpdateBookingLadles";
            public static string PDA_SP_GetEmptyLadlesForBF { get; private set; } = "ESL_PDA_SP_GetEmptyLadlesForBF";
            public static string PDA_SP_RequestReversalLadles { get; private set; } = "ESL_PDA_SP_RequestReversalLadles_V1";
            public static string PDA_SP_UpdateReversalBookingLadles { get; private set; } = "ESL_PDA_SP_UpdateReversalBookingLadles";
            public static string PDA_SP_GetTransactionLadles { get; private set; } = "ESL_PDA_SP_GetTransactionLadles";
            public static string PDA_SP_GetRequestReport { get; private set; } = "ESL_PDA_SP_GetRequestReport";
            public static string PDA_SP_GetFullReport { get; private set; } = "ESL_PDA_SP_GetFullReport";
            public static string WEB_SP_CompleteLadleRequest { get; private set; } = "ESL_WEB_SP_CompleteLadleRequest";
            #endregion

            #region Dashboard
            public static string WEB_SP_DashboardLogin { get; private set; } = "ESL_WEB_SP_DashboardLogin";
            public static string WEB_SP_Logout { get; private set; } = "ESL_WEB_SP_Logout";
            public static string SP_GetAllBFLadle { get; private set; } = "ESL_WEB_SP_GetAllBFLadle";
            public static string SP_GetLadleRequest { get; private set; } = "ESL_WEB_SP_GetLadleRequest_V1";
            public static string SP_GetLadleMovement { get; private set; } = "ESL_WEB_SP_GetLadleMovement_V2";
            public static string WEB_SP_CreateMovement { get; private set; } = "ESL_WEB_SP_CreateMovement_V1";
            public static string WEB_SP_CreateMovementTrips { get; private set; } = "ESL_WEB_SP_CreateMovementTrips";
            public static string GetTripSerialNumber { get; private set; } = "GetTripSerialNumber";
            public static string WEB_SP_GetAllLocoOccupied { get; private set; } = "ESL_WEB_SP_GetAllLocoOccupied";
            public static string WEB_SP_GetDashboardData { get; private set; } = "ESL_WEB_SP_GetDashboardData";
            public static string WEB_SP_GetReversalLadles { get; private set; } = "ESL_WEB_SP_GetReversalLadles";
            public static string WEB_SP_InsertLadleReversal { get; private set; } = "ESL_WEB_SP_InsertLadleReversal_V4";
            public static string WEB_SP_AssignedLocoToLadle { get; private set; } = "ESL_WEB_SP_AssignedLocoToLadle";
            public static string WEB_SP_GetLadleMovementTrailLocation { get; private set; } = "ESL_WEB_SP_GetLadleMovementTrailLocation";
            public static string WEB_SP_DeleteLadleFromMovement { get; private set; } = "ESL_WEB_SP_DeleteLadleFromMovement_V4";
            public static string WEB_SP_CompleteReversalLadleRequest { get; private set; } = "ESL_WEB_SP_CompleteReversalLadleRequest_V1";
            public static string ESL_WEB_GetUserPassword { get; private set; } = "ESL_WEB_GetUserPassword";
            public static string WEB_SP_GetTransactionReport { get; private set; } = "ESL_WEB_SP_GetTransactionReport";
            public static string WEB_SP_TransferLoco { get; private set; } = "ESL_WEB_SP_TransferLoco";
            public static string WEB_SP_GetLadleWeighmentReport { get; private set; } = "ESL_WEB_SP_GetLadleWeighmentReport";
            public static string WEB_SP_GetManualVsAutoAssignmentReport { get; private set; } = "ESL_WEB_SP_GetManualVsAutoAssignmentReport";
            #endregion

            #region Hot Metal
            public static string SP_GetHotmetalBooking { get; private set; } = "SP_GetHotmetalBooking";
            public static string SP_InsertProductionOrder { get; private set; } = "SP_InsertProductionOrder";
            public static string SP_GetProductionReport { get; private set; } = "SP_GetProductionReport";
            public static string SP_GetPrefixByLocation { get; private set; } = "SP_GetPrefixByLocation";
            #endregion

            #region Loco Ladle Mapping
            public static string WEB_SP_GetLatestLocoLadleMapping { get; private set; } = "ESL_WEB_SP_GetLatestLocoLadleMapping";
            #endregion

            #region System Status
            public static string WEB_SP_GetSystemStatus { get; private set; } = "ESL_WEB_SP_GetSystemStatus";
            #endregion

            #region New 
            public static string WEB_SP_GetReaderTransactionLadles { get; private set; }
            public static string WEB_SP_GetEmptyLadlesForBooking { get; private set; }
            public static string WEB_SP_RequestEmptyLadles { get; private set; }
            #endregion


            public static void loadSQLQueryCommands()
            {
                try
                {
                    #region PDA
                    PDA_SP_LoginData = GetAppSetting("PDA_SP_LoginData", "ESL_PDA_SP_LoginData");
                    PDA_SP_Logout = GetAppSetting("PDA_SP_Logout", "ESL_PDA_SP_Logout");
                    PDA_SP_SyncData = GetAppSetting("PDA_SP_SyncData", "ESL_PDA_SP_SyncData");
                    PDA_SP_GetLadleByFurnace = GetAppSetting("PDA_SP_GetLadleByFurnace", "ESL_PDA_SP_GetLadleByFurnace_V1");
                    PDA_SP_CreateCastAssignment = GetAppSetting("PDA_SP_CreateCastAssignment", "ESL_PDA_SP_CreateCastAssignment_V1");
                    PDA_SP_CreateLadleRequest = GetAppSetting("PDA_SP_CreateLadleRequest", "ESL_PDA_SP_CreateLadleRequest");
                    PDA_SP_GetLadleRequest = GetAppSetting("PDA_SP_GetLadleRequest", "ESL_PDA_SP_GetLadleRequest_V1");
                    PDA_SP_UpdateLadleRequest = GetAppSetting("PDA_SP_UpdateLadleRequest", "ESL_PDA_SP_UpdateLadleRequest");
                    PDA_SP_GetReversalLadles = GetAppSetting("PDA_SP_GetReversalLadles", "ESL_PDA_SP_GetReversalLadles");
                    PDA_SP_CreateRevarsalMovement = GetAppSetting("PDA_SP_CreateRevarsalMovement", "ESL_PDA_SP_CreateRevarsalMovement_V1");
                    PDA_SP_GetLadleMovement = GetAppSetting("PDA_SP_GetLadleMovement", "ESL_PDA_SP_GetLadleMovement_V1");
                    PDA_SP_CloseLadleMovement = GetAppSetting("PDA_SP_CloseLadleMovement", "ESL_PDA_SP_CloseLadleMovement_V2");
                    PDA_SP_TransferLadleMovement = GetAppSetting("PDA_SP_TransferLadleMovement", "ESL_PDA_SP_TransferLadleMovement");
                    PDA_SP_GetAvailableLadles = GetAppSetting("PDA_SP_GetAvailableLadles", "ESL_PDA_SP_GetAvailableLadles");
                    PDA_SP_RequestAvailableLadles = GetAppSetting("PDA_SP_RequestAvailableLadles", "ESL_PDA_SP_RequestAvailableLadles");
                    PDA_SP_GetSummary = GetAppSetting("PDA_SP_GetSummary", "ESL_PDA_SP_GetSummary_V2");
                    PDA_SP_UpdateBookingLadles = GetAppSetting("PDA_SP_UpdateBookingLadles", "ESL_PDA_SP_UpdateBookingLadles");
                    PDA_SP_GetEmptyLadlesForBF = GetAppSetting("PDA_SP_GetEmptyLadlesForBF", "ESL_PDA_SP_GetEmptyLadlesForBF");
                    PDA_SP_RequestReversalLadles = GetAppSetting("PDA_SP_RequestReversalLadles", "ESL_PDA_SP_RequestReversalLadles_V1");
                    PDA_SP_UpdateReversalBookingLadles = GetAppSetting("PDA_SP_UpdateReversalBookingLadles", "ESL_PDA_SP_UpdateReversalBookingLadles");
                    PDA_SP_GetTransactionLadles = GetAppSetting("PDA_SP_GetTransactionLadles", "ESL_PDA_SP_GetTransactionLadles");
                    WEB_SP_CompleteLadleRequest = GetAppSetting("WEB_SP_CompleteLadleRequest", "ESL_WEB_SP_CompleteLadleRequest");
                    PDA_SP_GetRequestReport = GetAppSetting("PDA_SP_GetRequestReport", "ESL_PDA_SP_GetRequestReport");
                    PDA_SP_GetFullReport = GetAppSetting("PDA_SP_GetFullReport", "ESL_PDA_SP_GetFullReport");
                    #endregion

                    #region Dashboard
                    WEB_SP_DashboardLogin = GetAppSetting("WEB_SP_DashboardLogin", "ESL_WEB_SP_DashboardLogin");
                    WEB_SP_Logout = GetAppSetting("WEB_SP_Logout", "ESL_WEB_SP_Logout");
                    SP_GetAllBFLadle = GetAppSetting("SP_GetAllBFLadle", "ESL_WEB_SP_GetAllBFLadle");
                    SP_GetLadleRequest = GetAppSetting("SP_GetLadleRequest", "ESL_WEB_SP_GetLadleRequest_V1");
                    SP_GetLadleMovement = GetAppSetting("SP_GetLadleMovement", "ESL_WEB_SP_GetLadleMovement_V2");
                    WEB_SP_CreateMovement = GetAppSetting("WEB_SP_CreateMovement", "ESL_WEB_SP_CreateMovement_V1");
                    WEB_SP_CreateMovementTrips = GetAppSetting("WEB_SP_CreateMovementTrips", "ESL_WEB_SP_CreateMovementTrips");
                    GetTripSerialNumber = GetAppSetting("GetTripSerialNumber", "GetTripSerialNumber");
                    WEB_SP_GetAllLocoOccupied = GetAppSetting("WEB_SP_GetAllLocoOccupied", "ESL_WEB_SP_GetAllLocoOccupied");
                    WEB_SP_GetDashboardData = GetAppSetting("WEB_SP_GetDashboardData", "ESL_WEB_SP_GetDashboardData");
                    WEB_SP_GetReversalLadles = GetAppSetting("WEB_SP_GetReversalLadles", "ESL_WEB_SP_GetReversalLadles");
                    WEB_SP_InsertLadleReversal = GetAppSetting("WEB_SP_InsertLadleReversal", "ESL_WEB_SP_InsertLadleReversal_V4");
                    WEB_SP_AssignedLocoToLadle = GetAppSetting("WEB_SP_AssignedLocoToLadle", "ESL_WEB_SP_AssignedLocoToLadle");
                    WEB_SP_GetLadleMovementTrailLocation = GetAppSetting("WEB_SP_GetLadleMovementTrailLocation", "ESL_WEB_SP_GetLadleMovementTrailLocation");
                    WEB_SP_DeleteLadleFromMovement = GetAppSetting("WEB_SP_DeleteLadleFromMovement", "ESL_WEB_SP_DeleteLadleFromMovement_V4");
                    WEB_SP_CompleteReversalLadleRequest = GetAppSetting("WEB_SP_CompleteReversalLadleRequest", "ESL_WEB_SP_CompleteReversalLadleRequest_V1");
                    ESL_WEB_GetUserPassword = GetAppSetting("ESL_WEB_GetUserPassword", "ESL_WEB_GetUserPassword");
                    WEB_SP_GetTransactionReport = GetAppSetting("WEB_SP_GetTransactionReport", "ESL_WEB_SP_GetTransactionReport");
                    WEB_SP_TransferLoco = GetAppSetting("WEB_SP_TransferLoco", "ESL_WEB_SP_TransferLoco");
                    WEB_SP_GetLadleWeighmentReport = GetAppSetting("WEB_SP_GetLadleWeighmentReport", "ESL_WEB_SP_GetLadleWeighmentReport");
                    WEB_SP_GetManualVsAutoAssignmentReport = GetAppSetting("WEB_SP_GetManualVsAutoAssignmentReport", "ESL_WEB_SP_GetManualVsAutoAssignmentReport");
                    #endregion

                    #region Hot Metal
                    SP_GetHotmetalBooking = GetAppSetting("SP_GetHotmetalBooking", "SP_GetHotmetalBooking");
                    SP_InsertProductionOrder = GetAppSetting("SP_InsertProductionOrder", "SP_InsertProductionOrder");
                    SP_GetProductionReport = GetAppSetting("SP_GetProductionReport", "SP_GetProductionReport");
                    SP_GetPrefixByLocation = GetAppSetting("SP_GetPrefixByLocation", "SP_GetPrefixByLocation");
                    #endregion

                    #region Loco Ladle Mapping
                    WEB_SP_GetLatestLocoLadleMapping = GetAppSetting("WEB_SP_GetLatestLocoLadleMapping", "ESL_WEB_SP_GetLatestLocoLadleMapping");
                    #endregion

                    #region System Status
                    WEB_SP_GetSystemStatus = GetAppSetting("WEB_SP_GetSystemStatus", "ESL_WEB_SP_GetSystemStatus");
                    #endregion

                    #region new
                    WEB_SP_GetReaderTransactionLadles = ConfigurationManager.AppSettings["WEB_SP_GetReaderTransactionLadles"].ToString().Trim();
                    WEB_SP_GetEmptyLadlesForBooking = ConfigurationManager.AppSettings["WEB_SP_GetEmptyLadlesForBooking"].ToString().Trim();
                    WEB_SP_RequestEmptyLadles = ConfigurationManager.AppSettings["WEB_SP_RequestEmptyLadles"].ToString().Trim();
                    #endregion
                }
                catch (Exception ex)
                {
                    Log.Error("[loadSQLQueryCommands] Warning during SP mapping: " + ex.Message, ex);
                }
            }
        }
    }
}