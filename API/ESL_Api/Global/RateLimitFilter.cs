using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace ESL_Api.Global
{
    public class RateLimitFilter : ActionFilterAttribute
    {
        private static readonly int MaxGeneralRequests = 300;
        private static readonly int MaxAuthRequests = 10;
        private static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(1);

        private static readonly ConcurrentDictionary<string, RequestInfo> GeneralRequestCounts
            = new ConcurrentDictionary<string, RequestInfo>();

        private static readonly ConcurrentDictionary<string, RequestInfo> AuthRequestCounts
            = new ConcurrentDictionary<string, RequestInfo>();

        private static int _requestCounter = 0;

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            string clientIP = GetClientIP(actionContext);
            var now = DateTime.UtcNow;
            var actionName = actionContext.ActionDescriptor.ActionName;

            bool isAuthAction = actionName == "DashboardLoginData" ||
                                actionName == "PDALoginData" ||
                                actionName == "Login";

            var targetDictionary = isAuthAction ? AuthRequestCounts : GeneralRequestCounts;
            int limit = isAuthAction ? MaxAuthRequests : MaxGeneralRequests;

            var requestInfo = targetDictionary.AddOrUpdate(
                clientIP,
                new RequestInfo { Count = 1, WindowStart = now },
                (key, existing) =>
                {
                    if (now - existing.WindowStart > TimeWindow)
                    {
                        existing.Count = 1;
                        existing.WindowStart = now;
                    }
                    else
                    {
                        existing.Count++;
                    }
                    return existing;
                }
            );

            if (++_requestCounter % 50 == 0)
            {
                CleanupOldEntries(GeneralRequestCounts, now);
                CleanupOldEntries(AuthRequestCounts, now);
            }

            if (requestInfo.Count > limit)
            {
                var response = actionContext.Request.CreateResponse(
                    (HttpStatusCode)429,
                    new
                    {
                        status = false,
                        message = isAuthAction
                            ? "Too many failed login attempts. Please wait 1 minute before trying again."
                            : "Rate limit exceeded. Too many requests. Please try again later."
                    }
                );

                response.Headers.Add("Retry-After", "60");
                actionContext.Response = response;
                return;
            }

            base.OnActionExecuting(actionContext);
        }

        private void CleanupOldEntries(ConcurrentDictionary<string, RequestInfo> dict, DateTime now)
        {
            foreach (var key in dict.Keys)
            {
                if (dict.TryGetValue(key, out var info))
                {
                    if (now - info.WindowStart > TimeWindow)
                    {
                        dict.TryRemove(key, out _);
                    }
                }
            }
        }

        private string GetClientIP(HttpActionContext actionContext)
        {
            if (actionContext.Request.Properties.ContainsKey("MS_HttpContext"))
            {
                var ctx = actionContext.Request.Properties["MS_HttpContext"]
                    as System.Web.HttpContextWrapper;
                if (ctx != null)
                {
                    // Check for X-Forwarded-For if behind reverse proxy
                    string forwarded = ctx.Request.Headers["X-Forwarded-For"];
                    if (!string.IsNullOrWhiteSpace(forwarded))
                    {
                        var ips = forwarded.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (ips.Length > 0)
                            return ips[0].Trim();
                    }
                    return ctx.Request.UserHostAddress;
                }
            }
            return "unknown";
        }
    }
    public class RequestInfo
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}