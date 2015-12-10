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
    public static class QuestionStatus
    {
        private static Regex postIDRegex = new Regex(@"(?i)q(uestions)?/(\d+)", RegexOptions.Compiled);
        private static Regex revsTableRegex = new Regex(@"(?s)(<table>.*?</table>)", RegexOptions.Compiled);
        private static Regex revsRegex = new Regex("(?s)(<tr class=\"((vote|owner)-)?revision\".*?</tr>)", RegexOptions.Compiled);
        private static Regex closedRegex = new Regex("(?s)(<td class=.*<b>Post Closed</b>)", RegexOptions.Compiled);
        private static Regex reopenedRegex = new Regex("(?s)(<td class=.*<b>Post Reopened</b>)", RegexOptions.Compiled);
        private static WebClient wc = new WebClient();

        [Flags]
        public enum Status
        {
            Open = 1,
            Closed = 2,
            Edited = 4,
            Reopened = 8
        }


        // Check how many times a post is edited AFTER being closed.


        public static Status? GetQuestionStatus(string url)
        {
            if (String.IsNullOrWhiteSpace(url) || !postIDRegex.IsMatch(url)) return null;

            var htmls = GetRevisionsHtml(url);

            if (htmls == null) return null;

            var st = IsClosedOrReopened(htmls);

            if (IsEdited(htmls))
            {
                st |= Status.Edited;
            }

            return st;
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

        private static Status IsClosedOrReopened(List<string> revs)
        {
            for (var i = 0; i < revs.Count; i++)
            {
                if (closedRegex.IsMatch(revs[i]))
                {
                    return Status.Closed;
                }

                if (reopenedRegex.IsMatch(revs[i]))
                {
                    return Status.Reopened;
                }
            }

            return Status.Open;
        }

        private static bool IsEdited(List<string> revs)
        {
            return revs.Any(x => x.StartsWith("<tr class=\"revision\"") ||
                                 x.StartsWith("<tr class=\"owner-revision\""));
        }
    }
}
