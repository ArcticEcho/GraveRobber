using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraveRobber.Database
{
    public class NotifUser
    {
        [Key]
        public int UserID { get; set; }
    }
}
