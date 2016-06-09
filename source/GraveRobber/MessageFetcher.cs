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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ChatExchangeDotNet;
using Jil;

namespace GraveRobber
{
    public class MessageFetcher
    {
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private readonly Regex cvplsMsg = new Regex(@"(?i)^←?\[tag:cv-?pl[zs]\].*https?://\S+?", regOpts);
        private readonly Regex cvplsPostUrl = new Regex(@"(https?://stackoverflow\.com/q(uestions)?/(\d+)\S*?)(\s|\z)", regOpts);
        private readonly Regex dupeReq = new Regex(@"(?i)cv-?pl[sz].*dup((e)|(licate))?.*https?://stackoverflow\.com/q(uestions)?/(\d+)", regOpts);
        private readonly string fkey;



        public MessageFetcher()
        {
            var html = new WebClient().DownloadString("http://chat.stackoverflow.com");
            fkey = Regex.Match(html, "(?i)id=\"fkey\".*value=\"([a-z0-9]+)\"").Groups[1].Value;
        }



        public Dictionary<Message, int> GetRecentMessage(Room room, int msgCount = 50)
        {
            msgCount = Math.Max(10, Math.Min(500, msgCount));

            var jsonStr = Encoding.UTF8.GetString(new WebClient().UploadValues("http://chat.stackoverflow.com/chats/90230/events", new NameValueCollection
            {
                { "since", "0" },
                { "mode", "Messages" },
                { "msgCount", msgCount.ToString() },
                { "fkey", fkey }
            }));

            var json = JSON.Deserialize<Dictionary<string, object>>(jsonStr);
            var events = JSON.Deserialize<Dictionary<string, object>[]>(json["events"].ToString());
            var msgs = new Dictionary<Message, int>();

            foreach (var m in events)
            {
                var idStr = m["message_id"].ToString();
                var id = -1;

                if (!int.TryParse(idStr, out id)) continue;

                Thread.Sleep(1000);

                var message = room[id];
                msgs[message] = GetPostID(message);
            }

            return msgs;
        }

        public int GetPostID(Message message)
        {
            var id = -1;

            if (cvplsMsg.IsMatch(message.Content) && !dupeReq.IsMatch(message.Content))
            {
                var idStr = cvplsPostUrl.Match(message.Content).Groups[3].Value;

                if (!int.TryParse(idStr, out id)) { }
            }

            return id;
        }
    }
}
