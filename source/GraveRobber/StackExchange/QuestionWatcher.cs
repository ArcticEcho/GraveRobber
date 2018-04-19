using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using StackExchange.Net.WebSockets;

namespace GraveRobber.StackExchange
{
	public class QuestionWatcher : IDisposable
	{
		private DefaultWebSocket ws;
		private bool restartPending;
		private bool dispose;

		internal Action<QuestionWatcher> WebsocketRestartCallback { get; set; }

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



		internal bool Init()
		{
			try
			{
				ws = new DefaultWebSocket();

				ws.OnError += ex =>
				{
					Console.WriteLine(ex);

					InvokeRestartCallback();
				};
				ws.OnTextMessage += HandleNewMessage;
				ws.OnClose += InvokeRestartCallback;

				ws.Connect("wss://qa.sockets.stackexchange.com");
				ws.Send($"1-question-{Id}");

				restartPending = false;

				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);

				return false;
			}
		}

		private void InvokeRestartCallback()
		{
			if (restartPending) return;
			restartPending = true;

			ws?.Dispose();

			WebsocketRestartCallback?.Invoke(this);
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
