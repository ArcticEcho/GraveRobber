/*
 * GraveRobber. A .NET PoC program for fetching data from the SOCVR graveyards.
 * Copyright © 2016, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CsQuery;

namespace GraveRobber
{
    public class SELogin
    {
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private readonly Regex userUrl = new Regex("href=\"/users/\\d*?/", regOpts);
        private readonly Regex openidDel = new Regex("https://openid\\.stackexchange\\.com/user/.*?\"", regOpts);
        private string openidUrl;

        public static CookieContainer Cookies { get; private set; } = new CookieContainer();



        public void SEOpenIDLogin(string email, string password)
        {
            var getResContent = Get("https://openid.stackexchange.com/account/login");

            if (string.IsNullOrEmpty(getResContent))
            {
                throw new Exception("Unable to find OpenID fkey.");
            }

            var data = $"email={Uri.EscapeDataString(email)}&password=" +
                       $"{Uri.EscapeDataString(password)}&fkey=" +
                       GetInputValue(CQ.Create(getResContent), "fkey");

            using (var res = PostRaw("https://openid.stackexchange.com/account/login/submit", data))
            {
                if (res == null)
                {
                    throw new Exception("Unable to authenticate using OpenID.");
                }
                if (res.ResponseUri.ToString() != "https://openid.stackexchange.com/user")
                {
                    throw new Exception("Invalid OpenID credentials.");
                }

                var html = GetContent(res);
                var del = openidDel.Match(html).Value;

                openidUrl = del.Remove(del.Length - 1, 1);
            }
        }

        public void SiteLogin(string host)
        {
            var getResContent = Get($"http://{host}/users/login");

            if (string.IsNullOrEmpty(getResContent))
            {
                throw new Exception($"Unable to find fkey from {host}.");
            }

            var fkey = GetInputValue(CQ.Create(getResContent), "fkey");

            var data = $"fkey={fkey}" +
                       "&oauth_version=&oauth_server=&openid_username=&openid_identifier=" +
                       Uri.EscapeDataString(openidUrl);

            var referrer = $"https://{host}/users/login?returnurl=" +
                           Uri.EscapeDataString($"http://{host}/");

            using (var postRes = PostRaw($"http://{host}/users/authenticate", data, referrer))
            {
                if (postRes == null) throw new Exception($"Unable to login to {host}.");

                var html = GetContent(postRes);
                HandleConfirmationPrompt(postRes.ResponseUri.ToString(), html);
                TryFetchUserID(html);
            }
        }

        public string Post(string uri, string content, string referer = null, string origin = null)
        {
            var req = GenerateRequest(uri, content, "POST", referer, origin);

            using (var res = GetResponse(req)) return GetContent(res);
        }

        public string Get(string uri)
        {
            var req = GenerateRequest(uri, null, "GET");

            using (var res = GetResponse(req)) return GetContent(res);
        }



        private void HandleConfirmationPrompt(string uri, string html)
        {
            if (!uri.ToString().StartsWith("https://openid.stackexchange.com/account/prompt")) return;

            var dom = CQ.Create(html);
            var session = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "session");
            var fkey = GetInputValue(dom, "fkey");
            var data = "session=" + session["value"] + "&fkey=" + fkey;

            Post("https://openid.stackexchange.com/account/prompt/submit", data);
        }

        private void TryFetchUserID(string html)
        {
            var dom = CQ.Create(html);
            var id = 0;

            foreach (var e in dom[".topbar a"])
            {
                if (userUrl.IsMatch(e.OuterHTML))
                {
                    id = int.Parse(e.Attributes["href"].Split('/')[2]);
                    break;
                }
            }

            if (id == 0) throw new Exception("Unable to login to Stack Exchange.");
        }

        private string GetInputValue(CQ dom, string elementName)
        {
            var fkeyE = dom["input"].FirstOrDefault(e => e.Attributes["name"] == elementName);
            return fkeyE?.Attributes["value"];
        }

        private HttpWebResponse PostRaw(string uri, string content, string referer = null, string origin = null)
        {
            var req = GenerateRequest(uri, content, "POST", referer, origin);

            return GetResponse(req);
        }

        private HttpWebRequest GenerateRequest(string uri, string content, string method, string referer = null, string origin = null)
        {
            if (uri == null) throw new ArgumentNullException("uri");

            var req = (HttpWebRequest)WebRequest.Create(uri);
            var meth = method.Trim().ToUpperInvariant();

            req.Method = meth;
            req.CookieContainer = Cookies;
            req.Referer = referer;

            if (!string.IsNullOrEmpty(origin))
                req.Headers.Add("Origin", origin);

            if (meth == "POST")
            {
                var data = Encoding.UTF8.GetBytes(content);

                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = data.Length;

                using (var dataStream = req.GetRequestStream())
                    dataStream.Write(data, 0, data.Length);
            }

            return req;
        }

        private HttpWebResponse GetResponse(HttpWebRequest req)
        {
            if (req == null) throw new ArgumentNullException("req");

            HttpWebResponse res = null;

            try
            {
                res = (HttpWebResponse)req.GetResponse();

                Cookies.Add(res.Cookies);
            }
            catch (WebException ex)
            {
                // Check if we've been throttled.
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.Conflict)
                {
                    // Yep, we have.
                    res = (HttpWebResponse)ex.Response;
                }
                else
                {
                    throw;
                }
            }

            return res;
        }

        private string GetContent(HttpWebResponse response)
        {
            if (response == null) throw new ArgumentNullException("response");

            using (var strm = response.GetResponseStream())
            using (var reader = new StreamReader(strm))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
