using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.Teacher
{
    [RoutePrefix("api/TeacherDashboard")]
    public class TeacherDashboardControllerController : ApiController
    {
         EPAMSEntities db = new EPAMSEntities();

        // GET: api/TeacherDashboard/GetActiveQuestionnaire
        [HttpGet]
        [Route("GetActiveQuestionnaire")]
        public IHttpActionResult GetActiveQuestionnaire()
        {
            try
            {
                // Get Questionnaire where flag = '1'
                var questionnaire = db.Questionares
                    .Include("Questions")   // ✅ EF6 string-based Include
                    .Where(q => q.flag == "1")
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Type = q.type,
                        Flag = q.flag,
                        Questions = q.Questions.Select(ques => new
                        {
                            ques.QuestionID,
                            ques.QuestionText
                        }).ToList()
                    })
                    .FirstOrDefault();

                if (questionnaire == null)
                    return Ok(new { Message = "No active questionnaire found" });

                return Ok(questionnaire);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        // GET: api/TeacherDashboard/GetTeachersWithCourses
        [HttpGet]
        [Route("GetTeachersWithCourses")]
        public IHttpActionResult GetTeachersWithCourses()
        {
            var data = db.Enrollments
                .GroupBy(e => e.teacherID)
                .Select(g => new
                {
                    TeacherID = g.Key,
                    TeacherName = db.Teachers
                        .Where(t => t.userID == g.Key)
                        .Select(t => t.name)
                        .FirstOrDefault(),

                    Courses = g
                        .Select(x => x.courseCode)
                        .Distinct()
                        .ToList()
                })
                .ToList();

            return Ok(data);
        }

        [HttpGet]
        [Route("IsEvaluator")]
        public IHttpActionResult IsEvaluator(string  userId)
        {
            // Convert userId to string to match teacherID type
            var exists = db.PeerEvaluators.Any(e => e.teacherID == userId);

            return Ok(new
            {
                isEvaluator = exists
            });
        }


        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluationDTO> evaluations)
        {
            try
            {
                if (evaluations == null || !evaluations.Any())
                    return BadRequest("Invalid submission");

                foreach (var eval in evaluations)
                {
                    // Optional validation
                    if (eval.evaluatorID <= 0 ||
                        string.IsNullOrEmpty(eval.evaluateeID) ||
                        eval.questionID <= 0 ||
                        string.IsNullOrEmpty(eval.courseCode))
                    {
                        return BadRequest("Invalid data in submission");
                    }

                    var record = new PeerEvaluation
                    {
                        evaluatorID = eval.evaluatorID,
                        evaluateeID = eval.evaluateeID,   // string
                        questionID = eval.questionID,
                        courseCode = eval.courseCode,
                        score = eval.score
                    };

                    db.PeerEvaluations.Add(record);
                }

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Evaluation submitted successfully"
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.InnerException?.Message;
                return Content(System.Net.HttpStatusCode.InternalServerError,
                    inner ?? ex.ToString());
            }

        }






        [HttpGet]
        [Route("GetSubmittedEvaluations")]
        public IHttpActionResult GetSubmittedEvaluations(int evaluatorID)
        {
            // fetch all submitted evaluations for this evaluator
            var submitted = db.PeerEvaluations
                .Where(p => p.evaluatorID == evaluatorID)
                .Select(p => new
                {
                    TeacherID = p.evaluateeID, // if your evaluateeID is int, adjust type
                    CourseCode = p.courseCode
                })
                .Distinct() // one entry per course per teacher
                .ToList();

            return Ok(submitted);
        }


        [HttpGet]
        [Route("GetTeacherName/{teacherId}")]
        public IHttpActionResult GetTeacherName(string teacherId)
        {
            var teacher = db.Teachers.FirstOrDefault(s => s.userID == teacherId);
            if (teacher == null)
                return NotFound();
            return Ok(teacher.name);
        }


        [HttpGet]
        [Route("GetPeerEvaluatorID")]
        public IHttpActionResult GetPeerEvaluatorID(string userId)
        {
            try
            {
                // Get the PeerEvaluator entry for this teacher (you may also filter by current session)
                var peerEvaluator = db.PeerEvaluators
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                if (peerEvaluator == null)
                    return Ok(new { peerEvaluatorID = (int?)null });

                return Ok(new { peerEvaluatorID = peerEvaluator.id });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


    }
}

public class PeerEvaluationDTO
{
    public int evaluatorID { get; set; }
    public string evaluateeID { get; set; }   // keep string if TeacherID is string
    public int questionID { get; set; }
    public string courseCode { get; set; }
    public int score { get; set; }
}

