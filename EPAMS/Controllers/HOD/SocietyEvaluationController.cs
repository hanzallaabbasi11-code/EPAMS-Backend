using EPAMS.Models;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.HOD
{
    [RoutePrefix("api/SocietyEvaluation")]
    public class SocietyEvaluationController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();


        [HttpPost]
        [Route("Submit")]
        public IHttpActionResult SubmitSocietyEvaluation([FromBody] List<SocietyEvaluationDTO> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            try
            {
                foreach (var e in evaluations)
                {
                    // 🔒 Prevent duplicate
                    var exists = db.SocietyEvaluations.Any(x =>
                        x.EvaluatorId == e.EvaluatorId &&
                        x.EvaluateeId == e.EvaluateeId &&
                        x.SocietyId == e.SocietyId &&
                        x.QuestionId == e.QuestionId &&
                        x.SessionId == e.SessionId &&  // ✅ USE FRONTEND SESSION
                        x.EvaluationType.Trim().ToLower() == e.EvaluationType.Trim().ToLower()
                    );

                    if (!exists)
                    {
                        db.SocietyEvaluations.Add(new SocietyEvaluation
                        {
                            EvaluatorId = e.EvaluatorId,
                            EvaluateeId = e.EvaluateeId,
                            SocietyId = e.SocietyId,
                            QuestionId = e.QuestionId,
                            Score = e.Score,
                            SessionId = e.SessionId,   // ✅ USE FRONTEND SESSION
                            EvaluationType = e.EvaluationType
                        });
                    }
                }

                db.SaveChanges();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        [Route("GetSubmitted/{evaluatorId}/{evaluationType}/{sessionId}")]
        public IHttpActionResult GetSubmittedEvaluations(string evaluatorId, string evaluationType, int sessionId)
        {

            var submitted = db.SocietyEvaluations
                .Where(x =>
                    x.EvaluatorId.Trim().ToLower() == evaluatorId.Trim().ToLower() &&
                    x.SessionId == sessionId &&
                    x.EvaluationType == evaluationType
                )
                .Select(x => x.EvaluateeId)
                .Distinct()
                .ToList();

            return Ok(submitted);
        }

        [HttpGet]
        [Route("GetChairpersonSocietyWithMentors/{teacherId}/{sessionId}")]
        public IHttpActionResult GetChairpersonSocietyWithMentors(string teacherId, int sessionId)
        {
            // 🔹 Find society where this teacher is chairperson
            var society = db.SocietyAssignments
                .Where(x => x.TeacherId == teacherId &&
                            x.SessionId == sessionId &&
                            x.IsChairperson == true)
                .Select(x => new
                {
                    x.SocietyId,
                    SocietyName = x.Society.SocietyName
                })
                .FirstOrDefault();

            // ❌ Not a chairperson
            if (society == null)
            {
                return Ok(new
                {
                    IsChairperson = false
                });
            }

            // 🔹 Get mentors of that society
            var mentors = db.SocietyAssignments
                .Where(x => x.SocietyId == society.SocietyId &&
                            x.SessionId == sessionId &&
                            x.IsMentor == true)
                .Join(db.Teachers,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          TeacherId = t.userID,
                          TeacherName = t.name,
                          SocietyId = a.SocietyId,
                          SocietyName = society.SocietyName
                      })
                .ToList();

            return Ok(new
            {
                IsChairperson = true,
                SocietyId = society.SocietyId,
                SocietyName = society.SocietyName,
                Mentors = mentors
            });
        }


        [HttpGet]
        [Route("GetChairpersons/{sessionId}")]
        public IHttpActionResult GetChairpersons(int sessionId)
        {
            var data = db.SocietyAssignments
                .Where(a => a.SessionId == sessionId
                         && a.IsChairperson == true)
                .Join(db.Teachers,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          SocietyId = a.SocietyId,
                          SocietyName = a.Society.SocietyName,
                          SessionId = a.SessionId,
                          TeacherId = a.TeacherId,
                          TeacherName = t.name
                      })
                .ToList(); // 👈 ONLY ONE CHAIRPERSON

            return Ok(data);
        }

        [HttpGet]
        [Route("Sessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Sessions
                .Select(s => new
                {
                    s.id,
                    s.name
                })
                .ToList();

            return Ok(sessions);
        }

        [HttpGet]
        [Route("GetActiveQuestionnaire")]
        public IHttpActionResult GetActiveQuestionnaire(String type)
        {
            try
            {
                // Get Questionnaire where flag = '1'
                var questionnaire = db.Questionares
                    .Include("Questions")
                    .Where(q => q.flag == "1" & q.type == type)
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

    }
}
