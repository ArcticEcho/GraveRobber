using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraveRobber.StackExchange;
using GraveRobber.StackExchange.Api;
using GraveRobber.StackExchange.Chat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Auth;
using StackExchange.Chat.Actions;
using StackExchange.Net;

namespace GraveRobber
{
	public static class Program
	{
		private const string savedQsPath = "saved-qs.json";
		private static object lck;
		private static ManualResetEvent shutdownMre;
		private static Dictionary<DateTime, QuestionWatcher> qWatchers;
		private static ActionScheduler actionScheduler;

		public static int WatchedQuestions => qWatchers.Count;

		public static ApiClient apiClient { get; private set; }

		public static void Main(string[] args)
		{
			lck = new object();
			shutdownMre = new ManualResetEvent(false);
			qWatchers = new Dictionary<DateTime, QuestionWatcher>();

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

			LoadQs();

			Console.Write("done\n\nSetup complete. Press CTRL + C to quit.\n\n");
			actionScheduler.CreateMessage("GraveRobber started.");
			Console.CancelKeyPress += (o, e) => shutdownMre.Set();

			shutdownMre.WaitOne();

			Console.Write("\nStopping...\n\n");

			actionScheduler.Dispose();
			foreach (var qw in qWatchers)
			{
				qw.Value.Dispose();
			}
		}


		private static void LoadQs()
		{
			if (!File.Exists(savedQsPath))
			{
				return;
			}

			var json = File.ReadAllText(savedQsPath);

			var typeDef = new[]
			{
				new
				{
					Id = 0,
					Added = DateTime.MinValue
				}
			};

			var objs = JsonConvert.DeserializeAnonymousType(json, typeDef);

			qWatchers = objs.ToDictionary(x => x.Added, x =>
			{
				var qw = new QuestionWatcher(x.Id);

				qw.OnQuestionEdit += () => HandleQuestionEdit(x.Id);

				return qw;
			});
		}

		private static void SaveQs()
		{
			lock (lck)
			{
				var json = JsonConvert.SerializeObject(qWatchers.Select(x => new
				{
					Id = x.Value.Id,
					Added = x.Key
				}));

				File.WriteAllText(savedQsPath, json);
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

		private static void RemoveOldQuestions()
		{
			while (true)
			{
				if (shutdownMre.WaitOne(TimeSpan.FromMinutes(1)))
				{
					break;
				}

				var toDelete = qWatchers.Keys
					.Where(x => (DateTime.UtcNow - x).TotalDays > 30)
					.ToArray();

				foreach (var x in toDelete)
				{
					qWatchers.Remove(x);
				}

				SaveQs();
			}
		}

		private static void HandleNewCvpls(int qId)
		{
			if (qWatchers.Values.Any(x => x.Id == qId))
			{
				return;
			}

			var qw = new QuestionWatcher(qId);

			qw.OnQuestionEdit += () => HandleQuestionEdit(qId);

			qWatchers[DateTime.UtcNow] = qw;

			SaveQs();
		}

		private static void HandleQuestionEdit(int qId)
		{
			try
			{
				var addedAt = qWatchers.First(x => x.Value.Id == qId).Key;

				HandleEdit(qId, addedAt);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private static void HandleEdit(int qId, DateTime qWatcherAdded)
		{
			var revs = apiClient.GetRevisions(qId);

			if (revs == null) return;

			var revBeforeCvpls = revs
				.Where(x => x.CreatedAt < qWatcherAdded)
				.OrderByDescending(x => x.CreatedAt)
				.First();

			var latestRev = revs
				.OrderByDescending(x => x.CreatedAt)
				.First();

			var change = DamerauLevenshteinDistance.Calculate(revBeforeCvpls.Body, latestRev.Body);
			var threshold = ConfigAccessor.GetValue<double>("Threshold");

			if (change >= threshold)
			{
				var votes = apiClient.GetQuestionVotes(qId);

				ReportQuestion(votes, change);

				var qw = qWatchers[qWatcherAdded];

				qWatchers.Remove(qWatcherAdded);

				qw.Dispose();

				SaveQs();
			}
		}

		private static void ReportQuestion(QuestionVotes v, double diff)
		{
			var sb = new StringBuilder();
			var baseUrl = "https://stackoverflow.com";
			var revsLink = $"{baseUrl}/posts/{v.Id}/revisions";
			var qLink = $"{baseUrl}/q/{v.Id}";
			var percent = Math.Round(diff * 100);

			sb.Append($"[{percent}%]");
			sb.Append($"({revsLink}) ");
			sb.Append("changed: [question]");
			sb.Append($"({qLink}) ");
			sb.Append($"(+{v.Up}/-{v.Down})");

			actionScheduler.CreateMessage(sb.ToString());
		}
	}
}