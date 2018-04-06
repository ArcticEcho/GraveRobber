using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using StackExchange.Net.WebSockets;

namespace GraveRobber.StackExchange
{
	public class QuestionWatcher : IDisposable
	{
		private readonly DefaultWebSocket ws;
		private bool dispose;

		public int Id { get; private set; }

		public event Action OnQuestionEdit;



		public QuestionWatcher(int questionId)
		{
			Id = questionId;
			ws = new DefaultWebSocket();

			ws.OnError += ex => Console.WriteLine(ex);
			ws.OnTextMessage += HandleNewMessage;

			ws.Connect("wss://qa.sockets.stackexchange.com");
			ws.Send($"1-question-{questionId}");
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



		private void HandleNewMessage(string message)
		{
			var data = JObject.Parse(message)?.Value<string>("data");

			if (string.IsNullOrEmpty(data))
			{
				return;
			}

			if (data == "hb")
			{
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
