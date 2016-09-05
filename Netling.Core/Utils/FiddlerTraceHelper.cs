using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace Netling.Core.Utils
{
    public static class FiddlerTraceHelper
    {
        public static List<Tuple<string, string, string>> GetRequests(string archivePath)
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
                            bodyBegins = true;
                            sbRequest.AppendLine(lines[i]);
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
