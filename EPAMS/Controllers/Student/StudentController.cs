using EPAMS.Models;
using ExcelDataReader;
using System;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;
using StudentModel = EPAMS.Models.Student;

namespace EPAMS.Controllers
{
    [RoutePrefix("api/student")]
    public class StudentController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        // POST: api/student/upload
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadStudent()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // 1️⃣ Validate file
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];

                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                if (!file.FileName.EndsWith(".xlsx"))
                    return BadRequest("Only .xlsx files are supported.");

                int insertedCount = 0;
                int skippedInvalidSession = 0;
                int skippedDuplicate = 0;

                // 2️⃣ Read Excel
                using (var stream = file.InputStream)
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (_) =>
                            new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = true
                            }
                    });

                    if (result.Tables.Count == 0)
                        return BadRequest("Excel file is empty.");

                    var dataTable = result.Tables[0];

                    // 3️⃣ Process rows
                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["UserID"] == DBNull.Value ||
                            row["Name"] == DBNull.Value ||
                            row["AdmissionSessionId"] == DBNull.Value)
                            continue;

                        string userId = row["UserID"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();

                        if (!int.TryParse(row["AdmissionSessionId"].ToString(), out int sessionId))
                            continue;

                        // 4️⃣ Validate session
                        if (!db.Sessions.Any(s => s.id == sessionId))
                        {
                            skippedInvalidSession++;
                            continue;
                        }

                        // 5️⃣ Add User if not exists
                        if (db.Users.Find(userId) == null)
                        {
                            db.Users.Add(new User
                            {
                                id = userId,
                                password = "default123", // TODO: hash later
                                role = "Student",
                                profileImagePath = null,
                                isActive = 1
                            });
                        }

                        // 6️⃣ Prevent duplicate student
                        if (db.Students.Any(s => s.userID == userId))
                        {
                            skippedDuplicate++;
                            continue;
                        }

                        // 7️⃣ Add Student
                        db.Students.Add(new StudentModel
                        {
                            userID = userId,
                            name = name,
                            admissionSessionID = sessionId
                        });


                        insertedCount++;
                    }

                    db.SaveChanges();
                }

                string message = $"{insertedCount} students uploaded successfully.";
                if (skippedDuplicate > 0)
                    message += $" {skippedDuplicate} duplicate(s) skipped.";
                if (skippedInvalidSession > 0)
                    message += $" {skippedInvalidSession} row(s) skipped due to invalid session ID.";

                return Ok(message);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/student/ping
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult PingStudent()
        {
            return Ok("Student API is alive");
        }
    }
}
