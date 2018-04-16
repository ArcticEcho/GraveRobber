using System;
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

			GC.SuppressFinalize(this);
		}



		private void Init()
		{
			ws = new DefaultWebSocket();

			ws.OnError += ex =>
			{
				Console.WriteLine(ex);

				Init();
			};
			ws.OnTextMessage += HandleNewMessage;
			ws.OnClose += () => Init();

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
