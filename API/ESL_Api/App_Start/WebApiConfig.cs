using ESL_Api.DataAccessLayer;
using ESL_Api.IDataAccessLayer;
using System.Web.Http;
using System.Web.Http.Cors;
using Unity;
using Unity.Lifetime;
using Unity.WebApi;
using ESL_Api.Global;

namespace ESL_Api
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            var container = new UnityContainer();
            container.RegisterType<IDAL, DAL>(new TransientLifetimeManager());
            container.RegisterType<IPDADAL, PDADAL>(new TransientLifetimeManager());
            config.DependencyResolver = new UnityDependencyResolver(container);

            config.Filters.Add(new JwtAuthFilter());
            config.Filters.Add(new RateLimitFilter());

            string allowedOrigins = System.Configuration.ConfigurationManager.AppSettings["CorsAllowedOrigins"];
            if (string.IsNullOrWhiteSpace(allowedOrigins))
            {
                allowedOrigins = "*";
            }

            var cors = new EnableCorsAttribute(
                origins: allowedOrigins,
                headers: "*",
                methods: "*"
            );
            config.EnableCors(cors);

            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;

            // Security Hardening: Never disclose server stack traces or internal exception details to callers
            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Never;
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
