﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Netling.Core.Models;
using Netling.Core.Performance;
using Netling.Core.Utils;
using System.Text;
using System.Net.Http;

namespace Netling.Core
{
    public static class Worker
    {
        public static Task<WorkerResult> Run(Uri uri, int threads, bool threadAffinity, int pipelining, TimeSpan duration, CancellationToken cancellationToken, string requestString, string body)
        {
            return Run(uri, threads, threadAffinity, pipelining, duration, null, cancellationToken, requestString, body);
        }

        public static Task<WorkerResult> Run(Uri uri, int count, CancellationToken cancellationToken, string requestString, string body)
        {
            return Run(uri, 1, false, 1, TimeSpan.MaxValue, count, cancellationToken, requestString, body);
        }

        private static Task<WorkerResult> Run(Uri uri, int threads, bool threadAffinity, int pipelining, TimeSpan duration, int? count, CancellationToken cancellationToken, string requestString, string body)
        {
            return Task.Run(() =>
            {
                var combinedWorkerThreadResult = QueueWorkerThreads(uri, threads, threadAffinity, pipelining, duration, count, cancellationToken, requestString, body);
                var workerResult = new WorkerResult(uri, threads, threadAffinity, pipelining, combinedWorkerThreadResult.Elapsed, requestString + "\r\n" + body);
                workerResult.Process(combinedWorkerThreadResult);
                return workerResult;
            });
        }

