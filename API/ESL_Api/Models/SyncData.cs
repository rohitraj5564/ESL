using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ESL_Api.Models
{
    public class SyncData
    {
        public List<LocationDetail> locationDetails { get; set; }
        public List<AssetDetail> assetDetails { get; set; }
    }
    public class LocationDetail
    {
        public int locationID { get; set; }
        public string locationName { get; set; }
    }
    public class AssetDetail
    {
        public string aName { get; set; }
        public string aTagID { get; set; }
        public string aTypeID { get; set; }
    }
}