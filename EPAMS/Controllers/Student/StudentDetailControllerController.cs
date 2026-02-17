using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.Student
{
    public class StudentDetailControllerController : ApiController
    {
        [RoutePrefix("api/Student")]

        public class StudentDetailController : ApiController
        {
            EPAMSEntities db = new EPAMSEntities();

            [HttpGet]
            [Route("GetStudentEnrollments/{studentId}")]
            public IHttpActionResult GetStudentEnrollments(string studentId)
            {
                var result = db.Enrollments
                    .Where(e => e.studentID == studentId)
                    .Select(e => new
                    {
                        EnrollmentID = e.id,
                        CourseCode = e.Course.code,
                        CourseTitle = e.Course.title,
                        TeacherName = e.Teacher.name,
                        SessionName = e.Session.name
                    })
                    .ToList();

                if (result.Count == 0)
                    return NotFound();

                return Ok(result);
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
                        .Where(q => q.flag == "1" & q.type== type)
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

            [HttpGet]
            [Route("GetStudentName/{studentId}")]
            public IHttpActionResult GetStudentName(string studentId)
            {
                var student = db.Students.FirstOrDefault(s => s.userID == studentId);
                if (student == null)
                    return NotFound();
                return Ok(student.name);
            }

        }



    }
}
