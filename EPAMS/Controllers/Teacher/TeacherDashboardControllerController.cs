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

    }
}
