using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StackExchange.Chat;
using StackExchange.Chat.Actions;
using StackExchange.Chat.Events;
using StackExchange.Chat.Events.User.Extensions;
using StackExchange.Net;
using StackExchange.Net.WebSockets;

namespace GraveRobber.StackExchange.Chat
{
	public class CommandProcessor
	{
		private ActionScheduler actionScheduler;
		private RoomWatcher<DefaultWebSocket> roomWatcher;
		private Regex pingRemover;

		public event Action OnKillRequest;



		public CommandProcessor(IEnumerable<Cookie> authCookies, string roomUrl)
		{
			pingRemover = new Regex("@\\S{2,}\\s?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
			actionScheduler = new ActionScheduler(authCookies, roomUrl);
			roomWatcher = new RoomWatcher<DefaultWebSocket>(authCookies, roomUrl);

			roomWatcher.WebSocket.OnError += ex => Console.WriteLine(ex);
			roomWatcher.AddUserMentionedEventHandler(HandleMention);
		}



		private void HandleMention(Message msg, User pinger)
		{
			var cmd = pingRemover
				.Replace(msg.Text, "")
				.Trim()
				.ToUpperInvariant();

			if (cmd == "DIE" || cmd == "STOP")
			{
				Kill(msg, pinger);
			}
			else if (cmd == "STATS")
			{
				PrintStats(msg);
			}
			else if (cmd == "COMMANDS")
			{
				PrintCommands(msg);
			}
			else if (cmd == "HELP")
			{
				PrintHelp(msg);
			}
			else if (cmd == "ALIVE")
			{
				actionScheduler.CreateMessage($":{msg.Id} Let me think about that for a moment...");
			}
			else if (cmd == "QUOTA")
			{
				PrintQuota(msg);
			}
		}

		private void PrintQuota(Message msg)
		{
			if (Program.apiClient.QuotaRemaining < 1)
			{
				actionScheduler.CreateMessage($":{msg.Id} I'm totally out of requests. :(");
			}
			else
			{
				var reqs = Program.apiClient.QuotaRemaining.ToString("N0");

				actionScheduler.CreateMessage($":{msg.Id} I only have {reqs} requests left :/");
			}
		}

		private void Kill(Message msg, User pinger)
		{
			if (pinger.IsModerator || pinger.Owns.Any(x => x.Id == roomWatcher.RoomId))
			{
				actionScheduler.CreateMessage("See you guys in the next timeline o/");
				OnKillRequest?.Invoke();
			}
			else
			{
				actionScheduler.CreateMessage($":{msg.Id} Puny mortal, only room owners and moderators can kill me! :P");
			}
		}

		private void PrintStats(Message msg)
		{
			var txt = $":{msg.Id} I'm currently watching {Program.WatchedQuestions} question";

			if (Program.WatchedQuestions != 1)
			{
				txt += "s";
			}

			actionScheduler.CreateMessage(txt + ".");
		}

		private void PrintCommands(Message msg)
		{
			var txt =
				"    commands       - Prints this beautifully formatted message.\n" +
				"    stats          - Prints how many questions I'm watching.\n" +
				"    opt-in         - Coming soon...\n" +
				"    opt-out        - Coming soon...\n" +
				"    watch <id/url> - Coming soon...\n" +
				"    help           - A little story about what I do.\n" +
				"    alive          - Checks if I'm still up and running.\n" +
				"    quota          - Prints how many API requests I have left.\n" +
				"    die/stop       - A slow and painful death await...";

			actionScheduler.CreateMessage(txt);
		}

		private void PrintHelp(Message msg)
		{
			var txt = $":{msg.Id} " +
				"I'm a chatbot who checks up on all your [tag:cv-pls] " +
				"requests to see if they warrant reopening (I'm not 100% accurate, so only take " +
				"my reports as *suggestions*). You can check out what I can do by using: " +
				"`commands`. My GH repo can be found " +
				"[here](https://github.com/SO-Close-Vote-Reviewers/GraveRobber).";

			actionScheduler.CreateMessage(txt);
		}
	}
}
