using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using EPAMS.Models.DTO;


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
        //[HttpGet]
        //[Route("GetTeachersWithCourses")]
        //public IHttpActionResult GetTeachersWithCourses()
        //{
        //    var data = db.Enrollments
        //        .GroupBy(e => e.teacherID)
        //        .Select(g => new
        //        {
        //            TeacherID = g.Key,
        //            TeacherName = db.Teachers
        //                .Where(t => t.userID == g.Key)
        //                .Select(t => t.name)
        //                .FirstOrDefault(),

        //            Courses = g
        //                .Select(x => x.courseCode)
        //                .Distinct()
        //                .ToList()
        //        })
        //        .ToList();

        //    return Ok(data);
        //}

        [HttpGet]
        [Route("GetTeachersWithCourses/{userId}")]
        public IHttpActionResult GetTeachersWithCourses(string userId)
        {
            try
            {
                var teacherProfile = db.Teachers.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());
                if (teacherProfile == null) return BadRequest("Teacher record not found.");

                IQueryable<Enrollment> enrollmentQuery = db.Enrollments;

                // Session filtering for non-permanent evaluators
                if (teacherProfile.isPermanentEvaluator != 1)
                {
                    var sessionAuth = db.PeerEvaluators.FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());
                    if (sessionAuth == null) return Ok(new List<object>());
                    enrollmentQuery = enrollmentQuery.Where(e => e.sessionID == sessionAuth.sessionID);
                }

                var data = enrollmentQuery
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherName = db.Teachers.Where(t => t.userID == g.Key).Select(t => t.name).FirstOrDefault(),
                        Designation = db.Teachers.Where(t => t.userID == g.Key).Select(t => t.designation).FirstOrDefault(), // Rank check ke liye
                        SessionID = g.FirstOrDefault().sessionID,
                        Courses = g.Select(x => x.courseCode).Distinct().ToList()
                    }).ToList();

                return Ok(data);
            }
            catch (Exception ex) { return InternalServerError(ex); }
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


        //[HttpPost]
        //[Route("SubmitEvaluation")]
        //public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluation> evaluations)
        //{
        //    if (evaluations == null || !evaluations.Any())
        //        return BadRequest("Invalid submission");

        //    // Get latest session
        //    var latestSession = db.Sessions
        //                          .OrderByDescending(s => s.id) // or CreatedDate
        //                          .FirstOrDefault();

        //    if (latestSession == null)
        //        return BadRequest("No active session found");

        //    foreach (var eval in evaluations)
        //    {
        //        var record = new PeerEvaluation
        //        {
        //            evaluatorID = eval.evaluatorID,
        //            evaluateeID = eval.evaluateeID,
        //            questionID = eval.questionID,
        //            courseCode = eval.courseCode,
        //            score = eval.score,
        //            SessionID = latestSession.id // <-- store latest session
        //        };

        //        db.PeerEvaluations.Add(record);
        //    }

        //    db.SaveChanges();

        //    return Ok(new { success = true, sessionID = latestSession.id });
        //}



        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] PeerSubmissionModel model)
        {
            if (model == null || model.Answers == null) return BadRequest("Invalid Data");

            try
            {
                // 1. Check karein ke kya user PeerEvaluator table mein hai?
                var sessionEvaluator = db.PeerEvaluators
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == model.EvaluatorUserId.Trim().ToLower());

                // 2. Check karein ke kya user Teacher table mein Permanent hai?
                var permanentTeacher = db.Teachers
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == model.EvaluatorUserId.Trim().ToLower()
                                    && t.isPermanentEvaluator == 1);

                // Agar dono jagah record nahi milta, toh hi error dein
                if (sessionEvaluator == null && permanentTeacher == null)
                {
                    return BadRequest("Unauthorized: You are not an assigned evaluator.");
                }

                foreach (var ans in model.Answers)
                {
                    db.PeerEvaluations.Add(new PeerEvaluation
                    {
                        // Agar session-based hai toh uski ID lagayein, 
                        // Agar Permanent hai toh yahan logic dekhein (evaluatorID agar null allow karta hai toh null bhejain)
                        evaluatorID = sessionEvaluator?.id,
                        evaluateeID = model.EvaluateeId,
                        questionID = ans.QuestionId,
                        courseCode = model.CourseCode,
                        score = ans.Score
                    });
                }

                db.SaveChanges();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
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








        [HttpGet]
        [Route("IsEvaluator/{userId}/{targetUserId?}")]
        public IHttpActionResult IsEvaluator(string userId, string targetUserId = null)
        {
            try
            {
                // 1. Evaluator aur Evaluatee (Target) ka data nikalain
                var evaluator = db.Teachers.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());

                if (evaluator == null) return Ok(new { isEvaluator = false, message = "User not found" });

                // 2. Designation Levels Define Karein (Rank System)
                Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Professor", 3 },
            { "Assistent Professor", 2 },
            { "Senior Lecturer", 1 },
            { "Junior Lecturer", 0 }
        };

                // Current evaluator ka rank nikalain
                int evaluatorRank = ranks.ContainsKey(evaluator.designation) ? ranks[evaluator.designation] : -1;

                // 3. Hierarchy Check (Agar targetUserId provide kiya gaya hai)
                if (!string.IsNullOrEmpty(targetUserId))
                {
                    var target = db.Teachers.FirstOrDefault(t => t.userID.Trim().ToLower() == targetUserId.Trim().ToLower());
                    if (target != null)
                    {
                        int targetRank = ranks.ContainsKey(target.designation) ? ranks[target.designation] : -1;

                        // Hierarchy Rule: Assistant Professor (Rank 2) Professor (Rank 3) ki evaluation nahi kar sakta
                        if (evaluatorRank < targetRank)
                        {
                            return Ok(new
                            {
                                isEvaluator = false,
                                message = "Hierarchy Violation: Juniors cannot evaluate Seniors."
                            });
                        }
                    }
                }

                // 4.Baki existing logic(Permanent or Session-based)
                var sessionEvaluator = db.PeerEvaluators.FirstOrDefault(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                if (evaluator.isPermanentEvaluator == 1)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = true,
                        designation = evaluator.designation,
                        sessionID = sessionEvaluator?.sessionID
                    });
                }

                if (sessionEvaluator != null)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = false,
                        designation = evaluator.designation,
                        sessionID = sessionEvaluator.sessionID
                    });
                }

                return Ok(new { isEvaluator = false });
            }
            catch (Exception ex) { return InternalServerError(ex); }
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

