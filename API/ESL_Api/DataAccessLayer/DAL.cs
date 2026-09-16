using Dapper;
using ESL_Api.IDataAccessLayer;
using ESL_Api.Global;
using ESL_Api.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace ESL_Api.DataAccessLayer
{
    public class DAL : IDAL
    {
        //public List<DashboardData> DashboardShowData()
        //{
        //    List<DashboardData> data = null;
        //    using (IDbConnection db = new SqlConnection(Appsetting.ConnectionString))
        //    {
        //        db.Open();
        //        var _params = new DynamicParameters();
        //        data = db.Query<DashboardData>(Appsetting.SQLQueryCommand.GetTripDetails, _params, commandType: CommandType.StoredProcedure, commandTimeout: 180).ToList();
        //    }
        //    return data;
        //}
    }
}