using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class EmailRequest
    {
        public string mail { get; set; }
        public string filter { get; set; } // "unread", "read", "all"
    }
}