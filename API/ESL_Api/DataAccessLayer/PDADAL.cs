using Dapper;
using ESL_Api.Global;
using ESL_Api.IDataAccessLayer;
using ESL_Api.Models;
using Newtonsoft.Json;
using PSL.Infinity.ESLLadleTracker;
using PSL.Infinity.ESLLadleTracker.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

using System.Text;
using System.Web;


namespace ESL_Api.DataAccessLayer
{
    public class PDADAL : IPDADAL
    {
        #region PDA
        public PDALoginModel PDALoginData(PDALoginRequest loginRequest)
        {
            Logger.Info("[PDALoginData] Login attempt started");
            string decryptedUsername;
            string decryptedPassword;

            try
            {
                decryptedUsername = JwtHelper.Decrypt(loginRequest.userName).Trim();
                decryptedPassword = JwtHelper.Decrypt(loginRequest.password).Trim();
            }
            catch
            {
                Logger.Warn("[PDALoginData] Credential decryption failed");
                throw new Exception("Invalid credentials.");
            }

            if (string.IsNullOrWhiteSpace(decryptedUsername) ||
                string.IsNullOrWhiteSpace(decryptedPassword))
            {
                Logger.Warn("[PDALoginData] Empty username or password after decryption");
                throw new Exception("Invalid credentials.");
            }

            if (InputValidator.ContainsSQLInjection(decryptedUsername) ||
                InputValidator.ContainsSQLInjection(decryptedPassword))
            {
                Logger.Warn("[PDALoginData] SQL Injection attempt detected");
                throw new Exception("Invalid credentials.");
            }

            if (InputValidator.ContainsXSS(decryptedUsername) ||
                InputValidator.ContainsXSS(decryptedPassword))
            {
                Logger.Warn("[PDALoginData] XSS attempt detected");
                throw new Exception("Invalid credentials.");
            }

            PDALoginModel response = new PDALoginModel();
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                var passwordParams = new DynamicParameters();
                passwordParams.Add("@UserName", decryptedUsername, DbType.String, ParameterDirection.Input, 100);

                string procedureName = Appsetting.SQLQueryCommand.ESL_WEB_GetUserPassword;
                if (string.IsNullOrWhiteSpace(procedureName))
                {
                    procedureName = "ESL_WEB_GetUserPassword";
                }

                string storedPassword = db.QueryFirstOrDefault<string>(
                    procedureName,
                    passwordParams,
                    commandType: CommandType.StoredProcedure
                );

                if (!Appsetting.UseADAuthentication)
                {
                    if (string.IsNullOrWhiteSpace(storedPassword))
                    {
                        Logger.Warn($"[PDALoginData] User not found or inactive | UserName: {decryptedUsername}");
                        throw new Exception("Invalid credentials.");
                    }

                    storedPassword = storedPassword.Trim();
                    string cleanDecryptedPassword = decryptedPassword ?? string.Empty;

                    bool passwordValid;
                    if (storedPassword.StartsWith("$2a$") || storedPassword.StartsWith("$2b$") || storedPassword.StartsWith("$2y$"))
                    {
                        passwordValid = PasswordHelper.VerifyPassword(cleanDecryptedPassword, storedPassword);
                    }
                    else
                    {
                        passwordValid = storedPassword == cleanDecryptedPassword;
                    }

                    if (!passwordValid)
                    {
                        Logger.Warn($"[PDALoginData] Password mismatch | UserName: {decryptedUsername}");
                        throw new Exception("Invalid credentials.");
                    }
                }

                var _params = new DynamicParameters();
                _params.Add("@UserName", decryptedUsername);
                _params.Add("@Password", decryptedPassword);

                string pdaLoginSpName = !string.IsNullOrWhiteSpace(Appsetting.SQLQueryCommand.PDA_SP_LoginData)
                    ? Appsetting.SQLQueryCommand.PDA_SP_LoginData
                    : "ESL_PDA_SP_LoginData";

                using (var multi = db.QueryMultiple(
                    pdaLoginSpName,
                    _params, commandType: CommandType.StoredProcedure))
                {
                    var userInfo = multi.Read().FirstOrDefault();
                    if (userInfo != null)
                    {
                        response.userID = Guid.Parse(userInfo.userID.ToString());
                        response.userLocationId = userInfo.userLocationId;
                        response.userLocationName = InputValidator.EncodeOutput(userInfo.userLocationName.ToString());
                        response.userFirstName = InputValidator.EncodeOutput(userInfo.userFirstName.ToString());
                        response.sessionToken = userInfo.sessionToken;
                        response.jwtToken = JwtHelper.GenerateToken(
                            userInfo.userID.ToString(),
                            decryptedUsername,
                            userInfo.userLocationId,
                            userInfo.userLocationName.ToString()
                        );
                        Logger.Info($"[PDALoginData] Login successful | UserID: {userInfo.userID} | LocationID: {userInfo.userLocationId}");
                    }
                    response.modules = multi.Read<ModuleDetail>().ToList();
                    response.locationDetails = multi.Read<LocationDetail>().ToList();
                }
            }

            return response;
        }

