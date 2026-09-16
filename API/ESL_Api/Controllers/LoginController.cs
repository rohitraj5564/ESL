using ESL_Api.IDataAccessLayer;
using ESL_Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace ESL_Api.Controllers
{
    [RoutePrefix("PSL")]
    public class LoginController : ApiController
    {
        private readonly IDAL PSLDAL;

        public LoginController(IDAL PSLDAL)
        {
            this.PSLDAL = PSLDAL;
        }

        //[HttpGet, Route("DashboardShowData")]
        //public IHttpActionResult DashboardShowData()
        //{
        //    ResponseData responseData = new ResponseData();
        //    List<DashboardData> data = null;
        //    try
        //    {
        //        data = PSLDAL.DashboardShowData();
        //        responseData.status = true;
        //        responseData.message = "Fetched Successfully";
        //        responseData.data = data;
        //    }
        //    catch (Exception ex)
        //    {
        //        responseData.status = false;
        //        responseData.message = ex.Message;
        //        responseData.data = null;
        //    }
        //    return Ok(responseData);
        //}
    }
}