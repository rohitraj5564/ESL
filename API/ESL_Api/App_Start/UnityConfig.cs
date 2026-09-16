using ESL_Api.DataAccessLayer;
using ESL_Api.IDataAccessLayer;
using System.Web.Http;
using Unity;
using Unity.WebApi;

namespace ESL_Api
{
    public static class UnityConfig
    {
        public static void RegisterComponents()
        {
			var container = new UnityContainer();

            container.RegisterType<IPDADAL, PDADAL>();

            // register all your components with the container here
            // it is NOT necessary to register your controllers

            // e.g. container.RegisterType<ITestService, TestService>();

            GlobalConfiguration.Configuration.DependencyResolver = new UnityDependencyResolver(container);
        }
    }
}