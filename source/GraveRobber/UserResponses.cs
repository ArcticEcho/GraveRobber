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





using System.Collections.Generic;

namespace GraveRobber
{
    internal class UserResponses
    {
        private Dictionary<int, string> responses = new Dictionary<int, string>
        {
            [2246344] = "Hiya dad!",
            [578411] = "I tried, but it's all blurry.",
            [4342498] = "Sorry, I only work with Real integers.",
            [4639281] = "Looks like MAGIC!",
            [1743880] = "This one seems fishy",
            [1677912] = "Who's Mogs?",
            [1043380] = "Yo G-Daddy!",
            [2193767] = "Nothing to watch really. Could anyone help him find it?",
            [871050] = "Uh, I seem to be in another dimension now.",
            [1252759] = "Cuteness sensor overloaded.",
            [426671] = "Yep, it's blue all right.",
            [811] = "Nope.",
            [4424245] = " Hey sis!",
            [6294609] = " She's circling around rene....",
        };

        public string this[int userID]
        {
            get
            {
                if (responses.ContainsKey(userID))
                {
                    return responses[userID];
                }

                return "Sorry, I've lent my stalking module to Closey.";
            }
        }
    }
}
