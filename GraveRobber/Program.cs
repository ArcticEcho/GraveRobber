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

            switch (cmd)
            {
                case "DIE":
                {
                    chatRoom.PostMessageFast("Bye.");
                    shutdownMre.Set();
                    break;
                }
                case "FETCH DATA":
                {
                    chatRoom.PostMessageFast("Fetching data, one moment...");
                    FetchData();
                    break;
                }
            }
        }

        private static void FetchData()
        {
            try
            {
                var fetcher = new MessageFetcher();
                var messages = fetcher.GetRecentMessage(chatRoom);
                var statuses = new Dictionary<string, Status?>();

                foreach (var msg in messages)
                {
                    Thread.Sleep(1500);
                    statuses[msg.Value] = QuestionStatus.GetQuestionStatus(msg.Value);
                }

                var data = statuses.Dump();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
