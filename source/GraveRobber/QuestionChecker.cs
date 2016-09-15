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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jil;

namespace GraveRobber
{
    public class QuestionChecker : IDisposable
    {
        private const string revUrl = "http://stackoverflow.com/revisions/";
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private readonly ConcurrentDictionary<Action<QuestionStatus>, QueuedQSReq> queue;
        private readonly SELogin sel;
        private readonly Regex revsTableRegex = new Regex(@"(?s)(<table>.*?</table>)", regOpts);
        private readonly Regex revRegex = new Regex("(?s)(<tr class=\"((vote|owner)-)?revision\".*?</tr>)", regOpts);
        private readonly Regex closedRegex = new Regex("(?s)(<td class=.*<b>Post Closed</b>)", regOpts);
        private readonly Regex reopenedRegex = new Regex("(?s)(<td class=.*<b>Post Reopened</b>)", regOpts);
        private readonly Regex closeDateRegex = new Regex("(?i)<span title=\"(.*?)\" class=\"relativetime\">", regOpts);
        private readonly Regex revIDRegex = new Regex("^<tr class=\"(owner-)?revision\">\\s+<td class=\"revcell1 vm\" onclick=\"StackExchange.revisions.toggle\\('([a-f0-9\\-]+)'\\)", regOpts);
        private readonly Regex postUrlRegex = new Regex(@"(?i)^https?://stackoverflow.com/(q(uestions)?|a)\/(\d+)", regOpts);
        private readonly Regex closedByRegex = new Regex(@"/users/(\d+)", regOpts);
        private bool dispose;



        public QuestionChecker(SELogin seLogin)
        {
            sel = seLogin;
            queue = new ConcurrentDictionary<Action<QuestionStatus>, QueuedQSReq>();

            Task.Run(() => ProcessQueue());
        }

        ~QuestionChecker()
        {
            Dispose();
        }



        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            GC.SuppressFinalize(this);
        }

        public QuestionStatus GetStatus(int postID, int fromRev = 0)
        {
            using (var mre = new ManualResetEvent(false))
            {
                QuestionStatus status = null;

                queue[new Action<QuestionStatus>(qs =>
                {
                    status = qs;
                    mre.Set();
                })] = new QueuedQSReq
                {
                    PostID = postID,
                    FromRev = fromRev
                };

                mre.WaitOne();

                return status;
            }
        }

        public int GetPostIDFromURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return -1;

            var id = -1;