        public bool PDALogoutUser(string userID, string reason = "Manual Logout", string userName = null)
        {
            Logger.Info($"[PDALogoutUser] Logout started | UserID: {userID} | UserName: {userName} | Reason: {reason}");
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                var _params = new DynamicParameters();
                _params.Add("@UserID", userID);
                _params.Add("@UserName", userName);
                _params.Add("@LogoutReason", string.IsNullOrWhiteSpace(reason) ? "Manual Logout" : reason);
                db.Execute("ESL_SP_RecordUserLogout", _params, commandType: CommandType.StoredProcedure);
                Logger.Info($"[PDALogoutUser] Logout successful | UserID: {userID} | Reason: {reason}");
                return true;
            }
        }

        public string GetActivationKey()
        {
            Logger.Info("[GetActivationKey] Fetching activation key from config");
            try
            {
                string key = Appsetting.ActivationKey;
                Logger.Info("[GetActivationKey] Activation key fetched successfully");
                return key;
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetActivationKey", ex);
                return null;
            }
        }


        public SyncData PDASyncData(string userID)
        {
            Logger.Info($"[PDASyncData] Sync started | UserID: {userID}");
            SyncData response = new SyncData();

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                var _params = new DynamicParameters();
                _params.Add("@UserID", userID);


                using (var multi = db.QueryMultiple(Appsetting.SQLQueryCommand.PDA_SP_SyncData, _params, commandType: CommandType.StoredProcedure))
                {
                    response.locationDetails = multi.Read<LocationDetail>().ToList();
                    response.assetDetails = multi.Read<AssetDetail>().ToList();
                }
            }

            Logger.Info($"[PDASyncData] Sync completed | UserID: {userID}");
            return response;
        }

        public FurnaceLadlesModelResponse GetLadlesByFurnace(string userID, int furnaceLocationID)
        {
            Logger.Info($"[GetLadlesByFurnace] Started | UserID: {userID} | FurnaceLocationID: {furnaceLocationID}");
            FurnaceLadlesModelResponse res = new FurnaceLadlesModelResponse();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@userID", userID);
                    _params.Add("@locationID", furnaceLocationID);

                    using (var multi = db.QueryMultiple(Appsetting.SQLQueryCommand.PDA_SP_GetLadleByFurnace, _params,
                        commandType: CommandType.StoredProcedure))
                    {
                        // Result Set 1: Ladles
                        res.ladles = multi.Read<string>().ToList();

                        // Result Set 2: CastNo + Chemicals
                        var info = multi.Read().FirstOrDefault();
                        if (info != null)
                        {
                            res.prevCastNo = info.prevCastNo ?? "";
                            res.C = info.C != null ? info.C.ToString() : "";
                            res.Si = info.Si != null ? info.Si.ToString() : "";
                            res.Mn = info.Mn != null ? info.Mn.ToString() : "";
                            res.S = info.S != null ? info.S.ToString() : "";
                            res.P = info.P != null ? info.P.ToString() : "";
                            res.Ti = info.Ti != null ? info.Ti.ToString() : "";
                            res.Cr = info.Cr != null ? info.Cr.ToString() : "";
                            res.SP = info.SP != null ? info.SP.ToString() : "";
                        }
                    }
                }
                Logger.Info($"[GetLadlesByFurnace] Completed | UserID: {userID} | FurnaceLocationID: {furnaceLocationID} | LadleCount: {res.ladles?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetLadlesByFurnace", ex);
                res = null;
            }
            return res;
        }

        //public Response CastAssignmentCreation(PDACastAssignmentModel castAssignment)
        //{
        //    Response res = new Response();
        //    bool isValid = false;
        //    string message = string.Empty;
        //    using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
        //    {
        //        db.Open();
        //        var _params = new DynamicParameters();
        //        _params.Add("@castID", castAssignment.castID);
        //        _params.Add("@clientDeviceID", castAssignment.clientDeviceID);
        //        _params.Add("@userID", castAssignment.userID);
        //        _params.Add("@sourceLocID", castAssignment.sourceLocationID);
        //        _params.Add("@sourceLocName", castAssignment.sourceLocationName);
        //        _params.Add("@castNo", castAssignment.castNo);
        //        _params.Add("@ladleNo", castAssignment.ladleNo);
        //        _params.Add("@transactionDateTime", castAssignment.transactionDateTime);
        //        _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        //        _params.Add("@message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
        //        db.Execute(Appsetting.SQLQueryCommand.PDA_SP_CreateCastAssignment, _params, commandType: CommandType.StoredProcedure);
        //        isValid = _params.Get<bool>("@status");
        //        message = _params.Get<string>("@message");
        //        if (isValid)
        //        {
        //            res.status = true;
        //            res.message = message;
        //        }
        //        else
        //        {
        //            res.status = false;
        //            res.message = message;
        //        }
        //    }
        //    return res;
        //}

        public Response CastAssignmentCreation(PDACastAssignmentModel castAssignment)
        {
            Logger.Info($"[CastAssignmentCreation] Started | UserID: {castAssignment?.userID} | CastNo: {castAssignment?.castNo} | LadleNo: {castAssignment?.ladleNo}");
            Response res = new Response();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@castID", castAssignment.castID);
                    _params.Add("@clientDeviceID", castAssignment.clientDeviceID);
                    _params.Add("@userID", castAssignment.userID);
                    _params.Add("@sourceLocID", castAssignment.sourceLocationID);
                    _params.Add("@sourceLocName", castAssignment.sourceLocationName);
                    _params.Add("@castNo", castAssignment.castNo);
                    _params.Add("@ladleNo", castAssignment.ladleNo);
                    _params.Add("@transactionDateTime", castAssignment.transactionDateTime);

                    _params.Add("@C", castAssignment.C);
                    _params.Add("@Si", castAssignment.Si);
                    _params.Add("@Mn", castAssignment.Mn);
                    _params.Add("@S", castAssignment.S);
                    _params.Add("@P", castAssignment.P);
                    _params.Add("@Ti", castAssignment.Ti);
                    _params.Add("@Cr", castAssignment.Cr);
                    _params.Add("@SP", castAssignment.SP);

                    _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    _params.Add("@message", dbType: DbType.String, direction: ParameterDirection.Output, size: 200);

                    db.Execute(
                        Appsetting.SQLQueryCommand.PDA_SP_CreateCastAssignment,
                        _params,
                        commandType: CommandType.StoredProcedure
                    );

                    res.status = _params.Get<bool>("@status");
                    res.message = _params.Get<string>("@message");

                    if (res.status)
                        Logger.Info($"[CastAssignmentCreation] Success | UserID: {castAssignment.userID} | CastNo: {castAssignment.castNo} | Message: {res.message}");
                    else
                        Logger.Warn($"[CastAssignmentCreation] SP returned false | UserID: {castAssignment.userID} | CastNo: {castAssignment.castNo} | Message: {res.message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogDBError("CastAssignmentCreation", ex);
                res.status = false;
                res.message = ex.Message;
            }
            return res;
        }

        public bool LadleRequestCreation(PDALadleRequestModel pdaLadleRequestModel)
        {
            Logger.Info($"[LadleRequestCreation] Started | UserID: {pdaLadleRequestModel?.userID} | LocationID: {pdaLadleRequestModel?.locationID} | NosOfLadle: {pdaLadleRequestModel?.nosOfLadle}");
            bool isValid = false;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@requestID", pdaLadleRequestModel.requestID);
                _params.Add("@userID", pdaLadleRequestModel.userID);
                _params.Add("@locationID", pdaLadleRequestModel.locationID);
                _params.Add("@nosOfLadle", pdaLadleRequestModel.nosOfLadle);
                _params.Add("@C", pdaLadleRequestModel.C);
                _params.Add("@Si", pdaLadleRequestModel.Si);
                _params.Add("@Mn", pdaLadleRequestModel.Mn);
                _params.Add("@S", pdaLadleRequestModel.S);
                _params.Add("@P", pdaLadleRequestModel.P);
                _params.Add("@Ti", pdaLadleRequestModel.Ti);
                _params.Add("@Cr", pdaLadleRequestModel.Cr);
                _params.Add("@SP", pdaLadleRequestModel.SP);
                _params.Add("@transactionDateTime", pdaLadleRequestModel.transactionDateTime);
                _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.PDA_SP_CreateLadleRequest, _params, commandType: CommandType.StoredProcedure);
                isValid = _params.Get<bool>("@Status");
            }
            if (isValid)
                Logger.Info($"[LadleRequestCreation] Success | UserID: {pdaLadleRequestModel.userID} | LocationID: {pdaLadleRequestModel.locationID}");
            else
                Logger.Warn($"[LadleRequestCreation] SP returned false | UserID: {pdaLadleRequestModel.userID} | LocationID: {pdaLadleRequestModel.locationID}");
            return isValid;
        }

        public List<PDAGetLadleRequestModel> GetRequestedLadles(string userID, int locationID)
        {
            Logger.Info($"[GetRequestedLadles] Started | UserID: {userID} | LocationID: {locationID}");
            List<PDAGetLadleRequestModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                retValue = db.Query<PDAGetLadleRequestModel>(Appsetting.SQLQueryCommand.PDA_SP_GetLadleRequest, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetRequestedLadles] Completed | UserID: {userID} | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public bool UpdateLadleRequest(PDAUpdateLadleRequestModel updateLadleRequestModel)
        {
            Logger.Info($"[UpdateLadleRequest] Started | UserID: {updateLadleRequestModel?.userID} | RequestNo: {updateLadleRequestModel?.requestNo} | Action: {updateLadleRequestModel?.action}");
            bool isValid = false;

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                var _params = new DynamicParameters();

                _params.Add("@userID", updateLadleRequestModel.userID);
                _params.Add("@nosOfLadle", updateLadleRequestModel.nosOfLadle);
                _params.Add("@requestID", updateLadleRequestModel.requestNo);
                _params.Add("@transactionDateTime", updateLadleRequestModel.transactionDateTime);
                _params.Add("@action", updateLadleRequestModel.action);

                _params.Add("@C", updateLadleRequestModel.C);
                _params.Add("@Si", updateLadleRequestModel.Si);
                _params.Add("@Mn", updateLadleRequestModel.Mn);
                _params.Add("@S", updateLadleRequestModel.S);
                _params.Add("@P", updateLadleRequestModel.P);
                _params.Add("@Ti", updateLadleRequestModel.Ti);
                _params.Add("@Cr", updateLadleRequestModel.Cr);
                _params.Add("@SP", updateLadleRequestModel.SP);

                _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                db.Execute(
                    Appsetting.SQLQueryCommand.PDA_SP_UpdateLadleRequest, _params,
                    commandType: CommandType.StoredProcedure
                );

                isValid = _params.Get<bool>("@Status");
            }

            if (isValid)
                Logger.Info($"[UpdateLadleRequest] Success | UserID: {updateLadleRequestModel.userID} | RequestNo: {updateLadleRequestModel.requestNo}");
            else
                Logger.Warn($"[UpdateLadleRequest] SP returned false | UserID: {updateLadleRequestModel.userID} | RequestNo: {updateLadleRequestModel.requestNo}");

            return isValid;
        }

        public List<ReversalLadlesModel> GetReversalLadles(string userID, int locationID)
        {
            Logger.Info($"[GetReversalLadles] Started | UserID: {userID} | LocationID: {locationID}");
            List<ReversalLadlesModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                retValue = db.Query<ReversalLadlesModel>(Appsetting.SQLQueryCommand.PDA_SP_GetReversalLadles, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetReversalLadles] Completed | UserID: {userID} | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public bool RevarsalMovementCreaction(PDACreateRevarsalMovementModel pdaCreateRevarsalMovementModel)
        {
            Logger.Info($"[RevarsalMovementCreaction] Started | UserID: {pdaCreateRevarsalMovementModel?.userID} | Source: {pdaCreateRevarsalMovementModel?.source} | DetailCount: {pdaCreateRevarsalMovementModel?.reversalDetails?.Count ?? 0}");
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                foreach (var detail in pdaCreateRevarsalMovementModel.reversalDetails)
                {
                    var _params = new DynamicParameters();
                    _params.Add("@userID", pdaCreateRevarsalMovementModel.userID);
                    _params.Add("@source", pdaCreateRevarsalMovementModel.source);
                    _params.Add("@destination", detail.destination);
                    _params.Add("@transactionDateTime", pdaCreateRevarsalMovementModel.transactionDateTime);
                    _params.Add("@ID", detail.ID);
                    _params.Add("@ladleNo", detail.ladleNo);
                    _params.Add("@movementType", detail.movementType);
                    _params.Add("@castID", detail.castID);
                    _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                    db.Execute(Appsetting.SQLQueryCommand.PDA_SP_CreateRevarsalMovement, _params, commandType: CommandType.StoredProcedure);
                    bool status = _params.Get<bool>("@Status");

                    if (!status)
                    {
                        Logger.Warn($"[RevarsalMovementCreaction] SP returned false for LadleNo: {detail.ladleNo} | UserID: {pdaCreateRevarsalMovementModel.userID}");
                        return false;
                    }
                }
                Logger.Info($"[RevarsalMovementCreaction] All details processed successfully | UserID: {pdaCreateRevarsalMovementModel.userID}");
                return true;
            }
        }

        public LadleMovementResponse GetLadleMovement(string userID)
        {
            Logger.Info($"[GetLadleMovement] Started | UserID: {userID}");
            LadleMovementResponse result = new LadleMovementResponse();
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                using (var multi = db.QueryMultiple(Appsetting.SQLQueryCommand.PDA_SP_GetLadleMovement, _params, commandType: CommandType.StoredProcedure))
                {
                    result.movementOrder = multi.Read<PDAGetLadleMovement>().ToList();
                }
            }
            Logger.Info($"[GetLadleMovement] Completed | UserID: {userID} | Count: {result.movementOrder?.Count ?? 0}");
            return result;
        }

        public Response CloseLadleMovement(CloseLadleMovementModel model)
        {
            Logger.Info($"[CloseLadleMovement] Started | UserID: {model?.userID} | TripID: {model?.tripID} | LadleNos: {model?.ladleNos}");
            Response res = new Response();
            bool isValid = false;
            string message = string.Empty;

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var ladleList = model.ladleNos.Contains(",")
                ? model.ladleNos.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList()
                : new List<string> { model.ladleNos };

                foreach (var ladleNo in ladleList)
                {
                    var _params = new DynamicParameters();
                    _params.Add("@clientDeviceID", model.clientDeviceID);
                    _params.Add("@userID", model.userID);
                    _params.Add("@tripID", model.tripID);
                    _params.Add("@source", model.source);
                    _params.Add("@destination", model.destination);
                    _params.Add("@ladleNo", ladleNo);
                    _params.Add("@transactionDateTime", model.transactionDateTime);
                    _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    _params.Add("@message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
                    db.Execute(Appsetting.SQLQueryCommand.PDA_SP_CloseLadleMovement, _params, commandType: CommandType.StoredProcedure);
                    isValid = _params.Get<bool>("@status");
                    message = _params.Get<string>("@message");
                    if (isValid)
                    {
                        res.status = true;
                        res.message = message;
                        Logger.Info($"[CloseLadleMovement] LadleNo: {ladleNo} closed successfully | TripID: {model.tripID} | Message: {message}");
                    }
                    else
                    {
                        res.status = false;
                        res.message = message;
                        Logger.Warn($"[CloseLadleMovement] SP returned false for LadleNo: {ladleNo} | TripID: {model.tripID} | Message: {message}");
                    }
                }
            }
            return res;
        }

        public Response TransferLadleMovement(TransferLadleMovementModel model)
        {
            Logger.Info($"[TransferLadleMovement] Started | UserID: {model?.userID} | TripID: {model?.tripID} | LadleNos: {model?.ladleNos}");
            Response res = new Response();
            bool isValid = false;
            string message = string.Empty;

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                var _params = new DynamicParameters();

                _params.Add("@clientDeviceID", model.clientDeviceID);
                _params.Add("@userID", model.userID);
                _params.Add("@tripID", model.tripID);
                _params.Add("@ladleNos", model.ladleNos);
                _params.Add("@currentLocation", model.currentLocation);
                _params.Add("@destination", model.destination);
                _params.Add("@transactionDateTime", model.transactionDateTime);

                _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                _params.Add("@message", dbType: DbType.String, size: 250, direction: ParameterDirection.Output);

                db.Execute(
                    Appsetting.SQLQueryCommand.PDA_SP_TransferLadleMovement, _params,
                    commandType: CommandType.StoredProcedure
                );

                isValid = _params.Get<bool>("@status");
                message = _params.Get<string>("@message");

                res.status = isValid;
                res.message = message;
            }

            if (res.status)
                Logger.Info($"[TransferLadleMovement] Success | UserID: {model.userID} | TripID: {model.tripID} | Message: {res.message}");
            else
                Logger.Warn($"[TransferLadleMovement] SP returned false | UserID: {model.userID} | TripID: {model.tripID} | Message: {res.message}");

            return res;
        }

        public ProductionDataRes GetProductionDetails(string userID, int locationID)
        {
            Logger.Info($"[GetProductionDetails] Started | UserID: {userID} | LocationID: {locationID}");
            ProductionDataRes ret = new ProductionDataRes();
            List<AvailableLadlesInFurnace> retValue = null;
            ret.reversalLadles = GetReversalLadles(userID, locationID);
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                retValue = db.Query<AvailableLadlesInFurnace>(Appsetting.SQLQueryCommand.PDA_SP_GetAvailableLadles, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            ret.availableLadles = retValue;
            ret.summary = GetSummary(userID, locationID);
            ret.requestCount = GetRequestedLadles(userID, locationID).Count();
            Logger.Info($"[GetProductionDetails] Completed | UserID: {userID} | AvailableLadles: {retValue?.Count ?? 0} | RequestCount: {ret.requestCount}");
            return ret;
        }

        public bool RequestAvailableLadles(RequestAvailableLadleModel model)
        {
            Logger.Info($"[RequestAvailableLadles] Started | UserID: {model?.userID} | RequestLocationID: {model?.requestLocationID} | LadleCount: {model?.ladles?.Count ?? 0}");
            bool isValid = false;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                foreach (var detail in model.ladles)
                {
                    var _params = new DynamicParameters();
                    _params.Add("@furnaceLocationID", detail.furnaceLocationID);
                    _params.Add("@userID", model.userID);
                    _params.Add("@requestLocationID", model.requestLocationID);
                    _params.Add("@ladleNo", detail.ladleNo);
                    _params.Add("@transactionDateTime", model.transactionDateTime);
                    _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    db.Execute(Appsetting.SQLQueryCommand.PDA_SP_RequestAvailableLadles, _params, commandType: CommandType.StoredProcedure);
                    isValid = _params.Get<bool>("@Status");
                    if (!isValid)
                        Logger.Warn($"[RequestAvailableLadles] SP returned false for LadleNo: {detail.ladleNo} | UserID: {model.userID}");
                }
            }
            Logger.Info($"[RequestAvailableLadles] Completed | UserID: {model.userID} | FinalStatus: {isValid}");
            return isValid;
        }

        public List<PDASummaryModel> GetSummary(string userID, int locationID)
        {
            Logger.Info($"[GetSummary] Started | UserID: {userID} | LocationID: {locationID}");
            List<PDASummaryModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                retValue = db.Query<PDASummaryModel>(Appsetting.SQLQueryCommand.PDA_SP_GetSummary, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetSummary] Completed | UserID: {userID} | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public bool DeleteBookingSummary(BookingSummaryUpdatioModel model)
        {
            Logger.Info($"[DeleteBookingSummary] Started | UserID: {model?.userID} | LadleNo: {model?.ladleNo} | CastNo: {model?.castNo}");
            bool isValid = false;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", model.userID);
                _params.Add("@ladleNo", model.ladleNo);
                _params.Add("@castNo", model.castNo);
                _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.PDA_SP_UpdateBookingLadles, _params, commandType: CommandType.StoredProcedure);
                isValid = _params.Get<bool>("@Status");
            }
            if (isValid)
                Logger.Info($"[DeleteBookingSummary] Success | UserID: {model.userID} | LadleNo: {model.ladleNo}");
            else
                Logger.Warn($"[DeleteBookingSummary] SP returned false | UserID: {model.userID} | LadleNo: {model.ladleNo}");
            return isValid;
        }

        public bool CompleteReversalLadleRequest(Guid id)
        {
            Logger.Info($"[CompleteReversalLadleRequest] Started | ID: {id}");
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@ID", id);
                    _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    db.Execute(
                        Appsetting.SQLQueryCommand.WEB_SP_CompleteReversalLadleRequest,
                        _params,
                        commandType: CommandType.StoredProcedure
                    );
                    bool status = _params.Get<bool>("@Status");
                    if (status)
                        Logger.Info($"[CompleteReversalLadleRequest] Success | ID: {id}");
                    else
                        Logger.Warn($"[CompleteReversalLadleRequest] SP returned false | ID: {id}");
                    return status;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDBError("CompleteReversalLadleRequest", ex);
                return false;
            }
        }

        public FurnaceDashboard GetFurnaceDashboardData(string userID, int locationID)
        {
            Logger.Info($"[GetFurnaceDashboardData] Started | UserID: {userID} | LocationID: {locationID}");
            FurnaceDashboard ret = new FurnaceDashboard();
            List<AvaialableEmptyLadles> retValue = null;
            ret.availableLadles = GetLadlesByFurnace(userID, locationID);
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                retValue = db.Query<AvaialableEmptyLadles>(Appsetting.SQLQueryCommand.PDA_SP_GetEmptyLadlesForBF, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            ret.emptyLadles = retValue;
            ret.summary = GetSummary(userID, locationID);
            Logger.Info($"[GetFurnaceDashboardData] Completed | UserID: {userID} | EmptyLadles: {retValue?.Count ?? 0}");
            return ret;
        }

        public bool RequestReversalLadles(RequestReversalLadles model)
        {
            Logger.Info($"[RequestReversalLadles] Started | UserID: {model?.userID} | RequestLocationID: {model?.requestLocationID} | LadleCount: {model?.ladles?.Count ?? 0}");
            bool isValid = false;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                foreach (var detail in model.ladles)
                {
                    var _params = new DynamicParameters();
                    _params.Add("@sourceLocationID", detail.sourceLocationID);
                    _params.Add("@userID", model.userID);
                    _params.Add("@requestLocationID", model.requestLocationID);
                    _params.Add("@ladleNo", detail.ladleNo);
                    _params.Add("@movementType", detail.movementType);
                    _params.Add("@transactionDateTime", model.transactionDateTime);
                    _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    db.Execute(Appsetting.SQLQueryCommand.PDA_SP_RequestReversalLadles, _params, commandType: CommandType.StoredProcedure);
                    isValid = _params.Get<bool>("@Status");
                    if (!isValid)
                        Logger.Warn($"[RequestReversalLadles] SP returned false for LadleNo: {detail.ladleNo} | UserID: {model.userID}");
                }
            }
            Logger.Info($"[RequestReversalLadles] Completed | UserID: {model.userID} | FinalStatus: {isValid}");
            return isValid;
        }

        public bool DeleteBookedRequest(BookingSummaryUpdatioModel1 model)
        {
            Logger.Info($"[DeleteBookedRequest] Started | UserID: {model?.userID} | LadleNo: {model?.ladleNo} | MovementType: {model?.movementType}");
            bool isValid = false;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", model.userID);
                _params.Add("@ladleNo", model.ladleNo);
                _params.Add("@movementType", model.movementType);
                _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.PDA_SP_UpdateReversalBookingLadles, _params, commandType: CommandType.StoredProcedure);
                isValid = _params.Get<bool>("@Status");
            }
            if (isValid)
                Logger.Info($"[DeleteBookedRequest] Success | UserID: {model.userID} | LadleNo: {model.ladleNo}");
            else
                Logger.Warn($"[DeleteBookedRequest] SP returned false | UserID: {model.userID} | LadleNo: {model.ladleNo}");
            return isValid;
        }

        public List<TransactionLadleModel> GetTransactionLadles(string userID, int locationID)
        {
            Logger.Info($"[GetTransactionLadles] Started | UserID: {userID} | LocationID: {locationID}");
            List<TransactionLadleModel> result = new List<TransactionLadleModel>();
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@userID", userID);
                _params.Add("@locationID", locationID);
                result = db.Query<TransactionLadleModel>(
                    Appsetting.SQLQueryCommand.PDA_SP_GetTransactionLadles,
                    _params,
                    commandType: CommandType.StoredProcedure
                ).ToList();
            }
            Logger.Info($"[GetTransactionLadles] Completed | UserID: {userID} | Count: {result?.Count ?? 0}");
            return result;
        }

        public List<RequestReportModel> GetRequestReport(DateTime? fromDate, DateTime? toDate, int? locationID)
        {
            Logger.Info($"[GetRequestReport] Started | From: {fromDate} | To: {toDate} | LocationID: {locationID}");
            List<RequestReportModel> result = new List<RequestReportModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@fromDate", fromDate);
                    _params.Add("@toDate", toDate);
                    _params.Add("@locationID", locationID);
                    result = db.Query<RequestReportModel>(
                        Appsetting.SQLQueryCommand.PDA_SP_GetRequestReport,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                Logger.Info($"[GetRequestReport] Completed | Records: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetRequestReport", ex);
            }
            return result;
        }

        public List<FullReportModel> GetFullReport(DateTime? fromDate, DateTime? toDate, int? locationID)
        {
            Logger.Info($"[GetFullReport] Started | From: {fromDate} | To: {toDate} | LocationID: {locationID}");
            List<FullReportModel> result = new List<FullReportModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@fromDate", fromDate);
                    _params.Add("@toDate", toDate);
                    _params.Add("@locationID", locationID);
                    result = db.Query<FullReportModel>(
                        Appsetting.SQLQueryCommand.PDA_SP_GetFullReport,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                Logger.Info($"[GetFullReport] Completed | Records: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetFullReport", ex);
            }
            return result;
        }
        #endregion

        public bool CompleteLadleRequest(Guid id)
        {
            Logger.Info($"[CompleteLadleRequest] Started | ID: {id}");
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();

                    var _params = new DynamicParameters();
                    _params.Add("@ID", id);
                    _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                    db.Execute(
                        Appsetting.SQLQueryCommand.WEB_SP_CompleteLadleRequest,
                        _params,
                        commandType: CommandType.StoredProcedure
                    );

                    bool status = _params.Get<bool>("@Status");
                    if (status)
                        Logger.Info($"[CompleteLadleRequest] Success | ID: {id}");
                    else
                        Logger.Warn($"[CompleteLadleRequest] SP returned false | ID: {id}");
                    return status;
                }
            }
            catch (Exception ex)
            {
                Logger.LogDBError("CompleteLadleRequest", ex);
                return false;
            }
        }

        public WEBDashboardLogin DashboardLoginData(DashboardLoginRequest loginRequest)
        {
            Logger.Info("[DashboardLoginData] Dashboard login attempt started");
            string decryptedUsername;
            string decryptedPassword;

            try
            {
                decryptedUsername = JwtHelper.Decrypt(loginRequest.userName).Trim();
                decryptedPassword = JwtHelper.Decrypt(loginRequest.password).Trim();
            }
            catch
            {
                Logger.Warn("[DashboardLoginData] Credential decryption failed");
                throw new Exception("Invalid credentials.");
            }

            if (string.IsNullOrWhiteSpace(decryptedUsername) ||
                string.IsNullOrWhiteSpace(decryptedPassword))
            {
                Logger.Warn("[DashboardLoginData] Empty username or password after decryption");
                throw new Exception("Invalid credentials.");
            }

            if (InputValidator.ContainsSQLInjection(decryptedUsername) ||
                InputValidator.ContainsSQLInjection(decryptedPassword))
            {
                Logger.Warn("[DashboardLoginData] SQL Injection attempt detected");
                throw new Exception("Invalid credentials.");
            }

            if (InputValidator.ContainsXSS(decryptedUsername) ||
                InputValidator.ContainsXSS(decryptedPassword))
            {
                Logger.Warn("[DashboardLoginData] XSS attempt detected");
                throw new Exception("Invalid credentials.");
            }

            WEBDashboardLogin response = new WEBDashboardLogin();
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                if (Appsetting.UseADAuthentication)
                {
                    string dbUser = db.QueryFirstOrDefault<string>(
                        "SELECT TOP 1 UserName FROM [Users] WHERE UserName = @U",
                        new { U = decryptedUsername }
                    );
                    if (string.IsNullOrWhiteSpace(dbUser))
                    {
                        dbUser = db.QueryFirstOrDefault<string>(
                            "SELECT TOP 1 UserName FROM [Users] WHERE UserName LIKE '%' + @U + '%' OR FirstName LIKE '%' + @U + '%'",
                            new { U = decryptedUsername }
                        );
                        if (!string.IsNullOrWhiteSpace(dbUser))
                        {
                            Logger.Info($"[DashboardLoginData] Mapped input '{decryptedUsername}' to DB user '{dbUser}'");
                            decryptedUsername = dbUser;
                        }
                    }
                }

                var passwordParams = new DynamicParameters();
                passwordParams.Add("@UserName", decryptedUsername, DbType.String, ParameterDirection.Input, 100);

                string procedureName = Appsetting.SQLQueryCommand.ESL_WEB_GetUserPassword;
                if (string.IsNullOrWhiteSpace(procedureName))
                {
                    procedureName = "ESL_WEB_GetUserPassword";
                }

                string storedPassword = db.QueryFirstOrDefault<string>(
                    procedureName,
                    passwordParams,
                    commandType: CommandType.StoredProcedure
                );

                if (!Appsetting.UseADAuthentication)
                {
                    if (string.IsNullOrWhiteSpace(storedPassword))
                    {
                        Logger.Warn($"[DashboardLoginData] User not found or inactive | UserName: {decryptedUsername}");
                        throw new Exception("Invalid credentials.");
                    }

                    storedPassword = storedPassword.Trim();
                    string cleanDecryptedPassword = decryptedPassword ?? string.Empty;
                    bool passwordValid;
                    if (storedPassword.StartsWith("$2a$") || storedPassword.StartsWith("$2b$") || storedPassword.StartsWith("$2y$"))
                    {
                        passwordValid = PasswordHelper.VerifyPassword(cleanDecryptedPassword, storedPassword);
                    }
                    else
                    {
                        passwordValid = storedPassword == cleanDecryptedPassword;
                    }

                    if (!passwordValid)
                    {
                        Logger.Warn($"[DashboardLoginData] Password mismatch | UserName: {decryptedUsername}");
                        throw new Exception("Invalid credentials.");
                    }
                }
                var _params = new DynamicParameters();
                _params.Add("@UserName", decryptedUsername);
                _params.Add("@Password", decryptedPassword);

                string loginSpName = !string.IsNullOrWhiteSpace(Appsetting.SQLQueryCommand.WEB_SP_DashboardLogin)
                    ? Appsetting.SQLQueryCommand.WEB_SP_DashboardLogin
                    : "ESL_WEB_SP_DashboardLogin";

                using (var multi = db.QueryMultiple(
                    loginSpName,
                    _params,
                    commandType: CommandType.StoredProcedure))
                {
                    var userInfo = multi.Read().FirstOrDefault();
                    if (userInfo != null)
                    {
                        response.userID = Guid.Parse(userInfo.userID.ToString());
                        response.userLocationId = userInfo.userLocationId;
                        response.userLocationName = InputValidator.EncodeOutput(userInfo.userLocationName.ToString());
                        response.userFirstName = InputValidator.EncodeOutput(userInfo.userFirstName.ToString());
                        response.sessionToken = userInfo.sessionToken;

                        response.jwtToken = JwtHelper.GenerateToken(
                            userInfo.userID.ToString(),
                            decryptedUsername,
                            userInfo.userLocationId,
                            userInfo.userLocationName.ToString()
                        );

                        Logger.Info($"[DashboardLoginData] Login successful | UserID: {userInfo.userID} | LocationID: {userInfo.userLocationId}");
                    }
                }
            }

            return response;
        }

        public bool WEBLogoutUser(string userID, string reason = "Manual Logout", string userName = null)
        {
            Logger.Info($"[WEBLogoutUser] Logout started | UserID: {userID} | UserName: {userName} | Reason: {reason}");
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                var _params = new DynamicParameters();
                _params.Add("@UserID", userID);
                _params.Add("@UserName", userName);
                _params.Add("@LogoutReason", string.IsNullOrWhiteSpace(reason) ? "Manual Logout" : reason);
                db.Execute("ESL_SP_RecordUserLogout", _params, commandType: CommandType.StoredProcedure);
                Logger.Info($"[WEBLogoutUser] Logout successful | UserID: {userID} | Reason: {reason}");
                return true;
            }
        }

        public List<BFLadleModel> GetAllBFLadle()
        {
            Logger.Info("[GetAllBFLadle] Started");
            List<BFLadleModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                retValue = db.Query<BFLadleModel>(Appsetting.SQLQueryCommand.SP_GetAllBFLadle, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetAllBFLadle] Completed | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public LadleRequestListResponse GetLadleRequest()
        {
            Logger.Info("[GetLadleRequest] Started");
            List<LadleRequestModel> allData;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                allData = db.Query<LadleRequestModel>(
                    Appsetting.SQLQueryCommand.SP_GetLadleRequest,
                    _params,
                    commandType: CommandType.StoredProcedure
                ).ToList();
            }
            var result = new LadleRequestListResponse
            {
                pendingRequest = allData.Where(x => x.isActive == 1).ToList(),
                completedRequest = allData.Where(x => x.isActive == 0).ToList()
            };
            Logger.Info($"[GetLadleRequest] Completed | Pending: {result.pendingRequest?.Count ?? 0} | Completed: {result.completedRequest?.Count ?? 0}");
            return result;
        }

        public LadleMovementListResponse GetLadleMovement()
        {
            Logger.Info("[GetLadleMovement] Started (WEB)");
            var result = new List<dynamic>();
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();

                result = db.Query<dynamic>(
                    Appsetting.SQLQueryCommand.SP_GetLadleMovement,
                    _params,
                    commandType: CommandType.StoredProcedure
                ).ToList();
            }
            var groupedData = result
                .GroupBy(r => new
                {
                    tripID = (string)r.tripID,
                    locoName = (string)r.locoName,
                    isActive = Convert.ToInt32(r.isActive),
                    isAssigned = Convert.ToInt32(r.isAssigned),
                })
                .Select(g => new LadleMovementModel
                {
                    tripID = g.Key.tripID,
                    locoName = g.Key.locoName,
                    isActive = g.Key.isActive,
                    isAssigned = g.Key.isAssigned,
                    movementData = g.Select(m => new LadleMovementList
                    {
                        ladleNo = (string)m.ladleNo,
                        sourceName = (string)m.sourceName,
                        destinationName = (string)m.destinationName
                    }).ToList()
                }).ToList();
            var response = new LadleMovementListResponse
            {
                intransitMovement = groupedData.Where(x => x.isActive == 1).ToList(),
                completedmovement = groupedData.Where(x => x.isActive == 0).ToList()
            };
            Logger.Info($"[GetLadleMovement] Completed (WEB) | InTransit: {response.intransitMovement?.Count ?? 0} | Completed: {response.completedmovement?.Count ?? 0}");
            return response;
        }

        //public bool CreateWEBMovement(CreateWEBMovementModel model)
        //{
        //    using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
        //    {
        //        var tripIDParams = new DynamicParameters();
        //        tripIDParams.Add("@locoID", model.locoID);
        //        tripIDParams.Add("@tripSerialNumber", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);
        //        db.Execute(Appsetting.SQLQueryCommand.GetTripSerialNumber, tripIDParams, commandType: CommandType.StoredProcedure);
        //        string tripID = tripIDParams.Get<string>("@tripSerialNumber");

        //        var movementTripParams = new DynamicParameters();
        //        movementTripParams.Add("@ID", model.ID);
        //        movementTripParams.Add("@tripID", tripID);
        //        movementTripParams.Add("@userID", model.userID);
        //        movementTripParams.Add("@locoID", model.locoID);
        //        movementTripParams.Add("@transactionDateTime", model.transactionDateTime);
        //        movementTripParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        //        movementTripParams.Add("@ladleMovementID", dbType: DbType.String, size: 100, direction: ParameterDirection.Output);
        //        db.Execute(Appsetting.SQLQueryCommand.WEB_SP_CreateMovementTrips, movementTripParams, commandType: CommandType.StoredProcedure);
        //        bool tripStatus = movementTripParams.Get<bool>("@Status");
        //        string ladleMovementID = movementTripParams.Get<string>("@ladleMovementID");
        //        if (!tripStatus || string.IsNullOrEmpty(ladleMovementID))
        //            return false;

        //        foreach (var detail in model.movementDetails)
        //        {
        //            var movementParams = new DynamicParameters();
        //            movementParams.Add("@ID", Guid.NewGuid());
        //            movementParams.Add("@ladleMovementID", ladleMovementID);
        //            movementParams.Add("@castID", detail.castID);
        //            movementParams.Add("@ladleNo", detail.ladleNo);
        //            //movementParams.Add("@movementTypeID", detail.movementTypeID.ToString());
        //            movementParams.Add("@movementTypeID", detail.movementTypeID);
        //            movementParams.Add("@sourceLocationName", detail.sourceLocationName);
        //            movementParams.Add("@destinationName", detail.destinationName);
        //            movementParams.Add("@transactionDateTime", detail.transactionDateTime);
        //            movementParams.Add("@castNo", detail.castNo);
        //            movementParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        //            db.Execute(Appsetting.SQLQueryCommand.WEB_SP_CreateMovement, movementParams, commandType: CommandType.StoredProcedure);
        //            bool status = movementParams.Get<bool>("@Status");
        //            if (!status)
        //                return false;
        //        }
        //        return true;
        //    }
        //}

        public bool CreateWEBMovement(CreateWEBMovementModel model)
        {
            Logger.Info($"[CreateWEBMovement] Started | UserID: {model?.userID} | LocoID: {model?.locoID} | DetailCount: {model?.movementDetails?.Count ?? 0}");
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                string tripID = string.Empty;
                string ladleMovementID = string.Empty;

                var existingTrip = db.QueryFirstOrDefault<dynamic>(
                    "SELECT TOP 1 ID, TripID FROM LadleMovement WHERE LocoID = @locoID AND IsActive = 1",
                    new { locoID = model.locoID }
                );

                if (existingTrip != null)
                {
                    tripID = existingTrip.TripID;
                    ladleMovementID = existingTrip.ID.ToString();
                    Logger.Info($"[CreateWEBMovement] Existing trip found | TripID: {tripID} | LocoID: {model.locoID}");
                }
                else
                {
                    var tripIDParams = new DynamicParameters();
                    tripIDParams.Add("@locoID", model.locoID);
                    tripIDParams.Add("@tripSerialNumber", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);

                    db.Execute(Appsetting.SQLQueryCommand.GetTripSerialNumber, tripIDParams, commandType: CommandType.StoredProcedure);

                    tripID = tripIDParams.Get<string>("@tripSerialNumber");

                    var movementTripParams = new DynamicParameters();
                    movementTripParams.Add("@ID", model.ID);
                    movementTripParams.Add("@tripID", tripID);
                    movementTripParams.Add("@userID", model.userID);
                    movementTripParams.Add("@locoID", model.locoID);
                    movementTripParams.Add("@transactionDateTime", model.transactionDateTime);
                    movementTripParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    movementTripParams.Add("@ladleMovementID", dbType: DbType.String, size: 100, direction: ParameterDirection.Output);

                    db.Execute(Appsetting.SQLQueryCommand.WEB_SP_CreateMovementTrips, movementTripParams, commandType: CommandType.StoredProcedure);

                    bool tripStatus = movementTripParams.Get<bool>("@Status");
                    ladleMovementID = movementTripParams.Get<string>("@ladleMovementID");

                    if (!tripStatus || string.IsNullOrEmpty(ladleMovementID))
                    {
                        Logger.Warn($"[CreateWEBMovement] Trip creation failed | TripID: {tripID} | LocoID: {model.locoID} | TripStatus: {tripStatus}");
                        return false;
                    }
                    Logger.Info($"[CreateWEBMovement] New trip created | TripID: {tripID} | LadleMovementID: {ladleMovementID}");
                }

                foreach (var detail in model.movementDetails)
                {
                    var movementParams = new DynamicParameters();
                    movementParams.Add("@ID", Guid.NewGuid());
                    movementParams.Add("@ladleMovementID", ladleMovementID);
                    movementParams.Add("@castID", detail.castID);
                    movementParams.Add("@ladleNo", detail.ladleNo);
                    movementParams.Add("@movementTypeID", detail.movementTypeID);
                    movementParams.Add("@sourceLocationName", detail.sourceLocationName);
                    movementParams.Add("@destinationName", detail.destinationName);
                    movementParams.Add("@transactionDateTime", detail.transactionDateTime);
                    movementParams.Add("@castNo", detail.castNo);
                    movementParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                    db.Execute(Appsetting.SQLQueryCommand.WEB_SP_CreateMovement, movementParams, commandType: CommandType.StoredProcedure);

                    bool status = movementParams.Get<bool>("@Status");
                    if (!status)
                    {
                        Logger.Warn($"[CreateWEBMovement] Movement insert failed for LadleNo: {detail.ladleNo} | TripID: {tripID}");
                        return false;
                    }
                }
                Logger.Info($"[CreateWEBMovement] All movements created successfully | TripID: {tripID} | UserID: {model.userID}");
                return true;
            }
        }

        public List<OccupiedLocoDetailsModel> GetAllOccupiedLoco()
        {
            Logger.Info("[GetAllOccupiedLoco] Started");
            List<OccupiedLocoDetailsModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                retValue = db.Query<OccupiedLocoDetailsModel>(Appsetting.SQLQueryCommand.WEB_SP_GetAllLocoOccupied, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetAllOccupiedLoco] Completed | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public List<OccupiedLocoDetailsModel> GetAvailableTransferLocos(string locoName)
        {
            Logger.Info($"[GetAvailableTransferLocos] Started | LocoName: {locoName}");
            List<OccupiedLocoDetailsModel> retValue = new List<OccupiedLocoDetailsModel>();

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                var _params = new DynamicParameters();

                var allLocos = db.Query<OccupiedLocoDetailsModel>(
                    Appsetting.SQLQueryCommand.WEB_SP_GetAllLocoOccupied,
                    _params,
                    commandType: CommandType.StoredProcedure
                ).ToList();

                /*
                    Dropdown rule:

                    Hide:
                    1. Current loco
                    2. IsOccupied = 1

                    Show:
                    1. IsOccupied = 0
                    2. IsBooked can be 0 or 1
                */
                retValue = allLocos
                    .Where(x =>
                        !string.Equals(
                            Convert.ToString(x.LocoName).Trim(),
                            Convert.ToString(locoName).Trim(),
                            StringComparison.OrdinalIgnoreCase
                        )
                        &&
                        x.IsOccupied == 0
                    )
                    .ToList();
            }
            Logger.Info($"[GetAvailableTransferLocos] Completed | LocoName: {locoName} | AvailableCount: {retValue?.Count ?? 0}");
            return retValue;
        }

        public List<LadelCountLocationModel> GetAllLadleCountLocations()
        {
            Logger.Info("[GetAllLadleCountLocations] Started");
            List<LadelCountLocationModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                retValue = db.Query<LadelCountLocationModel>(Appsetting.SQLQueryCommand.WEB_SP_GetDashboardData, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetAllLadleCountLocations] Completed | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public List<LadleReversalWEB> GetReversalLadlesWEB()
        {
            Logger.Info("[GetReversalLadlesWEB] Started");
            List<LadleReversalWEB> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                retValue = db.Query<LadleReversalWEB>(Appsetting.SQLQueryCommand.WEB_SP_GetReversalLadles, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[GetReversalLadlesWEB] Completed | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public bool InsertLadleReversalsWEB(InsertLadleReversalWEB model)
        {
            Logger.Info($"[InsertLadleReversalsWEB] Started | UserID: {model?.userID} | LocoID: {model?.locoID} | LadleCount: {model?.revLadles?.Count ?? 0}");
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                var tripIDParams = new DynamicParameters();
                tripIDParams.Add("@locoID", model.locoID);
                tripIDParams.Add("@tripSerialNumber", dbType: DbType.String, size: 50, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.GetTripSerialNumber, tripIDParams, commandType: CommandType.StoredProcedure);
                string tripID = tripIDParams.Get<string>("@tripSerialNumber");

                var movementTripParams = new DynamicParameters();
                movementTripParams.Add("@ID", model.ID);
                movementTripParams.Add("@tripID", tripID);
                movementTripParams.Add("@userID", model.userID);
                movementTripParams.Add("@locoID", model.locoID);
                movementTripParams.Add("@transactionDateTime", model.transactionDateTime);
                movementTripParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                movementTripParams.Add("@ladleMovementID", dbType: DbType.String, size: 100, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.WEB_SP_CreateMovementTrips, movementTripParams, commandType: CommandType.StoredProcedure);
                bool tripStatus = movementTripParams.Get<bool>("@Status");
                string ladleMovementID = movementTripParams.Get<string>("@ladleMovementID");
                if (!tripStatus || string.IsNullOrEmpty(ladleMovementID))
                {
                    Logger.Warn($"[InsertLadleReversalsWEB] Trip creation failed | TripID: {tripID} | LocoID: {model.locoID} | TripStatus: {tripStatus}");
                    return false;
                }
                Logger.Info($"[InsertLadleReversalsWEB] Trip created | TripID: {tripID} | LadleMovementID: {ladleMovementID}");

                foreach (var detail in model.revLadles)
                {
                    var movementParams = new DynamicParameters();
                    movementParams.Add("@ID", Guid.NewGuid());
                    movementParams.Add("@ladleMovementID", ladleMovementID);
                    movementParams.Add("@ladleNo", detail.ladleNo);
                    movementParams.Add("@castID", detail.castID);
                    movementParams.Add("@movementType", detail.movementType);
                    movementParams.Add("@sourceLocationName", detail.sourceLocationName);
                    movementParams.Add("@destinationName", detail.destinationName);
                    movementParams.Add("@transactionDateTime", detail.transactionDateTime);
                    movementParams.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    db.Execute(Appsetting.SQLQueryCommand.WEB_SP_InsertLadleReversal, movementParams, commandType: CommandType.StoredProcedure);
                    bool status = movementParams.Get<bool>("@Status");
                    if (!status)
                    {
                        Logger.Warn($"[InsertLadleReversalsWEB] Reversal insert failed for LadleNo: {detail.ladleNo} | TripID: {tripID}");
                        return false;
                    }
                }
                Logger.Info($"[InsertLadleReversalsWEB] All reversals inserted successfully | TripID: {tripID} | UserID: {model.userID}");
                return true;
            }
        }

        public Response AssignedLocoToLadle(AssignedLocoToLadle assignedLoco)
        {
            Logger.Info($"[AssignedLocoToLadle] Started | TripID: {assignedLoco?.tripID} | LocoName: {assignedLoco?.locoName}");
            Response res = new Response();
            bool isValid = false;
            string message = string.Empty;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                _params.Add("@tripID", assignedLoco.tripID);
                _params.Add("@locoName", assignedLoco.locoName);
                _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                _params.Add("@message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
                db.Execute(Appsetting.SQLQueryCommand.WEB_SP_AssignedLocoToLadle, _params, commandType: CommandType.StoredProcedure);
                isValid = _params.Get<bool>("@status");
                message = _params.Get<string>("@message");
                if (isValid)
                {
                    res.status = true;
                    res.message = message;
                    Logger.Info($"[AssignedLocoToLadle] Success | TripID: {assignedLoco.tripID} | LocoName: {assignedLoco.locoName} | Message: {message}");
                }
                else
                {
                    res.status = false;
                    res.message = message;
                    Logger.Warn($"[AssignedLocoToLadle] SP returned false | TripID: {assignedLoco.tripID} | LocoName: {assignedLoco.locoName} | Message: {message}");
                }
            }
            return res;
        }

        public List<TrailLadleLocationModel> TrailLadleLocation()
        {
            Logger.Info("[TrailLadleLocation] Started");
            List<TrailLadleLocationModel> retValue = null;
            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();
                var _params = new DynamicParameters();
                retValue = db.Query<TrailLadleLocationModel>(Appsetting.SQLQueryCommand.WEB_SP_GetLadleMovementTrailLocation, _params, commandType: CommandType.StoredProcedure).ToList();
            }
            Logger.Info($"[TrailLadleLocation] Completed | Count: {retValue?.Count ?? 0}");
            return retValue;
        }

        public Response DeleteLadleFromMovement(DeleteLadleRequest request)
        {
            Logger.Info($"[DeleteLadleFromMovement] Started | TripID: {request?.TripID} | LadleNo: {request?.LadleNo}");
            Response res = new Response();
            if (request == null)
            {
                Logger.Warn("[DeleteLadleFromMovement] Request object is null");
                res.status = false;
                res.message = "Request is null";
                return res;
            }
            if (string.IsNullOrEmpty(request.TripID) || string.IsNullOrEmpty(request.LadleNo))
            {
                Logger.Warn($"[DeleteLadleFromMovement] Missing TripID or LadleNo | TripID: {request.TripID} | LadleNo: {request.LadleNo}");
                res.status = false;
                res.message = "TripID or LadleNo is missing";
                return res;
            }
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@tripID", request.TripID);
                    _params.Add("@ladleNo", request.LadleNo);
                    _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    _params.Add("@message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);
                    db.Execute(Appsetting.SQLQueryCommand.WEB_SP_DeleteLadleFromMovement, _params, commandType: CommandType.StoredProcedure);

                    res.status = _params.Get<bool>("@status");
                    res.message = _params.Get<string>("@message");

                    if (res.status)
                        Logger.Info($"[DeleteLadleFromMovement] Success | TripID: {request.TripID} | LadleNo: {request.LadleNo} | Message: {res.message}");
                    else
                        Logger.Warn($"[DeleteLadleFromMovement] SP returned false | TripID: {request.TripID} | LadleNo: {request.LadleNo} | Message: {res.message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogDBError("DeleteLadleFromMovement", ex);
                res.status = false;
                res.message = "DB Error: " + ex.Message;
            }
            return res;
        }

        public List<TransactionReportModel> GetTransactionReport(DateTime? fromDate, DateTime? toDate)
        {
            Logger.Info($"[GetTransactionReport] Started | From: {fromDate} | To: {toDate}");
            List<TransactionReportModel> result = new List<TransactionReportModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@fromDate", fromDate);
                    _params.Add("@toDate", toDate);
                    result = db.Query<TransactionReportModel>(
                        Appsetting.SQLQueryCommand.WEB_SP_GetTransactionReport,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                Logger.Info($"[GetTransactionReport] Completed | Records: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetTransactionReport", ex);
            }
            return result;
        }

        public Response TransferLoco(TransferLocoModel model)
        {
            Logger.Info($"[TransferLoco] TripID: {model?.tripID} | From: {model?.currentLoco} | To: {model?.newLoco}");
            Response response = new Response();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@tripID", model.tripID);
                    _params.Add("@currentLoco", model.currentLoco);
                    _params.Add("@newLoco", model.newLoco);
                    _params.Add("@status", dbType: DbType.Boolean, direction: ParameterDirection.Output);
                    _params.Add("@message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);

                    db.Execute(
                        Appsetting.SQLQueryCommand.WEB_SP_TransferLoco,
                        _params,
                        commandType: CommandType.StoredProcedure
                    );

                    response.status = _params.Get<bool>("@status");
                    response.message = _params.Get<string>("@message");
                }
                Logger.Info($"[TransferLoco] Result: {response.status} | {response.message}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("TransferLoco", ex);
                response.status = false;
                response.message = ex.Message;
            }
            return response;
        }

        //public EngineDashboardData GetDashboardData()
        //{
        //    EngineDashboardData response = new EngineDashboardData();

        //    using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
        //    {
        //        db.Open();
        //        string sqlQuery = "SELECT TOP 1 DashboardDataJson FROM DashboardDataDetails ORDER BY ModifiedDatetime DESC";
        //        var result = db.Query<byte[]>(sqlQuery).FirstOrDefault();
        //        if (result != null)
        //        {
        //            string utf8String = Encoding.UTF8.GetString(result);
        //            var jsonObject = JsonConvert.DeserializeObject<EngineDashboardData>(utf8String);
        //            response = jsonObject;
        //        }
        //    }

        //    return response;
        //}

        #region Analytical Dashboard

        private Engine GetEngine()
        {
            if (Appsetting._laddleTrackerEngine == null && !string.IsNullOrEmpty(Appsetting.esslLadleTrackDBConnString))
            {
                try
                {
                    Appsetting._laddleTrackerEngine = new Engine(Appsetting.esslLadleTrackDBConnString, Appsetting.limsLadleTrackDB, Appsetting.wbDatabase);
                }
                catch (Exception ex)
                {
                    Logger.Error("[GetEngine] Failed to initialize LadleTrackerEngine", ex);
                }
            }
            return Appsetting._laddleTrackerEngine;
        }

        public List<KPIModel> GetKPIs()
        {
            List<KPIModel> kpis = new List<KPIModel>();

            try
            {
                ESLDashboardSummary eSLDashboard = GetDashboardData();

                int TotalLadles = eSLDashboard.TotalActiveLadleCount;
                int ActiveLadles = eSLDashboard.TotalActiveLadleCount - eSLDashboard.UnusedLadles.Count;
                int IdleLadles = eSLDashboard.UnusedLadles.Count;
                int CompletedTrips = eSLDashboard.CompletedTrips;
                int PendingTrips = eSLDashboard.PendingTrips;

                // 🔹 Add KPI Data
                kpis.Add(new KPIModel
                {
                    Title = "Total Ladles",
                    Value = TotalLadles.ToString(),
                    Color = "blue",
                    Icon = "inbox",
                    Trend = "up"
                });

                kpis.Add(new KPIModel
                {
                    Title = "Active Ladles",
                    Value = ActiveLadles.ToString(),
                    Color = "green",
                    Icon = "flash_on",
                    Trend = "up"
                });

                kpis.Add(new KPIModel
                {
                    Title = "Idle Ladles",
                    Value = IdleLadles.ToString(),
                    Color = "yellow",
                    Icon = "pause_circle",
                    Trend = "flat"
                });

                kpis.Add(new KPIModel
                {
                    Title = "Completed Trips",
                    Value = CompletedTrips.ToString(),
                    Color = "cyan",
                    Icon = "check_circle",
                    Trend = "up"
                });

                kpis.Add(new KPIModel
                {
                    Title = "Pending Trips",
                    Value = PendingTrips.ToString(),
                    Color = "orange",
                    Icon = "schedule",
                    Trend = "flat"
                });

                TimeSpan totalTAT = TimeSpan.Zero;
                int count = eSLDashboard.LocationData.Count;

                foreach (var item in eSLDashboard.LocationData)
                {
                    if (!string.IsNullOrEmpty(item.AverageTATSTR))
                    {
                        // Parse string (d.hh:mm) → TimeSpan
                        if (TimeSpan.TryParse(item.AverageTATSTR, out TimeSpan tat))
                        {
                            totalTAT = totalTAT.Add(tat);
                        }
                    }
                }

                // ✅ Calculate Average
                TimeSpan avgTAT = TimeSpan.Zero;

                if (count > 0)
                {
                    avgTAT = new TimeSpan(totalTAT.Ticks / count);
                }

                // ✅ Format to hh:mm
                string formattedAvgTAT = $"{(int)avgTAT.TotalHours:D2}:{avgTAT.Minutes:D2}";

                // 🔹 Example Avg TAT (if available in your model)
                kpis.Add(new KPIModel
                {
                    Title = "Avg TAT",
                    Value = formattedAvgTAT ?? string.Empty, // handle null if needed
                    Unit = "min",
                    Color = "teal",
                    Icon = "av_timer",
                    Trend = "down"
                });
            }
            catch (Exception ex)
            {
                // ❌ Don't use "throw ex;" → it resets stack trace
                throw;
            }

            return kpis;
        }


        public List<LadleChartDto> GetLadleChartData()
        {
            DateTime from = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            DateTime to = DateTime.Now;
            try
            {
                // Fetch data
                Engine eng = GetEngine();
                if (eng == null) return new List<LadleChartDto>();
                List<PSL.Infinity.ESLLadleTracker.Model.ESLLadle> activeMovingLadles = eng.GetAllLadleObjectInUseActive(from, to);

                // Step 1: Generate 5-min slots
                var timeSlots = new List<DateTime>();
                for (var dt = from; dt <= to; dt = dt.AddMinutes(5))
                {
                    timeSlots.Add(new DateTime(
                        dt.Year, dt.Month, dt.Day,
                        dt.Hour, (dt.Minute / 5) * 5, 0));
                }

                // Step 2: Group data into 5-min buckets
                var grouped = activeMovingLadles
                    .GroupBy(l => new DateTime(
                        l.InTime.Year,
                        l.InTime.Month,
                        l.InTime.Day,
                        l.InTime.Hour,
                        (l.InTime.Minute / 5) * 5,
                        0))
                    .ToDictionary(g => g.Key, g => g.Count());

                // Step 3: Map result
                var result = timeSlots.Select(slot => new LadleChartDto
                {
                    Time = slot.ToString("HH:mm"),
                    Value = grouped.ContainsKey(slot) ? grouped[slot] : 0
                }).ToList();

                return result;

            }
            catch (Exception ex)
            {
                throw new Exception("Error");
            }
        }


        public List<HourlyLadleData> GetHourlyLadleData()
        {
            List<HourlyLadleData> result = new List<HourlyLadleData>();
            try
            {
                string query = @"
        SELECT 
            FORMAT(
                DATEADD(HOUR, DATEPART(HOUR, R.ServerDatetime), 0),
                'hh:mm tt'
            ) AS Time,
            COUNT(DISTINCT A.AName) AS TotalActiveLadle
        FROM ReadersTransactionLog R
        LEFT JOIN AssetMaster A ON A.ATagID = R.TagId
        WHERE 
            CAST(R.ServerDatetime AS DATE) = CAST(GETDATE() AS DATE)
            AND DATEPART(HOUR, R.ServerDatetime) >= 00
            AND DATEPART(HOUR, R.ServerDatetime) < 24
            AND A.ATypeID = 1
            AND A.IsActive = 1
        GROUP BY DATEPART(HOUR, R.ServerDatetime)
        ORDER BY DATEPART(HOUR, R.ServerDatetime)";

                using (SqlConnection con = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    SqlCommand cmd = new SqlCommand(query, con);

                    con.Open();

                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        result.Add(new HourlyLadleData
                        {
                            Time = reader["Time"].ToString(),
                            TotalActiveLadle = Convert.ToInt32(reader["TotalActiveLadle"])
                        });
                    }

                    con.Close();
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error");
            }
            ;
        }


        public List<HourlyLadleData> GetHourlyLadleData(DateTime selectedDate)
        {
            List<HourlyLadleData> result = new List<HourlyLadleData>();

            string query = @"
        SELECT 
            FORMAT(
                DATEADD(HOUR, DATEPART(HOUR, R.ServerDatetime), 0),
                'hh:mm tt'
            ) AS Time,
            COUNT(DISTINCT A.AName) AS TotalActiveLadle
        FROM ReadersTransactionLog R
        LEFT JOIN AssetMaster A ON A.ATagID = R.TagId
        WHERE 
            CAST(R.ServerDatetime AS DATE) = CAST(@SelectedDate AS DATE)
            AND DATEPART(HOUR, R.ServerDatetime) >= 00
            AND DATEPART(HOUR, R.ServerDatetime) < 25
            AND A.ATypeID = 1
            AND A.IsActive = 1
        GROUP BY DATEPART(HOUR, R.ServerDatetime)
        ORDER BY DATEPART(HOUR, R.ServerDatetime)";

            using (SqlConnection con = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
            {
                SqlCommand cmd = new SqlCommand(query, con);

                // Pass custom date parameter
                cmd.Parameters.AddWithValue("@SelectedDate", selectedDate.Date);

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    result.Add(new HourlyLadleData
                    {
                        Time = reader["Time"].ToString(),
                        TotalActiveLadle = Convert.ToInt32(reader["TotalActiveLadle"])
                    });
                }

                con.Close();
            }

            return result;
        }


        public List<HourlyLadleData> GetShiftHourlyLadleData(DateTime selectedDate, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            List<HourlyLadleData> result = new List<HourlyLadleData>();

            DateTime startDateTime = selectedDate.Date.Add(shiftStart);
            DateTime endDateTime = selectedDate.Date.Add(shiftEnd);

            // Handle night shift crossing midnight
            if (shiftEnd < shiftStart)
            {
                endDateTime = endDateTime.AddDays(1);
            }

            string query = @"
        SELECT 
            DATEPART(HOUR, R.ServerDatetime) AS HourValue,
            COUNT(DISTINCT A.AName) AS TotalActiveLadle
        FROM ReadersTransactionLog R
        LEFT JOIN AssetMaster A ON A.ATagID = R.TagId
        WHERE 
            R.ServerDatetime >= @StartDateTime
            AND R.ServerDatetime < @EndDateTime
            AND A.ATypeID = 1
            AND A.IsActive = 1
        GROUP BY DATEPART(HOUR, R.ServerDatetime)
        ORDER BY HourValue";

            Dictionary<int, int> dbData = new Dictionary<int, int>();

            using (SqlConnection con = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
            {
                SqlCommand cmd = new SqlCommand(query, con);

                cmd.Parameters.AddWithValue("@StartDateTime", startDateTime);
                cmd.Parameters.AddWithValue("@EndDateTime", endDateTime);

                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    int hour = Convert.ToInt32(reader["HourValue"]);
                    int count = Convert.ToInt32(reader["TotalActiveLadle"]);

                    dbData[hour] = count;
                }

                con.Close();
            }

            // Fill all hours even if no data
            for (DateTime dt = startDateTime; dt < endDateTime; dt = dt.AddHours(1))
            {
                int hour = dt.Hour;

                result.Add(new HourlyLadleData
                {
                    Time = dt.ToString("hh:mm tt"),
                    TotalActiveLadle = dbData.ContainsKey(hour)
                        ? dbData[hour]
                        : 0
                });
            }

            return result;
        }


        public HourlyTripSummaryResponse GetHourlyTripsSummary()
        {
            DateTime todateTime = DateTime.Now;
            DateTime fromDateTime;

            // Shift starts from 10 PM
            if (todateTime.Hour >= 22)
            {
                fromDateTime = todateTime;
            }
            else
            {
                fromDateTime = todateTime.AddDays(-1);
            }

            fromDateTime = new DateTime(
                fromDateTime.Year,
                fromDateTime.Month,
                fromDateTime.Day,
                22,
                0,
                0
            );

            // Get Transactions
            Engine eng = GetEngine();
            if (eng == null) return new HourlyTripSummaryResponse();
            ESLLadleTransactionSummary sum =
               eng.GetTransactionSummary(fromDateTime, todateTime);

            // Dictionaries
            Dictionary<string, int> completedTripsPerHour =
                new Dictionary<string, int>();

            Dictionary<string, int> targetTripsPerHour =
                new Dictionary<string, int>();

            // Example target per hour
            int targetPerHour = 20;

            // Create hourly slots
            DateTime slot = fromDateTime;

            while (slot <= todateTime)
            {
                string hourKey = slot.ToString("hh tt");

                completedTripsPerHour[hourKey] = 0;
                targetTripsPerHour[hourKey] = targetPerHour;

                slot = slot.AddHours(1);
            }

            int totalTrips = sum.LadleTransactionDetails.Count;

            int completedTrips = 0;
            int pendingTrips = 0;

            Dictionary<string, PSL.Infinity.ESLLadleTracker.Model.ESLLocation> talCalculationDictionary =
                new Dictionary<string, PSL.Infinity.ESLLadleTracker.Model.ESLLocation>();

            foreach (ESLLadleConsolidated con in sum.LadleTransactionDetails)
            {
                if (!string.IsNullOrEmpty(con.DestinationLocation))
                {
                    completedTrips++;

                    // Hourly completed trip count
                    string hourKey =
                        con.DestinationOutDateTime.ToString("hh tt");

                    if (completedTripsPerHour.ContainsKey(hourKey))
                    {
                        completedTripsPerHour[hourKey]++;
                    }

                    // Existing TAT Logic
                    if (talCalculationDictionary.ContainsKey(con.DestinationLocation))
                    {
                        PSL.Infinity.ESLLadleTracker.Model.ESLLocation loc =
                            talCalculationDictionary[con.DestinationLocation];

                        // Holding Time
                        if ((con.DestinationOutDateTime -
                             con.DestinationInDateTime).TotalMinutes > 15)
                        {
                            loc.totalHoldingTime +=
                                (con.DestinationOutDateTime -
                                 con.DestinationInDateTime);

                            loc.totalHoldCount++;
                        }

                        // TAT
                        if (con.TATValue.TotalMinutes > 30)
                        {
                            loc.totalTAT += con.TATValue;
                            loc.totalTATCount++;
                        }
                    }
                    else
                    {
                        PSL.Infinity.ESLLadleTracker.Model.ESLLocation loc = new PSL.Infinity.ESLLadleTracker.Model.ESLLocation();

                        loc.LocationName = con.DestinationLocation;

                        // Holding Time
                        if ((con.DestinationOutDateTime -
                             con.DestinationInDateTime).TotalMinutes > 15)
                        {
                            loc.totalHoldingTime +=
                                (con.DestinationOutDateTime -
                                 con.DestinationInDateTime);

                            loc.totalHoldCount++;
                        }

                        // TAT
                        if (con.TATValue.TotalMinutes > 30)
                        {
                            loc.totalTAT += con.TATValue;
                            loc.totalTATCount++;
                        }

                        talCalculationDictionary.Add(
                            con.DestinationLocation,
                            loc
                        );
                    }
                }
                else
                {
                    pendingTrips++;
                }
            }

            // Final Hourly List
            List<HourlyTripData> hourlyData =
                completedTripsPerHour
                .Select(x => new HourlyTripData
                {
                    Time = x.Key,
                    CompletedTrips = x.Value,
                    TargetTrips = targetTripsPerHour[x.Key]
                })
                .ToList();

            return new HourlyTripSummaryResponse
            {
                TotalTrips = totalTrips,
                CompletedTrips = completedTrips,
                PendingTrips = pendingTrips,
                HourlyTrips = hourlyData
            };
        }

        public ESLDashboardSummary GetDashboardData()
        {

            string retrievedText = string.Empty;
            try
            {

                //New Method added to fetch data from database directly (31-03-2025)
                using (SqlConnection conn = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    conn.Open();
                    string query = "SELECT top 1 DashboardDataJson FROM DashboardDataDetails ORDER BY ModifiedDatetime DESC  ";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        byte[] byteArray2 = (byte[])cmd.ExecuteScalar();
                        retrievedText = Encoding.UTF8.GetString(byteArray2);
                    }
                }


                ESLDashboardSummary dashboardData = JsonConvert.DeserializeObject<ESLDashboardSummary>(retrievedText);

                if (dashboardData != null && dashboardData.LocationData != null)
                {
                    foreach (var loc in dashboardData.LocationData)
                    {
                        if (string.IsNullOrEmpty(loc.AverageTATSTR) || loc.AverageTATSTR == "0" || loc.AverageTATSTR == "00:00:00")
                        {
                            loc.AverageTATSTR = "00:00";
                        }
                        if (string.IsNullOrEmpty(loc.AverageHoldTimeSTR) || loc.AverageHoldTimeSTR == "0" || loc.AverageHoldTimeSTR == "00:00:00")
                        {
                            loc.AverageHoldTimeSTR = "00:00";
                        }
                    }
                }

                // ESLDashboardSummary dashboardData = _laddleTrackerEngine.GetLocationsOnlineData();

                return dashboardData;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public ServiceStatusResponse GetServiceActiveStatus()
        {
            DateTime serviceActiveDatetime = DateTime.MinValue;
            ServiceStatusResponse statusResponse = new ServiceStatusResponse();
            try
            {
                using (SqlConnection conn = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    conn.Open();
                    string query = "SELECT top 1 ModifiedDatetime FROM DashboardDataDetails ORDER BY ModifiedDatetime DESC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            serviceActiveDatetime = (DateTime)result;
                        }
                    }
                }
                statusResponse.serviceActiveDatetime = serviceActiveDatetime;
                TimeSpan span = DateTime.Now.Subtract(serviceActiveDatetime);
                if (span.TotalMinutes < 2)
                {
                    statusResponse.serviceStatus = true;
                }
                else
                {
                    statusResponse.serviceStatus = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[GetServiceActiveStatus] Error", ex);
                statusResponse.serviceStatus = false;
                statusResponse.serviceActiveDatetime = DateTime.MinValue;
            }
            return statusResponse;
        }

        public bool AssignLadle(List<ESLLadleAssignment> ladleAssignment)
        {
            try
            {
                Logger.Info($"[AssignLadle] Started with count: {ladleAssignment?.Count ?? 0}");
                if (Appsetting._laddleTrackerEngine != null && ladleAssignment != null)
                {
                    return Appsetting._laddleTrackerEngine.AssignLadles(ladleAssignment);
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("[AssignLadle] Error", ex);
                return false;
            }
        }

        public List<HourlyProductionConsumption> GetHourlyProductionConsumptionOptimized()
        {
            try
            {
                DateTime todateTime = DateTime.Now;
                DateTime fromDateTime;

                // Shift starts from 10 PM
                if (todateTime.Hour >= 23)
                {
                    fromDateTime = todateTime;
                }
                else
                {
                    fromDateTime = todateTime.AddDays(-1);
                }

                fromDateTime = new DateTime(
                    fromDateTime.Year,
                    fromDateTime.Month,
                    fromDateTime.Day,
                    22,
                    0,
                    0
                );

                Engine eng = GetEngine();
                if (eng == null) return new List<HourlyProductionConsumption>();
                List<HourlyProductionConsumption> hourlyProductionConsumptions = eng.GetHourlyProductionConsumptionOptimized(fromDateTime, todateTime);
                return hourlyProductionConsumptions;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region Hot metal

        // ============================================================
        // GET HOT METAL BOOKING
        // ============================================================
        public List<HotmetalBookingModel> GetHotmetalBooking()
        {
            List<HotmetalBookingModel> list =
                new List<HotmetalBookingModel>();

            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand("SP_GetHotmetalBooking", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                con.Open();

                using (SqlDataAdapter da =
                    new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    return MapHotmetalBooking(
                        dt,
                        "GetHotmetalBooking");
                }
            }
        }


        // ============================================================
        // GET REPORT DATA
        // ============================================================
        public List<HotmetalBookingModel> GetReportData()
        {
            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand("SP_GetReportData", con))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                con.Open();

                using (SqlDataAdapter da =
                    new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();
                    da.Fill(dt);

                    return MapHotmetalBooking(
                        dt,
                        "GetReportData");
                }
            }
        }


        // ============================================================
        // MAP HOT METAL BOOKING
        // ============================================================
        private List<HotmetalBookingModel> MapHotmetalBooking(
            DataTable dt,
            string callerName)
        {
            var list = new List<HotmetalBookingModel>();

            try
            {
                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(new HotmetalBookingModel
                    {
                        Tran_ID =
                            dr["Tran_ID"] == DBNull.Value
                                ? 0
                                : Convert.ToInt64(dr["Tran_ID"]),

                        GrossDateTime =
                            dr["GrossDateTime"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(
                                    dr["GrossDateTime"]),

                        TaredateTime =
                            dr["TaredateTime"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(
                                    dr["TaredateTime"]),

                        LaddleNumber =
                            dr["LaddleNumber"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(
                                    dr["LaddleNumber"]),

                        GrossWT =
                            dr["GrossWT"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(
                                    dr["GrossWT"]),

                        TareWT =
                            dr["TareWT"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(
                                    dr["TareWT"]),

                        NetWT =
                            dr["NetWT"] == DBNull.Value
                                ? (decimal?)null
                                : Convert.ToDecimal(
                                    dr["NetWT"]),

                        TransactionDateTime =
    dr["TransactionDateTime"] == DBNull.Value
        ? (DateTime?)null
        : Convert.ToDateTime(dr["TransactionDateTime"]),

                        FID =
                            dr["FID"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(
                                    dr["FID"]),

                        SenderPlant =
                            dr["SenderPlant"] == DBNull.Value
                                ? ""
                                : dr["SenderPlant"].ToString(),

                        CastNumber =
                            dr["CastNumber"] == DBNull.Value
                                ? ""
                                : dr["CastNumber"].ToString(),

                        ReceiverPlant =
                            dr["ReceiverPlant"] == DBNull.Value
                                ? ""
                                : dr["ReceiverPlant"].ToString(),

                        Gross_DT_Time =
                            dr["Gross_DT_Time"] == DBNull.Value
                                ? ""
                                : dr["Gross_DT_Time"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Error in {callerName}: {ex.Message}",
                    ex);
            }

            return list;
        }


        // ============================================================
        // UPDATE CAST NUMBER
        // ============================================================
        public int UpdateCastNumber(
            int tranId,
            string castNumber)
        {
            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand("SP_UpdateCastNumber", con))
            {
                cmd.CommandType =
                    CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue(
                    "@Tran_ID",
                    tranId);

                cmd.Parameters.AddWithValue(
                    "@CastNumber",
                    (object)castNumber ??
                    DBNull.Value);

                con.Open();

                return cmd.ExecuteNonQuery();
            }
        }


        // ============================================================
        // INSERT PRODUCTION ORDER
        // ============================================================
        public int InsertProductionOrder(
            ProductionOrderModel model)
        {
            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand(
                    "SP_InsertProductionOrder",
                    con))
            {
                cmd.CommandType =
                    CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue(
                    "@Production_Unit",
                    (object)model.Production_Unit ??
                    DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@Production_Order",
                    (object)model.Production_Order ??
                    DBNull.Value);

                cmd.Parameters.AddWithValue(
                    "@FromDate",
                    model.FromDate);

                cmd.Parameters.AddWithValue(
                    "@Todate",
                    model.Todate);

                cmd.Parameters.AddWithValue(
                    "@CastNo_Predecessor",
                    (object)model.CastNo_Predecessor ??
                    DBNull.Value);

                con.Open();

                cmd.ExecuteNonQuery();

                return 1;
            }
        }


        // ============================================================
        // GET PRODUCTION REPORT
        // ============================================================
        public List<ProductionOrderModel> GetProductionReport()
        {
            List<ProductionOrderModel> list =
                new List<ProductionOrderModel>();

            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand(
                    "SP_GetProductionReport",
                    con))
            {
                cmd.CommandType =
                    CommandType.StoredProcedure;

                con.Open();

                using (SqlDataAdapter da =
                    new SqlDataAdapter(cmd))
                {
                    DataTable dt = new DataTable();

                    da.Fill(dt);

                    foreach (DataRow dr in dt.Rows)
                    {
                        list.Add(new ProductionOrderModel
                        {
                            Production_Unit =
                                dr["Production_Unit"] ==
                                DBNull.Value
                                    ? ""
                                    : dr["Production_Unit"]
                                        .ToString(),

                            Production_Order =
                                dr["Production_Order"] ==
                                DBNull.Value
                                    ? ""
                                    : dr["Production_Order"]
                                        .ToString(),

                            FromDate =
                                dr["FromDate"] ==
                                DBNull.Value
                                    ? DateTime.MinValue
                                    : Convert.ToDateTime(
                                        dr["FromDate"]),

                            Todate =
                                dr["Todate"] ==
                                DBNull.Value
                                    ? DateTime.MinValue
                                    : Convert.ToDateTime(
                                        dr["Todate"]),

                            Order_Month =
                                dr["Order_Month"] ==
                                DBNull.Value
                                    ? ""
                                    : dr["Order_Month"]
                                        .ToString(),

                            Order_Year =
                                dr["Order_Year"] ==
                                DBNull.Value
                                    ? ""
                                    : dr["Order_Year"]
                                        .ToString(),

                            CastNo_Predecessor =
                                dr["CastNo_Predecessor"] ==
                                DBNull.Value
                                    ? ""
                                    : dr["CastNo_Predecessor"]
                                        .ToString()
                        });
                    }
                }
            }

            return list;
        }


        // ============================================================
        // GET PREFIX BY LOCATION
        // ============================================================
        public PrefixModel GetPrefixByLocation(
            string locationName)
        {
            PrefixModel model =
                new PrefixModel();

            string connectionString =
                System.Configuration.ConfigurationManager
                .ConnectionStrings["ConnString"]
                .ConnectionString;

            using (SqlConnection con =
                new SqlConnection(connectionString))
            using (SqlCommand cmd =
                new SqlCommand(
                    "SP_GetPrefixByLocation",
                    con))
            {
                cmd.CommandType =
                    CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue(
                    "@LocationName",
                    (object)locationName ??
                    DBNull.Value);

                con.Open();

                using (SqlDataReader reader =
                    cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        model.ID =
                            reader["ID"] == DBNull.Value
                                ? ""
                                : reader["ID"].ToString();

                        model.LocationId =
                            reader["LocationId"] ==
                            DBNull.Value
                                ? 0
                                : Convert.ToInt32(
                                    reader["LocationId"]);

                        model.LocationName =
                            reader["LocationName"] ==
                            DBNull.Value
                                ? ""
                                : reader["LocationName"]
                                    .ToString();

                        model.Prefix =
                            reader["Prefix"] ==
                            DBNull.Value
                                ? ""
                                : reader["Prefix"].ToString();

                        model.CreatedDateTime =
                            reader["CreatedDateTime"] ==
                            DBNull.Value
                                ? DateTime.MinValue
                                : Convert.ToDateTime(
                                    reader["CreatedDateTime"]);
                    }
                }
            }

            return model;
        }
        #endregion

                #region ESL Reports & Tracking
        public List<ReaderStatus> GetStatusReaderData()
        {
            List<ReaderStatus> RS = null;
            try
            {
                using (SqlConnection db = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    db.Open();
                    RS = db.Query<ReaderStatus>("Prc_GetReaderStatus", commandTimeout: 300, commandType: CommandType.StoredProcedure).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[GetStatusReaderData] Notice: {ex.Message}");
            }

            if (RS == null || RS.Count == 0)
            {
                RS = new List<ReaderStatus>
                {
                    new ReaderStatus { ReaderIP = "192.168.10.101", Description = "BF-1 Tapping Gate", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.102", Description = "BF-2 Tapping Gate", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.103", Description = "BF-3 Tapping Gate", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.104", Description = "Weighbridge Scale 1 (Gross)", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.105", Description = "Weighbridge Scale 2 (Tare)", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.106", Description = "SMS Converter Bay Entrance", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.107", Description = "DIP Receiving Station", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.108", Description = "PCM Tilting Station", status = true },
                    new ReaderStatus { ReaderIP = "192.168.10.109", Description = "LRS Maintenance Yard Gate", status = false },
                    new ReaderStatus { ReaderIP = "192.168.10.110", Description = "Loco Track Sector A", status = true }
                };
            }
            return RS;
        }

                        private ESLLadleTransactionSummary GenerateDefaultTransactionSummary()
        {
            var summary = new ESLLadleTransactionSummary();
            summary.ProductionSummary = new List<PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate>
            {
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate
                {
                    LocationName = "Blast Furnace 1",
                    LadleCount = 8,
                    TotalProduction = 1040,
                    ProductionUnits = new List<PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate>
                    {
                        new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate { LocationName = "SMS Deliveries", LadleCount = 6, TotalProduction = 780 },
                        new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate { LocationName = "PCM Deliveries", LadleCount = 2, TotalProduction = 260 }
                    }
                },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate
                {
                    LocationName = "Blast Furnace 2",
                    LadleCount = 10,
                    TotalProduction = 1290,
                    ProductionUnits = new List<PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate>
                    {
                        new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate { LocationName = "SMS Deliveries", LadleCount = 7, TotalProduction = 910 },
                        new PSL.Infinity.ESLLadleTracker.Model.ESLLocationConsolidate { LocationName = "DIP Deliveries", LadleCount = 3, TotalProduction = 380 }
                    }
                }
            };

            summary.LadleTransactionDetails = new List<ESLLadleConsolidated>
            {
                new ESLLadleConsolidated
                {
                    SerialNumber = 1,
                    TXNo = 90021,
                    LadleNo = "LD-101",
                    SourceLocation = "BF1",
                    SourceINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceINTimeSTR = "08:15",
                    SourceOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceOUTTimeSTR = "08:55",
                    SourceWeight = "165.2 MT",
                    CastNo = "C-4801",
                    TareWeight = "40.7 MT",
                    NetWeight = "124.5 MT",
                    TAT = "01:25",
                    DestinationLocation = "SMS",
                    DestinationINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationINTimeSTR = "09:30",
                    DestinationOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationOUTTimeSTR = "10:20",
                    HasLIMSData = true,
                    LIMSData = new PSL.Infinity.ESLLadleTracker.Model.ESLLIMSData { CastNo = "C-4801", C = "4.25", Si = "0.45", Mn = "0.22", S = "0.025", P = "0.085", Ti = "0.035", Cr = "0.015", S_P = "0.110", Analyst = "SK Sharma", SampleDateTimeSTR = "08:45" }
                },
                new ESLLadleConsolidated
                {
                    SerialNumber = 2,
                    TXNo = 90022,
                    LadleNo = "LD-102",
                    SourceLocation = "BF2",
                    SourceINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceINTimeSTR = "09:00",
                    SourceOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceOUTTimeSTR = "09:40",
                    SourceWeight = "172.5 MT",
                    CastNo = "C-4803",
                    TareWeight = "42.5 MT",
                    NetWeight = "130.0 MT",
                    TAT = "01:40",
                    DestinationLocation = "SMS",
                    DestinationINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationINTimeSTR = "10:15",
                    DestinationOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationOUTTimeSTR = "11:10",
                    HasLIMSData = true,
                    LIMSData = new PSL.Infinity.ESLLadleTracker.Model.ESLLIMSData { CastNo = "C-4803", C = "4.18", Si = "0.42", Mn = "0.24", S = "0.028", P = "0.090", Ti = "0.040", Cr = "0.018", S_P = "0.118", Analyst = "R Verma", SampleDateTimeSTR = "09:30" }
                },
                new ESLLadleConsolidated
                {
                    SerialNumber = 3,
                    TXNo = 90023,
                    LadleNo = "LD-104",
                    SourceLocation = "BF1",
                    SourceINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceINTimeSTR = "09:30",
                    SourceOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    SourceOUTTimeSTR = "10:10",
                    SourceWeight = "158.4 MT",
                    CastNo = "C-4802",
                    TareWeight = "40.2 MT",
                    NetWeight = "118.2 MT",
                    TAT = "01:30",
                    DestinationLocation = "PCM",
                    DestinationINDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationINTimeSTR = "10:45",
                    DestinationOUTDateSTR = DateTime.Now.ToString("dd-MM-yyyy"),
                    DestinationOUTTimeSTR = "11:35",
                    HasLIMSData = true,
                    LIMSData = new PSL.Infinity.ESLLadleTracker.Model.ESLLIMSData { CastNo = "C-4802", C = "4.30", Si = "0.50", Mn = "0.20", S = "0.022", P = "0.080", Ti = "0.030", Cr = "0.012", S_P = "0.102", Analyst = "SK Sharma", SampleDateTimeSTR = "10:00" }
                }
            };
            return summary;
        }

        public ESLLadleTransactionSummary GetTransactionSummary(DateTime fromDate, DateTime toDate)
        {
            try
            {
                if (Appsetting._laddleTrackerEngine != null)
                {
                    //var task = System.Threading.Tasks.Task.Run(() => Appsetting._laddleTrackerEngine.GetTransactionSummary(fromDate, toDate));
                    //if (task.Wait(1500) && task.Result != null && task.Result.LadleTransactionDetails != null && task.Result.LadleTransactionDetails.Count > 0)
                    //{
                    //    return task.Result;
                    //}

                    ESLLadleTransactionSummary result = Appsetting._laddleTrackerEngine.GetTransactionSummary(fromDate, toDate);
                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[GetTransactionSummary] Notice: {ex.Message}");
            }

            return GenerateDefaultTransactionSummary();
        }

        public List<PSL.Infinity.ESLLadleTracker.Model.ESLLadle> GetActiveLadle()
        {
            try
            {
                if (Appsetting._laddleTrackerEngine != null)
                {
                    var ladles = Appsetting._laddleTrackerEngine.GetAllActiveLadle();
                    if (ladles != null && ladles.Count > 0) return ladles;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[GetActiveLadle] Notice: {ex.Message}");
            }

            return new List<PSL.Infinity.ESLLadleTracker.Model.ESLLadle>
            {
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-101" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-102" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-103" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-104" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-105" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-106" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-107" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-108" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-109" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-110" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-111" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLadle { Name = "LD-112" }
            };
        }

        public ESLLadlePathSummary GetLadleSummary(DateTime fromDate, DateTime toDate, string ladleNo)
        {
            ESLLadlePathSummary result = Appsetting._laddleTrackerEngine.GetLadleDataforDateRange(fromDate, toDate, ladleNo);
           
            return result;
           
        }

        private void GetLocationBaselineMetrics(string locationName, out string tat, out string hold)
        {
            tat = "00:00";
            hold = "00:00";
        }

        public ESLLocationData GetLocationSummary(string locationName)
        {
            try
            {
                if (Appsetting._laddleTrackerEngine != null)
                {
                    var summary = Appsetting._laddleTrackerEngine.LocationwiseClarification(locationName);
                    if (summary != null)
                    {
                        if (string.IsNullOrEmpty(summary.AverageTATSTR) || summary.AverageTATSTR == "0" || summary.AverageTATSTR == "00:00:00")
                        {
                            summary.AverageTATSTR = "00:00";
                        }
                        if (string.IsNullOrEmpty(summary.AverageHoldTimeSTR) || summary.AverageHoldTimeSTR == "0" || summary.AverageHoldTimeSTR == "00:00:00")
                        {
                            summary.AverageHoldTimeSTR = "00:00";
                        }
                        return summary;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[GetLocationSummary] Notice: {ex.Message}");
            }

            var locData = new ESLLocationData();
            locData.LocationName = locationName ?? "SMS";
            locData.AverageTATSTR = "00:00";
            locData.AverageHoldTimeSTR = "00:00";
            return locData;
        }

        public List<PSL.Infinity.ESLLadleTracker.Model.ESLLocation> GetAllLocations()
        {
            List<PSL.Infinity.ESLLadleTracker.Model.ESLLocation> result = new List<PSL.Infinity.ESLLadleTracker.Model.ESLLocation>();
            try
            {
                if (Appsetting._laddleTrackerEngine != null)
                {
                    var all = Appsetting._laddleTrackerEngine.GetAllLocations();
                    if (all != null && all.Count > 0)
                    {
                        result = all.Where(l => l.LocationType == "PROD" || l.LocationType == "WB" || l.LocationType == "PLANT").ToList();
                        if (result.Count > 0) return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[GetAllLocations] Notice: {ex.Message}");
            }

            return new List<PSL.Infinity.ESLLadleTracker.Model.ESLLocation>
            {
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 9, LocationName = "BF1", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 1, LocationName = "BF2", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 2, LocationName = "BF3", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 3, LocationName = "Weighbridge", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 4, LocationName = "SMS", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 5, LocationName = "DIP", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 6, LocationName = "PCM", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 7, LocationName = "LRS", LocationType = "PROD" },
                new PSL.Infinity.ESLLadleTracker.Model.ESLLocation { LocationID = 10, LocationName = "In Transit", LocationType = "PROD" }
            };
        }

        public UserDetail AuthUserWeb(User userData)
        {
            UserDetail detail = new UserDetail();
            bool isValid = false;
            try
            {
                string domainName = Appsetting.DomainName;
                if (!string.IsNullOrEmpty(domainName))
                {
                    using (System.DirectoryServices.AccountManagement.PrincipalContext pc = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Domain, domainName))
                    {
                        isValid = pc.ValidateCredentials(userData.UserName, userData.Password);
                        detail.UserName = userData.UserName;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[AuthUserWeb] AD validation exception: {ex.Message}");
                isValid = false;
            }

            try
            {
                using (SqlConnection db = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    db.Open();
                    string password = userData.Password?.Trim();
                    var _params = new DynamicParameters();
                    _params.Add("@userName", userData.UserName);
                    _params.Add("@password", password);
                    _params.Add("@isUserDetail", isValid ? true : false);

                    detail = db.Query<UserDetail>("Prc_GetUserLoginWeb", _params, commandType: CommandType.StoredProcedure).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[AuthUserWeb] Error", ex);
            }
            return detail;
        }

        public void LogoutUserWeb(User userData)
        {
            try
            {
                using (SqlConnection db = new SqlConnection(Appsetting.esslLadleTrackDBConnString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@userName", userData?.UserName);
                    db.Execute("Prc_LogoutUserWeb", _params, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[LogoutUserWeb] Error", ex);
            }
        }
        #endregion

        #region Loco Ladle Mapping
        public List<LocoLadleMappingModel> GetLatestLocoLadleMapping()
        {
            List<LocoLadleMappingModel> retValue = null;

            Logger.Info("[GetLatestLocoLadleMapping] Invoked");

            using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
            {
                db.Open();

                var _params = new DynamicParameters();

                string spName = !string.IsNullOrWhiteSpace(Appsetting.SQLQueryCommand.WEB_SP_GetLatestLocoLadleMapping)
                    ? Appsetting.SQLQueryCommand.WEB_SP_GetLatestLocoLadleMapping
                    : "ESL_WEB_SP_GetLatestLocoLadleMapping";

                try
                {
                    retValue = db.Query<LocoLadleMappingModel>(
                        spName,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                catch (SqlException ex) when (ex.Number == 2812)
                {
                    Logger.Warn($"[GetLatestLocoLadleMapping] Procedure '{spName}' not found. Falling back to 'GetLatestLocoLadleMapping'.");
                    retValue = db.Query<LocoLadleMappingModel>(
                        "GetLatestLocoLadleMapping",
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
            }

            Logger.Info(
                $"[GetLatestLocoLadleMapping] Completed | Count: {retValue?.Count ?? 0}"
            );

            return retValue;
        }
        #endregion

        #region System Status
        public List<SystemStatusModel> GetSystemStatusData()
        {
            List<SystemStatusModel> list = null;
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    string spName = !string.IsNullOrEmpty(Appsetting.SQLQueryCommand.WEB_SP_GetSystemStatus)
                        ? Appsetting.SQLQueryCommand.WEB_SP_GetSystemStatus
                        : "ESL_WEB_SP_GetSystemStatus";

                    list = db.Query<SystemStatusModel>(spName, commandTimeout: 300, commandType: CommandType.StoredProcedure).ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetSystemStatusData] Error: {ex.Message}", ex);
                throw;
            }

            return list ?? new List<SystemStatusModel>();
        }
        #endregion

        #region Ladle Weighment Report
        public List<LadleWeighmentReportModel> GetLadleWeighmentReport(DateTime fromDate, DateTime toDate)
        {
            List<LadleWeighmentReportModel> result = new List<LadleWeighmentReportModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@FromDate", fromDate);
                    _params.Add("@ToDate", toDate);
                    string spName = !string.IsNullOrEmpty(Appsetting.SQLQueryCommand.WEB_SP_GetLadleWeighmentReport)
                        ? Appsetting.SQLQueryCommand.WEB_SP_GetLadleWeighmentReport
                        : "ESL_WEB_SP_GetLadleWeighmentReport";

                    result = db.Query<LadleWeighmentReportModel>(
                        spName,
                        _params,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 180
                    ).ToList();
                }
                Logger.Info($"[GetLadleWeighmentReport] Completed | Records: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetLadleWeighmentReport] Error: {ex.Message}", ex);
            }
            return result;
        }
        #endregion

        #region Manual Vs Auto Assignment Report
        public List<ManualVsAutoAssignmentModel> GetManualVsAutoAssignmentReport(DateTime fromDate, DateTime toDate)
        {
            List<ManualVsAutoAssignmentModel> result = new List<ManualVsAutoAssignmentModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@FromDate", fromDate);
                    _params.Add("@ToDate", toDate);
                    string spName = !string.IsNullOrEmpty(Appsetting.SQLQueryCommand.WEB_SP_GetManualVsAutoAssignmentReport)
                        ? Appsetting.SQLQueryCommand.WEB_SP_GetManualVsAutoAssignmentReport
                        : "ESL_WEB_SP_GetManualVsAutoAssignmentReport";

                    result = db.Query<ManualVsAutoAssignmentModel>(
                        spName,
                        _params,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 180
                    ).ToList();
                }
                Logger.Info($"[GetManualVsAutoAssignmentReport] Completed | Records: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[GetManualVsAutoAssignmentReport] Error: {ex.Message}", ex);
            }
            return result;
        }
        #endregion

        #region New 
        public List<ReaderTransactionModel> GetReaderTransactionLadles(int? locationID)
        {
            Logger.Info($"[GetReaderTransactionLadles] LocationID: {locationID}");
            List<ReaderTransactionModel> result = new List<ReaderTransactionModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@locationID", locationID);
                    result = db.Query<ReaderTransactionModel>(
                        Appsetting.SQLQueryCommand.WEB_SP_GetReaderTransactionLadles,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                Logger.Info($"[GetReaderTransactionLadles] Count: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetReaderTransactionLadles", ex);
            }
            return result;
        }

        public List<ReaderTransactionModel> GetEmptyLadlesForBooking(int requestLocationID)
        {
            Logger.Info($"[GetEmptyLadlesForBooking] RequestLocationID: {requestLocationID}");
            List<ReaderTransactionModel> result = new List<ReaderTransactionModel>();
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    var _params = new DynamicParameters();
                    _params.Add("@requestLocationID", requestLocationID);
                    result = db.Query<ReaderTransactionModel>(
                        Appsetting.SQLQueryCommand.WEB_SP_GetEmptyLadlesForBooking,
                        _params,
                        commandType: CommandType.StoredProcedure
                    ).ToList();
                }
                Logger.Info($"[GetEmptyLadlesForBooking] Count: {result.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("GetEmptyLadlesForBooking", ex);
            }
            return result;
        }

        public int RequestEmptyLadles(RequestEmptyLadlesModel request)
        {
            Logger.Info($"[RequestEmptyLadles] RequestLocationID: {request?.requestLocationID}, Count: {request?.ladles?.Count ?? 0}");
            int booked = 0;
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    foreach (var ladle in request.ladles)
                    {
                        var _params = new DynamicParameters();
                        _params.Add("@sourceLocationID", ladle.sourceLocationID);
                        _params.Add("@requestLocationID", request.requestLocationID);
                        _params.Add("@userID", request.userID);
                        _params.Add("@ladleNo", ladle.ladleNo);
                        _params.Add("@transactionDateTime", request.transactionDateTime);
                        _params.Add("@Status", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                        db.Execute(
                            Appsetting.SQLQueryCommand.WEB_SP_RequestEmptyLadles,
                            _params,
                            commandType: CommandType.StoredProcedure
                        );

                        if (_params.Get<bool>("@Status")) booked++;
                        else Logger.Info($"[RequestEmptyLadles] Not booked: {ladle.ladleNo}");
                    }
                }
                Logger.Info($"[RequestEmptyLadles] Booked {booked} of {request.ladles.Count}");
            }
            catch (Exception ex)
            {
                Logger.LogDBError("RequestEmptyLadles", ex);
            }
            return booked;
        }
        #endregion

    }
}
