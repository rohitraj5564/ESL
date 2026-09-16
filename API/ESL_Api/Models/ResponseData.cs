using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class ResponseData
    {
        public bool status { get; set; }
        public string message { get; set; }
        public object data { get; set; }
    }
    public class Response
    {
        public bool status { get; set; }
        public string message { get; set; }
    }
}