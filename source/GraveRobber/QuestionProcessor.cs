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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static GraveRobber.QuestionChecker;

namespace GraveRobber
{
    public class QuestionProcessor : IDisposable
    {
        private ConcurrentDictionary<string, QuestionWatcher> watchers;
        private ConcurrentQueue<string> queuedUrls;
        private Logger<QueuedQuestion> watchedPosts;
        private SELogin seLogin;
        private bool dispose;

        public int WatchedPosts => watchedPosts?.Count ?? 0;

        public Logger<QuestionStatus> PostsPendingReview { get; }

        public bool Checking { get; set; }

        public Action<Exception> SeriousDamnHappened { get; set; }



        public QuestionProcessor(SELogin seLogin)
        {
            this.seLogin = seLogin;
            watchers = new ConcurrentDictionary<string, QuestionWatcher>();
            queuedUrls = new ConcurrentQueue<string>();

            // Queued posts to check back on later.
            watchedPosts = new Logger<QueuedQuestion>("watched-posts.txt", TimeSpan.FromHours(2));

            foreach (var q in watchedPosts)
            {
                var id = -1;
                TrimUrl(q.Url, out id);

                watchers[q.Url] = new QuestionWatcher(id)
                {
                    OnException = ohNoItDidnt =>
                    {
                        if (SeriousDamnHappened != null)
                        {
                            SeriousDamnHappened(ohNoItDidnt);
                        }
                    },
                    QuestionEdited = () =>
                    {
                        var qs = GetQuestionStatus(q.Url, seLogin);

                        if (QSMatchesCriteria(qs))
                        {
                            HandleEditedQuestion(qs);
                        }
                    }
                };
            }

            // Save any active posts.
            PostsPendingReview = new Logger<QuestionStatus>("posts-pending-review.txt");

            Task.Run(() => ProcessNewUrlsQueue());
        }

        ~QuestionProcessor()
        {
            Dispose();
        }



        public void WatchPost(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("'url' must not be null, empty, or entirely whitespace.", "url");
            }

            queuedUrls.Enqueue(url);
        }

        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            foreach (var w in watchers.Values)
            {
                w.Dispose();
            }

            watchedPosts.Dispose();
            PostsPendingReview.Dispose();

            GC.SuppressFinalize(this);
        }




        private void ProcessNewUrlsQueue()
        {
            var url = "";

            while (!dispose)
            {
                Thread.Sleep(2000);

                if (dispose || queuedUrls.Count == 0)
                {
                    continue;
                }

                queuedUrls.TryDequeue(out url);
                var id = -1;
                var trimmed = TrimUrl(url, out id);

                if (watchedPosts.Any(x => x.Url == trimmed) ||
                    PostsPendingReview.Any(x => x.Url == trimmed))
                {
                    continue;
                }

                var qs = GetQuestionStatus(url, seLogin);

                // Ignore the post as it is either open or deleted.
                if (qs?.CloseDate == null) continue;

                if (QSMatchesCriteria(qs))
                {
                    HandleEditedQuestion(qs, false);
                }
                else
                {
                    watchedPosts.EnqueueItem(new QueuedQuestion
                    {
                        Url = trimmed,
                        CloseDate = (DateTime)qs?.CloseDate
                    });
                    watchers[trimmed] = new QuestionWatcher(id)
                    {
                        OnException = ex =>
                        {
                            if (SeriousDamnHappened != null)
                            {
                                SeriousDamnHappened(ex);
                            }
                        },
                        QuestionEdited = () =>
                        {
                            var status = GetQuestionStatus(trimmed, seLogin);

                            if (QSMatchesCriteria(status))
                            {
                                HandleEditedQuestion(qs);
                            }
                        }
                    };
                }
            }
        }

        //private bool? CheckPost(QueuedQuestion post)
        //{
        //    if ((DateTime.UtcNow - post.CloseDate).TotalDays < 1 || dispose) return false;

        //    var status = GetQuestionStatus(post.Url, seLogin);
        //    var res = false;

        //    if (QSMatchesCriteria(status))
        //    {
        //        PostsPendingReview.EnqueueItem(status);
        //        res = true;
        //    }
        //    if (status?.CloseDate != null)
        //    {
        //        // Keep the post as it hasn't been reopened or deleted.
        //        return false;
        //    }

        //    QuestionWatcher temp;
        //    watchedPosts.RemoveItem(post);
        //    watchers.TryRemove(post.Url, out temp);

        //    return res;
        //}

        private bool QSMatchesCriteria(QuestionStatus qs) =>
            qs.CloseDate != null &&
            (DateTime.UtcNow - qs.CloseDate.Value).TotalDays > 1 &&
            qs.EditedSinceClosure &&
            qs.Difference > 0.3 &&
            PostsPendingReview.All(x => x.Url != qs.Url);

        private void HandleEditedQuestion(QuestionStatus qs, bool removeWatchedQQ = true)
        {
            PostsPendingReview.EnqueueItem(qs);

            if (removeWatchedQQ)
            {
                QuestionWatcher temp;
                watchedPosts.RemoveItem(watchedPosts.First(qq => qq.Url == qs.Url));
                watchers.TryRemove(qs.Url, out temp);
            }
        }
    }
}
