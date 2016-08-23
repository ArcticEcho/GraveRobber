/*
 * GraveRobber. A .NET PoC program for fetching data from the SOCVR graveyards.
 * Copyright © 2016, ArcticEcho.
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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ChatExchangeDotNet;
using GraveRobber.Database;
using Microsoft.Data.Entity;

namespace GraveRobber
{
    public class QuestionWatcherPool : IDisposable
    {
        private readonly ManualResetEvent grimReaperMre;
        private readonly ConcurrentDictionary<int, QuestionWatcher> watchers;
        private readonly ConcurrentQueue<QueuedQwpItem> queuedPostIDs;
        private readonly QuestionChecker qChkr;
        private uint errorCount;
        private bool dispose;

        public int WatchedPosts => watchers?.Count ?? 0;

        public Action<string> NewReport { get; set; }

        public Action<Exception> OnException { get; set; }

        public Action<uint> HighErrorCountPerMinuteReached { get; set; }



        public QuestionWatcherPool(QuestionChecker qChecker, string dataFilesDir = null)
        {
            if (qChecker == null)
            {
                throw new ArgumentNullException(nameof(qChecker));
            }

            using (var db = new DB())
            {
                db.Database.EnsureCreated();
            }

            qChkr = qChecker;
            watchers = new ConcurrentDictionary<int, QuestionWatcher>();
            queuedPostIDs = new ConcurrentQueue<QueuedQwpItem>();
            grimReaperMre = new ManualResetEvent(false);

            Task.Run(() => PopulateWatchers());
            Task.Run(() => ProcessNewUrlsQueue());
            Task.Run(() => GrimReaper());
            Task.Run(() => WatchErrorRate());
        }

        ~QuestionWatcherPool()
        {
            Dispose();
        }



        public void WatchPost(int postID, int cvplsReqMessageID, bool reportIfOverThreshold = true)
        {
            queuedPostIDs.Enqueue(new QueuedQwpItem
            {
                PostID = postID,
                ActionRequestMessageID = cvplsReqMessageID,
                ReportIfOverThreshold = reportIfOverThreshold
            });
        }

        public bool IsPostWatched(int postID)
        {
            using (var db = new DB())
            {
                return db.WatchedQuestions.Any(x => x.PostID == postID);
            }
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

            GC.SuppressFinalize(this);
        }



        private void WatchErrorRate()
        {
            var prevER = 0U;
            while (!dispose)
            {
                prevER = errorCount;

                Thread.Sleep(60000); // One minute.

                var errors = errorCount - prevER;

                // Fire the event if (on average) we're receiving more than
                // 1 error per watcher per minute.
                if (errors > watchers.Count)
                {
                    HighErrorCountPerMinuteReached?.Invoke(errorCount);
                }
            }
        }

        private void PopulateWatchers()
        {
            using (var db = new DB())
            {
                foreach (var q in db.WatchedQuestions)
                {
                    using (var dbManualCheck = new DB())
                    {
                        QuestionStatus qs = null;
                        var m = dbManualCheck.ManualReportNotifUsers.SingleOrDefault(x => x.PostID == q.PostID);

                        if (m != null)
                        {
                            qChkr.GetStatus(q.PostID, m.FromRevNo);
                        }
                        else
                        {
                            qs = qChkr.GetStatus(q.PostID);
                        }

                        if (qs?.CloseDate != null)
                        {
                            if (QSMatchesCriteria(qs))
                            {
                                var report = GenerateReport(q, qs);
                                NewReport?.Invoke(report);
                                RemoveWatchedPost(qs.PostID);
                                continue;
                            }

                            watchers[q.PostID] = CreateWatcher(qs.PostID);
                        }
                    }
                }
            }
        }

        private void GrimReaper()
        {
            var qqsToRemove = new HashSet<WatchedQuestion>();
            var wc = new WebClient();

            using (var db = new DB())
            {
                while (!dispose)
                {
                    foreach (var q in db.WatchedQuestions)
                    {
                        if (dispose) break;

                        if ((DateTime.UtcNow - q.CloseDate).TotalDays > 30)
                        {
                            qqsToRemove.Add(q);
                            continue;
                        }

                        grimReaperMre.WaitOne(TimeSpan.FromSeconds(15));

                        try
                        {
                            wc.DownloadString($"http://stackoverflow.com/posts/ajax-load-realtime/{q.PostID}");
                        }
                        catch // Something bad happened, that's all we need to know.
                        {
                            qqsToRemove.Add(q);
                        }
                    }

                    foreach (var q in qqsToRemove)
                    {
                        RemoveWatchedPost(q.PostID);
                    }

                    grimReaperMre.WaitOne(TimeSpan.FromMinutes(5));
                }
            }
        }

        private void ProcessNewUrlsQueue()
        {
            var queuedPost = new QueuedQwpItem();

            using (var db = new DB())
            {
                while (!dispose)
                {
                    try
                    {
                        Thread.Sleep(500);

                        if (dispose || queuedPostIDs.IsEmpty) continue;

                        queuedPostIDs.TryDequeue(out queuedPost);

                        if (db.WatchedQuestions.Any(x => x.PostID == queuedPost.PostID))
                        {
                            continue;
                        }

                        var qs = qChkr.GetStatus(queuedPost.PostID);

                        // Ignore the post as it is either open or deleted.
                        if (qs?.CloseDate == null) continue;

                        db.WatchedQuestions.Add(new WatchedQuestion
                        {
                            PostID = qs.PostID,
                            CloseDate = (DateTime)qs?.CloseDate,
                            CVPlsMessageID = queuedPost.ActionRequestMessageID,
                            CVPlsIssuerUserID = Program.GetChatMessageAuthor(queuedPost.ActionRequestMessageID).ID
                        });

                        foreach (var uID in qs.ClosedBy)
                        {
                            db.CVs.Add(new CloseVote
                            {
                                PostID = qs.PostID,
                                UserID = uID
                            });
                        }

                        db.SaveChanges();

                        if (queuedPost.ReportIfOverThreshold && QSMatchesCriteria(qs))
                        {
                            HandleEditedQuestion(qs);
                        }
                        else
                        {
                            watchers[qs.PostID] = CreateWatcher(qs.PostID);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        OnException?.Invoke(ex);
                    }
                }
            }
        }

        private QuestionWatcher CreateWatcher(int id)
        {
            return new QuestionWatcher(id)
            {
                OnException = ex =>
                {
                    errorCount++;
                    OnException?.Invoke(ex);
                },
                QuestionEdited = () =>
                {
                    using (var db = new DB())
                    {
                        QuestionStatus status;

                        var m = db.ManualReportNotifUsers.SingleOrDefault(x => x.PostID == id);

                        if (m != null)
                        {
                            status = qChkr.GetStatus(id, m.FromRevNo);
                        }
                        else
                        {
                            status = qChkr.GetStatus(id);
                        }

                        if (QSMatchesCriteria(status))
                        {
                            HandleEditedQuestion(status);
                        }
                    }
                }
            };
        }

        private void HandleEditedQuestion(QuestionStatus qs)
        {
            using (var db = new DB())
            {
                var wq = db.WatchedQuestions
                    .Include(x => x.CloseVotes)
                    .Single(x => x.PostID == qs.PostID);
                var report = GenerateReport(wq, qs);

                RemoveWatchedPost(qs.PostID);

                NewReport?.Invoke(report);
            }
        }

        private string GenerateReport(WatchedQuestion wq, QuestionStatus qs)
        {
            var msg = new MessageBuilder();
            var percentage = Math.Round(qs.Difference * 100);
            var revLink = $"http://stackoverflow.com/posts/{qs.PostID}/revisions";
            var qLink = $"http://stackoverflow.com/q/{qs.PostID}";

            msg.AppendLink($"{percentage}%", revLink);
            msg.AppendText(" changed: ");
            msg.AppendLink($"question", qLink);
            msg.AppendText($" (+{qs.UpvoteCount}/-{qs.DownvoteCount})");

            if (wq.CVPlsMessageID > 0)
            {
                var reqLink = $"http://chat.stackoverflow.com/transcript/message/{wq.CVPlsMessageID}";
                msg.AppendText(" - ");
                msg.AppendLink("req", reqLink);
            }

            msg.AppendText(" ");

            using (var db = new DB())
            {
                var usersToNotif = new List<int>();
                var m = db.ManualReportNotifUsers.SingleOrDefault(x => x.PostID == wq.PostID);
                if (m != null)
                {
                    usersToNotif.Add(m.UserID);
                    db.ManualReportNotifUsers.Remove(m);
                }

                if (db.NotifUsers.Any(x => x.UserID == wq.CVPlsIssuerUserID))
                {
                    usersToNotif.Add(wq.CVPlsIssuerUserID);
                }

                usersToNotif
                    .AddRange(wq.CloseVotes
                        .Select(x => x.UserID)
                        .Where(cver => db.NotifUsers
                            .Any(notifUser => notifUser.UserID == cver)));

                foreach (var u in usersToNotif.Distinct())
                {
                    msg.AppendPing(Program.GetChatUser(u));
                }

                db.SaveChanges();
            }

            return msg.ToString();
        }

        private void RemoveWatchedPost(int postID)
        {
            using (var db = new DB())
            {
                var wq = db.WatchedQuestions.SingleOrDefault(x => x.PostID == postID);

                if (wq != null)
                {
                    db.WatchedQuestions.Remove(wq);
                    db.SaveChanges();
                }

                if (watchers.ContainsKey(postID))
                {
                    QuestionWatcher w;
                    watchers.TryRemove(postID, out w);
                    w.Dispose();
                }
            }
        }

        private bool QSMatchesCriteria(QuestionStatus qs) =>
            qs?.CloseDate != null &&
            qs.EditedSinceClosure &&
            qs.Difference > 0.2;
    }
}
