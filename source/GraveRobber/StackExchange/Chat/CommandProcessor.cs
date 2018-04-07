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



		private void HandleMention(Message msg)
		{
			var cmd = pingRemover
				.Replace(msg.Text, "")
				.Trim()
				.ToUpperInvariant();

			if (cmd == "DIE" || cmd == "STOP")
			{
				Kill(msg);
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
				actionScheduler.CreateReply("Let me think about that for a moment...", msg);
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
				actionScheduler.CreateReply("I'm totally out of requests. :(", msg);
			}
			else
			{
				var reqs = Program.apiClient.QuotaRemaining.ToString("N0");

				actionScheduler.CreateReply($"I only have {reqs} requests left :/", msg);
			}
		}

		private void Kill(Message msg)
		{
			var pinger = new User(msg.Host, msg.AuthorId);

			if (pinger.IsModerator || pinger.Owns.Any(x => x.Id == roomWatcher.RoomId))
			{
				actionScheduler.CreateMessage("See you guys in the next timeline o/");
				OnKillRequest?.Invoke();
			}
			else
			{
				actionScheduler.CreateReply("Puny mortal, only room owners and moderators can kill me! :P", msg);
			}
		}

		private void PrintStats(Message msg)
		{
			var txt = $"I'm currently watching {Program.WatchedQuestions} question";

			if (Program.WatchedQuestions != 1)
			{
				txt += "s";
			}

			actionScheduler.CreateReply(txt + ".", msg);
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
				"I'm a chatbot that monitors [tag:cv-pls] requests to see if they " +
				"warrant reviewing. I post reports when a question receives a " +
				"non-trivial edit; where 'non-trivial' is defined by calculating " +
				"the Damerau-Levenshtein distance between revisions (hover over the " +
				"*x%* link for debug info). You can check out what I can do by " +
				"using: `commands`. My repository can be found " +
				"[here](https://github.com/SO-Close-Vote-Reviewers/GraveRobber).";

			actionScheduler.CreateMessage(txt);
		}
	}
}
