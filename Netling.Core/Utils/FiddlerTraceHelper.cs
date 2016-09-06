using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Netling.Core.Utils
{
    public static class FiddlerTraceHelper
    {
        public static List<Tuple<string, string, string>> GetRequests(string archivePath)
        {
            try
            {
                string extractFilesPath = extractZipArchive(archivePath);
                var files = Directory.GetFiles(extractFilesPath);
                List<Tuple<string, string, string>> requestsList = new List<Tuple<string, string, string>>();

                foreach (var file in files)
                {
                    string request, body, uri;
                    if (TryReadRawRequest(file, out request, out body, out uri))
                    {
                        requestsList.Add(new Tuple<string, string, string>(uri, request, body));
                    }
                }

                return requestsList;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
            return new List<Tuple<string, string, string>>();
        }

        public static List<HttpRequestMessage> GetRequestMessages(string archivePath)
        {
            try
            {
                string extractFilesPath = extractZipArchive(archivePath);
                var files = Directory.GetFiles(extractFilesPath);
                List<HttpRequestMessage> requestsList = new List<HttpRequestMessage>();

                foreach (var file in files)
                {
                    requestsList.Add(parseRawRequest(file));
                }

                return requestsList;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return new List<HttpRequestMessage>();
        }

        private static HttpRequestMessage parseRawRequest(string file)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                HttpRequestMessage request = ParseRawRequestFirstLine(lines[0]);
                int bodyIndex = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    if(lines[i].Trim() == string.Empty)
                    {
                        bodyIndex = i;
                        break;
                    }
                    else
                    {
                        AddHeaders(request, lines[i]);
                    }
                }
                if(bodyIndex != 0)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = bodyIndex;i< lines.Length; i++)
                    {
                        if (lines[i].Trim() != string.Empty)
                        {
                            sb.Append(lines[i]);
                        }
                    }
                    if (sb.ToString() != string.Empty)
                        request.Content = new StringContent(sb.ToString(), Encoding.UTF8);
                }

                return request;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw ex;
            }
        }

        private static void AddHeaders(HttpRequestMessage request, string rawHeader)
        {
            try
            {
                var items = rawHeader.Split(new string[] { ": " }, StringSplitOptions.RemoveEmptyEntries);
                if (items[0].Trim().ToLowerInvariant().StartsWith("content"))
                {
                    request.Content.Headers.TryAddWithoutValidation(items[0].Trim(), items[1].Trim());
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(items[0].Trim(), items[1].Trim());
                }   
                
            }
            catch(Exception ex)
            {
                throw new Exception($"unable to add header from line - {rawHeader}", ex);
            }
        }

        private static void AddCookies(HttpRequestMessage request, string rawHeader)
        {
            try
            {
                var items = rawHeader.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                //request.Headers.TryAddWithoutValidation(items[0].Trim(), items[1].Trim());
            }
            catch (Exception ex)
            {
                throw new Exception($"unable to add header from line - {rawHeader}", ex);
            }
        }

        private static HttpRequestMessage ParseRawRequestFirstLine(string line1)
        {
            try
            {
                HttpRequestMessage request;
                var items = line1.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (items[0])
                {
                    case "GET":
                        request = new HttpRequestMessage(HttpMethod.Get, items[1]);
                        break;
                    case "POST":
                        request = new HttpRequestMessage(HttpMethod.Post, items[1]);
                        break;
                    default:
                        throw new Exception("invalid http method");
                }

                return request;
            }
            catch(Exception ex)
            {
                throw new Exception($"unable to read raw request first line - {line1}", ex);
            }
        }

        private static string extractZipArchive(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"file - {archivePath} doesn't exists...");
            }

            string extractPath = renametoZip(archivePath);
            var directoryPath = $"{Path.GetDirectoryName(extractPath)}\\{Path.GetFileNameWithoutExtension(extractPath)}";
            Directory.CreateDirectory(directoryPath);

            using (ZipArchive archive = ZipFile.OpenRead(extractPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("_c.txt", StringComparison.OrdinalIgnoreCase))
                    {                
                        entry.ExtractToFile(Path.Combine(directoryPath, entry.Name));
                    }
                }
            }
            return directoryPath;
        }

        private static string renametoZip(string filePath)
        {
            var tempPath = Environment.GetEnvironmentVariable("temp");
            var destFile = Path.Combine(tempPath, $"{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now.ToFileTimeUtc()}.zip");
            File.Copy(filePath, destFile);
            return destFile;
        }

        private static bool TryReadRawRequest(string file, out string request, out string body, out string uri)
        {
            request = string.Empty;
            body = string.Empty;
            uri = string.Empty;
            bool bodyBegins = false;

            StringBuilder sbRequest = new StringBuilder();
            StringBuilder sbBody = new StringBuilder();

            try
            {
                var lines = File.ReadAllLines(file);
                if (lines.Length > 0)
                {
                    //extract URI
                    string[] items = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    uri = items[1];

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
            catch (Exception ex)
            {
                // toDo: log it
                //throw new Exception("Failed to read request raw data", ex);
                return false;
            }
        }
    }
}