            return int.TryParse(postUrlRegex.Match(url).Groups[3].Value, out id) ? id : -1;
        }



        private void ProcessQueue()
        {
            var mre = new ManualResetEvent(false);

            while (true)
            {
                mre.WaitOne(1000);

                if (queue.IsEmpty) continue;

                QueuedQSReq qqs;
                var callback = queue.Keys.First();

                queue.TryRemove(callback, out qqs);

                var qs = GetQuestionStatus(qqs);

                callback(qs);
            }
        }

        private QuestionStatus GetQuestionStatus(QueuedQSReq qqs)
        {
            if (qqs.PostID <= 0) return null;

            var revs = GetRevisionsHtml(qqs.PostID);

            if (revs == null) return null;

            var closeDate = ClosedAt(revs);

            if (closeDate == null) return null;

            var edited = EditedSinceClosure(revs);
            var latestRevID = -1;
            var diff = CalcDiff(revs, qqs.FromRev, out latestRevID);
            var votes = GetVotes(qqs.PostID);
            var cvers = ClosedBy(revs);

            return new QuestionStatus
            {
                CloseDate = closeDate,
                EditedSinceClosure = edited,
                UpvoteCount = votes.Key,
                DownvoteCount = votes.Value,
                Difference = diff,
                PostID = qqs.PostID,
                ClosedBy = cvers,
                LastestRevID = latestRevID
            };
        }

        private List<KeyValuePair<string, string>> GetRevisionsHtml(int postID)
        {
            try
            {
                var revTable = new WebClient().DownloadString($"http://stackoverflow.com/posts/{postID}/revisions");
                revTable = revsTableRegex.Match(revTable).Groups[1].Value;

                var revMatches = new List<Match>(revRegex.Matches(revTable).Cast<Match>());
                var revHtmls = new List<KeyValuePair<string, string>>();

                for (var i = 0; i < revMatches.Count; i++)
                {
                    var revID = revIDRegex.Match(revMatches[i].Value).Groups[2].Value;
                    revHtmls.Add(new KeyValuePair<string, string>(revID, revMatches[i].Value));
                }

                return revHtmls;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Calculates the difference between two post's revisions.
        /// </summary>
        /// <param name="fromRevInv">The inversed index at which the starting revision is located at.</param>
        /// <param name="latestRevInv">The inversed index of the post's latest revision.</param>
        /// <returns></returns>
        private float CalcDiff(List<KeyValuePair<string, string>> revs, int fromRevInv, out int latestRevInv)
        {
            var wc = new WebClient();
            var closeID = -1;
            var startRev = -1; // Typically the revision before closure.
            var endRev = -1;   // Normally the latest revision.

            for (var i = 0; i < revs.Count; i++)
            {
                if (closedRegex.IsMatch(revs[i].Value) && closeID == -1)
                {
                    closeID = i;
                }

                if (endRev == -1)
                {
                    try
                    {
                        wc.DownloadString($"{revUrl}{revs[i].Key}/view-source");
                        endRev = i;
                    }
                    catch
                    {
                        endRev = -1;
                    }
                }

                if (closeID != -1 && endRev != -1) break;
            }

            if (fromRevInv <= 0)
            {
                for (var i = closeID + 1; i < revs.Count; i++)
                {
                    if (revIDRegex.IsMatch(revs[i].Value))
                    {
                        try
                        {
                            wc.DownloadString($"{revUrl}{revs[i].Key}/view-source");
                            startRev = i;
                            break;
                        }
                        catch { }
                    }
                }
            }
            else
            {
                startRev = revs.Count - fromRevInv;
            }

            if (closeID == -1 || endRev == -1 || startRev == -1)
            {
                latestRevInv = revs.Count - endRev;
                return -1;
            }

            try
            {
                latestRevInv = revs.Count - endRev;
                var startRevUrl = $"{revUrl}{revs[startRev].Key}/view-source";
                var endRevUrl = $"{revUrl}{revs[endRev].Key}/view-source";
                var startRevSource = wc.DownloadString(startRevUrl);
                var endRevSource = wc.DownloadString(endRevUrl);

                return LevenshteinDistance.Calculate(startRevSource, endRevSource, int.MaxValue) / Math.Max((float)startRevSource.Length, endRevSource.Length);
            }
            catch (Exception ex)
            {
                // Probably hit a tag-only/title-only edit.
                Console.WriteLine(ex);
                latestRevInv = -1;
                return -1;
            }
        }

        private DateTime? ClosedAt(List<KeyValuePair<string, string>> revs)
        {
            foreach (var rev in revs)
            {
                if (closedRegex.IsMatch(rev.Value))
                {
                    var date = closeDateRegex.Match(rev.Value).Groups[1].Value;
                    return DateTime.Parse(date);
                }

                if (reopenedRegex.IsMatch(rev.Value))
                {
                    return null;
                }
            }

            return null;
        }

        private KeyValuePair<int, int> GetVotes(int postID)
        {
            if (sel != null)
            {
                try
                {
                    var res = sel.Get($"http://stackoverflow.com/posts/{postID}/vote-counts");
                    var json = JSON.Deserialize<Dictionary<string, string>>(res);
                    var up = int.Parse(json["up"]);
                    var down = Math.Abs(int.Parse(json["down"]));

                    return new KeyValuePair<int, int>(up, down);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            return default(KeyValuePair<int, int>);
        }

        private int[] ClosedBy(List<KeyValuePair<string, string>> revs)
        {
            var cvers = new List<int>();

            foreach (var rev in revs)
            {
                if (closedRegex.IsMatch(rev.Value))
                {
                    foreach (Match m in closedByRegex.Matches(rev.Value))
                    {
                        var userID = int.Parse(m.Groups[1].Value);
                        cvers.Add(userID);
                    }

                    break;
                }
            }

            return cvers.ToArray();
        }

        private bool EditedSinceClosure(List<KeyValuePair<string, string>> revs)
        {
            var editCount = 0;

            foreach (var rev in revs)
            {
                if (rev.Value.StartsWith("<tr class=\"revision\"") ||
                    rev.Value.StartsWith("<tr class=\"owner-revision\""))
                {
                    editCount++;
                    continue;
                }

                if (closedRegex.IsMatch(rev.Value))
                {
                    return editCount > 0;
                }

                if (reopenedRegex.IsMatch(rev.Value)) break;
            }

            return false;
        }
    }
}
