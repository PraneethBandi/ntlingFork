using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Netling.Core.Models
{
    public static class MultipleRequestsStore
    {
        public static List<Tuple<string, string, string>> RequestsList { get; set; }
        public static List<HttpRequestMessage> HttpRequestMessageList { get; set; }
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
        public static HttpRequestMessage GetHttpRequestMessage()
        {
            lock (syncLock)
            {
                var item = HttpRequestMessageList[counter];
                counter = (counter == (HttpRequestMessageList.Count - 1)) ? 0 : counter + 1;
                return cloneRequest(item);
            }
        }

        public static HttpRequestMessage cloneRequest(HttpRequestMessage input)
        {
            HttpRequestMessage request = new HttpRequestMessage(input.Method, input.RequestUri);
            foreach (var item in input.Headers.ToDictionary(x => x.Key, v => v.Value.FirstOrDefault()))
            {
                request.Headers.Add(item.Key, item.Value);
            }
            if (input.Content != null)
                request.Content = input.Content;

            return request;
        }
    }
}
