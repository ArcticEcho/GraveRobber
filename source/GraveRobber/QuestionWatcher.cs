using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Text;
using WebSocket4Net; // Let's try using a stable ws lib (if all goes well, I'll port this to CE.NET).

namespace GraveRobber
{
    public class QuestionWatcher : IDisposable
    {
        private bool dispose;
        private WebSocket socket;

        public int ID { get; private set; }

        public Action<Exception> OnException { get; set; }

        public Action QuestionEdited { get; set; }



        public QuestionWatcher(int questionID)
        {
            if (questionID < 0) throw new ArgumentOutOfRangeException("questionID", "'questionID' must be a positive number.");

            ID = questionID;

            StartSocket();
        }



        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            socket.Close();
            socket.Dispose();

            GC.SuppressFinalize(this);
        }



        private void StartSocket()
        {
            socket = new WebSocket("wss://qa.sockets.stackexchange.com");
            socket.Opened += (o, e) => socket.Send($"1-question-{ID}");
            socket.Closed += (o, e) => { if (!dispose) StartSocket(); };
            socket.MessageReceived += (o, e) => HandleMessage(e.Message);
            socket.Error += (o, e) => { if (OnException != null) OnException(e.Exception); };
            socket.Open();
        }

        private void HandleMessage(string msg)
        {
            try
            {
                var outter = JsonSerializer.DeserializeFromString<Dictionary<string, object>>(msg);
                var inner = DynamicJson.Deserialize((string)outter["data"]);

                if (inner.a == "post-edit" && inner.id == ID.ToString() && QuestionEdited != null)
                {
                    QuestionEdited();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
