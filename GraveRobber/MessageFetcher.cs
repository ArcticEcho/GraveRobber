/*
 * GraveRobber. A .NET PoC program for fetching data from the SOCVR graveyards.
 * Copyright © 2015, ArcticEcho.
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ChatExchangeDotNet;
using ServiceStack.Text;

namespace GraveRobber
{
    public class MessageFetcher
    {
        private readonly Regex cvplsMsg = new Regex(@"(?i)^←?\[tag:cv-?pl[zs]\].*https?://\S+?", RegexOptions.Compiled);
        private readonly Regex cvplsPostUrl = new Regex(@"(https?://\S*?)(\s|\z)", RegexOptions.Compiled);
        private readonly string fkey;



        public MessageFetcher()
        {
            var html = new WebClient().DownloadString("http://chat.stackoverflow.com");
            fkey = Regex.Match(html, "(?i)id=\"fkey\".*value=\"([a-z0-9]+)\"").Groups[1].Value;
        }



        public Dictionary<Message, string> GetRecentMessage(Room room, int msgCount = 50)
        {
            msgCount = Math.Max(10, Math.Min(500, msgCount));

            var jsonStr = Encoding.UTF8.GetString(new WebClient().UploadValues("http://chat.stackoverflow.com/chats/90230/events", new NameValueCollection
            {
                { "since", "0" },
                { "mode", "Messages" },
                { "msgCount", msgCount.ToString() },
                { "fkey", fkey }
            }));

            var msgIDs = new HashSet<string>();
            var json = JsonSerializer.DeserializeFromString<Dictionary<string, Dictionary<string, object>[]>>(jsonStr);
            var msgs = new Dictionary<Message, string>();

            foreach (var m in json["events"])
            {
                var idStr = (string)m["message_id"];
                var id = -1;

                if (!int.TryParse(idStr, out id)) continue;

                Thread.Sleep(1000);

                var message = room[id];

                if (cvplsMsg.IsMatch(message.Content))
                {
                    var postUrl = cvplsPostUrl.Match(message.Content).Groups[1].Value.Trim();
                    postUrl = postUrl.EndsWith(")") ? postUrl.Substring(0, postUrl.Length - 1) : postUrl;

                    msgs[message] = postUrl;
                }
            }

            return msgs;
        }
    }
}
