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

namespace GraveRobber
{
    public class QuestionStatus
    {
        public string Url { get; set; }

        public DateTime? CloseDate { get; set; }

        public int EditsSinceClosure { get; set; }



        public override int GetHashCode()
        {
            return Url?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is QuestionStatus)) return false;

            return Url == ((QuestionStatus)obj).Url;
        }
    }
}
