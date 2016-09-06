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
        private static HttpClientHandler handler = new HttpClientHandler() { UseCookies = false };
        private static HttpClient httpClient = new HttpClient(handler, false);
        
        public static async Task<string> Send(HttpRequestMessage request)
        {
            try
            {
                var response = await httpClient.SendAsync(request).ConfigureAwait(false);
                var respString = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return $"{response.StatusCode.ToString()}|{respString.Length}";
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
