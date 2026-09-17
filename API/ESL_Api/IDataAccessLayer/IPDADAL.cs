using ESL_Api.Models;
using PSL.Infinity.ESLLadleTracker.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.IDataAccessLayer
{
    public interface IPDADAL
    {
        #region PDA
        PDALoginModel PDALoginData(PDALoginRequest loginRequest);
        bool PDALogoutUser(string userID, string reason = "Manual Logout", string userName = null);
        SyncData PDASyncData(string userID);
        FurnaceLadlesModelResponse GetLadlesByFurnace(string userID, int furnaceLocationID);
        Response CastAssignmentCreation(PDACastAssignmentModel castAssignment);
        bool LadleRequestCreation(PDALadleRequestModel pdaLadleRequestModel);
        List<PDAGetLadleRequestModel> GetRequestedLadles(string userID, int locationID);
        bool UpdateLadleRequest(PDAUpdateLadleRequestModel updateLadleRequestModel);
        bool RevarsalMovementCreaction(PDACreateRevarsalMovementModel pdaCreateRevarsalMovementModel);
        LadleMovementResponse GetLadleMovement(string userID);
        List<ReversalLadlesModel> GetReversalLadles(string userID, int locationID);
        Response CloseLadleMovement(CloseLadleMovementModel model);
        Response TransferLadleMovement(TransferLadleMovementModel model);
        ProductionDataRes GetProductionDetails(string userID, int locationID);
        bool RequestAvailableLadles(RequestAvailableLadleModel model);
        bool DeleteBookingSummary(BookingSummaryUpdatioModel model);
        FurnaceDashboard GetFurnaceDashboardData(string userID, int locationID);
        bool RequestReversalLadles(RequestReversalLadles model);
        bool DeleteBookedRequest(BookingSummaryUpdatioModel1 model);
        List<TransactionLadleModel> GetTransactionLadles(string userID, int locationID);
        string GetActivationKey();
        List<RequestReportModel> GetRequestReport(DateTime? fromDate, DateTime? toDate, int? locationID);
        List<FullReportModel> GetFullReport(DateTime? fromDate, DateTime? toDate, int? locationID);

        #endregion

        #region Dashboard
        WEBDashboardLogin DashboardLoginData(DashboardLoginRequest loginRequest);
        bool WEBLogoutUser(string userID, string reason = "Manual Logout", string userName = null);
        List<BFLadleModel> GetAllBFLadle();
        LadleRequestListResponse GetLadleRequest();
        LadleMovementListResponse GetLadleMovement();
        bool CreateWEBMovement(CreateWEBMovementModel model);
        List<OccupiedLocoDetailsModel> GetAllOccupiedLoco();
        List<OccupiedLocoDetailsModel> GetAvailableTransferLocos(string locoName);
        List<LadelCountLocationModel> GetAllLadleCountLocations();
        List<LadleReversalWEB> GetReversalLadlesWEB();
        bool InsertLadleReversalsWEB(InsertLadleReversalWEB model);
        Response AssignedLocoToLadle(AssignedLocoToLadle assignedLoco);
        List<TrailLadleLocationModel> TrailLadleLocation();
        bool CompleteReversalLadleRequest(Guid id);
        bool CompleteLadleRequest(Guid id);
        Response DeleteLadleFromMovement(DeleteLadleRequest request);
        List<TransactionReportModel> GetTransactionReport(DateTime? fromDate, DateTime? toDate);
        Response TransferLoco(TransferLocoModel model);
        #endregion

        #region Analytical & Live Plant Dashboard
        List<KPIModel> GetKPIs();
        ESLDashboardSummary GetDashboardData();
        ServiceStatusResponse GetServiceActiveStatus();
        bool AssignLadle(List<ESLLadleAssignment> ladleAssignment);
        List<LadleChartDto> GetLadleChartData();
        List<HourlyLadleData> GetHourlyLadleData();
        List<HourlyLadleData> GetHourlyLadleData(DateTime selectedDate);

        List<HourlyLadleData> GetShiftHourlyLadleData(
            DateTime selectedDate,
            TimeSpan shiftStart,
            TimeSpan shiftEnd);

        HourlyTripSummaryResponse GetHourlyTripsSummary();
        List<HourlyProductionConsumption> GetHourlyProductionConsumptionOptimized();
        #endregion

        #region Hot Metal

        List<HotmetalBookingModel> GetHotmetalBooking();
        int UpdateCastNumber(int tranId, string castNumber);
        int InsertProductionOrder(ProductionOrderModel model);
        List<ProductionOrderModel> GetProductionReport();
        PrefixModel GetPrefixByLocation(string locationName);
        List<HotmetalBookingModel> GetReportData();

        #endregion

                #region ESL Reports & Tracking
        List<ReaderStatus> GetStatusReaderData();
        ESLLadleTransactionSummary GetTransactionSummary(DateTime fromDate, DateTime toDate);
        List<PSL.Infinity.ESLLadleTracker.Model.ESLLadle> GetActiveLadle();
        ESLLadlePathSummary GetLadleSummary(DateTime fromDate, DateTime toDate, string ladleNo);
        ESLLocationData GetLocationSummary(string locationName);
        List<PSL.Infinity.ESLLadleTracker.Model.ESLLocation> GetAllLocations();
        List<LadleWeighmentReportModel> GetLadleWeighmentReport(DateTime fromDate, DateTime toDate);
        UserDetail AuthUserWeb(User userData);
        void LogoutUserWeb(User userData);
        #endregion

        #region Loco Ladle Mapping
        List<LocoLadleMappingModel> GetLatestLocoLadleMapping();
        #endregion

        #region System Status
        List<SystemStatusModel> GetSystemStatusData();
        #endregion

        #region new
        List<ReaderTransactionModel> GetReaderTransactionLadles(int? locationID);
        List<ReaderTransactionModel> GetEmptyLadlesForBooking(int requestLocationID);
        int RequestEmptyLadles(RequestEmptyLadlesModel request);
        #endregion

        #region Manual Vs Auto Assignment Report
        List<ManualVsAutoAssignmentModel> GetManualVsAutoAssignmentReport(DateTime fromDate, DateTime toDate);
        #endregion

        #region User Login History Report
        UserLoginReportData GetUserLoginHistoryReport(DateTime fromDate, DateTime toDate, string userName = null);
        List<UserOptionItem> GetLoginReportUsers();
        #endregion
    }
}


