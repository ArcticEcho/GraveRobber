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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace GraveRobber
{
    public static class QuestionChecker
    {
        private const string revUrl = "http://stackoverflow.com/revisions/";
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private static Regex postIDRegex = new Regex(@"(?i)q(uestions)?/(\d+)", regOpts);
        private static Regex revsTableRegex = new Regex(@"(?s)(<table>.*?</table>)", regOpts);
        private static Regex revRegex = new Regex("(?s)(<tr class=\"((vote|owner)-)?revision\".*?</tr>)", regOpts);
        private static Regex closedRegex = new Regex("(?s)(<td class=.*<b>Post Closed</b>)", regOpts);
        private static Regex reopenedRegex = new Regex("(?s)(<td class=.*<b>Post Reopened</b>)", regOpts);
        private static Regex closeDateRegex = new Regex("(?i)<span title=\"(.*?)\" class=\"relativetime\">", regOpts);
        private static Regex revIDRegex = new Regex("^<tr class=\"(owner-)?revision\">\\s+<td class=\"revcell1 vm\" onclick=\"StackExchange.revisions.toggle\\('([a-f0-9\\-]+)'\\)", regOpts);
        private static WebClient wc = new WebClient();



        public static QuestionStatus GetQuestionStatus(string url)
        {
            if (String.IsNullOrWhiteSpace(url) || !postIDRegex.IsMatch(url)) return null;

            var revs = GetRevisionsHtml(url);

            if (revs == null) return null;

            var closeDate = ClosedAt(revs);
            var edits = EditsSinceClosure(revs);
            var diff = CalcDiff(revs);

            return new QuestionStatus
            {
                Url = url,
                CloseDate = closeDate,
                EditsSinceClosure = edits,
                Difference = diff
            };
        }



        private static List<KeyValuePair<string, string>> GetRevisionsHtml(string url)
        {
            try
            {
                var id = postIDRegex.Match(url).Groups[2].Value;
                var revTable = wc.DownloadString($"http://stackoverflow.com/posts/{id}/revisions");
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

        private static float CalcDiff(List<KeyValuePair<string, string>> revs)
        {
            var closeIndex = 0;
            var revIdBeforeClose = 0;
            var latestRevI = -1;
            for (var i = 0; i < revs.Count; i++)
            {
                if (closedRegex.IsMatch(revs[i].Value))
                {
                    closeIndex = i;
                }
                if (latestRevI == -1 && revIDRegex.IsMatch(revs[i].Value))
                {
                    latestRevI = i;
                }
            }

            for (var i = closeIndex; i < revs.Count; i++)
            {
                if (revIDRegex.IsMatch(revs[i].Value))
                {
                    revIdBeforeClose = i;
                    break;
                }
            }

            if (closeIndex == 0 || latestRevI == revIdBeforeClose) return -1;

            try
            {
                var latestUrl = $"{revUrl}{revs[latestRevI].Key}/view-source";
                var urlBeforeClose = $"{revUrl}{revs[revIdBeforeClose].Key}/view-source";
                var latestRev = wc.DownloadString(latestUrl);
                var revBeforeClose = wc.DownloadString(urlBeforeClose);

                return LevenshteinDistance.Calculate(revBeforeClose, latestRev, int.MaxValue) / Math.Max((float)revBeforeClose.Length, latestRev.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }

        private static DateTime? ClosedAt(List<KeyValuePair<string, string>> revs)
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

        private static int EditsSinceClosure(List<KeyValuePair<string, string>> revs)
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
                    return editCount;
                }

                if (reopenedRegex.IsMatch(rev.Value))
                {
                    return 0;
                }
            }

            return 0;
        }
    }
}
