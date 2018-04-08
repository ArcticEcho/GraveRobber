using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraveRobber.Edit;
using GraveRobber.StackExchange;
using GraveRobber.StackExchange.Api;
using GraveRobber.StackExchange.Chat;
using StackExchange.Auth;
using StackExchange.Chat;
using StackExchange.Chat.Actions;
using StackExchange.Net;

namespace GraveRobber
{
	public static class Program
	{
		private static ManualResetEvent shutdownMre;
		private static HashSet<QuestionWatcher> watchers;
		private static ActionScheduler actionScheduler;

		public static int WatchedQuestions => watchers.Count;

		public static ApiClient apiClient { get; private set; }

		public static void Main(string[] args)
		{
			shutdownMre = new ManualResetEvent(false);
			watchers = new HashSet<QuestionWatcher>();

			Console.Write("Initialising SE API client...");

			apiClient = new ApiClient();

			Console.Write("done\nAuthenticating with SE...");

			var roomUrl = ConfigAccessor.GetValue<string>("StackExchange.Chat.RoomUrl");
			var cookies = Login(roomUrl);

			Console.Write("done\nStarting cv-pls monitor...");

			var cvWatcher = new CloseRequestWatcher(cookies);
			cvWatcher.OnNewRequest += HandleNewCvpls;
			Task.Run(() => RemoveOldQuestions());

			Console.Write("done\nInitialising chat command processor...");

			var cmdPro = new CommandProcessor(cookies, roomUrl);
			cmdPro.OnKillRequest += () => shutdownMre.Set();

			Console.Write("done\nInitialising report poster...");

			actionScheduler = new ActionScheduler(cookies, roomUrl);

			Console.Write("done\nLoading watched questions...");

			InitialiseWatchers();

			Console.Write("done\n\nSetup complete. Press CTRL + C to quit.\n\n");
			actionScheduler.CreateMessage("GraveRobber started.");
			Console.CancelKeyPress += (o, e) => shutdownMre.Set();

			shutdownMre.WaitOne();

			Console.Write("\nStopping...\n\n");

			actionScheduler.Dispose();
			foreach (var qw in watchers)
			{
				qw.Dispose();
			}
		}


		private static void InitialiseWatchers()
		{
			var reqs = CloseRequestStore.Requests.ToArray();

			watchers = new HashSet<QuestionWatcher>(reqs.Select(x =>
			{
				var qw = new QuestionWatcher(x.QuestionId);

				qw.OnQuestionEdit += () => HandleQuestionEdit(x.QuestionId);

				return qw;
			}));
		}

		private static IEnumerable<Cookie> Login(string roomUrl)
		{
			var email = ConfigAccessor.GetValue<string>("StackExchange.Chat.Email");
			var password = ConfigAccessor.GetValue<string>("StackExchange.Chat.Password");
			var host = roomUrl.Split('/')[2].Replace("chat.", "");

			var auth = new EmailAuthenticationProvider(email, password);

			return auth.GetAuthCookies(host);
		}

		private static void RemoveOldQuestions()
		{
			while (true)
			{
				if (shutdownMre.WaitOne(TimeSpan.FromMinutes(1)))
				{
					break;
				}

				var toDeleteIds = new HashSet<int>(CloseRequestStore.Requests
					.Where(x => (DateTime.UtcNow - x.RequestedAt).TotalDays > 30)
					.Select(x => x.QuestionId)
				);

				foreach (var qId in toDeleteIds)
				{
					var qw = watchers.FirstOrDefault(x => x.Id == qId);

					watchers.Remove(qw);

					qw.Dispose();

					CloseRequestStore.Remove(qId);
				}

			}
		}

		private static void HandleNewCvpls(Message msg, int questionId)
		{
			if (CloseRequestStore.Requests.Any(x => x.QuestionId == questionId))
			{
				return;
			}

			CloseRequestStore.Add(new CloseRequest
			{
				QuestionId = questionId,
				AuthorId = msg.AuthorId,
				MessageId = msg.Id,
				RequestedAt = DateTime.UtcNow
			});

			var qw = new QuestionWatcher(questionId);

			qw.OnQuestionEdit += () => HandleQuestionEdit(questionId);

			watchers.Add(qw);
		}

		private static void HandleQuestionEdit(int qId)
		{
			try
			{
				var req = CloseRequestStore.Requests.First(x => x.QuestionId == qId);

				HandleEdit(req);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private static void HandleEdit(CloseRequest req)
		{
			var revs = apiClient.GetRevisions(req.QuestionId);

			if ((revs?.Length ?? 0) < 2) return;

			var revBeforeCvpls = revs
				.Where(x => x.CreatedAt < req.RequestedAt)
				.OrderByDescending(x => x.CreatedAt)
				.First();

			var latestRev = revs
				.OrderByDescending(x => x.CreatedAt)
				.First();

			var diff = EditClassifier.Classify(revBeforeCvpls.Body, latestRev.Body);
			var threshold = ConfigAccessor.GetValue<double>("Threshold");

			if (diff.AdjustedNormalised >= threshold)
			{
				var votes = apiClient.GetQuestionVotes(req.QuestionId);
				var editedByOp = latestRev.AuthorId == votes.AuthorId
					&& latestRev.AuthorId != int.MinValue
					&& votes.AuthorId != int.MinValue;

				ReportQuestion(req, votes, diff, editedByOp);

				var qw = watchers.First(x => x.Id == req.QuestionId);

				watchers.Remove(qw);

				qw.Dispose();

				CloseRequestStore.Remove(req.QuestionId);
			}
		}

		private static void ReportQuestion(CloseRequest req, QuestionVotes v, EditModel edit, bool editByOp)
		{
			var sb = new StringBuilder();
			var baseUrl = "https://stackoverflow.com";
			var revsLink = $"{baseUrl}/posts/{v.Id}/revisions";
			var qLink = $"{baseUrl}/q/{v.Id}";
			var msgLink = $"https://chat.stackoverflow.com/transcript/message/{req.MessageId}";
			var author = new User("chat.stackoverflow.com", req.AuthorId);
			var ping = $"@{author.Username.Replace(" ", "").Trim()}";

			sb.Append($"[{edit.NormalisedPretty}]");
			sb.Append($"({revsLink} ");
			sb.Append($"\"Adjusted: {edit.AdjustedNormalisedPretty}. ");
			sb.Append($"Distance: {edit.DistancePretty}.\") ");
			sb.Append("changed");
			sb.Append($", {edit.CodePretty} code");
			sb.Append($", {edit.FormattingPretty} formatting");
			sb.Append(editByOp ? " (by OP)" : "");
			sb.Append(": [question]");
			sb.Append($"({qLink}) ");
			sb.Append($"(+{v.Up}/-{v.Down}) ");
			sb.Append($" - [req]");
			sb.Append($"({msgLink}) ");
			sb.Append(ping);

			actionScheduler.CreateMessage(sb.ToString());
		}
	}
}