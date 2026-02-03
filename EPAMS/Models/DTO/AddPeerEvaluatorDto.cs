using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class AddPeerEvaluatorDto
    {
        public int SessionId { get; set; }
        public List<int> TeacherIds { get; set; }
    }
}