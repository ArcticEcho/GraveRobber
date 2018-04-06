using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace GraveRobber
{
	public class CloseRequest
	{
		public int QuestionId { get; set; }

		public int AuthorId { get; set; }

		public int MessageId { get; set; }

		public DateTime RequestedAt { get; set; }
	}

	public static class CloseRequestStore
	{
		private const string file = "close-requests.json";
		private readonly static object lck;


		public static HashSet<CloseRequest> Requests { get; private set; }



		static CloseRequestStore()
		{
			Requests = LoadReqs();
			lck = new object();
		}



		public static void Add(CloseRequest req)
		{
			lock (lck)
			{
				Requests.Add(req);

				var json = JsonConvert.SerializeObject(Requests);

				File.WriteAllText(file, json);
			}
		}

		public static void Remove(int questionId)
		{
			var req = Requests.FirstOrDefault(x => x.QuestionId == questionId);

			Remove(req);
		}

		public static void Remove(CloseRequest req)
		{
			if (req == null)
			{
				return;
			}

			lock (lck)
			{
				Requests.Remove(req);

				var json = JsonConvert.SerializeObject(Requests);

				File.WriteAllText(file, json);
			}
		}



		private static HashSet<CloseRequest> LoadReqs()
		{
			if (!File.Exists(file))
			{
				return new HashSet<CloseRequest>();
			}

			var json = File.ReadAllText(file);

			return JsonConvert.DeserializeObject<HashSet<CloseRequest>>(json);
		}
	}
}