        public static Task<CombinedEndpointResult> Run(List<Tuple<string, string, string>> requestsList, bool threadAffinity, int pipelining, TimeSpan duration, int? count, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var combinedWorkerThreadResult = QueueWorkerThreads(requestsList, threadAffinity, pipelining, duration, count, cancellationToken);
                return combinedWorkerThreadResult;
            });
        }

        public static Task<CombinedEndpointResult> Run(List<HttpRequestMessage> requestsList, bool threadAffinity, TimeSpan duration, int? count, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var combinedWorkerThreadResult = QueueWorkerThreads(requestsList, threadAffinity, duration, count, cancellationToken);
                return combinedWorkerThreadResult;
            });
        }

        private static CombinedWorkerThreadResult QueueWorkerThreads(Uri uri, int threads, bool threadAffinity, int pipelining, TimeSpan duration, int? count, CancellationToken cancellationToken, string requestString, string body)
        {
            var results = new ConcurrentQueue<WorkerThreadResult>();
            var events = new List<ManualResetEventSlim>();
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < threads; i++)
            {
                var resetEvent = new ManualResetEventSlim(false);

                ThreadHelper.QueueThread(i, threadAffinity, (threadIndex) =>
                {
                    DoWork(uri, duration, count, pipelining, results, sw, cancellationToken, resetEvent, threadIndex, requestString, body);
                });

                events.Add(resetEvent);
            }

            for (var i = 0; i < events.Count; i += 50)
            {
                var group = events.Skip(i).Take(50).Select(r => r.WaitHandle).ToArray();
                WaitHandle.WaitAll(group);
            }
            sw.Stop();

            return new CombinedWorkerThreadResult(results, sw.Elapsed);
        }

        private static CombinedEndpointResult QueueWorkerThreads(List<HttpRequestMessage> requestsList, bool threadAffinity, TimeSpan duration, int? count, CancellationToken cancellationToken)
        {
            var results = new ConcurrentQueue<EndpointResult>();
            var events = new List<ManualResetEventSlim>();
            var sw = new Stopwatch();
            sw.Start();
            MultipleRequestsStore.HttpRequestMessageList = requestsList;

            for (var i = 0; i < requestsList.Count; i++)
            {
                var resetEvent = new ManualResetEventSlim(false);

                ThreadHelper.QueueThread(i, threadAffinity, (threadIndex) =>
                {
                    DoWork(duration, count, results, sw, cancellationToken, resetEvent, threadIndex);
                });

                events.Add(resetEvent);
            }

            for (var i = 0; i < events.Count; i += 50)
            {
                var group = events.Skip(i).Take(50).Select(r => r.WaitHandle).ToArray();
                WaitHandle.WaitAll(group);
            }
            sw.Stop();

            return new CombinedEndpointResult()
            {
                EndpointResults = results,
                Elapsed = sw.Elapsed
            };
        }

        private static CombinedEndpointResult QueueWorkerThreads(List<Tuple<string, string, string>> requestsList, bool threadAffinity, int pipelining, TimeSpan duration, int? count, CancellationToken cancellationToken)
        {
            var results = new ConcurrentQueue<EndpointResult>();
            var events = new List<ManualResetEventSlim>();
            var sw = new Stopwatch();
            sw.Start();
            MultipleRequestsStore.RequestsList = requestsList;

            for (var i = 0; i < requestsList.Count; i++)
            {
                var resetEvent = new ManualResetEventSlim(false);

                ThreadHelper.QueueThread(i, threadAffinity, (threadIndex) =>
                {
                    DoWork(duration, count, pipelining, results, sw, cancellationToken, resetEvent, threadIndex);
                });

                events.Add(resetEvent);
            }

            for (var i = 0; i < events.Count; i += 50)
            {
                var group = events.Skip(i).Take(50).Select(r => r.WaitHandle).ToArray();
                WaitHandle.WaitAll(group);
            }
            sw.Stop();

            return new CombinedEndpointResult()
            {
                EndpointResults = results,
                Elapsed = sw.Elapsed
            };
        }

        private static EndpointResult createResult(Tuple<string, string, string> request)
        {
            return new EndpointResult()
            {
                request = request.Item2,
                body = request.Item3,
                uri = request.Item1,
                error = false,
                responselength = 0,
                elapsed = 0,
                statuscode = "unknown",
                exception = string.Empty,
                starttime = DateTime.Now
            };
        }

        private static EndpointResult createResult(HttpRequestMessage request)
        {
            if (request == null)
                return new EndpointResult();

            return new EndpointResult()
            {
                request = request.ToString(),
                body = request.Content?.ToString(),
                uri = request.RequestUri.AbsoluteUri,
                error = false,
                responselength = 0,
                elapsed = 0,
                statuscode = "unknown",
                exception = string.Empty,
                starttime = DateTime.Now
            };
        }

        private async static void DoWork(TimeSpan duration, int? count, ConcurrentQueue<EndpointResult> results, Stopwatch sw, CancellationToken cancellationToken, ManualResetEventSlim resetEvent, int workerIndex)
        {
            var sw2 = new Stopwatch();
            var current = 0;

            while (!cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds && (!count.HasValue || current < count.Value))
            {
                current++;
                HttpRequestMessage request = null;
                try
                {
                    sw2.Restart();
                    request = MultipleRequestsStore.GetHttpRequestMessage();
                    var response = await Netling.Core.Utils.HttpHelper.Send(request);

                    var result = createResult(request);
                    var items = response.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                    result.elapsed = (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000;
                    result.responselength = items.Length == 2 ? Convert.ToInt64(items[1]) : 0;
                    result.statuscode = items.Length == 2 ? items[0] : "unknown";
                    results.Enqueue(result);
                }
                catch (Exception ex)
                {
                    var result = createResult(request);
                    result.exception = ex.ToString();
                    result.error = true;
                    results.Enqueue(result);
                }
            }
            resetEvent.Set();
        }

        private static void DoWork(TimeSpan duration, int? count, int pipelining, ConcurrentQueue<EndpointResult> results, Stopwatch sw, CancellationToken cancellationToken, ManualResetEventSlim resetEvent, int workerIndex)
        {
            var sw2 = new Stopwatch();
            var sw3 = new Stopwatch();
            byte[] bodyByteArray = null;
            byte[] requestByteArray = null;

            var request = MultipleRequestsStore.Get();

            if (!string.IsNullOrEmpty(request.Item2))
            {
                requestByteArray = Encoding.UTF8.GetBytes(request.Item2);
            }
            if (!string.IsNullOrEmpty(request.Item3))
            {
                bodyByteArray = Encoding.UTF8.GetBytes(request.Item3);
            }

            Uri uri = null;
            if(!Uri.TryCreate(request.Item1, UriKind.Absolute, out uri))
            {
                resetEvent.Set();
                return;
            }

            var worker = new HttpWorker(uri, request: requestByteArray, data: bodyByteArray);
            var current = 0;

            // Priming connection ...
            if (!count.HasValue)
            {
                try
                {
                    int tmpStatusCode;

                    if (pipelining > 1)
                    {
                        worker.WritePipelined(pipelining);
                        worker.Flush();
                        for (var j = 0; j < pipelining; j++)
                        {
                            worker.ReadPipelined(out tmpStatusCode);
                        }
                    }
                    else
                    {
                        worker.Write();
                        worker.Flush();
                        worker.Read(out tmpStatusCode);
                    }

                }
                catch (Exception ex)
                {

                }
            }

            if (pipelining == 1)
            {
                while (!cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds && (!count.HasValue || current < count.Value))
                {
                    current++;
                    try
                    {
                        sw2.Restart();
                        worker.Write();
                        worker.Flush();
                        int statusCode;
                        var length = worker.Read(out statusCode);

                        var result = createResult(request);
                        result.elapsed = (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000;
                        result.responselength = length;
                        result.statuscode = statusCode.ToString();
                        results.Enqueue(result);
                    }
                    catch (Exception ex)
                    {
                        var result = createResult(request);
                        result.exception = ex.ToString();
                        result.error = true;
                        results.Enqueue(result);
                    }
                }
            }
            else
            {
                try
                {
                    sw2.Restart();
                    worker.WritePipelined(pipelining);
                    worker.Flush();
                }
                catch (Exception ex)
                {
                    var result = createResult(request);
                    result.exception = ex.ToString();
                    result.error = true;
                    results.Enqueue(result);
                }

                while (!cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds)
                {
                    try
                    {
                        for (var j = 0; j < pipelining; j++)
                        {
                            int statusCode;
                            var length = worker.ReadPipelined(out statusCode);

                            var result = createResult(request);
                            result.elapsed = (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000;
                            result.responselength = length;
                            result.statuscode = statusCode.ToString();
                            results.Enqueue(result);

                            if (j == 0 && !cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds)
                            {
                                sw3.Restart();
                                worker.WritePipelined(pipelining);
                                worker.Flush();
                            }
                        }

                        var tmp = sw2;
                        sw2 = sw3;
                        sw3 = tmp;
                    }
                    catch (Exception ex)
                    {
                        var result = createResult(request);
                        result.exception = ex.ToString();
                        result.error = true;
                        results.Enqueue(result);
                    }
                }
            }

            resetEvent.Set();
        }

        private static void DoWork(Uri uri, TimeSpan duration, int? count, int pipelining, ConcurrentQueue<WorkerThreadResult> results, Stopwatch sw, CancellationToken cancellationToken, ManualResetEventSlim resetEvent, int workerIndex, string requestString, string body)
        {
            var result = new WorkerThreadResult();
            var sw2 = new Stopwatch();
            var sw3 = new Stopwatch();
            byte[] bodyByteArray = null;
            byte[] requestByteArray = null;

            if (!string.IsNullOrEmpty(requestString)){
                requestByteArray = Encoding.UTF8.GetBytes(requestString);
            }
            if (!string.IsNullOrEmpty(body))
            {
                bodyByteArray = Encoding.UTF8.GetBytes(body);
            }

            var worker = new HttpWorker(uri, request: requestByteArray, data: bodyByteArray);
            var current = 0;

            // To save memory we only track response times from the first 20 workers
            var trackResponseTime = workerIndex < 20;

            // Priming connection ...
            if (!count.HasValue)
            {
                try
                {
                    int tmpStatusCode;

                    if (pipelining > 1)
                    {
                        worker.WritePipelined(pipelining);
                        worker.Flush();
                        for (var j = 0; j < pipelining; j++)
                        {
                            worker.ReadPipelined(out tmpStatusCode);
                        }
                    }
                    else
                    {
                        worker.Write();
                        worker.Flush();
                        worker.Read(out tmpStatusCode);
                    }

                }
                catch (Exception ex)
                {

                }
            }

            if (pipelining == 1)
            {
                while (!cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds && (!count.HasValue || current < count.Value))
                {
                    current++;
                    try
                    {
                        sw2.Restart();
                        worker.Write();
                        worker.Flush();
                        int statusCode;
                        var length = worker.Read(out statusCode);
                        result.Add((int)(sw.ElapsedTicks / Stopwatch.Frequency), length, (float) sw2.ElapsedTicks / Stopwatch.Frequency * 1000, statusCode, trackResponseTime);
                    }
                    catch (Exception ex)
                    {
                        result.AddError((int)(sw.ElapsedTicks / Stopwatch.Frequency), (float) sw2.ElapsedTicks / Stopwatch.Frequency * 1000, ex);
                    }
                }
            }
            else
            {
                try
                {
                    sw2.Restart();
                    worker.WritePipelined(pipelining);
                    worker.Flush();
                }
                catch (Exception ex)
                {
                    result.AddError((int)(sw.ElapsedTicks / Stopwatch.Frequency), (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000, ex);
                }

                while (!cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds)
                {
                    try
                    {
                        for (var j = 0; j < pipelining; j++)
                        {
                            int statusCode;
                            var length = worker.ReadPipelined(out statusCode);
                            result.Add((int)Math.Floor((float)sw.ElapsedTicks / Stopwatch.Frequency), length, (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000, statusCode, trackResponseTime);

                            if (j == 0 && !cancellationToken.IsCancellationRequested && duration.TotalMilliseconds > sw.Elapsed.TotalMilliseconds)
                            {
                                sw3.Restart();
                                worker.WritePipelined(pipelining);
                                worker.Flush();
                            }
                        }

                        var tmp = sw2;
                        sw2 = sw3;
                        sw3 = tmp;
                    }
                    catch (Exception ex)
                    {
                        result.AddError((int)(sw.ElapsedTicks / Stopwatch.Frequency), (float)sw2.ElapsedTicks / Stopwatch.Frequency * 1000, ex);
                    }
                }
            }

            results.Enqueue(result);
            resetEvent.Set();
        }
    }
}
