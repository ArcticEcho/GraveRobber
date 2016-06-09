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
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GraveRobber.Database
{
    public class WatchedQuestion
    {
        [Key]
        public int PostID { get; set; }
        public DateTime CloseDate { get; set; }
        public string CVPlsMessageUrl { get; set; }
        public virtual User CVPlsIssuer { get; set; }
        public virtual ICollection<User> CloseVoters { get; set; }
    }
}
