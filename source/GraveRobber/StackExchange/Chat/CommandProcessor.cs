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
			else if (cmd == "REPORT HELP")
			{
				PrintReportHelp(msg);
			}
			else if (cmd == "ALIVE")
			{
				actionScheduler.CreateReply("Let me think about that for a moment...", msg);
			}
			else if (cmd == "QUOTA")
			{
				PrintQuota(msg);
			}
			else if (cmd == "OPT-OUT" || cmd == "OPTOUT" || cmd == "OPT OUT")
			{
				OptOut(msg);
			}
			else if (cmd == "OPT-IN" || cmd == "OPTIN" || cmd == "OPT IN")
			{
				OptIn(msg);
			}
		}

		private void OptOut(Message msg)
		{
			string txt;

			if (!IgnoreList.Ids.Contains(msg.AuthorId))
			{
				IgnoreList.Add(msg.AuthorId);

				txt = "I will no longer automatically notify you of edited " +
					"questions that you have [tag:cv-pls]'d.";
			}
			else
			{
				txt = "You're already opted out, duh.";
			}

			actionScheduler.CreateReply(txt, msg);
		}

		private void OptIn(Message msg)
		{
			string txt;

			if (IgnoreList.Ids.Contains(msg.AuthorId))
			{
				IgnoreList.Remove(msg.AuthorId);

				txt = "I will now automatically notify you of edited " +
					"questions that you have [tag:cv-pls]'d.";
			}
			else
			{
				txt = "You're already opted in, duh.";
			}

			actionScheduler.CreateReply(txt, msg);
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
				actionScheduler.CreateReply("You must be a room owner or moderator to kill me. " +
					"Please contact your local Sam for further assistance.", msg);
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
				"    opt-in         - I'll ping you when a question that you've cv-pls'd gets edited.\n" +
				"    opt-out        - I'll no longer ping you for edited questions that you have cv-pls'd.\n" +
				"    watch <id/url> - Coming soon...\n" +
				"    help           - A little story about what I do.\n" +
				"    report help    - Prints an explanation on what my reports mean.\n" +
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
				"the Damerau-Levenshtein distance between revisions. You can " +
				"check out what I can do by using: `commands`. My repository " +
				"can be found " +
				"[here](https://github.com/SO-Close-Vote-Reviewers/GraveRobber).";

			actionScheduler.CreateMessage(txt);
		}

		private void PrintReportHelp(Message msg)
		{
			var header = $":{msg.Id} The following is an example request broken down and explained in " +
				$"detail: *[33%](http://example.com \"Adjusted 26%. Distance 98.\") changed, +40% code, " +
				$"-100% formatting (by OP): [question](http://example.com) - [req](http://example.com) @Username*";

			var body = 
				"    A report is a comparison of a question's current state (revision) to its revision before a close request was issued for it.\n    \n" +
				"    '33% changed'      - How much the question has changed overall. Clicking the link will take you to the history of the question's revisions. (Hovering over the link will display extra debug info.)\n" +
				"    '+40% code'        - The change in the amount of code. In this example, it now contains 40% more code.\n" +
				"    '-100% formatting' - The change in how much formatted text (excluding code blocks) the question has. This example says the question now has no formatted text.\n" +
				"    '(by OP)'          - The question was edited by the Original Poster (the author of the question).\n" +
				"    'question'         - A link back to the question.\n" +
				"    '(+3/-1)'          - A break-down of the votes on the question. In this case, it has 3 downvotes and 1 upvote.\n" +
				"    'req'              - A link back to the close request.\n" +
				"    '@Username'        - Notifies ('pings') the author of the close request.";

			actionScheduler.CreateMessage(header);
			actionScheduler.CreateMessage(body);
		}
	}
}
