using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class PeerSubmissionModel
    {

        public string EvaluatorUserId { get; set; }
        public string EvaluateeId { get; set; }
        public string CourseCode { get; set; }
        public List<AnswerDto> Answers { get; set; }

        public int SessionID { get; set; }
    }

    public class AnswerDto
    {
        public int QuestionId { get; set; }
        public int Score { get; set; }
    }
}