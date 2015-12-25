/*
 * GraveRobber. A .NET PoC program for fetching data from the SOCVR graveyards.
 * Copyright © 2015, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.Collections.Generic;
using System.Threading;
using ServiceStack;
using ServiceStack.Text;
using WebSocketSharp;

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

            GC.SuppressFinalize(this);
        }



        private void StartSocket()
        {
            try
            {
                socket = new WebSocket("wss://qa.sockets.stackexchange.com");
                socket.OnOpen += (o, e) => socket.Send($"1-question-{ID}");
                socket.OnClose += (o, e) =>
                {
                    Thread.Sleep(5000);
                    if (!dispose) StartSocket();
                };
                socket.OnError += (o, e) => { if (OnException != null) OnException(e.Exception); };
                socket.OnMessage += (o, e) => HandleMessage(e.Data);
                socket.Connect();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                if (OnException != null) OnException(ex);
            }
        }

        private void HandleMessage(string msg)
        {
            try
            {
                var outter = JsonSerializer.DeserializeFromString<Dictionary<string, object>>(msg);
                var inner = JsonSerializer.DeserializeFromString<Dictionary<string, object>>((string)outter["data"]);

                if (inner.ContainsKey("a") && inner.ContainsKey("id") &&
                    (string)inner["a"] == "post-edit" && (string)inner["id"] == ID.ToString() &&
                    QuestionEdited != null)
                {
                    QuestionEdited();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"-----\nAHHH NO!!! I FOUND AN EXCEPTION!!!\n{ex}\nMESSAGE\n{msg}\n------");
            }
        }
    }
}
