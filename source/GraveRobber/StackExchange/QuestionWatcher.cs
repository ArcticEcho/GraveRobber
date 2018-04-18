using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using StackExchange.Net.WebSockets;

namespace GraveRobber.StackExchange
{
	public class QuestionWatcher : IDisposable
	{
		private DefaultWebSocket ws;
		private bool dispose;

		public int Id { get; private set; }

		public event Action OnQuestionEdit;



		public QuestionWatcher(int questionId)
		{
			Id = questionId;

			Init();
		}

		~QuestionWatcher()
		{
			Dispose();
		}



		public void Dispose()
		{
			if (dispose) return;
			dispose = true;

			ws.Dispose();

			OnQuestionEdit = null;

			GC.SuppressFinalize(this);
		}



		private void Init(bool isRestart = false)
		{
			ws = new DefaultWebSocket();

			ws.OnError += ex =>
			{
				Console.WriteLine(ex);

				Init(true);
			};
			ws.OnTextMessage += HandleNewMessage;
			ws.OnClose += () => Init(true);

			if (isRestart)
			{
				// Wait a bit before retrying.
				Thread.Sleep(3000);
			}

			ws.Connect("wss://qa.sockets.stackexchange.com");
			ws.Send($"1-question-{Id}");
		}

		private void HandleNewMessage(string message)
		{
			var data = JObject.Parse(message)?.Value<string>("data");

			if (string.IsNullOrEmpty(data))
			{
				return;
			}

			if (data == "hb")
			{
				ws.Send("{\"action\":\"hb\",\"data\":\"hb\"}");
				return;
			}

			var innerData = JObject.Parse(data);
			var a = innerData.Value<string>("a");
			var id = innerData.Value<int>("id");

			if (a == "post-edit" && id == Id)
			{
				OnQuestionEdit?.Invoke();
			}
		}
	}
}
