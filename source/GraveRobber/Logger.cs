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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Text;

namespace GraveRobber
{
    /// <summary>
    /// Stolen and modified from the Phamhilator project.
    /// </summary>
    public partial class Logger<T> : IEnumerable<T>, IDisposable
    {
        private readonly ManualResetEvent itemRemoverMre = new ManualResetEvent(false);
        private readonly HashSet<T> removeItemsQueue = new HashSet<T>();
        private readonly object lockObj = new object();
        private readonly string logPath;
        private bool dispose;

        public TimeSpan FlushRate { get; }

        public int Count { get; private set; }

        public Action<T> ItemRemovedEvent { get; set; }

        internal Action CollectionCheckedEvent { get; set; }



        public Logger(string logFileName, TimeSpan? flushRate = null)
        {
            FlushRate = flushRate ?? TimeSpan.FromMinutes(60);
            logPath = logFileName;

            if (!File.Exists(logFileName))
            {
                File.Create(logFileName).Dispose();
            }

            InitialiseCount();

            Task.Run(() => RemoveItems());
        }

        ~Logger()
        {
            Dispose();
        }



        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            itemRemoverMre.Set();

            GC.SuppressFinalize(this);
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (lockObj)
            {
                var lines = File.ReadLines(logPath);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var entry = JsonSerializer.DeserializeFromString<Entry>(line);
                    var data = (T)entry.Data;

                    if (removeItemsQueue.Contains(data)) continue;

                    yield return data;
                }
            }
        }

        public void EnqueueItem(T item)
        {
            var entry = new Entry
            {
                Data = item,
                Timestamp = DateTime.UtcNow
            };
            var json = JsonSerializer.SerializeToString(entry);

            lock (lockObj)
            {
                File.AppendAllLines(logPath, new[] { json });

                Count++;
            }
        }

        public void EnqueueItems(IEnumerable<T> items)
        {
            lock (lockObj)
            {
                foreach (var item in items)
                {
                    var entry = new Entry
                    {
                        Data = item,
                        Timestamp = DateTime.UtcNow
                    };
                    var json = JsonSerializer.SerializeToString(entry);

                    File.AppendAllLines(logPath, new[] { json });

                    Count++;
                }
            }
        }

        public void RemoveItem(T item)
        {
            if (removeItemsQueue.Contains(item))
            {
                throw new ArgumentException("This item is already queued for removal.", "item");
            }

            lock (lockObj)
            {
                removeItemsQueue.Add(item);
                Count--;
            }
        }

        public void ClearLog()
        {
            lock (lockObj)
            {
                File.WriteAllText(logPath, "");
                removeItemsQueue.Clear();
                Count = 0;
            }
        }



        private void InitialiseCount()
        {
            if (!File.Exists(logPath))
            {
                File.Create(logPath).Dispose();
            }
            else
            {
                lock (lockObj)
                {
                    var lines = File.ReadLines(logPath);

                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line)) Count++;
                    }
                }
            }
        }

        private void RemoveItems()
        {
            while (!dispose)
            {
                itemRemoverMre.WaitOne(FlushRate);

                if (removeItemsQueue.Count > 0)
                {
                    lock (lockObj)
                    {
                        var lines = File.ReadLines(logPath);
                        var temp = Path.GetTempFileName();

                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            var entry = JsonSerializer.DeserializeFromString<Entry>(line);
                            var data = (T)entry.Data;

                            if (!removeItemsQueue.Contains(data))
                            {
                                File.AppendAllLines(temp, new[] { line });
                            }
                            else
                            {
                                removeItemsQueue.Remove(data);
                            }
                        }

                        File.Delete(logPath);
                        File.Move(temp, logPath);
                    }
                }

                if (CollectionCheckedEvent != null)
                {
                    Task.Run(CollectionCheckedEvent);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}