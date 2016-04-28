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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Text;
using static GraveRobber.QuestionChecker;

namespace GraveRobber
{
    public class QuestionProcessor : IDisposable
    {
        private readonly ManualResetEvent grimReaperMre;
        private readonly ConcurrentDictionary<string, QuestionWatcher> watchers;
        private readonly ConcurrentQueue<KeyValuePair<string, string>> queuedUrls;
        private readonly Logger<QueuedQuestion> watchedPosts;
        private readonly SELogin seLogin;
        private bool dispose;

        public int WatchedPosts => watchers?.Count ?? 0;

        public Action<QuestionStatus> PostFound { get; set; }

        public Action<Exception> SeriousDamnHappened { get; set; }



        public QuestionProcessor(SELogin login, string dataFilesDir = null)
        {
            seLogin = login;
            watchers = new ConcurrentDictionary<string, QuestionWatcher>();
            queuedUrls = new ConcurrentQueue<KeyValuePair<string, string>>();
            grimReaperMre = new ManualResetEvent(false);

            var wpPath = "watched-posts.txt";
            var pprPath = "posts-pending-review.txt";

            if (!string.IsNullOrWhiteSpace(dataFilesDir) && Directory.Exists(dataFilesDir))
            {
                wpPath = Path.Combine(dataFilesDir, wpPath);
                pprPath = Path.Combine(dataFilesDir, pprPath);
            }

            // Queued posts to check back on later.
            watchedPosts = new Logger<QueuedQuestion>(wpPath);
            Task.Run(() =>
            {
                foreach (var q in watchedPosts)
                {
                    Thread.Sleep(5000);

                    var qs = GetQuestionStatus(q.Url, seLogin);

                    if (qs?.CloseDate == null) continue;

                    var id = -1;
                    var url = TrimUrl(q.Url, out id);
                    watchers[url] = CreateWatcher(url, id);
                }
            });

            Task.Run(() => ProcessNewUrlsQueue());
            Task.Run(() => GrimReaper());
        }

        ~QuestionProcessor()
        {
            Dispose();
        }



        public void WatchPost(string url, string cvplsReqUrl)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("'url' must not be null, empty, or entirely whitespace.", "url");
            }

            queuedUrls.Enqueue(new KeyValuePair<string, string>(url, cvplsReqUrl));
        }

        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            grimReaperMre.Set();

            foreach (var w in watchers.Values)
            {
                Task.Run(() => w.Dispose());
            }

            grimReaperMre.Dispose();
            watchedPosts.Dispose();

            GC.SuppressFinalize(this);
        }



        private void GrimReaper()
        {
            var qqsToRemove = new HashSet<QueuedQuestion>();
            var wc = new WebClient();

            while (!dispose)
            {
                foreach (var q in watchedPosts)
                {
                    if (dispose) break;

                    if ((DateTime.UtcNow - q.CloseDate).TotalDays > 30)
                    {
                        qqsToRemove.Add(q);
                        continue;
                    }

                    grimReaperMre.WaitOne(TimeSpan.FromSeconds(15));

                    var id = -1;
                    TrimUrl(q.Url, out id);

                    try
                    {
                        wc.DownloadString($"http://stackoverflow.com/posts/ajax-load-realtime/{id}");
                    }
                    catch // Something bad happened, that's all we need to know.
                    {
                        qqsToRemove.Add(q);
                    }
                }

                foreach (var q in qqsToRemove)
                {
                    RemoveWatchedPost(q.Url);
                }
            }
        }

        private void ProcessNewUrlsQueue()
        {
            var kv = new KeyValuePair<string, string>();

            while (!dispose)
            {
                try
                {
                    Thread.Sleep(5000);

                    if (dispose || queuedUrls.Count == 0) continue;

                    queuedUrls.TryDequeue(out kv);
                    var qId = -1;
                    var qUrl = TrimUrl(kv.Key, out qId);

                    if (watchedPosts.Any(x => x.Url == qUrl)) continue;

                    var qs = GetQuestionStatus(kv.Key, seLogin);

                    // Ignore the post as it is either open or deleted.
                    if (qs?.CloseDate == null) continue;

                    qs.CloseReqMessage = kv.Value;

                    if (QSMatchesCriteria(qs))
                    {
                        HandleEditedQuestion(qs);
                    }
                    else
                    {
                        watchedPosts.EnqueueItem(new QueuedQuestion
                        {
                            Url = qUrl,
                            CloseDate = (DateTime)qs?.CloseDate,
                            CloseReqMessage = kv.Value
                        });
                        watchers[qUrl] = CreateWatcher(qUrl, qId);
                    }
                }
                catch (Exception ex)
                {
                    Console.Write($"\nERROR: an exception was thrown while processing new CV-PLS messages: {ex.Message}");
                }
            }
        }

        private QuestionWatcher CreateWatcher(string url, int id)
        {
            return new QuestionWatcher(id)
            {
                OnException = ex => SeriousDamnHappened?.Invoke(ex),
                QuestionEdited = () =>
                {
                    var status = GetQuestionStatus(url, seLogin);

                    if (QSMatchesCriteria(status))
                    {
                        Console.Write("\nINFO: post " + status.Url + " was edited and reported.");
                        HandleEditedQuestion(status);
                    }
                    else
                    {
                        Console.Write("\nINFO: post " + status.Url + " was edited, but did not meet the search criteria.");
                    }

                    Console.Write($"\nQuestion status:\n{status.Dump()}");
                }
            };
        }

        private void HandleEditedQuestion(QuestionStatus qs)
        {
            qs.CloseReqMessage = watchedPosts.SingleOrDefault(x => x.Url == qs.Url)?.CloseReqMessage;

            RemoveWatchedPost(qs.Url);

            PostFound?.Invoke(qs);
        }

        private void RemoveWatchedPost(string url)
        {
            if (watchedPosts.Any(qq => qq.Url == url))
            {
                watchedPosts.RemoveItem(watchedPosts.First(qq => qq.Url == url));
            }

            if (watchers.ContainsKey(url))
            {
                QuestionWatcher w;
                watchers.TryRemove(url, out w);
                w.Dispose();
            }
        }

        private bool QSMatchesCriteria(QuestionStatus qs) =>
            qs?.CloseDate != null &&
            qs.EditedSinceClosure &&
            qs.Difference > 0.2;
    }
}
