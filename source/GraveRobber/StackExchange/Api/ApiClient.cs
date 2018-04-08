using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using StackExchange.Net;

namespace GraveRobber.StackExchange.Api
{
	public class ApiClient
	{
		private const string configKeyPath = "StackExchange.API.Key";
		private const string configSitePath = "StackExchange.API.Site";
		private const string apiBase = "https://api.stackexchange.com/2.2";
		private const string site = "stackoverflow";
		private const string getQVotesFilter = "!*7PYFjZaY-6Fywr94JJhdvGGcWzs";
		private const string getRevsFilter = "!SWKA(o3c(mLvI6gCeF";
		private readonly string apiKey;

		public int QuotaRemaining { get; private set; } = -1;



		public ApiClient()
		{
			var key = ConfigAccessor.GetValue<string>(configKeyPath);

			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException($"'{configKeyPath}' (config file) cannot be null or empty.");
			}

			apiKey = key;

			// Initialise QuotaRemaining.
			GetQuestionVotes(1);
		}



		public Revision[] GetRevisions(int id)
		{
			var endpoint = EndpointBuilder($"posts/{id}/revisions", apiKey, site, getRevsFilter);
			var obj = GetJson(endpoint);

			if (obj == null)
			{
				return null;
			}

			var revs = new List<Revision>();

			foreach (var r in obj["items"])
			{
				var revSecs = r.Value<int?>("creation_date");
				var user = r.Value<JObject>("user");
				var userId = int.MinValue;

				if (user != null)
				{
					userId = user.Value<int?>("user_id") ?? int.MinValue;
				}

				var rev = new Revision
				{
					QuestionId = id,
					AuthorId = userId,
					CreatedAt = ParseJsonTime(revSecs),
					Body = r.Value<string>("body"),
				};

				if (string.IsNullOrEmpty(rev.Body))
				{
					continue;
				}

				revs.Add(rev);
			}

			return revs.ToArray();
		}

		public QuestionVotes GetQuestionVotes(int id)
		{
			var endpoint = EndpointBuilder($"questions/{id}", apiKey, site, getQVotesFilter);
			var obj = GetJson(endpoint);

			if (obj == null)
			{
				return null;
			}

			var data = obj["items"][0];
			var user = data.Value<JObject>("owner");
			var userId = int.MinValue;

			if (user != null)
			{
				userId = user.Value<int?>("user_id") ?? int.MinValue;
			}

			return new QuestionVotes
			{
				Id = id,
				AuthorId = userId,
				Up = data.Value<int>("up_vote_count"),
				Down = data.Value<int>("down_vote_count")
			};
		}



		private DateTime ParseJsonTime(int? secs)
		{
			var time = DateTime.MinValue;

			if (secs != null)
			{
				time = new DateTime(1970, 1, 1).AddSeconds(secs.Value);
			}

			return time;
		}

		private JObject GetJson(string endpoint)
		{
			var json = HttpRequest.Get(endpoint);

			var obj = JObject.Parse(json);

			QuotaRemaining = obj.Value<int>("quota_remaining");

			var wait = obj.Value<int?>("backoff");

			if (wait != null)
			{
				Thread.Sleep(wait.Value * 1000);

				return GetJson(endpoint);
			}

			if (obj["items"] == null)
			{
				var errorId = obj.Value<int>("error_id");

				throw new Exception($"Unable to fetch JSON from URL '{endpoint}'. Error code {errorId}.");
			}

			if (obj["items"].Count() == 0)
			{
				return null;
			}

			return obj;
		}

		private string EndpointBuilder(string endpoint, string key, string site, string filter)
		{
			return $"{apiBase}/{endpoint}?key={key}&site={site}&filter={filter}";
		}
	}
}
