using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Netling.Core.Utils
{
    public static class HttpHelper
    {
        private static HttpClient httpClient = new HttpClient();

        public static async Task<string> Send(HttpRequestMessage request)
        {
            try
            {
                var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                return "sent sucessfully";
            }
            catch(Exception ex)
            {
                return ex.ToString();
            }
        }

        public static async Task<string> Send<T>(string uri, T data)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync<T>(uri, data).ConfigureAwait(false);
                return "sent sucessfully";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}
