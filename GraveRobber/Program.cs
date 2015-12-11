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
using System.Threading;
using ChatExchangeDotNet;

namespace GraveRobber
{
    using System.Linq;
    using ServiceStack.Text;
    using Status = QuestionStatus.Status;

    class Program
    {
        private static readonly ManualResetEvent shutdownMre = new ManualResetEvent(false);
        private static Client chatClient;
        private static Room chatRoom;



        public static void Main(string[] args)
        {
            Console.Title = "GraveRobber";
            Console.CancelKeyPress += (o, oo) =>
            {
                oo.Cancel = true;
                shutdownMre.Set();
            };

            Console.Write("Authenticating...");
            InitialiseFromConfig();
            Console.Write("done.\nJoining chat room(s)...");
            JoinRooms();

#if DEBUG
            Console.WriteLine("done.\nGraveRobber started (debug).");
#else
            Console.WriteLine("done.\nGraveRobber started.");
#endif

            shutdownMre.WaitOne();

            Console.Write("Stopping...");

            chatRoom?.Leave();
            shutdownMre.Dispose();
            chatClient?.Dispose();

            Console.WriteLine("done.");
        }



        private static void InitialiseFromConfig()
        {
            var cr = new ConfigReader();

            var email = cr.GetSetting("se email");
            var pwd = cr.GetSetting("se pass");

            chatClient = new Client(email, pwd);
        }

        private static void JoinRooms()
        {
            var cr = new ConfigReader();

            chatRoom = chatClient.JoinRoom(cr.GetSetting("room"));
            chatRoom.EventManager.ConnectListener(EventType.UserMentioned, new Action<Message>(HandleCommand));
        }

        private static void HandleCommand(Message msg)
        {
            var cmd = msg.Content.Trim().ToUpperInvariant();

            if (cmd == "DIE")
            {
                chatRoom.PostMessageFast("Bye.");
                shutdownMre.Set();
            }
            else if (cmd.StartsWith("FETCH DATA"))
            {
                var msgCount = 50;

                if (cmd.Any(Char.IsDigit))
                {
                    if (!int.TryParse(new string(cmd.Where(Char.IsDigit).ToArray()), out msgCount))
                    {
                        // Well, do nothing since we've already initialised
                        // the field with a default value (of 50).
                    }
                }

                chatRoom.PostMessageFast("Fetching data, one moment...");
                FetchData(msgCount);
            }
        }

        private static void FetchData(int msgCount)
        {
            try
            {
                var fetcher = new MessageFetcher();
                var messages = fetcher.GetRecentMessage(chatRoom, msgCount);
                var statuses = new Dictionary<string, KeyValuePair<Status, int>?>();

                foreach (var msg in messages)
                {
                    Thread.Sleep(1000);

                    var qStatus = QuestionStatus.GetQuestionStatus(msg.Value);

                    statuses[msg.Value] = qStatus;
                }

                var data = statuses.Dump();

                var chatMsg = new MessageBuilder(MultiLineMessageType.None, false);

                foreach (var post in statuses)
                {
                    if ((post.Value?.Value ?? 0) == 0) continue;

                    chatMsg.AppendText($"{post.Value.Value.Key}, edited {post.Value.Value.Value} time(s): {post.Key}\n");
                }

                if (!String.IsNullOrWhiteSpace(chatMsg.ToString()))
                {
                    var msgText = $"{statuses.Count} messages checked\n{chatMsg}";
                    chatRoom.PostMessageFast(msgText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
