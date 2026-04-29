using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class PeerEvaluationDto
    {
        public int evaluatorID { get; set; }
        public string evaluateeID { get; set; }   // keep string if TeacherID is string
        public int questionID { get; set; }
        public string courseCode { get; set; }
        public int score { get; set; }
    }
}