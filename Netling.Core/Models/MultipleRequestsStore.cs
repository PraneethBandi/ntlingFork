using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netling.Core.Models
{
    public static class MultipleRequestsStore
    {
        public static List<Tuple<string, string, string>> RequestsList { get; set; }
        private static int counter = 0;
        private static object syncLock = new object();
        public static Tuple<string, string, string> Get()
        {
            lock (syncLock)
            {
                var item = RequestsList[counter];
                counter = (counter == (RequestsList.Count - 1)) ? 0 : counter + 1;
                return item;
            }
        }
    }
}
