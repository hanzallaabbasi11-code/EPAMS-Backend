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


        //[HttpPost]
        //[Route("UploadEnrollment")]
        //public IHttpActionResult UploadEnrollment()
        //{
        //    try
        //    {
        //        var httpRequest = HttpContext.Current.Request;

        //        // ✅ read sessionId from frontend
        //        int sessionId;
        //        if (!int.TryParse(httpRequest["sessionId"], out sessionId))
        //            return BadRequest("SessionID missing or invalid");

        //        if (httpRequest.Files.Count == 0)
        //            return BadRequest("No file uploaded.");

        //        var file = httpRequest.Files[0];

        //        using (var stream = file.InputStream)
        //        using (var reader = ExcelReaderFactory.CreateReader(stream))
        //        {
        //            var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        //            {
        //                ConfigureDataTable = (_) =>
        //                    new ExcelDataTableConfiguration() { UseHeaderRow = true }
        //            });

        //            var table = result.Tables[0];

        //            int inserted = 0;
        //            int skippedInvalidFK = 0;
        //            int skippedDuplicate = 0;

        //            foreach (DataRow row in table.Rows)
        //            {
        //                if (row["StudentID"] == DBNull.Value ||
        //                    row["TeacherID"] == DBNull.Value ||
        //                    row["CourseCode"] == DBNull.Value)
        //                    continue;

        //                string studentId = row["StudentID"].ToString().Trim();
        //                string teacherId = row["TeacherID"].ToString().Trim();
        //                string courseCode = row["CourseCode"].ToString().Trim();

        //                // ✅ FK validation
        //                if (db.Students.Find(studentId) == null ||
        //                    db.Teachers.Find(teacherId) == null ||
        //                    db.Courses.Find(courseCode) == null ||
        //                    db.Sessions.Find(sessionId) == null)
        //                {
        //                    skippedInvalidFK++;
        //                    continue;
        //                }

        //                // ✅ duplicate check
        //                bool exists = db.Enrollments.Any(e =>
        //                    e.studentID == studentId &&
        //                    e.teacherID == teacherId &&
        //                    e.courseCode == courseCode &&
        //                    e.sessionID == sessionId);

        //                if (exists)
        //                {
        //                    skippedDuplicate++;
        //                    continue;
        //                }

        //                db.Enrollments.Add(new EPAMS.Models.Enrollment
        //                {
        //                    studentID = studentId,
        //                    teacherID = teacherId,
        //                    courseCode = courseCode,
        //                    sessionID = sessionId   // ✅ from dropdown
        //                });

        //                inserted++;
        //            }

        //            db.SaveChanges();

        //            return Ok($"{inserted} enrollments added. " +
        //                      $"{skippedDuplicate} duplicates skipped. " +
        //                      $"{skippedInvalidFK} invalid FK rows skipped.");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return Content(HttpStatusCode.InternalServerError, ex.ToString());
        //    }
        //}

        [HttpPost]
        [Route("UploadExcel")]
        public IHttpActionResult UploadEnrollment()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
                    return BadRequest("Session not selected.");

                int sessionId = int.Parse(httpRequest.Form["sessionId"]);

                if (db.Sessions.Find(sessionId) == null)
                    return BadRequest("Invalid session.");

                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                //System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                int inserted = 0;
                int updated = 0;

                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true
                        }
                    });

                    var table = result.Tables[0];

                    // ✅ Column loop se bahar — ek baar dhundo
                    var sectionCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Section", StringComparison.OrdinalIgnoreCase));
                    var gradeCol = table.Columns.Cast<DataColumn>()
                        .FirstOrDefault(c => c.ColumnName.Equals("Grade", StringComparison.OrdinalIgnoreCase));

                    foreach (DataRow row in table.Rows)
                    {
                        string studentID = row["studentID"]?.ToString().Trim();
                        string teacherID = row["teacherID"]?.ToString().Trim();
                        string courseCode = row["courseCode"]?.ToString().Trim();

                        string section = (sectionCol != null && row[sectionCol] != DBNull.Value)
                            ? row[sectionCol].ToString().Trim() : null;

                        string grade = (gradeCol != null && row[gradeCol] != DBNull.Value)
                            ? row[gradeCol].ToString().Trim() : null;

                        if (string.IsNullOrEmpty(studentID) ||
                            string.IsNullOrEmpty(teacherID) ||
                            string.IsNullOrEmpty(courseCode))
                            continue;

                        var existingEnrollment = db.Enrollments.FirstOrDefault(e =>
                            e.studentID == studentID &&
                            e.courseCode == courseCode &&
                            e.sessionID == sessionId
                        );

                        if (existingEnrollment != null)
                        {
                            // ✅ Update Section aur Grade
                            existingEnrollment.teacherID = teacherID; // TeacherID bhi update karna chahte hain
                            existingEnrollment.courseCode = courseCode; // CourseCode bhi update karna chahte hain
                            existingEnrollment.Section = section;
                            existingEnrollment.Grade = grade;
                            updated++;
                            continue;
                        }

                        // ✅ Naya record insert karo
                        db.Enrollments.Add(new Enrollment
                        {
                            studentID = studentID,
                            teacherID = teacherID,
                            courseCode = courseCode,
                            sessionID = sessionId,
                            Section = section,
                            Grade = grade
                        });

                        inserted++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{inserted} new enrollments added. {updated} existing records updated (Section & Grade).");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

}
