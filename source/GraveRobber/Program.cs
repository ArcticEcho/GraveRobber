using System;
using System.Collections.Generic;
using System.Linq;
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
		private static QuestionWatcherFactory qwFactory;
		private static ActionScheduler actionScheduler;

		public static bool IsStillLoading { get; private set; } = true;

		public static int WatchedQuestions => watchers.Count;

		public static ApiClient ApiClient { get; private set; }



		public static void Main(string[] args)
		{
			shutdownMre = new ManualResetEvent(false);
			watchers = new HashSet<QuestionWatcher>();
			qwFactory = new QuestionWatcherFactory();

			Console.Write("Initialising SE API client...");

			ApiClient = new ApiClient();

			Console.Write("done\nAuthenticating with SE...");

			var roomUrl = ConfigAccessor.GetValue<string>("StackExchange.Chat.RoomUrl");
			var cookies = Login(roomUrl);

			Console.Write("done\nStarting cv-pls monitor...");

			var cvWatcher = new CloseRequestWatcher(cookies);
			cvWatcher.OnNewRequest += HandleNewCvpls;
			Task.Run(() => RemoveOldQuestionsLoop(false));

			Console.Write("done\nInitialising chat command processor...");

			var cmdPro = new CommandProcessor(cookies, roomUrl);
			cmdPro.OnKillRequest += () => shutdownMre.Set();

			Console.Write("done\nInitialising report poster...");

			actionScheduler = new ActionScheduler(cookies, roomUrl);

			Console.Write("done\n\nSetup complete. Press CTRL + C to quit.\n\n");

			// load old requests in the background
			Task.Run(() =>
			{
				try
				{
					RemoveOldQuestionsLoop(true);
					InitialiseWatchers();
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}

				IsStillLoading = false;
			});

			actionScheduler.CreateMessage("GraveRobber started.");
			Console.CancelKeyPress += (o, e) => shutdownMre.Set();

			shutdownMre.WaitOne();

			Console.Write("\nStopping...\n\n");
		}



		private static void InitialiseWatchers()
		{
			var reqs = CloseRequestStore.Requests.ToArray();

			foreach (var r in reqs)
			{
				// This loop could be running for a while,
				// so let's double check that the request
				// is still alive.
				if (CloseRequestStore.Requests.All(y => r.QuestionId != y.QuestionId))
				{
					continue;
				}

				var qw = qwFactory.Create(r.QuestionId);

				qw.OnQuestionEdit += () => HandleQuestionEdit(r.QuestionId);

				lock (watchers)
				{
					watchers.Add(qw);
				}
			}
		}

		private static IEnumerable<Cookie> Login(string roomUrl)
		{
			var email = ConfigAccessor.GetValue<string>("StackExchange.Chat.Email");
			var password = ConfigAccessor.GetValue<string>("StackExchange.Chat.Password");
			var host = roomUrl.Split('/')[2].Replace("chat.", "");

			var auth = new EmailAuthenticationProvider(email, password);

			return auth.GetAuthCookies(host);
		}

		private static void RemoveOldQuestionsLoop(bool runOnce)
		{
			while (true)
			{
				try
				{
					var ttl = ConfigAccessor.GetValue<double>("MaxWatchTimeDays");
					var toDeleteIds = new HashSet<int>(CloseRequestStore.Requests
						.Where(x => (DateTime.UtcNow - x.RequestedAt).TotalDays > ttl)
						.Select(x => x.QuestionId)
					);

					foreach (var qId in toDeleteIds)
					{
						CloseRequestStore.Remove(qId);

						lock (watchers)
						{
							var qw = watchers.FirstOrDefault(x => x.Id == qId);

							if (qw == null) continue;

							watchers.Remove(qw);

							qw.Dispose();
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}

				if (runOnce || shutdownMre.WaitOne(TimeSpan.FromMinutes(1)))
				{
					return;
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

			var qw = qwFactory.Create(questionId);

			qw.OnQuestionEdit += () => HandleQuestionEdit(questionId);

			lock (watchers)
			{
				watchers.Add(qw);
			}
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
			var revs = ApiClient.GetRevisions(req.QuestionId);

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
				PostNewReport(req, diff, latestRev);
			}
		}

		private static void PostNewReport(CloseRequest req, EditModel diff, Revision latestRev)
		{
			var votes = ApiClient.GetQuestionVotes(req.QuestionId);
			var editedByOp = latestRev.AuthorId == votes.AuthorId
				&& latestRev.AuthorId != int.MinValue
				&& votes.AuthorId != int.MinValue;

			var reportTxt = ReportBuilder.Build(req, votes, diff, editedByOp);

			actionScheduler.CreateMessage(reportTxt);

			lock (watchers)
			using (var qw = watchers.FirstOrDefault(x => x.Id == req.QuestionId))
			{
				if (qw != null)
				{
					watchers.Remove(qw);
				}
			}

			CloseRequestStore.Remove(req.QuestionId);
		}
	}
}