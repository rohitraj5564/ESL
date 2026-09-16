using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace ESL_Api.Global
{
    public class JwtAuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var actionName = actionContext.ActionDescriptor.ActionName;

            // Only allow unauthenticated access to login, activation, and session termination endpoints
            if (actionName == "DashboardLoginData" ||
                actionName == "PDALoginData" ||
                actionName == "GetActivationKey" ||
                actionName == "AuthUser" ||
                actionName == "EndUserSession" ||
                actionName == "DashboardLogout" ||
                actionName == "PDALogout")
            {
                base.OnActionExecuting(actionContext);
                return;
            }

            var authHeader = actionContext.Request.Headers.Authorization;
            if (authHeader == null || authHeader.Scheme != "Bearer" ||
                string.IsNullOrEmpty(authHeader.Parameter))
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    new { status = false, message = "Unauthorized. Bearer token is missing or invalid." });
                return;
            }

            var principal = JwtHelper.ValidateToken(authHeader.Parameter);
            if (principal == null)
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    new { status = false, message = "Invalid or expired token." });
                return;
            }

            // Bind principal to current context and thread for downstream role/identity checks
            if (HttpContext.Current != null)
            {
                HttpContext.Current.User = principal;
            }
            System.Threading.Thread.CurrentPrincipal = principal;
            actionContext.RequestContext.Principal = principal;

            // Single active session enforcement:
            // If the token contains a sessionToken, ensure it is still the currently active session in DB
            var tokenSessionId = principal.Claims.FirstOrDefault(c => c.Type == "sessionToken")?.Value;
            var tokenUserId = principal.Claims.FirstOrDefault(c => c.Type == "userID")?.Value;
            var tokenUserName = principal.Claims.FirstOrDefault(c => c.Type == "userName")?.Value;

            if (!string.IsNullOrEmpty(tokenSessionId) && (!string.IsNullOrEmpty(tokenUserId) || !string.IsNullOrEmpty(tokenUserName)))
            {
                if (!IsSessionActive(tokenUserId, tokenUserName, tokenSessionId))
                {
                    actionContext.Response = actionContext.Request.CreateResponse(
                        HttpStatusCode.Unauthorized,
                        new
                        {
                            status = false,
                            isSessionTerminated = true,
                            message = "Your session was terminated because this account was logged in from another browser or device."
                        });
                    return;
                }
            }

            var apiVersion = actionContext.Request.Headers.Contains("X-Api-Version")
                ? actionContext.Request.Headers.GetValues("X-Api-Version").FirstOrDefault()
                : null;

            if (string.IsNullOrEmpty(apiVersion) || apiVersion != "1.0")
            {
                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    new { status = false, message = "Invalid or missing API version header (X-Api-Version: 1.0 required)." });
                return;
            }

            base.OnActionExecuting(actionContext);
        }

        private static bool IsSessionActive(string userId, string userName, string sessionToken)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(Appsetting.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP 1 [SessionToken], [IsLogin] 
                            FROM [dbo].[Users] WITH (NOLOCK) 
                            WHERE (@UserID IS NOT NULL AND [UserID] = @UserID)
                               OR (@UserName IS NOT NULL AND [UserName] = @UserName)";
                        cmd.Parameters.AddWithValue("@UserID", (object)userId ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserName", (object)userName ?? System.DBNull.Value);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string activeToken = reader["SessionToken"] != System.DBNull.Value ? reader["SessionToken"].ToString() : null;
                                bool isLogin = reader["IsLogin"] != System.DBNull.Value && System.Convert.ToBoolean(reader["IsLogin"]);

                                return isLogin && string.Equals(activeToken, sessionToken, System.StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[JwtAuthFilter] Session check failed | User: {userName ?? userId}", ex);
            }

            return true;
        }
    }
}
