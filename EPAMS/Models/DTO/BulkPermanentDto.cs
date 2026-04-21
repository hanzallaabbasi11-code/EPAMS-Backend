using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class BulkPermanentDto
    {
        public int SessionId { get; set; }
        public List<string> UserIDs { get; set; }
    }
}