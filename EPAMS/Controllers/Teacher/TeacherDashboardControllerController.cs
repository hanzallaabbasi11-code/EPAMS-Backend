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


       


        private int GetDesignationRank(string designation)
        {
            if (string.IsNullOrWhiteSpace(designation))
                return 0;

            switch (designation.Trim().ToLower())
            {
                case "hod": return 5;                  // 🔥 highest
                case "professor": return 4;
                case "assistant professor": return 3;
                case "teacher": return 2;
                case "junior teacher": return 1;
                default: return 0;
            }
        }

        [HttpGet]
        [Route("GetTeachersWithCourses")]
        public IHttpActionResult GetTeachersWithCourses(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return BadRequest("UserId is required");

                string normalizedUserId = userId.Trim().ToLower();

                // 🔹 Current Teacher
                var currentTeacher = db.Teachers
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == normalizedUserId);

                if (currentTeacher == null)
                    return Ok(new List<object>());

                int currentRank = GetDesignationRank(currentTeacher.designation);

                var data = db.Enrollments
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherInfo = db.Teachers
                            .Where(t => t.userID == g.Key)
                            .Select(t => new
                            {
                                t.name,
                                t.designation
                            })
                            .FirstOrDefault(),

                        Courses = g
                            .Select(x => x.courseCode)
                            .Distinct()
                            .ToList()
                    })
                    .ToList()

                    // 🔥 FILTER LOGIC
                    .Where(t =>
                    {
                        if (t.TeacherInfo == null)
                            return false;

                        int targetRank = GetDesignationRank(t.TeacherInfo.designation);

                        // ❌ no self evaluation
                        if (t.TeacherID.Trim().ToLower() == normalizedUserId)
                            return false;

                        // 🔥 RULE: same level + lower
                        return targetRank <= currentRank;
                    })

                    .Select(t => new
                    {
                        TeacherID = t.TeacherID,
                        TeacherName = t.TeacherInfo.name,
                        Courses = t.Courses
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("IsEvaluator")]
        public IHttpActionResult IsEvaluator(string userId)
        {

            var exists = db.PeerEvaluators.Any(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());


            return Ok(new
            {
                isEvaluator = exists
            });
        }


    

        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluation> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            // Get latest session
            var latestSession = db.Sessions
                                  .OrderByDescending(s => s.id) // or CreatedDate
                                  .FirstOrDefault();

            if (latestSession == null)
                return BadRequest("No active session found");

            foreach (var eval in evaluations)
            {
                var record = new PeerEvaluation
                {
                    evaluatorID = eval.evaluatorID,
                    evaluateeID = eval.evaluateeID,
                    questionID = eval.questionID,
                    courseCode = eval.courseCode,
                    score = eval.score,
                    SessionID = latestSession.id // <-- store latest session
                };

                db.PeerEvaluations.Add(record);
            }

            db.SaveChanges();

            return Ok(new { success = true, sessionID = latestSession.id });
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
                if (string.IsNullOrWhiteSpace(userId))
                    return BadRequest("UserId is required");

                string normalizedUserId = userId.Trim().ToLower();

                var teacher = db.Teachers
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == normalizedUserId);

                if (teacher == null)
                    return Ok(new { peerEvaluatorID = (int?)null, isAllowed = false });

                var latestSession = db.Sessions
                    .OrderByDescending(s => s.id)
                    .FirstOrDefault();

                if (latestSession == null)
                    return Ok(new { peerEvaluatorID = (int?)null, isAllowed = false });

                bool isPermanent = teacher.isPermanentEvaluator == 1;

                // STEP 1: check existing evaluator in latest session
                var peerEvaluator = db.PeerEvaluators
                    .FirstOrDefault(pe =>
                        pe.teacherID.Trim().ToLower() == normalizedUserId &&
                        pe.sessionID == latestSession.id
                    );

                // STEP 2: AUTO INSERT ONLY ONCE (FIXED)
                if (isPermanent && peerEvaluator == null)
                {
                    peerEvaluator = new PeerEvaluator
                    {
                        teacherID = normalizedUserId,
                        sessionID = latestSession.id
                    };

                    db.PeerEvaluators.Add(peerEvaluator);
                    db.SaveChanges(); // save immediately so ID is generated
                }

                // STEP 3: response
                if (peerEvaluator != null)
                {
                    return Ok(new
                    {
                        peerEvaluatorID = peerEvaluator.id,
                        isAllowed = true,
                        source = isPermanent ? "PermanentTeacherAutoAdded" : "SessionEvaluator"
                    });
                }

                return Ok(new
                {
                    peerEvaluatorID = (int?)null,
                    isAllowed = false,
                    source = "NotEvaluator"
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }




        int employeeTypeId;









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

