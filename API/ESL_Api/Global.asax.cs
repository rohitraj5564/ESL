using ESL_Api.Global;
using log4net;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Dapper;

namespace ESL_Api
{
    public class WebApiApplication : System.Web.HttpApplication
    { 
        private static readonly ILog Log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected void Application_Start()
        {
            string configPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "log4net.config"
            );
            log4net.Config.XmlConfigurator.Configure(
                new System.IO.FileInfo(configPath)
            );

            Logger.Info("=== ESL API Starting ===");
            Logger.Info($"[log4net] Config loaded from: {configPath}");
            Appsetting.loadAppsetting();
            
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            GlobalConfiguration.Configuration.MessageHandlers
                .Add(new RemoveServerHeaderHandler());
            MvcHandler.DisableMvcResponseHeader = true;
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            WarmUpApplication();
            Logger.Info("=== ESL API Started Successfully ===");
        }

        private void WarmUpApplication()
        {
            try
            {
                WarmUpDatabase();
                WarmUpJwt();
                Logger.Info("WarmUp completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("WarmUp failed", ex);
            }
        }

        private void WarmUpDatabase()
        {
            try
            {
                using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
                {
                    db.Open();
                    db.Execute("SELECT 1");
                    Logger.Info("Database WarmUp successful");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Database WarmUp failed", ex);
            }
        }

        private void WarmUpJwt()
        {
            try
            {
                JwtHelper.GenerateToken("warmup", "warmup", 0, "warmup");
                Logger.Info("JWT WarmUp successful");
            }
            catch (Exception ex)
            {
                Logger.Error("JWT WarmUp failed", ex);
            }
        }

        protected void Application_End(object sender, EventArgs e)
        {
            Logger.Info("=== ESL API Stopping ===");
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "OPTIONS")
            {
                Response.Clear();
                string origin = Request.Headers["Origin"] ?? "*";
                Response.AddHeader("Access-Control-Allow-Origin", origin);
                Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Version, Accept, Origin, X-Requested-With");
                Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                Response.AddHeader("Access-Control-Max-Age", "86400");
                Response.StatusCode = 200;
                Response.End();
            }
        }

        protected void Application_PreSendRequestHeaders()
        {
            try
            {
                Response.Headers.Remove("Server");
                Response.Headers.Remove("X-Powered-By");
                Response.Headers.Remove("X-AspNet-Version");
                Response.Headers.Remove("X-AspNetMvc-Version");
            }
            catch
            {
                // Ignore if headers are already committed
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                Logger.Fatal("Unhandled Application Error", ex);
            }
        }
    }
}