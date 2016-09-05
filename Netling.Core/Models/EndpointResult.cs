using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netling.Core.Models
{
    public class EndpointResult
    {
        public string Request { get; set; }
        public string Body { get; set; }
        public float elapsed { get; set; }
        public bool Error { get; set; }
        public long ResponseLength { get; set; }
        public string Uri { get; set; }
        public int StatusCode { get; set; }
        public string Exception { get; set; }
        public long TimeStampTicks { get; set; }
    }
}
