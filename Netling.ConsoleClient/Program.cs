﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NDesk.Options;
using Netling.Core;
using Netling.Core.Models;
using System.IO;
using System.Text;
using Netling.Core.Utils;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net;

namespace Netling.ConsoleClient
{
    class Program
    {
        private static string _runName = string.Empty;
        private static string _runId = string.Empty;
        private static DateTime _startTime = DateTime.MinValue;

        static void Main(string[] args)
        {
            //sendTestApiCall();
            //return;
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en");

            var threads = 1;
            var pipelining = 1;
            var duration = 10;
            int? count = null;
            var requestFile = string.Empty;
            //var runName = string.Empty;

            var p = new OptionSet()
            {
                {"t|threads=", (int v) => threads = v},
                {"p|pipelining=", (int v) => pipelining = v},
                {"d|duration=", (int v) => duration = v},
                {"c|count=", (int? v) => count = v},
                {"r|requestFile=", (string v) => requestFile = v},
                {"n|runname=", (string v) => _runName = v}
            };

            var extraArgs = p.Parse(args);
            var threadAffinity = extraArgs.Contains("-a");

            if(string.IsNullOrEmpty(_runName) || string.IsNullOrEmpty(requestFile))
            {
                ShowHelp();
                return;
            }

            if (extraArgs.Contains("-h"))
            {
                var requestsList = FiddlerTraceHelper.GetRequestMessages(requestFile);
                if (requestsList.Any())
                {
                    if (count.HasValue)
                        RunWithHttpClient(requestsList, 1, threadAffinity, TimeSpan.MaxValue, count).Wait();
                    else
                        RunWithHttpClient(requestsList, 1, threadAffinity, TimeSpan.FromSeconds(duration), count).Wait();
                }
                else
                {
                    Console.WriteLine("no requests to process");
                }
                return;
            }

            if (requestFile.EndsWith(".txt"))
            {
                var url = extraArgs.FirstOrDefault(e => e.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
                Uri uri = null;

                string request = string.Empty;
                string requestBody = string.Empty;
                TryReadRawRequest(requestFile, out request, out requestBody);

                if (url != null && !Uri.TryCreate(url, UriKind.Absolute, out uri))
                    Console.WriteLine("Failed to parse URL");
                else if (url != null && count.HasValue)
                    RunWithCount(uri, count.Value, request, requestBody).Wait();
                else if (url != null)
                    RunWithDuration(uri, threads, threadAffinity, pipelining, TimeSpan.FromSeconds(duration), request, requestBody).Wait();
                else
                    ShowHelp();
            }
            else if(requestFile.EndsWith(".saz"))
            {
                var requestsList = FiddlerTraceHelper.GetRequests(requestFile);
                if (!requestsList.Any())
                {
                    Console.WriteLine("No requests in archive path");
                    ShowHelp();
                }
                else if (count.HasValue)
                    RunMultipleWithCount(requestsList, count.Value).Wait();
                else
                    RunMultipleWithDuration(requestsList, threads, threadAffinity, pipelining, TimeSpan.FromSeconds(duration)).Wait();
            }
        }

        private static bool TryReadRawRequest(string file, out string request, out string body)
        {
            request = string.Empty;
            body = string.Empty;
            bool bodyBegins = false;

            StringBuilder sbRequest = new StringBuilder();
            StringBuilder sbBody = new StringBuilder();

            try
            {
                var lines = File.ReadAllLines(file);
                if(lines.Length > 0)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Trim() == string.Empty)
                        {
                            if (bodyBegins) break;
                            sbRequest.AppendLine(lines[i]);
                            bodyBegins = true;
                            continue;
                        }
                        if (bodyBegins)
                        {
                            sbBody.AppendLine(lines[i]);
                        }
                        else
                        {
                            sbRequest.AppendLine(lines[i]);
                        }
                    }
                    request = sbRequest.ToString();
                    body = sbBody.ToString();
                    return true;
                }
                return false;
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to read request raw data");
                Console.WriteLine("Exception-{0}", ex);
                return false;
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine(HelpString);
        }

        private static Task RunWithCount(Uri uri, int count, string requestString = "", string body = "")
        {
            Console.WriteLine(StartRunWithCountString, count, uri);
            return Run(uri, 1, false, 1, TimeSpan.MaxValue, count, requestString, body);
        }

        private static Task RunWithDuration(Uri uri, int threads, bool threadAffinity, int pipelining, TimeSpan duration, string requestString = "", string body = "")
        {
            Console.WriteLine(StartRunWithDurationString, duration.TotalSeconds, uri, threads, pipelining, threadAffinity ? "ON" : "OFF");
            return Run(uri, threads, threadAffinity, pipelining, duration, null, requestString, body);
        }

        private static Task RunMultipleWithCount(List<Tuple<string, string, string>> requestsList, int count)
        {
            Console.WriteLine(StartRunWithCountString, count, "multipleRequests");
            return Run(requestsList, 1, false, 1, TimeSpan.MaxValue, count);
        }

        private static Task RunMultipleWithDuration(List<Tuple<string, string, string>> requestsList, int threads, bool threadAffinity, int pipelining, TimeSpan duration, string requestString = "", string body = "")
        {
            Console.WriteLine(StartRunWithDurationString, duration.TotalSeconds, "multipleRequests", threads, pipelining, threadAffinity ? "ON" : "OFF");
            return Run(requestsList, threads, threadAffinity, pipelining, duration, null);
        }

