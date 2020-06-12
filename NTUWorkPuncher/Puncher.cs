using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NTUWorkPuncher
{
    public class Puncher
    {

        public string Username { get; set; }

        public DateTime TimeLogined { get; set; }

        private const string USER_AGENT = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_9_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/36.0.1985.125 Safari/537.36";

        private string lineNotifyApiToken { get; set; }

        private string account { get; set; }

        private string password { get; set; }

        private CookieContainer cookie { get; set; }

        public Puncher(string Account, string Password, string LineNotifyApiToken = null)
        {
            getCalendarJsonFromDoc();
            account = Account;
            password = Password;
            lineNotifyApiToken = LineNotifyApiToken;
        }

        public void Login()
        {
            Debug.WriteLine($"帳號登入:{account}");

            cookie = new CookieContainer();
            using (HttpWebResponse resp = webAPI("GET", "https://my.ntu.edu.tw/login.aspx", cookie)) { }

            using (HttpWebResponse resp = webAPI("POST", "https://web2.cc.ntu.edu.tw/p/s/login2/p1.php", cookie, $"user={account}&pass={password}"))
            {
                string respText = getResponseString(resp);
                if (respText == null || respText.Contains("Authentication Fail"))
                {
                    throw new AuthFailedException();
                }
            }

            using (HttpWebResponse resp = webAPI("GET", "https://my.ntu.edu.tw/attend/ssi.aspx?type=login", cookie))
            {
                string respText = getResponseString(resp);
                Regex regex = new Regex(@".*<span id=""LabName"">(.*\([a-zA-Z0-9]*\))</span>.*");
                Match result = regex.Match(respText);
                if (result.Success)
                {
                    Username = result.Groups[1].Value;
                }
                else
                {
                    Username = null;
                }
            }

            TimeLogined = DateTime.Now;

            Debug.WriteLine($"{TimeLogined.ToString()}登入成功");
        }

        public CardRecord FetchRecords()
        {
            Debug.WriteLine("抓取卡時紀錄:");

            CardRecord records = new CardRecord();
            JsonValue json = null;

            using (HttpWebResponse resp = webAPI("POST", "https://my.ntu.edu.tw/attend/ajax/signin.ashx", cookie, "type=4&day=7"))
            {
                string respText = getResponseString(resp);
                if (string.IsNullOrEmpty(respText))
                {
                    respText = "[]";
                }
                json = JsonValue.Parse(respText);
            }

            if (json.Count == 0)
            {
                Debug.WriteLine("沒有卡時紀錄");
                return records;
            }
            else
            {
                for (int i = 0; i < json.Count; i++)
                {
                    CardRecordItem item = new CardRecordItem
                    {
                        SignDateString = json[i]["signdate"],
                        PunchedInString = json[i]["startdate"],
                        PunchedOutString = json[i]["enddate"]
                    };
                    records.Items.Add(item);
                }
            }

            Debug.WriteLine($"共{records.Items.Count}筆卡時紀錄");

            return records;
        }

        public string PunchIn()
        {
            using (HttpWebResponse resp = webAPI("POST", "https://my.ntu.edu.tw/attend/ajax/signin.ashx", cookie, "type=6&t=1"))
            {
                string respText = getResponseString(resp);
                JsonValue respJson = JsonValue.Parse(respText);
                return respJson["msg"];
            }
        }

        public string PunchOut()
        {
            using (HttpWebResponse resp = webAPI("POST", "https://my.ntu.edu.tw/attend/ajax/signin.ashx", cookie, "type=6&t=2"))
            {
                string respText = getResponseString(resp);
                JsonValue respJson = JsonValue.Parse(respText);
                return respJson["msg"];
            }
        }

        public void LineNotify(string msg)
        {
            Debug.WriteLine($"傳送 line notification: {msg}");

            if (string.IsNullOrEmpty(lineNotifyApiToken))
            {
                Debug.WriteLine("沒有 line notify token, 故略過");
                return;
            }

            string m = $"\r\n現在時間: {DateTime.Now.ToString("hh:mm")}:\r\n{account}: {msg}";
            HttpWebResponse resp = webAPI("POST", "https://notify-api.line.me/api/notify", null, $"message={m}", new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {lineNotifyApiToken}" }
            });
            resp.Dispose();
        }

        public bool IsTodayHoliday()
        {
            return getCalendar().Where(x => x.Date == DateTime.Today).Any();
        }

        public void SetLineNotifyAPIToken(string Token)
        {
            lineNotifyApiToken = Token;
        }

        public void ClearLineNotifyAPIToken()
        {
            lineNotifyApiToken = null;
        }

        private List<DateTime> getCalendar()
        {
            Debug.WriteLine("取得假日紀錄:");

            List<DateTime> holidays = new List<DateTime>();
            int thisYear = DateTime.Now.Year;
            JsonValue json = null;

            try
            {
                using (HttpWebResponse resp = webAPI("GET", "https://data.ntpc.gov.tw/api/datasets/308DCD75-6434-45BC-A95F-584DA4FED251/json?page=0&size=2000"))
                {
                    string respText = getResponseString(resp);
                    json = JsonValue.Parse(respText);
                }
            }
            catch (APIFailedException ex)
            {
                Debug.WriteLine($"線上取得資料失敗: {ex.Message}, 改採用預先下載的檔案紀錄");
                json = getCalendarJsonFromDoc();
            }

            foreach (JsonValue day in json)
            {
                DateTime date = DateTime.Parse(day["date"].ToString().Trim('"'));
                bool isHoliday = day["isHoliday"].ToString().Trim('"') == "是";

                if (date.Year == thisYear && isHoliday)
                {
                    holidays.Add(date);
                }
            }

            Debug.WriteLine($"取得成功, 共{holidays.Count}筆假日資訊");
            return holidays;
        }

        private JsonValue getCalendarJsonFromDoc()
        {
            JsonValue json = JsonValue.Parse(Encoding.UTF8.GetString(Properties.Resources.GovCal_20200401));
            return json;
        }

        private HttpWebResponse webAPI(string method, string uri, CookieContainer cc = null, string data = null, Dictionary<string, string> headers = null)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = method;
            req.UserAgent = USER_AGENT;
            req.KeepAlive = true;

            if (cc != null)
            {
                req.CookieContainer = cc;
            }

            if (headers != null)
            {
                WebHeaderCollection headerCollection = req.Headers;
                foreach (KeyValuePair<string, string> header in headers)
                {
                    req.Headers.Add(header.Key, header.Value);
                }
            }

            if (method == "POST" && data != null)
            {
                byte[] postData = Encoding.UTF8.GetBytes(data);
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = postData.Length;
                using (Stream stream = req.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }
            }

            HttpWebResponse resp = null;
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (Exception)
            {
                throw new APIFailedException(resp);
            }

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw new APIFailedException(resp);
            }

            return resp;
        }

        private string getResponseString(HttpWebResponse resp)
        {
            string respText = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            return respText == "null" ? null : respText;
        }
    }
}
