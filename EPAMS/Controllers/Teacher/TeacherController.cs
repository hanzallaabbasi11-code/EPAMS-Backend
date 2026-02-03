using EPAMS.Models;
using ExcelDataReader;
using System;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;
using TeacherModel = EPAMS.Models.Teacher;

namespace FYP.Controllers.Teacher
{
    [RoutePrefix("api/teacher")]
    public class TeacherController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        // POST: api/teacher/upload
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult Upload()
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

                int insertedTeachers = 0;
                int insertedUsers = 0;

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
                        if (row["Userid"] == DBNull.Value ||
                            row["Name"] == DBNull.Value ||
                            row["Department"] == DBNull.Value)
                            continue;

                        string userId = row["Userid"].ToString().Trim();
                        string name = row["Name"].ToString().Trim();
                        string department = row["Department"].ToString().Trim();

                        if (string.IsNullOrEmpty(userId))
                            continue;

                        // 4️⃣ Add User if not exists
                        var existingUser = db.Users.Find(userId);

                        if (existingUser == null)
                        {
                            db.Users.Add(new User
                            {
                                id = userId,
                                password = "default123",   // TODO: hash later
                                role = "Teacher",
                                profileImagePath = null,
                                isActive = 1
                            });

                            insertedUsers++;
                        }

                        // 5️⃣ Prevent duplicate teacher
                        if (db.Teachers.Any(t => t.userID == userId))
                            continue;

                        db.Teachers.Add(new TeacherModel
                        {
                            userID = userId,
                            name = name,
                            department = department
                        });

                        insertedTeachers++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{insertedTeachers} teachers uploaded successfully. Users added: {insertedUsers}");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/teacher/ping
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult Ping()
        {
            return Ok("Teacher API is alive");
        }
    }
}
