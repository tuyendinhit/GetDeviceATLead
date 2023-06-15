using IniParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace GetDeviceATLead
{
    internal class Program
    {
        private static string statusServer = "";
        private static string[] requestingDevices;
        private static string requestInfo;
        private static string? responseInfo;
        private static Dictionary<string, List<deviceInfo>> remainDeviceInfo = new();
        private static string currentRequestInfo;

        static string svApi = "sv1";
        static string serverApi = "http://api.zchanger.pro";
        static string fauser = string.Empty;
        static string? privateServerApi = string.Empty;

    static void Main(string[] args)
        {
            string? privateServerApi = new FileIniDataParser().ReadFile($"{AppDomain.CurrentDomain.BaseDirectory}global.ini")["INFO"]["PrivateSvApi"];
            fauser = new FileIniDataParser().ReadFile($"{AppDomain.CurrentDomain.BaseDirectory}global\\config.ini")["Account"]["Username"];

            if (!string.IsNullOrEmpty(privateServerApi))
            {
                string[] splitApi = privateServerApi.Split(new[] { "//" }, StringSplitOptions.None);
                svApi = splitApi[1].Split('.')[0];
                serverApi = privateServerApi;
            }
            else
            {
                svApi = "sv1";
                serverApi = $"http://{fauser}-device.atlead.net";
            }

            while (true || IsControllerWindowOpen())
            {
                Thread.Sleep(500);
                requestingDevices = Directory.GetFiles($"{AppDomain.CurrentDomain.BaseDirectory}RequestDevice", "*_request.txt", SearchOption.AllDirectories);
                if (requestingDevices.Length == 0) continue;

                foreach (string device in requestingDevices)
                {
                    ConLog("Serial: " + Path.GetFileName(device).Replace("_request.txt", ""));
                    requestInfo = File.ReadAllText(device);
                    currentRequestInfo = Clean(requestInfo);
                    do
                    {
                        responseInfo = GetDeviceInfoOffline();
                        if (string.IsNullOrEmpty(responseInfo))
                        {
                            GetDeviceInfoOnline();
                            Task.Delay(3000).Wait();
                        }
                    } while (string.IsNullOrEmpty(responseInfo));

                    string fileResponse_ = device.Replace("_request", "_response_");
                    File.Move(device, fileResponse_, true);
                    File.WriteAllText(fileResponse_, responseInfo);
                    File.Move(fileResponse_, fileResponse_.Replace("_response_", "_response"), true);
                }
            }
        }

        private static void ConLog(string v)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]{v}");
        }

        private static string Clean(string requestInfo)
        {
            requestInfo = Regex.Replace(requestInfo, "&hwid=[^&]+", "&hwid=e927f2734571872d");
            requestInfo = Regex.Replace(requestInfo, "&Latitude=[^&]+", "&Latitude=21.0313");
            requestInfo = Regex.Replace(requestInfo, "&Longitude=[^&]+", "&Longitude=105.8516");
            requestInfo = Regex.Replace(requestInfo, "&timeZone=[^&]+", "timeZone=QXNpYS9CYW5na29r");
            //requestInfo = Regex.IsMatch(requestInfo, "connection=mobile") ? requestInfo : Regex.Replace(requestInfo, "&isp=[^&]+", "isp=\"Viettel Telecom\"");

            return requestInfo;
        }

        private static bool IsControllerWindowOpen()
        {
            return Process.GetProcessesByName("Controller").Length > 0;
        }

        private static string? GetDeviceInfoOffline()
        {
            if (remainDeviceInfo.Count == 0 || !remainDeviceInfo.ContainsKey(currentRequestInfo))
            {
                return null;
            }

            string? result = null;
            KeyValuePair<string, List<deviceInfo>> item = remainDeviceInfo.FirstOrDefault(x => x.Key == currentRequestInfo);
            ConLog("#1Remain device: " + item.Value.Count);

            if (item.Value.Count > 0)
            {
                result = item.Value.First().ResponseInfo;
                item.Value.RemoveAt(0);
            }
            ConLog("#2Remain device: " + item.Value.Count);

            return result;
        }

        private static void GetDeviceInfoOnline()
        {
            int tryCount = 0;
            DateTime sTime = DateTime.Now;
            while (true)
            {
                tryCount++;
                ConLog($"Getting devices...{tryCount}");
                string requestUri = $"{serverApi}/v6.php?user={fauser}&ip={GetIP()}&numDevice=20";
                requestUri = requestUri.Replace("//v6", "/v6");
                string requestData = currentRequestInfo;

                var content = new StringContent(requestData, Encoding.ASCII, "application/x-www-form-urlencoded");

                using HttpClient client = new();
                string resp = string.Empty;
                JObject? info = null;
                try
                {
                    resp = client.PostAsync(requestUri, content).Result.Content.ReadAsStringAsync().Result;
                    info = JObject.Parse(resp);
                }
                catch (Exception ex)
                { 
                    ConLog("Error get device: " + ex.Message);
                }
                
                if (info != null)
                {
                    if (info.TryGetValue("success", out JToken? iSuccess))
                    {
                        if (iSuccess != null && (bool)iSuccess == true)
                        {
                            if (info.TryGetValue("devices", out JToken? devices))
                            {
                                remainDeviceInfo[currentRequestInfo] = new List<deviceInfo>();
                                foreach (var device in devices)
                                {
                                    remainDeviceInfo[currentRequestInfo].Add(new deviceInfo
                                    {
                                        RequestInfo = currentRequestInfo,
                                        ResponseInfo = "{\"success\":true,\"error\":\"\",\"device\":\"" + device + "\",\"nonce\":\"" + info["nonce"] + "\"}"
                                    });
                                }
                                ConLog($"Get device success ({(DateTime.Now - sTime).TotalSeconds}s)");
                                return;
                            }
                        }
                    }
                    ConLog($"Get device fail: {info} || ({(DateTime.Now - sTime).TotalSeconds}s)");
                }

                ConLog($"Get device fail: {resp} || ({(DateTime.Now - sTime).TotalSeconds}s)");

                
                Thread.Sleep(3000);
            }
        }

        private static string? GetIP()
        {
            using HttpClient client = new HttpClient();
            try
            {
                //string ipAddress = client.GetStringAsync("https://api.ipify.org").Result;
                string ipAddress = client.GetStringAsync("https://myipv4.p1.opendns.com/get_my_ip").Result;
                return ipAddress;
            }
            catch (HttpRequestException)
            {
                // Handle the exception as necessary
                return null;
            }
        }
    }
}