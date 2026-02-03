using EPAMS.Models;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Text;
using System.Web.Http;

namespace EPAMS.Controllers.Datacell
{
    [RoutePrefix("api/Enrollment")]

    public class EnrollmentController : ApiController
    {

        EPAMSEntities db = new EPAMSEntities();


        [HttpPost]
        [Route("UploadEnrollment")]
        public IHttpActionResult UploadEnrollment()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // ✅ read sessionId from frontend
                int sessionId;
                if (!int.TryParse(httpRequest["sessionId"], out sessionId))
                    return BadRequest("SessionID missing or invalid");

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) =>
                            new ExcelDataTableConfiguration() { UseHeaderRow = true }
                    });

                    var table = result.Tables[0];

                    int inserted = 0;
                    int skippedInvalidFK = 0;
                    int skippedDuplicate = 0;

                    foreach (DataRow row in table.Rows)
                    {
                        if (row["StudentID"] == DBNull.Value ||
                            row["TeacherID"] == DBNull.Value ||
                            row["CourseCode"] == DBNull.Value)
                            continue;

                        string studentId = row["StudentID"].ToString().Trim();
                        string teacherId = row["TeacherID"].ToString().Trim();
                        string courseCode = row["CourseCode"].ToString().Trim();

                        // ✅ FK validation
                        if (db.Students.Find(studentId) == null ||
                            db.Teachers.Find(teacherId) == null ||
                            db.Courses.Find(courseCode) == null ||
                            db.Sessions.Find(sessionId) == null)
                        {
                            skippedInvalidFK++;
                            continue;
                        }

                        // ✅ duplicate check
                        bool exists = db.Enrollments.Any(e =>
                            e.studentID == studentId &&
                            e.teacherID == teacherId &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId);

                        if (exists)
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        db.Enrollments.Add(new EPAMS.Models.Enrollment
                        {
                            studentID = studentId,
                            teacherID = teacherId,
                            courseCode = courseCode,
                            sessionID = sessionId   // ✅ from dropdown
                        });

                        inserted++;
                    }

                    db.SaveChanges();

                    return Ok($"{inserted} enrollments added. " +
                              $"{skippedDuplicate} duplicates skipped. " +
                              $"{skippedInvalidFK} invalid FK rows skipped.");
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

    }
}