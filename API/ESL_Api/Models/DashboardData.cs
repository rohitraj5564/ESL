using PSL.Infinity.ESLLadleTracker.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ESL_Api.Models
{
    public class DashboardData
    {
        public List<ESLLocation> ESLLocation { get; set; }
        public List<ESLLadle> ESLLadle { get; set; }
    }

    public class ReaderStatus
    {
        public string ReaderIP { get; set; }
        public bool status { get; set; }
        public string Description { get; set; }
    }

    public class SystemStatusModel
    {
        public Guid ID { get; set; }
        public string LocationName { get; set; }
        public string ReaderIP { get; set; }
        public DateTime? ReaderModifiedDatetime { get; set; }
        public string Controller_1IP { get; set; }
        public DateTime? Controller1ModifiedDatetime { get; set; }
        public string Controller_2IP { get; set; }
        public DateTime? Controller2ModifiedDatetime { get; set; }
        public string CameraIP { get; set; }
        public DateTime? CameraModifiedDatetime { get; set; }
        public bool? ApplicationHealth { get; set; }
        public DateTime? ApplicationModifiedDatetime { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ServerDateTime { get; set; }
    }

    public class TransactionInput
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string LadleNo { get; set; }
    }

    public class UserDetail
    {
        public int ID { get; set; }
        public string UserName { get; set; }
        public string CustomerName { get; set; }
        public string Password { get; set; }
        public Nullable<System.Guid> CustomerID { get; set; }
        public string RoleName { get; set; }
    }

    public class User
    {
        public int UserID { get; set; }
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public bool IsActive { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Nullable<System.Guid> CustomerID { get; set; }
    }
}
