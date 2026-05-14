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
        //[HttpPost]
        //[Route("upload")]
        //public IHttpActionResult UploadStudent()
        //{
        //    try
        //    {
        //        var httpRequest = HttpContext.Current.Request;

        //        // 1️⃣ Validate file
        //        if (httpRequest.Files.Count == 0)
        //            return BadRequest("No file uploaded.");

        //        var file = httpRequest.Files[0];

        //        if (file == null || file.ContentLength == 0)
        //            return BadRequest("Empty file.");

        //        if (!file.FileName.EndsWith(".xlsx"))
        //            return BadRequest("Only .xlsx files are supported.");

        //        // 2️⃣ Read sessionId from frontend
        //        if (!int.TryParse(httpRequest.Form["sessionId"], out int sessionId))
        //            return BadRequest("Invalid session ID.");

        //        // 3️⃣ Validate session
        //        if (!db.Sessions.Any(s => s.id == sessionId))
        //            return BadRequest("Session not found.");

        //        int insertedCount = 0;
        //        int skippedDuplicate = 0;

        //        // 4️⃣ Read Excel
        //        using (var stream = file.InputStream)
        //        using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
        //        {
        //            var result = reader.AsDataSet(new ExcelDataSetConfiguration()
        //            {
        //                ConfigureDataTable = (_) =>
        //                    new ExcelDataTableConfiguration
        //                    {
        //                        UseHeaderRow = true
        //                    }
        //            });

        //            if (result.Tables.Count == 0)
        //                return BadRequest("Excel file is empty.");

        //            var dataTable = result.Tables[0];

        //            // 5️⃣ Process rows
        //            foreach (DataRow row in dataTable.Rows)
        //            {
        //                if (row["UserID"] == DBNull.Value ||
        //                    row["Name"] == DBNull.Value)
        //                    continue;

        //                string userId = row["UserID"].ToString().Trim();
        //                string name = row["Name"].ToString().Trim();

        //                // 6️⃣ Add User if not exists
        //                if (db.Users.Find(userId) == null)
        //                {
        //                    db.Users.Add(new User
        //                    {
        //                        id = userId,
        //                        password = "default123",
        //                        role = "Student",
        //                        isActive = 1
        //                    });
        //                }

        //                // 7️⃣ Prevent duplicate student
        //                if (db.Students.Any(s => s.userID == userId))
        //                {
        //                    skippedDuplicate++;
        //                    continue;
        //                }

        //                // 8️⃣ Add Student
        //                db.Students.Add(new StudentModel
        //                {
        //                    userID = userId,
        //                    name = name,
        //                    admissionSessionID = sessionId
        //                });

        //                insertedCount++;
        //            }

        //            db.SaveChanges();
        //        }

        //        string message =
        //            $"{insertedCount} students uploaded successfully.";

        //        if (skippedDuplicate > 0)
        //            message += $" {skippedDuplicate} duplicate(s) skipped.";

        //        return Ok(message);
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(ex.ToString());
        //    }
        //}


        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadStudent()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // 1️⃣ Read sessionId from form
                if (string.IsNullOrEmpty(httpRequest.Form["sessionId"]))
                    return BadRequest("Session not selected.");

                int sessionId = int.Parse(httpRequest.Form["sessionId"]);

                // 2️⃣ Check session exists
                if (db.Sessions.Find(sessionId) == null)
                    return BadRequest("Invalid session selected.");

                // 3️⃣ Check file uploaded
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];
                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

               // System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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

                    var dataTable = result.Tables[0];

                    int inserted = 0;
                    int skippedDuplicate = 0;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["UserID"] == DBNull.Value || row["Name"] == DBNull.Value)
                            continue;

                        string userId = row["UserID"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();

                        // ✅ FIXED CGPA reading
                        decimal cgpa = 0;
                        var cgpaCol = dataTable.Columns.Cast<DataColumn>()
                            .FirstOrDefault(c => c.ColumnName.Equals("CGPA", StringComparison.OrdinalIgnoreCase));

                        if (cgpaCol != null && row[cgpaCol] != DBNull.Value)
                        {
                            decimal.TryParse(row[cgpaCol].ToString().Trim(), out cgpa);
                        }

                        // Users table
                        if (db.Users.Find(userId) == null)
                        {
                            db.Users.Add(new User
                            {
                                id = userId,
                                password = "123",
                                role = "Student",
                                isActive = 1
                            });
                        }
                        var existingStudent = db.Students.Find(userId);
                        if (existingStudent != null)
                        {
                            // ✅ Sirf CGPA update karo
                            existingStudent.CGPA = cgpa;
                            skippedDuplicate++;
                            continue;
                        }


                        // if (db.Student.Find(userId) != null)
                        //{
                        //    skippedDuplicate++;
                        //    continue;
                        //}

                        db.Students.Add(new EPAMS.Models.Student
                        {
                            userID = userId,
                            name = name,
                            admissionSessionID = sessionId,
                            CGPA = cgpa  // ✅ Ab sahi value jayegi
                        });

                        inserted++;
                    }
                    // Student duplicate check

                    db.SaveChanges();

                    return Ok($"{inserted} students uploaded. {skippedDuplicate} duplicates skipped.");
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
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


        // GET: api/student/ping
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult PingStudent()
        {
            return Ok("Student API is alive");
        }
    }
}
