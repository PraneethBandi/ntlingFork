using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netling.Core.Models
{
    public class EndpointResult
    {
        public string request { get; set; }
        public string body { get; set; }
        public float elapsed { get; set; }
        public bool error { get; set; }
        public long responselength { get; set; }
        public string uri { get; set; }
        public int statuscode { get; set; }
        public string exception { get; set; }
        public DateTime starttime { get; set; }
        public string id { get; set; }

    }
}
