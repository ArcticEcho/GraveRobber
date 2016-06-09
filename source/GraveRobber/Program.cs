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
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using ChatExchangeDotNet;
using GraveRobber.Database;

namespace GraveRobber
{
    class Program
    {
        private static readonly ManualResetEvent shutdownMre = new ManualResetEvent(false);
        private static readonly MessageFetcher messageFetcher = new MessageFetcher();
        private static readonly SELogin seLogin = new SELogin();
        private static QuestionWatcherPool qwPool;
        private static QuestionChecker qChecker;
        private static Client chatClient;
        private static Room mainRoom;
        private static Room watchingRoom;



        public static void Main(string[] args)
        {
            var fileVer = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            var currentVer = $"{fileVer.FileMajorPart}.{fileVer.FileMinorPart}.{fileVer.FilePrivatePart}";

            Console.Title = $"GraveRobber {currentVer}";
            Console.CancelKeyPress += (o, oo) =>
            {
                oo.Cancel = true;
                shutdownMre.Set();
            };

            Console.Write("Authenticating...");
            InitialiseFromConfig();
            Console.Write("done.\nInitialising question watcher pool...");
            StartQuestionWatcherPool();
            Console.Write("done.\nJoining chat room(s)...");
            JoinRooms();

#if DEBUG
            Console.WriteLine("done.\nGraveRobber started (debug).");
            mainRoom.PostMessageLight($"GraveRobber started {currentVer} (debug).");
#else
            Console.WriteLine("done.\nGraveRobber started.");
            mainRoom.PostMessageLight($"GraveRobber started {currentVer}.");
#endif

            mainRoom.PostMessageLight("Initialising tracking tests...");

            shutdownMre.WaitOne();
            shutdownMre?.Dispose();

            Console.Write("\nShutting down...");

            mainRoom?.Leave();
            watchingRoom?.Leave();
            chatClient?.Dispose();
            qwPool?.Dispose();

            Console.WriteLine("\nShutdown complete.");
        }



        private static void InitialiseFromConfig()
        {
            if (!string.IsNullOrWhiteSpace(ConfigReader.AccountEmailAddressSecondary) &&
                !string.IsNullOrWhiteSpace(ConfigReader.AccountPasswordSecondary))
            {
                seLogin.SEOpenIDLogin(ConfigReader.AccountEmailAddressSecondary, ConfigReader.AccountPasswordSecondary);
                seLogin.SiteLogin("stackoverflow.com");
            }
        }

        private static void StartQuestionWatcherPool()
        {
            qChecker = new QuestionChecker(seLogin);
            qwPool = new QuestionWatcherPool(qChecker, seLogin);
            qwPool.OnException = ex => Console.WriteLine(ex);
            qwPool.NewReport = report => mainRoom.PostMessageLight(report);
            qwPool.HighErrorCountPerMinuteReached = errorsTotal =>
            {
                mainRoom.PostMessageLight("High internal error rate detected. Shutting down...");
                shutdownMre.Set();
            };
        }

        private static void JoinRooms()
        {
            chatClient = new Client(ConfigReader.AccountEmailAddressPrimary, ConfigReader.AccountPasswordPrimary);

            mainRoom = chatClient.JoinRoom(ConfigReader.RoomUrl, true);
            mainRoom.InitialisePrimaryContentOnly = true;
            mainRoom.StripMention = true;
            mainRoom.EventManager.ConnectListener(EventType.UserMentioned, new Action<Message>(HandleCommand));

            watchingRoom = chatClient.JoinRoom("http://chat.stackoverflow.com/rooms/90230/cv-request-graveyard", true);//("http://chat.stackoverflow.com/rooms/68414/socvr-testing-facility", true);//
            watchingRoom.InitialisePrimaryContentOnly = true;
            watchingRoom.EventManager.ConnectListener(EventType.MessageMovedIn, new Action<Message>(m =>
            {
                var id = messageFetcher.GetPostID(m);

                if (id > 0)
                {
                    qwPool.WatchPost(id, $"http://chat.{m.Host}/transcript/message/{m.ID}");
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
                mainRoom.PostMessageLight("Bye.");
                shutdownMre.Set();
            }
            else if (cmd == "INIT" && (msg.Author.IsRoomOwner ||
                     msg.Author.IsMod || msg.Author.ID == 2246344))
            {
                mainRoom.PostReplyLight(msg, "Initialising...");

                var ms = messageFetcher.GetRecentMessage(mainRoom, 500);

                foreach (var m in ms)
                {
                    if (m.Value >= 0)
                    {
                        qwPool.WatchPost(m.Value, $"http://chat.{m.Key.Host}/transcript/message/{m.Key.ID}");
                    }
                }

                mainRoom.PostReplyLight(msg, "Done.");
            }
            else if (cmd == "COMMANDS")
            {
                mainRoom.PostMessageLight("    commands ~ ~ ~ ~ ~ ~ ~ Prints this beautifully formatted message.\n" +
                                          "    stats  ~ ~ ~ ~ ~ ~ ~ ~ Displays the number of posts being watched.\n" +
                                          "    help ~ ~ ~ ~ ~ ~ ~ ~ ~ Pretty self-explanatory...\n" +
                                          "    alive  ~ ~ ~ ~ ~ ~ ~ ~ Checks if I'm still running.\n" +
                                          "    die  ~ ~ ~ ~ ~ ~ ~ ~ ~ I die a slow and painful death.");
            }
            else if (cmd == "STATS")
            {
                var watchingQs = qwPool.WatchedPosts;
                mainRoom.PostMessageLight($"I'm currently watching `{watchingQs}` post{(watchingQs == 1 ? "" : "s")}.");
            }
            else if (cmd.StartsWith("ALIVE"))
            {
                mainRoom.PostReplyLight(msg, "Yep.");
            }
            else if (cmd == "HELP")
            {
                mainRoom.PostReplyLight(msg, "I'm another chatbot who checks up on all your [tag:cv-pls] " +
                                            "requests to see if they warrant reopening (I'm not 100% accurate, so only take " +
                                            "my messages as *suggestions*). You can check out what I can do by using: " +
                                            "`commands`. My GH repo can be found " +
                                            "[here](https://github.com/SO-Close-Vote-Reviewers/GraveRobber).");
            }
            else if (cmd == "DIE")
            {
                mainRoom.PostReplyLight(msg, "You need to be a room owner or moderator. " +
                                             "Please contact your local Sam for further assistance.");
            }
        }
    }
}
