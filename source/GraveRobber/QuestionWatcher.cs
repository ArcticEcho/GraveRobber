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
using Jil;
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

            Console.Write($"\nINFO: Safely closed WebSocket {ID}.");

            GC.SuppressFinalize(this);
        }



        private void StartSocket()
        {
            var mre = new ManualResetEvent(false);

            try
            {
                socket = new WebSocket("wss://qa.sockets.stackexchange.com");
                socket.OnOpen += (o, e) =>
                {
                    socket.Send($"1-question-{ID}");
                    mre.Set();
                };
                socket.OnClose += (o, e) =>
                {
                    if (!dispose)
                    {
                        Console.Write($"\nWARNING: WebSocket {ID} has closed. Attempting to restart...");
                        Thread.Sleep(5000);
                        StartSocket();
                    }
                };
                socket.OnError += (o, e) =>
                {
                    Console.Write($"\nERROR: an exception was raised from WebSocket {ID}: {e.Message}");
                    OnException?.Invoke(e.Exception);
                };
                socket.OnMessage += (o, e) => HandleMessage(e.Data);
                socket.Log.Output = new Action<LogData, string>((l, d) => { });
                socket.Connect();
            }
            catch (Exception ex)
            {
                Console.Write($"\nERROR: an exception occurred while opening WebSocket {ID}: {ex.Message}");
                OnException?.Invoke(ex);
            }

            Thread.Sleep(3000);

            if ((socket?.ReadyState ?? WebSocketState.Closed) == WebSocketState.Open)
            {
                Console.Write($"\nINFO: successfully opened WebSocket {ID}.");
            }
            else
            {
                Console.Write($"\nWARNING: failed to open WebSocket {ID}.");
            }
        }

        private void HandleMessage(string msg)
        {
            try
            {
                var outter = JSON.Deserialize<Dictionary<string, object>>(msg);
                var inner = JSON.Deserialize<Dictionary<string, object>>(outter["data"].ToString());

                if (inner.ContainsKey("a") && inner.ContainsKey("id") &&
                    (string)inner["a"] == "post-edit" && (string)inner["id"] == ID.ToString() &&
                    QuestionEdited != null)
                {
                    QuestionEdited();
                }
            }
            catch (Exception ex)
            {
                Console.Write($"\nERROR: an exception occurred whilst handling event data from WebSocket {ID}: {ex.Message}");
            }
        }
    }
}
