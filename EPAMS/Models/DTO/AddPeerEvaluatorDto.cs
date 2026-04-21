using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class AddPeerEvaluatorDto
    {
        public int SessionId { get; set; }
        public List<string> TeacherIds { get; set; }
    }

    public class TogglePermanentDto
    {
        public string UserID { get; set; }
        public bool IsPermanent { get; set; } // True = Permanent banado, False = Hatado
    }
}