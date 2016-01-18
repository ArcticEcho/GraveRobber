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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using ChatExchangeDotNet;

namespace GraveRobber
{
    class Program
    {
        private static readonly string currentVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
        private static readonly ManualResetEvent shutdownMre = new ManualResetEvent(false);
        private static readonly MessageFetcher messageFetcher = new MessageFetcher();
        private static readonly SELogin seLogin = new SELogin();
        private static QuestionProcessor qProcessor;
        private static Client chatClient;
        private static Room mainRoom;
        private static Room watchingRoom;



        public static void Main(string[] args)
        {
            Console.Title = $"GraveRobber {currentVer}";
            Console.CancelKeyPress += (o, oo) =>
            {
                oo.Cancel = true;
                shutdownMre.Set();
            };

            Console.Write("Authenticating...");
            InitialiseFromConfig();
            Console.Write("done.\nStarting question processor...");
            StartQuestionProcessor();
            Console.Write("done.\nJoining chat room(s)...");
            JoinRooms();

#if DEBUG
            Console.WriteLine("done.\nGraveRobber started (debug).");
            mainRoom.PostMessageFast($"GraveRobber started {currentVer} (debug).");
#else
            Console.WriteLine("done.\nGraveRobber started.");
            mainRoom.PostMessageFast($"GraveRobber started {currentVer}.");
#endif

            shutdownMre.WaitOne();
            shutdownMre?.Dispose();

            Console.Write("\nShutting down...");

            mainRoom?.Leave();
            watchingRoom?.Leave();
            chatClient?.Dispose();
            qProcessor?.Dispose();

            Console.WriteLine("\nShutdown complete.");
        }



        private static void InitialiseFromConfig()
        {
            var cr = new ConfigReader();

            chatClient = new Client(cr.AccountEmailAddressPrimary, cr.AccountPasswordPrimary);

            if (!string.IsNullOrWhiteSpace(cr.AccountEmailAddressSecondary) &&
                !string.IsNullOrWhiteSpace(cr.AccountPasswordSecondary))
            {
                seLogin.SEOpenIDLogin(cr.AccountEmailAddressSecondary, cr.AccountPasswordSecondary);
                seLogin.SiteLogin("stackoverflow.com");
            }
        }

        private static void StartQuestionProcessor()
        {
            var cr = new ConfigReader();
            qProcessor = new QuestionProcessor(seLogin, cr.DataFilesDir);
            qProcessor.SeriousDamnHappened = ex => Console.WriteLine(ex);
            qProcessor.PostFound = qs =>
            {             //       V Zero-width char here in.
                var msg = $"A `cv-p​ls`'ed [question]({qs.Url} " +
                          $"\"+{qs.UpvoteCount}/-{Math.Abs(qs.DownvoteCount)}\") " +
                          $"has been edited ({Math.Round(qs.Difference * 100)}% changed).";
                mainRoom.PostMessageFast(msg);
            };
        }

        private static void JoinRooms()
        {
            var cr = new ConfigReader();

            mainRoom = chatClient.JoinRoom(cr.RoomUrl);
            mainRoom.InitialisePrimaryContentOnly = true;
            mainRoom.AggressiveWebSocketRecovery = true;
            mainRoom.EventManager.ConnectListener(EventType.UserMentioned, new Action<Message>(HandleCommand));

            watchingRoom = chatClient.JoinRoom("http://chat.stackoverflow.com/rooms/90230/cv-request-graveyard");//("http://chat.stackoverflow.com/rooms/68414/socvr-testing-facility");//
            watchingRoom.InitialisePrimaryContentOnly = true;
            watchingRoom.AggressiveWebSocketRecovery = true;
            watchingRoom.EventManager.ConnectListener(EventType.MessageMovedIn, new Action<Message>(m =>
            {
                var url = messageFetcher.GetPostUrl(m);

                if (!string.IsNullOrWhiteSpace(url))
                {
                    qProcessor.WatchPost(url);
                }
            }));
        }

        /// <summary>
        /// Beware, laziness ahead...
        /// </summary>
        private static void HandleCommand(Message msg)
        {
            var cmd = msg.Content.Trim().ToUpperInvariant();

            if (cmd == "DIE" && (msg.Author.IsRoomOwner ||
                msg.Author.IsMod || msg.Author.ID == 2246344))
            {
                mainRoom.PostMessageFast("Bye.");
                shutdownMre.Set();
            }
            else if (cmd == "REFILL" && (msg.Author.IsRoomOwner ||
                     msg.Author.IsMod || msg.Author.ID == 2246344))
            {
                mainRoom.PostReplyFast(msg, "Working...");

                var ms = messageFetcher.GetRecentMessage(mainRoom, 500);

                foreach (var m in ms.Values)
                {
                    if (!string.IsNullOrWhiteSpace(m))
                    {
                        qProcessor.WatchPost(m);
                    }
                }

                mainRoom.PostReplyFast(msg, "Done.");
            }
            else if (cmd == "COMMANDS")
            {
                mainRoom.PostMessageFast("    commands ~~~~~~~~~~~~~ Prints this beautifully formatted message.\n" +
                                         "    stats ~~~~~~~~~~~~~~~~ Displays the number of posts being watched.\n" +
                                         "    help ~~~~~~~~~~~~~~~~~ Pretty self-explanatory...\n" +
                                         "    die ~~~~~~~~~~~~~~~~~~ I die a slow and painful death.");
            }
            else if (cmd == "STATS")
            {
                var watchingQs = qProcessor.WatchedPosts;
                mainRoom.PostMessageFast($"I'm currently watching `{watchingQs}` post{(watchingQs > 1 ? "s" : "")}.");
            }
            else if (cmd =="HELP")
            {
                mainRoom.PostReplyFast(msg, "I *think* I'm a chatbot. Although, I do love a good " +
                                            "bit of poetry every now and then (don't ask me for any " +
                                            "though, I'm useless). I also love being in this room so much " +
                                            "in fact, that I've decided to check up on all your [tag:cv-pls]'es " +
                                            "to see if they need reopening (I'm not 100% accurate, so only take " +
                                            "messages as suggestions). You can check out what I can do by using: " +
                                            "`commands`. You can find my GH repo " +
                                            "[here](https://github.com/SO-Close-Vote-Reviewers/GraveRobber).");
            }
            else if (cmd == "DIE")
            {
                mainRoom.PostReplyFast(msg, "You need to be a room owner, moderator, or Sam to kill me.");
            }
            else if (cmd.StartsWith("CHECK GRAVE"))
            {
                mainRoom.PostReplyFast(msg, "You need at least 3000 reputation to run this command.");
            }
        }
    }
}
