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
    }
}
