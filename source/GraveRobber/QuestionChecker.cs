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
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private static Regex postIDRegex = new Regex(@"(?i)q(uestions)?/(\d+)", regOpts);
        private static Regex revsTableRegex = new Regex(@"(?s)(<table>.*?</table>)", regOpts);
        private static Regex revsRegex = new Regex("(?s)(<tr class=\"((vote|owner)-)?revision\".*?</tr>)", regOpts);
        private static Regex closedRegex = new Regex("(?s)(<td class=.*<b>Post Closed</b>)", regOpts);
        private static Regex reopenedRegex = new Regex("(?s)(<td class=.*<b>Post Reopened</b>)", regOpts);
        private static Regex closeDateRegex = new Regex("(?i)<span title=\"(.*?)\" class=\"relativetime\">", regOpts);
        private static WebClient wc = new WebClient();



        public static QuestionStatus GetQuestionStatus(string url)
        {
            if (String.IsNullOrWhiteSpace(url) || !postIDRegex.IsMatch(url)) return null;

            var htmls = GetRevisionsHtml(url);

            if (htmls == null) return null;

            var closeDate = ClosedAt(htmls);
            var edits = EditsSinceClosure(htmls);

            return new QuestionStatus
            {
                Url = url,
                CloseDate = closeDate,
                EditsSinceClosure = edits
            };
        }




        private static List<string> GetRevisionsHtml(string url)
        {
            try
            {
                var id = postIDRegex.Match(url).Groups[2].Value;
                var html = wc.DownloadString($"http://stackoverflow.com/posts/{id}/revisions");
                html = revsTableRegex.Match(html).Groups[1].Value;

                var matches = revsRegex.Matches(html);
                var revsHtml = new List<string>();

                foreach (Match m in matches)
                {
                    revsHtml.Add(m.Value);
                }

                return revsHtml;
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? ClosedAt(List<string> revs)
        {
            for (var i = 0; i < revs.Count; i++)
            {
                if (closedRegex.IsMatch(revs[i]))
                {
                    var date = closeDateRegex.Match(revs[i]).Groups[1].Value;
                    return DateTime.Parse(date);
                }

                if (reopenedRegex.IsMatch(revs[i]))
                {
                    return null;
                }
            }

            return null;
        }

        private static int EditsSinceClosure(List<string> revs)
        {
            var editCount = 0;

            for (var i = 0; i < revs.Count; i++)
            {
                if (revs[i].StartsWith("<tr class=\"revision\"") ||
                    revs[i].StartsWith("<tr class=\"owner-revision\""))
                {
                    editCount++;
                    continue;
                }

                if (closedRegex.IsMatch(revs[i]))
                {
                    return editCount;
                }

                if (reopenedRegex.IsMatch(revs[i]))
                {
                    return 0;
                }
            }

            return 0;
        }
    }
}
