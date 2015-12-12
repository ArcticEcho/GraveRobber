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
using static GraveRobber.QuestionChecker;

namespace GraveRobber
{
    public class QuestionProcessor
    {
        private Logger<string> queuedPosts;

        public Logger<QuestionStatus> ActiveClosedPosts { get; }



        public QuestionProcessor()
        {
            // Queued posts to check back on later.
            queuedPosts = new Logger<string>("queued posts log.txt");
            queuedPosts.CollectionFlushedEvent = new Action(CheckPosts);

            // Save any active posts (rather than caching them).
            ActiveClosedPosts = new Logger<QuestionStatus>("active closed posts.txt");
        }



        public void WatchPost(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("'url' must not be null, empty, or entirely whitespace.", "url");
            }

            queuedPosts.EnqueueItem(url);
        }



        private void CheckPosts()
        {
            var foundPosts = new HashSet<string>();

            foreach (var entry in queuedPosts)
            {
                if ((DateTime.UtcNow - entry.Timestamp).TotalDays < 1) continue;

                var status = GetQuestionStatus((string)entry.Data);

                if (status != null && status.Status.HasFlag(Status.Closed) && status.EditsSinceClosure > 0)
                {
                    ActiveClosedPosts.EnqueueItem(status);
                    foundPosts.Add(status.Url);
                }
            }

            foreach (var url in foundPosts)
            {
                queuedPosts.RemoveItem(url);
            }
        }
    }
}
