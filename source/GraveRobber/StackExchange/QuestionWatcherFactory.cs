using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraveRobber.StackExchange
{
	public class QuestionWatcherFactory : IDisposable
	{
		private class QueueItem
		{
			public int QuestionId;
			public Action<QuestionWatcher> Callback;
		}

		private AutoResetEvent waitMre;
		private Queue<QueueItem> queue;
		private int waitMs;
		private bool dispose;



		public QuestionWatcherFactory(int prodWaitMs = 3000)
		{
			waitMs = prodWaitMs;
			waitMre = new AutoResetEvent(false);
			queue = new Queue<QueueItem>();

			Task.Run(() => ProductionLoop());
		}

		~QuestionWatcherFactory()
		{
			Dispose();
		}



		public void Dispose()
		{
			if (dispose) return;
			dispose = true;

			waitMre.Set();
			waitMre.Dispose();

			GC.SuppressFinalize(this);
		}

		public QuestionWatcher Create(int qId)
		{
			var mre = new ManualResetEvent(false);

			QuestionWatcher questionWatcher = null;

			queue.Enqueue(new QueueItem
			{
				QuestionId = qId,
				Callback = new Action<QuestionWatcher>(qw =>
				{
					questionWatcher = qw;
					mre.Set();
				})
			});

			mre.WaitOne();

			return questionWatcher;
		}



		private void ProductionLoop()
		{
			while (!waitMre.WaitOne(waitMs))
			{
				if (queue.Count == 0) continue;

				var item = queue.Dequeue();
				var qw = TryCreate(item.QuestionId, waitMs);

				item.Callback?.Invoke(qw);
			}
		}

		private QuestionWatcher TryCreate(int id, int wait)
		{
			try
			{
				return new QuestionWatcher(id);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);

				return TryCreate(id, wait + 1000);
			}
		}
	}
}
