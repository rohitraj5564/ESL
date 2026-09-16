using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Http;
using Dapper;
using ESL_Api.IDataAccessLayer;
using ESL_Api.Models;
using ESL_Api.Services;
using ESL_Api.Global;
using System.Web.Http.Cors;
using PSL.Infinity.ESLLadleTracker.Model;

namespace ESL_Api.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("LTS")]

    public class PDAController : ApiController

    {

        private readonly IPDADAL PDADAL;
        public PDAController(IPDADAL PDADAL)

        {

            this.PDADAL = PDADAL;

        }
        #region PDA

        [HttpPost, Route("Login")]

        public IHttpActionResult PDALoginData([FromBody] PDALoginRequest loginRequest)

        {

            Logger.LogRequest("LTS/Login", loginRequest?.userName ?? "unknown");

            ResponseData responseData = new ResponseData();

            PDALoginModel data = null;
            bool validateKey = Psl.Chase.Utils.ProductKeyHelper.ValidateProductKey(loginRequest.activationKey);

            if (!validateKey)

            {

                responseData.status = false;

                responseData.message = "777";

                responseData.data = null;

                Logger.Warn("[PDALoginData] Invalid product key on Login"); 

                Logger.LogResponse("LTS/Login", false, "Invalid product key");

                return Ok(responseData);

            }
            if (Appsetting.UseADAuthentication)

            {

                string plainUserName;

                string plainPassword;

                try

                {

                    plainUserName = JwtHelper.Decrypt(loginRequest.userName);

                    plainPassword = JwtHelper.Decrypt(loginRequest.password);

                }

                catch

                {

                    responseData.status = false;

                    responseData.message = "Invalid credentials.";

                    responseData.data = null;

                    Logger.Warn("[PDALoginData] AD - Credential decryption failed");

                    Logger.LogResponse("LTS/Login", false, "Invalid credentials.");

                    return Ok(responseData);

                }
                try
                {
                    ADAuthService adAuthService = new ADAuthService();

                    // Step 1: Check if user exists in DB and retrieve IsActive
                    string matchedDbUserName = null;
                    bool? isUserActive = null;

                    try
                    {
                        using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                        {
                            var dbUser = db.QueryFirstOrDefault<dynamic>(
                                "SELECT TOP 1 UserName, IsActive FROM [Users] WHERE UserName = @U OR FirstName = @U",
                                new { U = plainUserName }
                            );

                            if (dbUser != null)
                            {
                                matchedDbUserName = (string)dbUser.UserName;
                                isUserActive = (bool?)dbUser.IsActive;
                            }
                            else
                            {
                                // If not found directly, resolve sAMAccountName / DisplayName via AD
                                var adUser = adAuthService.FindUserInAD(plainUserName);
                                if (adUser != null)
                                {
                                    var mappedDbUser = db.QueryFirstOrDefault<dynamic>(
                                        "SELECT TOP 1 UserName, IsActive FROM [Users] WHERE UserName = @U1 OR UserName = @U2 OR UserName = @U3 OR FirstName = @U4",
                                        new { U1 = plainUserName, U2 = adUser.DisplayName ?? "", U3 = adUser.UserName ?? "", U4 = adUser.DisplayName ?? "" }
                                    );
                                    if (mappedDbUser != null)
                                    {
                                        matchedDbUserName = (string)mappedDbUser.UserName;
                                        isUserActive = (bool?)mappedDbUser.IsActive;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception dbEx)
                    {
                        Logger.Error($"[PDALoginData] Error querying DB [Users]: {dbEx.Message}", dbEx);
                    }

                    // Step 2: Validate DB presence & IsActive
                    if (string.IsNullOrWhiteSpace(matchedDbUserName))
                    {
                        responseData.status = false;
                        responseData.message = "User does not exist in application database.";
                        responseData.data = null;
                        Logger.Warn($"[PDALoginData] User '{plainUserName}' not found in [Users] table.");
                        Logger.LogResponse("LTS/Login", false, responseData.message);
                        return Ok(responseData);
                    }

                    if (isUserActive != true)
                    {
                        responseData.status = false;
                        responseData.message = "User account is inactive. Please contact administrator.";
                        responseData.data = null;
                        Logger.Warn($"[PDALoginData] User '{matchedDbUserName}' is inactive in [Users] table.");
                        Logger.LogResponse("LTS/Login", false, responseData.message);
                        return Ok(responseData);
                    }

                    // Step 3: Check username and password against AD server
                    var adResult = adAuthService.AuthenticateUserDetailed(plainUserName, plainPassword);

                    if (!adResult.Success)
                    {
                        responseData.status = false;
                        responseData.message = adResult.ErrorMessage ?? "Invalid Active Directory credentials.";
                        responseData.data = null;
                        Logger.Warn($"[PDALoginData] AD authentication failed | UserName: {plainUserName}");
                        Logger.LogResponse("LTS/Login", false, responseData.message);
                        return Ok(responseData);
                    }

                    Logger.Info($"[PDALoginData] AD authentication successful | UserName: {plainUserName} | Matched DB User: {matchedDbUserName}");
                    loginRequest.userName = JwtHelper.Encrypt(matchedDbUserName);
                }
                catch (Exception ex)
                {
                    responseData.status = false;
                    responseData.message = ex.Message;
                    responseData.data = null;
                    Logger.Error("[PDALoginData] AD authentication error", ex);
                    Logger.LogResponse("LTS/Login", false, ex.Message);
                    return Ok(responseData);
                }
                try

                {

                    data = PDADAL.PDALoginData(loginRequest);

                    responseData.status = true;

                    responseData.message = "Fetched Successfully";

                    responseData.data = data;

                    Logger.LogResponse("LTS/Login", true, "AD Login successful");

                }

                catch (Exception ex)

                {

                    responseData.status = false;

                    responseData.message = ex.Message;

                    responseData.data = null;

                    Logger.Error("[PDALoginData] AD - Login failed", ex);

                    Logger.LogResponse("LTS/Login", false, ex.Message);

                }

            }

            else

            {

                try

                {

                    data = PDADAL.PDALoginData(loginRequest);

                    responseData.status = true;

                    responseData.message = "Fetched Successfully";

                    responseData.data = data;

                    Logger.LogResponse("LTS/Login", true, "Login successful");

                }

                catch (Exception ex)

                {

                    responseData.status = false;

                    responseData.message = ex.Message;

                    responseData.data = null;

                    Logger.Error("[PDALoginData] Login failed", ex);

                    Logger.LogResponse("LTS/Login", false, ex.Message);

                }

            }
            return Ok(responseData);

        }        
        [HttpPost, Route("PDALogout")]

        public IHttpActionResult PDALogout([FromBody] LogoutRequest logoutRequest)

        {

            Logger.LogRequest("LTS/PDALogout", logoutRequest?.userID ?? "unknown");

            ResponseData responseData = new ResponseData();

            bool validateKey = Psl.Chase.Utils.ProductKeyHelper.ValidateProductKey(logoutRequest.activationKey);

            if (validateKey)

            {

                try

                {

                    string reason = string.IsNullOrWhiteSpace(logoutRequest?.reason) ? "Manual Logout" : logoutRequest.reason;
                    PDADAL.PDALogoutUser(logoutRequest.userID, reason, logoutRequest.userName);

                    responseData.status = true;

                    responseData.message = "Logged out successfully";

                    responseData.data = null;

                    Logger.LogResponse("LTS/PDALogout", true, $"UserID: {logoutRequest.userID} logged out ({reason})");

                }

                catch (Exception ex)

                {

                    responseData.status = false;

                    responseData.message = ex.Message;

                    responseData.data = null;

                    Logger.Error($"[PDALogout] Logout failed | UserID: {logoutRequest.userID}", ex);

                    Logger.LogResponse("LTS/PDALogout", false, ex.Message);

                }

            }

            else

            {

                responseData.status = false;

                responseData.message = "777";

                responseData.data = null;

                Logger.Warn("[PDALogout] Invalid product key");

                Logger.LogResponse("LTS/PDALogout", false, "Invalid product key");

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetActivationKey")]

        public IHttpActionResult GetActivationKey()

        {

            Logger.LogRequest("LTS/GetActivationKey", "system");

            ResponseData responseData = new ResponseData();

            try

            {

                var key = PDADAL.GetActivationKey();

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = key;

                Logger.LogResponse("LTS/GetActivationKey", true, "Activation key fetched");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetActivationKey] Failed to fetch activation key", ex);

                Logger.LogResponse("LTS/GetActivationKey", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("Sync")]

        public IHttpActionResult PDASyncData(string userID)

        {

            Logger.LogRequest("LTS/Sync", userID);

            ResponseData responseData = new ResponseData();

            SyncData data = null;

            try

            {

                data = PDADAL.PDASyncData(userID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/Sync", true, $"UserID: {userID}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[PDASyncData] Failed | UserID: {userID}", ex);

                Logger.LogResponse("LTS/Sync", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetLadlesByFurnace")]

        public IHttpActionResult GetLadlesByFurnace(string clientDeviceID, string userID, int furnaceLocationID)

        {

            Logger.LogRequest("LTS/GetLadlesByFurnace", userID, $"FurnaceLocationID: {furnaceLocationID} | ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            FurnaceLadlesModelResponse data = null;

            try

            {

                data = PDADAL.GetLadlesByFurnace(userID, furnaceLocationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetLadlesByFurnace", true, $"UserID: {userID} | FurnaceLocationID: {furnaceLocationID}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetLadlesByFurnace] Failed | UserID: {userID} | FurnaceLocationID: {furnaceLocationID}", ex);

                Logger.LogResponse("LTS/GetLadlesByFurnace", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("InsertCastAssignment")]

        public IHttpActionResult CastAssignmentCreation([FromBody] PDACastAssignmentModel castAssignment)

        {

            Logger.LogRequest("LTS/InsertCastAssignment", castAssignment?.userID ?? "unknown", $"CastNo: {castAssignment?.castNo} | LadleNo: {castAssignment?.ladleNo}");

            Response responseData = new Response();

            try

            {

                responseData = PDADAL.CastAssignmentCreation(castAssignment);

                Logger.LogResponse("LTS/InsertCastAssignment", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[CastAssignmentCreation] Failed | UserID: {castAssignment?.userID}", ex);

                Logger.LogResponse("LTS/InsertCastAssignment", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("InsertLadleRequest")]

        public IHttpActionResult LadleRequestCreation([FromBody] PDALadleRequestModel pdaLadleRequestModel)

        {

            Logger.LogRequest("LTS/InsertLadleRequest", pdaLadleRequestModel?.userID ?? "unknown", $"LocationID: {pdaLadleRequestModel?.locationID} | NosOfLadle: {pdaLadleRequestModel?.nosOfLadle}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.LadleRequestCreation(pdaLadleRequestModel);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/InsertLadleRequest", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.ToString();

                responseData.data = null;

                Logger.Error($"[LadleRequestCreation] Failed | UserID: {pdaLadleRequestModel?.userID}", ex);

                Logger.LogResponse("LTS/InsertLadleRequest", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetRequestedLadles")]

        public IHttpActionResult GetRequestedLadles(string userID, int locationID)

        {

            Logger.LogRequest("LTS/GetRequestedLadles", userID, $"LocationID: {locationID}");

            ResponseData responseData = new ResponseData();

            List<PDAGetLadleRequestModel> data = null;

            try

            {

                data = PDADAL.GetRequestedLadles(userID, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetRequestedLadles", true, $"UserID: {userID} | Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetRequestedLadles] Failed | UserID: {userID} | LocationID: {locationID}", ex);

                Logger.LogResponse("LTS/GetRequestedLadles", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("UpdateLadleRequest")]

        public IHttpActionResult UpdateLadleRequest([FromBody] PDAUpdateLadleRequestModel updateLadleRequestModel)

        {

            Logger.LogRequest("LTS/UpdateLadleRequest", updateLadleRequestModel?.userID ?? "unknown", $"RequestNo: {updateLadleRequestModel?.requestNo} | Action: {updateLadleRequestModel?.action}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.UpdateLadleRequest(updateLadleRequestModel);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Updated successfully.";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/UpdateLadleRequest", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[UpdateLadleRequest] Failed | UserID: {updateLadleRequestModel?.userID}", ex);

                Logger.LogResponse("LTS/UpdateLadleRequest", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetTransactionLadles")]

        public IHttpActionResult GetTransactionLadles(string clientDeviceID, string userID, int locationID)

        {

            Logger.LogRequest("LTS/GetTransactionLadles", userID, $"LocationID: {locationID} | ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            try

            {

                var data = PDADAL.GetTransactionLadles(userID, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetTransactionLadles", true, $"UserID: {userID} | Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetTransactionLadles] Failed | UserID: {userID} | LocationID: {locationID}", ex);

                Logger.LogResponse("LTS/GetTransactionLadles", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetReversalLadles")]

        public IHttpActionResult GetReversalLadles(string clientDeviceID, string userID, int locationID)

        {

            Logger.LogRequest("LTS/GetReversalLadles", userID, $"LocationID: {locationID} | ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            List<ReversalLadlesModel> data = null;

            try

            {

                data = PDADAL.GetReversalLadles(userID, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetReversalLadles", true, $"UserID: {userID} | Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetReversalLadles] Failed | UserID: {userID} | LocationID: {locationID}", ex);

                Logger.LogResponse("LTS/GetReversalLadles", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("CreateRevarsalMovement")]

        public IHttpActionResult RevarsalMovementCreaction([FromBody] PDACreateRevarsalMovementModel pdaCreateRevarsalMovementModel)

        {

            Logger.LogRequest("LTS/CreateRevarsalMovement", pdaCreateRevarsalMovementModel?.userID ?? "unknown", $"Source: {pdaCreateRevarsalMovementModel?.source}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.RevarsalMovementCreaction(pdaCreateRevarsalMovementModel);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/CreateRevarsalMovement", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.ToString(); ;

                responseData.data = null;

                Logger.Error($"[RevarsalMovementCreaction] Failed | UserID: {pdaCreateRevarsalMovementModel?.userID}", ex);

                Logger.LogResponse("LTS/CreateRevarsalMovement", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetLadleMovement")]

        public IHttpActionResult GetLadleMovement(string clientDeviceID, string userID)

        {

            Logger.LogRequest("LTS/GetLadleMovement", userID, $"ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            try

            {

                LadleMovementResponse data = PDADAL.GetLadleMovement(userID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetLadleMovement", true, $"UserID: {userID}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetLadleMovement] Failed | UserID: {userID}", ex);

                Logger.LogResponse("LTS/GetLadleMovement", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("CloseLadleMovement")]

        public IHttpActionResult CloseLadleMovement([FromBody] CloseLadleMovementModel model)

        {

            Logger.LogRequest("LTS/CloseLadleMovement", model?.userID ?? "unknown", $"TripID: {model?.tripID} | LadleNos: {model?.ladleNos}");

            Response responseData = new Response();

            try

            {

                responseData = PDADAL.CloseLadleMovement(model);

                Logger.LogResponse("LTS/CloseLadleMovement", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[CloseLadleMovement] Failed | UserID: {model?.userID} | TripID: {model?.tripID}", ex);

                Logger.LogResponse("LTS/CloseLadleMovement", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetProductionDetails")]

        public IHttpActionResult GetProductionDetails(string clientDeviceID, string userID, int locationID)

        {

            Logger.LogRequest("LTS/GetProductionDetails", userID, $"LocationID: {locationID} | ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            ProductionDataRes data = null;

            try

            {

                data = PDADAL.GetProductionDetails(userID, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetProductionDetails", true, $"UserID: {userID} | LocationID: {locationID}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetProductionDetails] Failed | UserID: {userID} | LocationID: {locationID}", ex);

                Logger.LogResponse("LTS/GetProductionDetails", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("TransferLadleMovement")]

        public IHttpActionResult TransferLadleMovement([FromBody] TransferLadleMovementModel model)

        {

            Logger.LogRequest("LTS/TransferLadleMovement", model?.userID ?? "unknown", $"TripID: {model?.tripID} | LadleNos: {model?.ladleNos}");

            Response responseData = new Response();
            try

            {

                responseData = PDADAL.TransferLadleMovement(model);

                Logger.LogResponse("LTS/TransferLadleMovement", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[TransferLadleMovement] Failed | UserID: {model?.userID} | TripID: {model?.tripID}", ex);

                Logger.LogResponse("LTS/TransferLadleMovement", false, ex.Message);

            }
            return Ok(responseData);

        }
        [HttpPost, Route("RequestAvailableLadles")]

        public IHttpActionResult RequestAvailableLadles([FromBody] RequestAvailableLadleModel model)

        {

            Logger.LogRequest("LTS/RequestAvailableLadles", model?.userID ?? "unknown", $"RequestLocationID: {model?.requestLocationID}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.RequestAvailableLadles(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/RequestAvailableLadles", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.ToString();

                responseData.data = null;

                Logger.Error($"[RequestAvailableLadles] Failed | UserID: {model?.userID}", ex);

                Logger.LogResponse("LTS/RequestAvailableLadles", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("DeleteBookingSummary")]

        public IHttpActionResult DeleteBookingSummary([FromBody] BookingSummaryUpdatioModel model)

        {

            Logger.LogRequest("LTS/DeleteBookingSummary", model?.userID ?? "unknown", $"LadleNo: {model?.ladleNo} | CastNo: {model?.castNo}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.DeleteBookingSummary(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Deleted successfully.";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/DeleteBookingSummary", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[DeleteBookingSummary] Failed | UserID: {model?.userID} | LadleNo: {model?.ladleNo}", ex);

                Logger.LogResponse("LTS/DeleteBookingSummary", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetFurnaceDashboardData")]

        public IHttpActionResult GetFurnaceDashboardData(string clientDeviceID, string userID, int locationID)

        {

            Logger.LogRequest("LTS/GetFurnaceDashboardData", userID, $"LocationID: {locationID} | ClientDeviceID: {clientDeviceID}");

            ResponseData responseData = new ResponseData();

            FurnaceDashboard data = null;

            try

            {

                data = PDADAL.GetFurnaceDashboardData(userID, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetFurnaceDashboardData", true, $"UserID: {userID} | LocationID: {locationID}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error($"[GetFurnaceDashboardData] Failed | UserID: {userID} | LocationID: {locationID}", ex);

                Logger.LogResponse("LTS/GetFurnaceDashboardData", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("RequestReversalLadles")]

        public IHttpActionResult RequestReversalLadles([FromBody] RequestReversalLadles model)

        {

            Logger.LogRequest("LTS/RequestReversalLadles", model?.userID ?? "unknown", $"RequestLocationID: {model?.requestLocationID}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.RequestReversalLadles(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/RequestReversalLadles", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.ToString();

                responseData.data = null;

                Logger.Error($"[RequestReversalLadles] Failed | UserID: {model?.userID}", ex);

                Logger.LogResponse("LTS/RequestReversalLadles", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("DeleteBookedRequest")]

        public IHttpActionResult DeleteBookedRequest([FromBody] BookingSummaryUpdatioModel1 model)

        {

            Logger.LogRequest("LTS/DeleteBookedRequest", model?.userID ?? "unknown", $"LadleNo: {model?.ladleNo} | MovementType: {model?.movementType}");

            bool stat;

            ResponseData responseData = new ResponseData();

            try

            {

                stat = PDADAL.DeleteBookedRequest(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Deleted successfully.";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/DeleteBookedRequest", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[DeleteBookedRequest] Failed | UserID: {model?.userID} | LadleNo: {model?.ladleNo}", ex);

                Logger.LogResponse("LTS/DeleteBookedRequest", false, ex.Message);

            }

            return Ok(responseData);

        }

        #endregion
        [HttpPost, Route("CompleteReversalLadleRequest")]

        public IHttpActionResult CompleteReversalLadleRequest([FromBody] CompleteLadleRequestModel model)

        {

            Logger.LogRequest("LTS/CompleteReversalLadleRequest", "system", $"ID: {model?.ID}");

            Response responseData = new Response();

            if (model == null || model.ID == Guid.Empty)

            {

                responseData.status = false;

                responseData.message = "Invalid request data";

                Logger.Warn("[CompleteReversalLadleRequest] Invalid request - model is null or ID is empty");

                Logger.LogResponse("LTS/CompleteReversalLadleRequest", false, "Invalid request data");

                return Ok(responseData);

            }

            try

            {

                bool stat = PDADAL.CompleteReversalLadleRequest(model.ID);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Ladle marked as Completed";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong";

                }

                Logger.LogResponse("LTS/CompleteReversalLadleRequest", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[CompleteReversalLadleRequest] Failed | ID: {model?.ID}", ex);

                Logger.LogResponse("LTS/CompleteReversalLadleRequest", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetRequestReport")]

        public IHttpActionResult GetRequestReport(DateTime? fromDate = null, DateTime? toDate = null, int? locationID = null)

        {

            Logger.LogRequest("PDA/GetRequestReport", "system", $"From: {fromDate} | To: {toDate} | LocationID: {locationID}");

            ResponseData responseData = new ResponseData();

            try

            {

                var data = PDADAL.GetRequestReport(fromDate, toDate, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("PDA/GetRequestReport", true, $"Records: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetRequestReport] Failed", ex);

                Logger.LogResponse("PDA/GetRequestReport", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetFullReport")]

        public IHttpActionResult GetFullReport(DateTime? fromDate = null, DateTime? toDate = null, int? locationID = null)

        {

            Logger.LogRequest("LTS/GetFullReport", "system", $"From: {fromDate} | To: {toDate} | LocationID: {locationID}");

            ResponseData responseData = new ResponseData();

            try

            {

                var data = PDADAL.GetFullReport(fromDate, toDate, locationID);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetFullReport", true, $"Records: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetFullReport] Failed", ex);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("TransferLoco")]

        public IHttpActionResult TransferLoco([FromBody] TransferLocoModel model)

        {

            Logger.LogRequest("LTS/TransferLoco", "system",

                $"TripID: {model?.tripID} | From: {model?.currentLoco} | To: {model?.newLoco}");

            Response responseData = new Response();

            try

            {

                responseData = PDADAL.TransferLoco(model);

                Logger.LogResponse("LTS/TransferLoco", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error("[TransferLoco] Failed", ex);

            }

            return Ok(responseData);

        }
        #region Dashboard

        [HttpPost, Route("DashboardLoginData")]

        public IHttpActionResult DashboardLoginData([FromBody] DashboardLoginRequest loginRequest)

        {

            Logger.LogRequest("LTS/DashboardLoginData", loginRequest?.userName ?? "unknown");

            ResponseData responseData = new ResponseData();

            WEBDashboardLogin data = null;
            bool validateKey = Psl.Chase.Utils.ProductKeyHelper.ValidateProductKey(loginRequest.activationKey);

            if (!validateKey)

            {

                responseData.status = false;

                responseData.message = "777";

                responseData.data = null;

                Logger.Warn("[DashboardLoginData] Invalid product key on Dashboard Login");

                Logger.LogResponse("LTS/DashboardLoginData", false, "Invalid product key");

                return Ok(responseData);

            }
            if (Appsetting.UseADAuthentication)

            {

                string plainUserName;

                string plainPassword;

                try

                {

                    plainUserName = JwtHelper.Decrypt(loginRequest.userName);

                    plainPassword = JwtHelper.Decrypt(loginRequest.password);

                }

                catch

                {

                    responseData.status = false;

                    responseData.message = "Invalid credentials.";

                    responseData.data = null;

                    Logger.Warn("[DashboardLoginData] AD - Credential decryption failed");

                    Logger.LogResponse("LTS/DashboardLoginData", false, "Invalid credentials.");

                    return Ok(responseData);

                }
                try
                {
                    ADAuthService adAuthService = new ADAuthService();

                    // Step 1: Check if user exists in DB and retrieve IsActive
                    string matchedDbUserName = null;
                    bool? isUserActive = null;

                    try
                    {
                        using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                        {
                            var dbUser = db.QueryFirstOrDefault<dynamic>(
                                "SELECT TOP 1 UserName, IsActive FROM [Users] WHERE UserName = @U OR FirstName = @U",
                                new { U = plainUserName }
                            );

                            if (dbUser != null)
                            {
                                matchedDbUserName = (string)dbUser.UserName;
                                isUserActive = (bool?)dbUser.IsActive;
                            }
                            else
                            {
                                // If not found directly, resolve sAMAccountName / DisplayName via AD
                                var adUser = adAuthService.FindUserInAD(plainUserName);
                                if (adUser != null)
                                {
                                    var mappedDbUser = db.QueryFirstOrDefault<dynamic>(
                                        "SELECT TOP 1 UserName, IsActive FROM [Users] WHERE UserName = @U1 OR UserName = @U2 OR UserName = @U3 OR FirstName = @U4",
                                        new { U1 = plainUserName, U2 = adUser.DisplayName ?? "", U3 = adUser.UserName ?? "", U4 = adUser.DisplayName ?? "" }
                                    );
                                    if (mappedDbUser != null)
                                    {
                                        matchedDbUserName = (string)mappedDbUser.UserName;
                                        isUserActive = (bool?)mappedDbUser.IsActive;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception dbEx)
                    {
                        Logger.Error($"[DashboardLoginData] Error querying DB [Users]: {dbEx.Message}", dbEx);
                    }

                    // Step 2: Validate DB presence & IsActive
                    if (string.IsNullOrWhiteSpace(matchedDbUserName))
                    {
                        responseData.status = false;
                        responseData.message = "User does not exist in application database.";
                        responseData.data = null;
                        Logger.Warn($"[DashboardLoginData] User '{plainUserName}' not found in [Users] table.");
                        Logger.LogResponse("LTS/DashboardLoginData", false, responseData.message);
                        return Ok(responseData);
                    }

                    if (isUserActive != true)
                    {
                        responseData.status = false;
                        responseData.message = "User account is inactive. Please contact administrator.";
                        responseData.data = null;
                        Logger.Warn($"[DashboardLoginData] User '{matchedDbUserName}' is inactive in [Users] table.");
                        Logger.LogResponse("LTS/DashboardLoginData", false, responseData.message);
                        return Ok(responseData);
                    }

                    // Step 3: Check username and password against AD server
                    var adResult = adAuthService.AuthenticateUserDetailed(plainUserName, plainPassword);

                    if (!adResult.Success)
                    {
                        responseData.status = false;
                        responseData.message = adResult.ErrorMessage ?? "Invalid Active Directory credentials.";
                        responseData.data = null;
                        Logger.Warn($"[DashboardLoginData] AD authentication failed | UserName: {plainUserName}");
                        Logger.LogResponse("LTS/DashboardLoginData", false, responseData.message);
                        return Ok(responseData);
                    }

                    Logger.Info($"[DashboardLoginData] AD authentication successful | UserName: {plainUserName} | Matched DB User: {matchedDbUserName}");
                    loginRequest.userName = JwtHelper.Encrypt(matchedDbUserName);
                }
                catch (Exception ex)
                {
                    responseData.status = false;
                    responseData.message = ex.Message;
                    responseData.data = null;
                    Logger.Error("[DashboardLoginData] AD authentication error", ex);
                    Logger.LogResponse("LTS/DashboardLoginData", false, ex.Message);
                    return Ok(responseData);
                }
                try

                {

                    data = PDADAL.DashboardLoginData(loginRequest);

                    responseData.status = true;

                    responseData.message = "Login Successfully";

                    responseData.data = data;

                    Logger.LogResponse("LTS/DashboardLoginData", true, "AD Dashboard login successful");

                }

                catch (Exception ex)

                {

                    responseData.status = false;

                    responseData.message = ex.Message;

                    responseData.data = null;

                    Logger.Error("[DashboardLoginData] AD - Dashboard login failed", ex);

                    Logger.LogResponse("LTS/DashboardLoginData", false, ex.Message);

                }

            }

            else

            {

                try

                {

                    data = PDADAL.DashboardLoginData(loginRequest);

                    responseData.status = true;

                    responseData.message = "Login Successfully";

                    responseData.data = data;

                    Logger.LogResponse("LTS/DashboardLoginData", true, "Dashboard login successful");

                }

                catch (Exception ex)

                {

                    responseData.status = false;

                    responseData.message = ex.Message;

                    responseData.data = null;

                    Logger.Error("[DashboardLoginData] Dashboard login failed", ex);

                    Logger.LogResponse("LTS/DashboardLoginData", false, ex.Message);

                }

            }
            return Ok(responseData);

        }
        [HttpPost, Route("DashboardLogout")]
        public IHttpActionResult DashboardLogout([FromBody] LogoutRequest logoutRequest)
        {
            string reason = string.IsNullOrWhiteSpace(logoutRequest?.reason) ? "Manual Logout" : logoutRequest.reason;
            Logger.LogRequest("LTS/DashboardLogout", logoutRequest?.userID ?? logoutRequest?.userName ?? "unknown", $"Reason: {reason}");

            ResponseData responseData = new ResponseData();
            bool validateKey = Psl.Chase.Utils.ProductKeyHelper.ValidateProductKey(logoutRequest?.activationKey);

            if (validateKey)
            {
                try
                {
                    PDADAL.WEBLogoutUser(logoutRequest.userID, reason, logoutRequest.userName);
                    responseData.status = true;
                    responseData.message = "Logged out successfully";
                    responseData.data = null;
                    Logger.LogResponse("LTS/DashboardLogout", true, $"UserID: {logoutRequest.userID} logged out ({reason})");
                }
                catch (Exception ex)
                {
                    responseData.status = false;
                    responseData.message = ex.Message;
                    responseData.data = null;
                    Logger.Error($"[DashboardLogout] Failed | UserID: {logoutRequest?.userID}", ex);
                    Logger.LogResponse("LTS/DashboardLogout", false, ex.Message);
                }
            }
            else
            {
                responseData.status = false;
                responseData.message = "777";
                responseData.data = null;
                Logger.Warn("[DashboardLogout] Invalid product key");
                Logger.LogResponse("LTS/DashboardLogout", false, "Invalid product key");
            }

            return Ok(responseData);
        }

        [HttpPost, Route("EndUserSession")]
        public IHttpActionResult EndUserSession([FromBody] LogoutRequest logoutRequest)
        {
            string reason = string.IsNullOrWhiteSpace(logoutRequest?.reason) ? "Manual Logout" : logoutRequest.reason;
            string userIdentifier = logoutRequest?.userID ?? logoutRequest?.userName ?? "unknown";
            Logger.LogRequest("LTS/EndUserSession", userIdentifier, $"Reason: {reason}");

            ResponseData responseData = new ResponseData();
            try
            {
                PDADAL.WEBLogoutUser(logoutRequest?.userID, reason, logoutRequest?.userName);
                responseData.status = true;
                responseData.message = $"Session ended successfully ({reason})";
                responseData.data = null;
                Logger.LogResponse("LTS/EndUserSession", true, $"User {userIdentifier} session ended ({reason})");
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error($"[EndUserSession] Failed | User: {userIdentifier}", ex);
                Logger.LogResponse("LTS/EndUserSession", false, ex.Message);
            }

            return Ok(responseData);
        }

        [HttpGet, Route("CheckSession")]
        public IHttpActionResult CheckSession()
        {
            return Ok(new ResponseData { status = true, message = "Session active", data = null });
        }

        [HttpGet, Route("GetAllBFLadle")]

        public IHttpActionResult GetAllBFLadle()

        {

            Logger.LogRequest("LTS/GetAllBFLadle", "system");

            ResponseData responseData = new ResponseData();

            List<BFLadleModel> data = null;

            try

            {

                data = PDADAL.GetAllBFLadle();

                responseData.status = true;

                responseData.message = "Fethced Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetAllBFLadle", true, $"Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetAllBFLadle] Failed", ex);

                Logger.LogResponse("LTS/GetAllBFLadle", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetLadleRequest")]

        public IHttpActionResult GetLadleRequest()

        {

            Logger.LogRequest("LTS/GetLadleRequest", "system");

            ResponseData responseData = new ResponseData();

            try

            {

                var ladleData = PDADAL.GetLadleRequest();

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = ladleData;

                Logger.LogResponse("LTS/GetLadleRequest", true, $"Pending: {ladleData?.pendingRequest?.Count ?? 0} | Completed: {ladleData?.completedRequest?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetLadleRequest] Failed", ex);

                Logger.LogResponse("LTS/GetLadleRequest", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetLadleMovement")]

        public IHttpActionResult GetLadleMovement()

        {

            Logger.LogRequest("LTS/GetLadleMovement (WEB)", "system");

            ResponseData responseData = new ResponseData();

            try

            {

                var ladleData = PDADAL.GetLadleMovement();

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = ladleData;

                Logger.LogResponse("LTS/GetLadleMovement (WEB)", true, $"InTransit: {ladleData?.intransitMovement?.Count ?? 0} | Completed: {ladleData?.completedmovement?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetLadleMovement] (WEB) Failed", ex);

                Logger.LogResponse("LTS/GetLadleMovement (WEB)", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("CreateWEBMovement")]

        public IHttpActionResult CreateWEBMovement([FromBody] CreateWEBMovementModel model)

        {

            Logger.LogRequest("LTS/CreateWEBMovement", model?.userID ?? "unknown", $"LocoID: {model?.locoID}");

            bool stat;

            Response responseData = new Response();

            try

            {

                stat = PDADAL.CreateWEBMovement(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/CreateWEBMovement", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[CreateWEBMovement] Failed | UserID: {model?.userID} | LocoID: {model?.locoID}", ex);

                Logger.LogResponse("LTS/CreateWEBMovement", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetAllOccupiedLoco")]

        public IHttpActionResult GetAllOccupiedLoco()

        {

            Logger.LogRequest("LTS/GetAllOccupiedLoco", "system");

            ResponseData responseData = new ResponseData();

            List<OccupiedLocoDetailsModel> data = null;

            try

            {

                data = PDADAL.GetAllOccupiedLoco();

                responseData.status = true;

                responseData.message = "Fethced Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetAllOccupiedLoco", true, $"Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetAllOccupiedLoco] Failed", ex);

                Logger.LogResponse("LTS/GetAllOccupiedLoco", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost]

        [Route("GetAvailableTransferLocos")]

        public IHttpActionResult GetAvailableTransferLocos(PDAAvailableTransferLocoRequest request)

        {

            Logger.LogRequest("LTS/GetAvailableTransferLocos", "system", $"LocoID: {request?.locoID}");

            try

            {

                if (request == null)

                {

                    Logger.Warn("[GetAvailableTransferLocos] Request is null");

                    return Ok(new

                    {

                        status = false,

                        message = "Invalid request",

                        data = new

                        {

                            locos = new List<OccupiedLocoDetailsModel>()

                        }

                    });

                }
                if (string.IsNullOrWhiteSpace(request.locoID))

                {

                    Logger.Warn("[GetAvailableTransferLocos] LocoID is empty or whitespace");

                    return Ok(new

                    {

                        status = false,

                        message = "LocoID is required",

                        data = new

                        {

                            locos = new List<OccupiedLocoDetailsModel>()

                        }

                    });

                }
                var locos = PDADAL.GetAvailableTransferLocos(request.locoID);

                Logger.LogResponse("LTS/GetAvailableTransferLocos", true, $"LocoID: {request.locoID} | AvailableCount: {locos?.Count ?? 0}");

                return Ok(new

                {

                    status = true,

                    message = "Fetched Successfully",

                    data = new

                    {

                        locos = locos

                    }

                });

            }

            catch (Exception ex)

            {

                Logger.Error($"[GetAvailableTransferLocos] Failed | LocoID: {request?.locoID}", ex);

                Logger.LogResponse("LTS/GetAvailableTransferLocos", false, ex.Message);

                return Ok(new

                {

                    status = false,

                    message = ex.Message,

                    data = new

                    {

                        locos = new List<OccupiedLocoDetailsModel>()

                    }

                });

            }

        }
        [HttpGet, Route("GetAllLadleCountLocations")]

        public IHttpActionResult GetAllLadleCountLocations()

        {

            Logger.LogRequest("LTS/GetAllLadleCountLocations", "system");

            ResponseData responseData = new ResponseData();

            List<LadelCountLocationModel> data = null;

            try

            {

                data = PDADAL.GetAllLadleCountLocations();

                responseData.status = true;

                responseData.message = "Fethced Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetAllLadleCountLocations", true, $"Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetAllLadleCountLocations] Failed", ex);

                Logger.LogResponse("LTS/GetAllLadleCountLocations", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetReversalLadlesWEB")]

        public IHttpActionResult GetReversalLadlesWEB()

        {

            Logger.LogRequest("LTS/GetReversalLadlesWEB", "system");

            ResponseData responseData = new ResponseData();

            List<LadleReversalWEB> data = null;

            try

            {

                data = PDADAL.GetReversalLadlesWEB();

                responseData.status = true;

                responseData.message = "Fethced Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetReversalLadlesWEB", true, $"Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetReversalLadlesWEB] Failed", ex);

                Logger.LogResponse("LTS/GetReversalLadlesWEB", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("InsertLadleReversalsWEB")]

        public IHttpActionResult InsertLadleReversalsWEB([FromBody] InsertLadleReversalWEB model)

        {

            Logger.LogRequest("LTS/InsertLadleReversalsWEB", model?.userID ?? "unknown", $"LocoID: {model?.locoID}");

            bool stat;

            Response responseData = new Response();

            try

            {

                stat = PDADAL.InsertLadleReversalsWEB(model);

                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Created Successfully";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong. Please try again.";

                }

                Logger.LogResponse("LTS/InsertLadleReversalsWEB", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[InsertLadleReversalsWEB] Failed | UserID: {model?.userID} | LocoID: {model?.locoID}", ex);

                Logger.LogResponse("LTS/InsertLadleReversalsWEB", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("AssignedLocoToLadle")]

        public IHttpActionResult AssignedLocoToLadle([FromBody] AssignedLocoToLadle assignedLoco)

        {

            Logger.LogRequest("LTS/AssignedLocoToLadle", "system", $"TripID: {assignedLoco?.tripID} | LocoName: {assignedLoco?.locoName}");

            Response responseData = new Response();

            try

            {

                responseData = PDADAL.AssignedLocoToLadle(assignedLoco);

                Logger.LogResponse("LTS/AssignedLocoToLadle", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[AssignedLocoToLadle] Failed | TripID: {assignedLoco?.tripID} | LocoName: {assignedLoco?.locoName}", ex);

                Logger.LogResponse("LTS/AssignedLocoToLadle", false, ex.Message);

            }

            return Ok(responseData);

        }
        [HttpGet, Route("GetTrailLadleLocation")]

        public IHttpActionResult GetTrailLadleLocation()

        {

            Logger.LogRequest("LTS/GetTrailLadleLocation", "system");

            ResponseData responseData = new ResponseData();

            List<TrailLadleLocationModel> data = null;

            try

            {

                data = PDADAL.TrailLadleLocation();

                responseData.status = true;

                responseData.message = "Fethced Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetTrailLadleLocation", true, $"Count: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetTrailLadleLocation] Failed", ex);

                Logger.LogResponse("LTS/GetTrailLadleLocation", false, ex.Message);

            }

            return Ok(responseData);

        }
        //CompleteLadleRequest

        [HttpPost, Route("CompleteLadleRequest")]

        public IHttpActionResult CompleteLadleRequest([FromBody] CompleteLadleRequestModel model)

        {

            Logger.LogRequest("LTS/CompleteLadleRequest", "system", $"ID: {model?.ID}");

            Response responseData = new Response();
            if (model == null || model.ID == Guid.Empty)

            {

                responseData.status = false;

                responseData.message = "Invalid request data";

                Logger.Warn("[CompleteLadleRequest] Invalid request - model is null or ID is empty");

                Logger.LogResponse("LTS/CompleteLadleRequest", false, "Invalid request data");

                return Ok(responseData);

            }
            try

            {

                bool stat = PDADAL.CompleteLadleRequest(model.ID);
                if (stat)

                {

                    responseData.status = true;

                    responseData.message = "Ladle marked as Completed";

                }

                else

                {

                    responseData.status = false;

                    responseData.message = "Something went wrong";

                }

                Logger.LogResponse("LTS/CompleteLadleRequest", stat, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[CompleteLadleRequest] Failed | ID: {model?.ID}", ex);

                Logger.LogResponse("LTS/CompleteLadleRequest", false, ex.Message);

            }
            return Ok(responseData);

        }
        //DeleteLadleFromMovement

        [HttpPost, Route("DeleteLadleFromMovement")]

        public IHttpActionResult DeleteLadleFromMovement([FromBody] DeleteLadleRequest request)

        {

            Logger.LogRequest("LTS/DeleteLadleFromMovement", "system", $"TripID: {request?.TripID} | LadleNo: {request?.LadleNo}");

            if (request == null)

            {

                Logger.Warn("[DeleteLadleFromMovement] Request is null from controller");

                return Ok(new Response

                {

                    status = false,

                    message = "Request is null from controller"

                });

            }
            if (string.IsNullOrEmpty(request.TripID) || string.IsNullOrEmpty(request.LadleNo))

            {

                Logger.Warn($"[DeleteLadleFromMovement] Missing TripID or LadleNo | TripID: {request.TripID} | LadleNo: {request.LadleNo}");

                return Ok(new Response

                {

                    status = false,

                    message = "TripID or LadleNo is missing"

                });

            }
            Response responseData = new Response();
            try

            {

                responseData = PDADAL.DeleteLadleFromMovement(request);

                Logger.LogResponse("LTS/DeleteLadleFromMovement", responseData.status, responseData.message);

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                Logger.Error($"[DeleteLadleFromMovement] Failed | TripID: {request?.TripID} | LadleNo: {request?.LadleNo}", ex);

                Logger.LogResponse("LTS/DeleteLadleFromMovement", false, ex.Message);

            }
            return Ok(responseData);

        }
        [HttpGet, Route("GetTransactionReport")]

        public IHttpActionResult GetTransactionReport(DateTime? fromDate = null, DateTime? toDate = null)

        {

            Logger.LogRequest("LTS/GetTransactionReport", "system", $"From: {fromDate} | To: {toDate}");

            ResponseData responseData = new ResponseData();

            try

            {

                var data = PDADAL.GetTransactionReport(fromDate, toDate);

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

                Logger.LogResponse("LTS/GetTransactionReport", true, $"Records: {data?.Count ?? 0}");

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetTransactionReport] Failed", ex);

                Logger.LogResponse("LTS/GetTransactionReport", false, ex.Message);

            }

            return Ok(responseData);

        }

        #endregion
        #region Analytical Dashboard
        [HttpGet]

        [Route("kpis")]

        public IHttpActionResult GetKPIs()

        {
            List<KPIModel> kpis = PDADAL.GetKPIs();
            return Ok(kpis);

        }


        [HttpGet, Route("GetAnalyticalDashboardData")]

        public IHttpActionResult GetAnalyticalDashboardData()

        {

            ResponseData responseData = new ResponseData();

            ESLDashboardSummary data = null;

            try

            {

                data = PDADAL.GetDashboardData();

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;
            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

            }

            return Ok(responseData);

        }

        [HttpGet]
        [Route("dashboard")]
        [Route("GetDashboardData")]

        public IHttpActionResult GetDashboardData()

        {

            return GetAnalyticalDashboardData();

        }
        [HttpGet]
        [Route("ServiceActiveStatus")]
        [Route("GetServiceActiveStatus")]

        public IHttpActionResult GetServiceActiveStatus()

        {

            ResponseData responseData = new ResponseData();

            try

            {

                var data = PDADAL.GetServiceActiveStatus();

                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;

                Logger.Error("[GetServiceActiveStatus] Error", ex);

            }

            return Ok(responseData);

        }
        [HttpPost, Route("AssignLadle")]

        public IHttpActionResult AssignLadle([FromBody] List<ESLLadleAssignment> ladleAssignment)

        {

            ResponseData responseData = new ResponseData();

            try

            {

                bool result = PDADAL.AssignLadle(ladleAssignment);

                responseData.status = result;

                responseData.message = result ? "Ladle assigned successfully" : "Failed to assign ladle";

                responseData.data = result;

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = false;

                Logger.Error("[AssignLadle] Error", ex);

            }

            return Ok(responseData);

        }


        [HttpGet]

        [Route("ladle-chart")]

        public IHttpActionResult GetLadleChart()

        {
            var data = PDADAL.GetLadleChartData();

            return Ok(data);

        }


        [HttpGet]
        [Route("hourly-ladle-data")]
        public IHttpActionResult GetHourlyLadleData(DateTime? date = null)
        {
            var data = date.HasValue ? PDADAL.GetHourlyLadleData(date.Value) : PDADAL.GetHourlyLadleData();
            return Ok(data);
        }
        [HttpGet]

        [Route("shift-hourly-ladle-data")]

        public IHttpActionResult GetShiftHourlyLadleData(DateTime date, TimeSpan shiftStart, TimeSpan shiftEnd)

        {
            var data = PDADAL.GetShiftHourlyLadleData(date, shiftStart, shiftEnd);

            return Ok(data);

        }


        [HttpGet]

        [Route("GetHourlyTrips")]

        public IHttpActionResult GetHourlyTrips()

        {

            var data = PDADAL.GetHourlyTripsSummary();
            return Ok(data);
        }


        [HttpGet]

        [Route("hourly-production-consumption")]

        public IHttpActionResult GetHourlyProductionConsumptionReport()

        {

            var data = PDADAL.GetHourlyProductionConsumptionOptimized();
            return Ok(data);
        }
        #endregion

        #region Hot Metal

        // ============================================================

        // GET HOT METAL BOOKING

        // ============================================================

        [HttpGet, Route("GetHotmetalBooking")]

        public IHttpActionResult GetHotmetalBooking()

        {

            Logger.LogRequest(

                "LTS/GetHotmetalBooking",

                "system"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                var data = PDADAL.GetHotmetalBooking();
                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;
                Logger.LogResponse(

                    "LTS/GetHotmetalBooking",

                    true,

                    $"Records: {data?.Count ?? 0}"

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[GetHotmetalBooking] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/GetHotmetalBooking",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }


        // ============================================================

        // UPDATE CAST NUMBER

        // ============================================================

        [HttpPost, Route("UpdateCastNumber")]

        public IHttpActionResult UpdateCastNumber(

            [FromBody] UpdateCastNumberModel model)

        {

            Logger.LogRequest(

                "LTS/UpdateCastNumber",

                "system",

                $"Tran_ID: {model?.Tran_ID} | CastNumber: {model?.CastNumber}"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                if (model == null)

                {

                    responseData.status = false;

                    responseData.message = "Model is Null";

                    responseData.data = null;
                    return Ok(responseData);

                }
                int result = PDADAL.UpdateCastNumber(

                    model.Tran_ID,

                    model.CastNumber

                );
                responseData.status = result > 0;

                responseData.message =

                    result > 0

                        ? "Updated Successfully"

                        : "Update Failed";
                responseData.data = null;
                Logger.LogResponse(

                    "LTS/UpdateCastNumber",

                    result > 0,

                    responseData.message

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[UpdateCastNumber] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/UpdateCastNumber",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }


        // ============================================================

        // GET REPORT DATA

        // ============================================================

        [HttpGet, Route("GetReportData")]

        public IHttpActionResult GetReportData()

        {

            Logger.LogRequest(

                "LTS/GetReportData",

                "system"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                var data = PDADAL.GetReportData();
                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;
                Logger.LogResponse(

                    "LTS/GetReportData",

                    true,

                    $"Records: {data?.Count ?? 0}"

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[GetReportData] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/GetReportData",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }


        // ============================================================

        // INSERT PRODUCTION ORDER

        // ============================================================

        [HttpPost, Route("InsertProductionOrder")]

        public IHttpActionResult InsertProductionOrder(

            [FromBody] ProductionOrderModel model)

        {

            Logger.LogRequest(

                "LTS/InsertProductionOrder",

                "system"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                if (model == null)

                {

                    responseData.status = false;

                    responseData.message = "Model is Null";

                    responseData.data = null;
                    return Ok(responseData);

                }
                int result =

                    PDADAL.InsertProductionOrder(model);
                responseData.status = result > 0;

                responseData.message =

                    result > 0

                        ? "Saved Successfully"

                        : "Save Failed";
                responseData.data = null;
                Logger.LogResponse(

                    "LTS/InsertProductionOrder",

                    result > 0,

                    responseData.message

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[InsertProductionOrder] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/InsertProductionOrder",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }


        // ============================================================

        // GET PRODUCTION REPORT

        // ============================================================

        [HttpGet, Route("GetProductionReport")]

        public IHttpActionResult GetProductionReport()

        {

            Logger.LogRequest(

                "LTS/GetProductionReport",

                "system"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                var data = PDADAL.GetProductionReport();
                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;
                Logger.LogResponse(

                    "LTS/GetProductionReport",

                    true,

                    $"Records: {data?.Count ?? 0}"

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[GetProductionReport] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/GetProductionReport",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }


        // ============================================================

        // GET PREFIX BY LOCATION

        // ============================================================

        [HttpGet, Route("GetPrefixByLocation/{locationName}")]

        public IHttpActionResult GetPrefixByLocation(

            string locationName)

        {

            Logger.LogRequest(

                "LTS/GetPrefixByLocation",

                "system",

                $"LocationName: {locationName}"

            );
            ResponseData responseData = new ResponseData();
            try

            {

                if (string.IsNullOrWhiteSpace(locationName))

                {

                    responseData.status = false;

                    responseData.message = "LocationName is required";

                    responseData.data = null;
                    return Ok(responseData);

                }
                var data =

                    PDADAL.GetPrefixByLocation(locationName);
                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;
                Logger.LogResponse(

                    "LTS/GetPrefixByLocation",

                    true,

                    $"LocationName: {locationName}"

                );

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    "[GetPrefixByLocation] Failed",

                    ex

                );
                Logger.LogResponse(

                    "LTS/GetPrefixByLocation",

                    false,

                    ex.Message

                );

            }
            return Ok(responseData);

        }
        #endregion
        #region Loco Ladle Mapping

        [HttpGet, Route("GetLatestLocoLadleMapping")]

        public IHttpActionResult GetLatestLocoLadleMapping()

        {

            ResponseData responseData = new ResponseData();

            List<LocoLadleMappingModel> data = null;
            try

            {

                data = PDADAL.GetLatestLocoLadleMapping();
                responseData.status = true;

                responseData.message = "Fetched Successfully";

                responseData.data = data;

            }

            catch (Exception ex)

            {

                responseData.status = false;

                responseData.message = ex.Message;

                responseData.data = null;
                Logger.Error(

                    $"[GetLatestLocoLadleMapping] Error: {ex.Message}"

                );

            }
            return Ok(responseData);

        }

        #endregion

    
        #region Merged ESL Endpoints

        [HttpGet]
        [Route("statusreader")]
        [Route("GetStatusReaderData")]
        public IHttpActionResult GetStatusReaderData()
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetStatusReaderData();
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetStatusReaderData] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpPost]
        [Route("transactionsum")]
        [Route("GetTransactionSummary")]
        public IHttpActionResult GetTransactionData([FromBody] TransactionInput inputData)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                DateTime fromDate = inputData != null && inputData.FromDate != DateTime.MinValue ? inputData.FromDate : DateTime.Today.AddDays(-1);
                DateTime toDate = inputData != null && inputData.ToDate != DateTime.MinValue ? inputData.ToDate : DateTime.Today.AddDays(1);
                var data = PDADAL.GetTransactionSummary(fromDate, toDate);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetTransactionData] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpGet]
        [Route("ladelddl")]
        [Route("GetActiveLadle")]
        public IHttpActionResult GetLaderData()
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetActiveLadle();
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetLaderData] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpPost]
        [Route("ladle")]
        [Route("GetLadleSummary")]
        public IHttpActionResult GetLadleData([FromBody] TransactionInput inputData)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                DateTime fromDate = inputData != null && inputData.FromDate != DateTime.MinValue ? inputData.FromDate : DateTime.Today.AddDays(-1);
                DateTime toDate = inputData != null && inputData.ToDate != DateTime.MinValue ? inputData.ToDate : DateTime.Today.AddDays(1);
                var data = PDADAL.GetLadleSummary(fromDate, toDate, inputData?.LadleNo);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetLadleData] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpPost]
        [Route("LocationSummary")]
        [Route("GetLocationSummary")]
        public IHttpActionResult GetLocationSummary([FromBody] TransactionInput inputData)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetLocationSummary(inputData?.LadleNo);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetLocationSummary] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpGet]
        [Route("AllLocation")]
        [Route("GetAllLocations")]
        public IHttpActionResult GetAllLocation()
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetAllLocations();
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetAllLocation] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpPost]
        [Route("authenticate")]
        public IHttpActionResult AuthUser([FromBody] User usr)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var result = PDADAL.AuthUserWeb(usr);
                responseData.status = result != null;
                responseData.message = result != null ? "Authentication successful" : "Authentication failed";
                responseData.data = result;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[AuthUser] Error", ex);
            }
            return Ok(responseData);
        }

        [HttpPost]
        [Route("logoutuser")]
        public IHttpActionResult LogoutUser([FromBody] User usr)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                PDADAL.LogoutUserWeb(usr);
                responseData.status = true;
                responseData.message = "Logged out successfully";
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                Logger.Error("[LogoutUser] Error", ex);
            }
            return Ok(responseData);
        }

        #endregion

        #region System Status

        [HttpGet]
        [Route("systemstatus")]
        [Route("GetSystemStatusData")]
        [Route("GetSystemStatus")]
        public IHttpActionResult GetSystemStatusData()
        {
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetSystemStatusData();
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetSystemStatusData] Error", ex);
            }
            return Ok(responseData);
        }

        #endregion

        #region Ladle Weighment Report

        [HttpPost]
        [Route("ladleweighmentreport")]
        [Route("GetLadleWeighmentReport")]
        public IHttpActionResult GetLadleWeighmentReport([FromBody] TransactionInput inputData)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                Logger.Info($"[GetLadleWeighmentReport] Received inputData: {(inputData != null ? $"FromDate={inputData.FromDate}, ToDate={inputData.ToDate}" : "NULL")}");
                DateTime fromDate = inputData != null && inputData.FromDate != DateTime.MinValue ? inputData.FromDate : DateTime.Today.AddDays(-30);
                DateTime toDate = inputData != null && inputData.ToDate != DateTime.MinValue ? inputData.ToDate : DateTime.Today.AddDays(1);
                Logger.Info($"[GetLadleWeighmentReport] Querying from {fromDate:yyyy-MM-dd HH:mm:ss} to {toDate:yyyy-MM-dd HH:mm:ss}");
                var data = PDADAL.GetLadleWeighmentReport(fromDate, toDate);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetLadleWeighmentReport] Error", ex);
            }
            return Ok(responseData);
        }

        #endregion

        #region Manual Vs Auto Assignment Report

        [HttpPost]
        [Route("manualvsautoreport")]
        [Route("GetManualVsAutoAssignmentReport")]
        public IHttpActionResult GetManualVsAutoAssignmentReport([FromBody] TransactionInput inputData)
        {
            ResponseData responseData = new ResponseData();
            try
            {
                Logger.Info($"[GetManualVsAutoAssignmentReport] Received inputData: {(inputData != null ? $"FromDate={inputData.FromDate}, ToDate={inputData.ToDate}" : "NULL")}");
                DateTime fromDate = inputData != null && inputData.FromDate != DateTime.MinValue ? inputData.FromDate : DateTime.Now.AddHours(-24);
                DateTime toDate = inputData != null && inputData.ToDate != DateTime.MinValue ? inputData.ToDate : DateTime.Now;
                Logger.Info($"[GetManualVsAutoAssignmentReport] Querying from {fromDate:yyyy-MM-dd HH:mm:ss} to {toDate:yyyy-MM-dd HH:mm:ss}");
                var data = PDADAL.GetManualVsAutoAssignmentReport(fromDate, toDate);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetManualVsAutoAssignmentReport] Error", ex);
            }
            return Ok(responseData);
        }

        #endregion

        #region new
        [HttpGet, Route("GetReaderTransactionLadles")]
        public IHttpActionResult GetReaderTransactionLadles(int? locationID = null)
        {
            Logger.LogRequest("LTS/GetReaderTransactionLadles", "system", $"LocationID: {locationID}");
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetReaderTransactionLadles(locationID);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
                Logger.LogResponse("LTS/GetReaderTransactionLadles", true, $"Count: {data?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetReaderTransactionLadles] Failed", ex);
            }
            return Ok(responseData);
        }

        [HttpGet, Route("GetEmptyLadlesForBooking")]
        public IHttpActionResult GetEmptyLadlesForBooking(int requestLocationID)
        {
            Logger.LogRequest("LTS/GetEmptyLadlesForBooking", "system", $"RequestLocationID: {requestLocationID}");
            ResponseData responseData = new ResponseData();
            try
            {
                var data = PDADAL.GetEmptyLadlesForBooking(requestLocationID);
                responseData.status = true;
                responseData.message = "Fetched Successfully";
                responseData.data = data;
                Logger.LogResponse("LTS/GetEmptyLadlesForBooking", true, $"Count: {data?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[GetEmptyLadlesForBooking] Failed", ex);
            }
            return Ok(responseData);
        }

        [HttpPost, Route("RequestEmptyLadles")]
        public IHttpActionResult RequestEmptyLadles(RequestEmptyLadlesModel request)
        {
            Logger.LogRequest("LTS/RequestEmptyLadles", request?.userID ?? "system", $"Count: {request?.ladles?.Count ?? 0}");
            ResponseData responseData = new ResponseData();
            try
            {
                if (request == null || request.ladles == null || request.ladles.Count == 0)
                {
                    responseData.status = false;
                    responseData.message = "No ladles supplied.";
                    return Ok(responseData);
                }

                int booked = PDADAL.RequestEmptyLadles(request);
                int total = request.ladles.Count;

                responseData.status = booked > 0;
                responseData.message = booked == total
                    ? "Requested Successfully"
                    : booked == 0
                        ? "No ladles could be requested. They may already be booked."
                        : $"Requested {booked} of {total}. The rest may already be booked.";
                responseData.data = new { requested = booked, total = total };

                Logger.LogResponse("LTS/RequestEmptyLadles", responseData.status, responseData.message);
            }
            catch (Exception ex)
            {
                responseData.status = false;
                responseData.message = ex.Message;
                responseData.data = null;
                Logger.Error("[RequestEmptyLadles] Failed", ex);
            }
            return Ok(responseData);
        }
        #endregion
    }

}






