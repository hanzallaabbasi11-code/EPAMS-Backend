using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.HOD
{
    [RoutePrefix("api/CourseManagement")]
    public class CourseManagementController : ApiController
    {

        EPAMSEntities db = new EPAMSEntities();
        [HttpGet]
        [Route("EnrollmentCourses/{sessionId}")]
        public IHttpActionResult GetEnrollmentCourses(int sessionId)
        {
            var data = (from e in db.Enrollments
                        join t in db.Teachers
                            on e.teacherID equals t.userID
                        join c in db.Courses
                            on e.courseCode equals c.code
                        where e.sessionID == sessionId
                        select new
                        {
                            id = e.id,
                            teacher = t.name,
                            course = c.title,
                            code = e.courseCode
                        }).ToList();

            return Ok(data);
        }

    }
}
