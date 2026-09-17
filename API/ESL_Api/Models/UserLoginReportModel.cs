using System;
using System.Collections.Generic;

namespace ESL_Api.Models
{
    public class UserLoginSummaryItem
    {
        public string Username { get; set; }
        public string Name { get; set; }
        public int TotalLogins { get; set; }
        public int TotalDurationSeconds { get; set; }
        public string TotalDurationFormatted { get; set; }
        public DateTime? LastLoginDateTime { get; set; }
        public bool IsCurrentlyOnline { get; set; }
    }

    public class UserLoginDetailItem
    {
        public long Id { get; set; }
        public string UserID { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public DateTime LoginDateTime { get; set; }
        public DateTime? LogoutDateTime { get; set; }
        public string TotalDuration { get; set; }
        public int? TotalDurationSeconds { get; set; }
        public string LogoutReason { get; set; }
        public bool IsLoggedIn { get; set; }
        public DateTime ServerDateTime { get; set; }
    }

    public class UserLoginReportData
    {
        public List<UserLoginSummaryItem> summary { get; set; } = new List<UserLoginSummaryItem>();
        public List<UserLoginDetailItem> details { get; set; } = new List<UserLoginDetailItem>();
    }

    public class UserOptionItem
    {
        public string Username { get; set; }
        public string Name { get; set; }
    }
}
