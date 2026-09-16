using System.Web.Http;
using System.Web.Mvc;

namespace ESL_Api.Areas.HelpPage
{
    public class HelpPageAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "HelpPage";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            // Disabled HelpPage area to prevent endpoint enumeration in VAPT compliance
        }
    }
}