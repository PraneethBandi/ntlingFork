using System.Linq;
using Netling.Core.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netling.Core.Models
{
    internal class CombinedWorkerThreadResult
    {
        public Dictionary<int, Second> Seconds { get; private set; }
        public List<List<float>> ResponseTimes { get; private set; }
        public TimeSpan Elapsed { get; private set; }

        public CombinedWorkerThreadResult(ConcurrentQueue<WorkerThreadResult> results, TimeSpan elapsed)
        {
            Seconds = new Dictionary<int, Second>();
            ResponseTimes = new List<List<float>>();
            Elapsed = elapsed;

            foreach (var result in results)
            {
                foreach (var second in result.Seconds)
                {
                    ResponseTimes.Add(second.Value.ResponseTimes);
                    second.Value.ClearResponseTimes();
                    
                    if (Seconds.ContainsKey(second.Key))
                        Seconds[second.Key].AddMerged(second.Value);
                    else
                        Seconds.Add(second.Key, second.Value);
                }
            }
        }
    }

    public class CombinedEndpointResult
    {
        public ConcurrentQueue<EndpointResult> EndpointResults { get; set; }
        public TimeSpan Elapsed { get; set; }

        public async Task<string> processResults(string uri, string runName, string runId)
        {
            try
            {
                EndpointResult[] data = EndpointResults.ToArray();
                foreach (var item in data)
                {
                    item.id = runId;
                    item.runname = runName;
                }

                List<Task<string>> tasks = new List<Task<string>>();

                for (var i = 0; i < data.Length; i += 50)
                {
                    var group = data.Skip(i).Take(50).ToArray();
                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    payload.Add("data", group);
                    var task = HttpHelper.Send($"{uri}/{runName}", payload);
                    tasks.Add(task);
                }
                await Task.WhenAll(tasks);
                return "service times sent";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }

    public class RunModel
    {
        public string id { get; set; }
        public string name { get; set; }
        public DateTime starttime { get; set; }
        public double ellapsed { get; set; }
        public string metadata { get; set; }
    }
}