        private static async Task Run(List<Tuple<string, string, string>> requestsList, int threads, bool threadAffinity, int pipelining, TimeSpan duration, int? count)
        {
            CombinedEndpointResult result;
            _startTime = DateTime.Now;

            result = await Worker.Run(requestsList, threadAffinity, pipelining, duration, count, new CancellationToken());
            await SendRunInfo(result.Elapsed.TotalSeconds);
            Console.WriteLine("Run completed.pused run info to server");

            Console.WriteLine("sending service results...");
            var serverUri = ConfigurationManager.AppSettings["BaseUri"] + ConfigurationManager.AppSettings["serviceResultsEndpoint"];
            string response = await result.processResults(serverUri, _runName, _runId);
            Console.WriteLine(response);

            Console.WriteLine("Run completed.Check push url for resulsts");
        }

        private static async Task RunWithHttpClient(List<HttpRequestMessage> requestsList, int threads, bool threadAffinity, TimeSpan duration, int? count)
        {
            CombinedEndpointResult result;
            _startTime = DateTime.Now;

            result = await Worker.Run(requestsList, threadAffinity, duration, count, new CancellationToken());
            await SendRunInfo(result.Elapsed.TotalSeconds);
            Console.WriteLine("Run completed.pused run info to server");

            Console.WriteLine("sending service results...");
            var serverUri = ConfigurationManager.AppSettings["BaseUri"] + ConfigurationManager.AppSettings["serviceResultsEndpoint"];
            string response = await result.processResults(serverUri, _runName, _runId);
            Console.WriteLine(response);

            Console.WriteLine("Run completed.Check push url for resulsts");
        }

        private async static void sendTestApiCall()
        {
            var serverUri = ConfigurationManager.AppSettings["BaseUri"] + ConfigurationManager.AppSettings["serviceResultsEndpoint"];
            string response = await HttpHelper.Send(serverUri, "sasdasd");
        }

        private static async Task Run(Uri uri, int threads, bool threadAffinity, int pipelining, TimeSpan duration, int? count, string requestString, string body)
        {
            WorkerResult result;

            if (count.HasValue)
                result = await Worker.Run(uri, count.Value, new CancellationToken(), requestString, body);
            else
                result = await Worker.Run(uri, threads, threadAffinity, pipelining, duration, new CancellationToken(), requestString, body);

            Console.WriteLine(ResultString, 
                result.Count,
                result.Elapsed.TotalSeconds,
                result.RequestsPerSecond, 
                result.Bandwidth, 
                result.Errors,
                result.Median,
                result.StdDev,
                result.Min,
                result.Max,
                GetAsciiHistogram(result));
        }

        private static async Task SendRunInfo(double ellapsed)
        {
            RunModel run = new RunModel()
            {
                id = $"{_runName}_{DateTime.Now.ToFileTime()}",
                name = _runName,
                ellapsed = ellapsed,
                metadata = "Test String... please update with correct run metadata details like env details, etc...",
                starttime = _startTime
            };

            _runId = run.id;
            var serverUri = ConfigurationManager.AppSettings["BaseUri"] + ConfigurationManager.AppSettings["RunEndpoint"];
            var result = await HttpHelper.Send(serverUri, run);
            Console.WriteLine(result);
        }

        private static string GetAsciiHistogram(WorkerResult workerResult)
        {
            if (workerResult.Histogram.Length == 0)
                return string.Empty;

            const string filled = "█";
            const string empty = " ";
            var histogramText = new string[7];
            var max = workerResult.Histogram.Max();

            foreach (var t in workerResult.Histogram)
            {
                for (var j = 0; j < histogramText.Length; j++)
                {
                    histogramText[j] += t > max / histogramText.Length * (histogramText.Length - j - 1) ? filled : empty;
                }
            }

            var text = string.Join("\r\n", histogramText);
            var minText = string.Format("{0:0.000} ms ", workerResult.Min);
            var maxText = string.Format(" {0:0.000} ms", workerResult.Max);
            text += "\r\n" + minText + new string('=', workerResult.Histogram.Length - minText.Length - maxText.Length) + maxText;
            return text;
        }

        private const string HelpString = @"
Usage: netling [-t threads] [-d duration] [-p pipelining] [-a] [-h] -r <full file path> -n runName [url]

Options:
    -t count        Number of threads to spawn.
    -d count        Duration of the run in seconds.
    -c count        Amount of requests to send on a single thread.
    -p count        Number of requests to pipeline.
    -a              Use thread affinity on the worker threads.
    -r string       Full path of the text file which contains raw request string that is being load tested. mandatory arg
    -n string       Run name - mandatory arg
    -h              Run with httpclient instead of lowlevel Tcp sockets, some times tcp sockets error out :(.

Examples: 
    netling http://localhost -n searchTest -t 8 -d 60 -r ""c:\temp\requests.txt""
    netling http://localhost -n apitest -c 3000 
    netling http://localhost -n loadtest -h -r ""c:\temp\requests.txt""
";

        private const string StartRunWithCountString = @"
Running {0} test @ {1}";

        private const string StartRunWithDurationString = @"
Running {0}s test @ {1}
    Threads:        {2}
    Pipelining:     {3}
    Thread affinity: {4}";

        private const string ResultString = @"
{0} requests in {1:0.##}s
    Requests/sec:   {2:0}
    Bandwidth:      {3:0} mbit
    Errors:         {4:0}
Latency
    Median:         {5:0.000} ms
    StdDev:         {6:0.000} ms
    Min:            {7:0.000} ms
    Max:            {8:0.000} ms

{9}
";
    }
}
