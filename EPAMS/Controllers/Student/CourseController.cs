using ExcelDataReader;
using EPAMS.Models;
using System;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace FYP.Controllers.Student
{
    // COURSE API
    [RoutePrefix("api/course")]
    public class CourseController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        // POST: api/course/upload
        [HttpPost]
        [Route("upload")]
        public IHttpActionResult UploadCourse()
        {
            try
            {
                var httpRequest = HttpContext.Current.Request;

                // 1️⃣ Check file existence
                if (httpRequest.Files.Count == 0)
                    return BadRequest("No file uploaded.");

                var file = httpRequest.Files[0];

                if (file == null || file.ContentLength == 0)
                    return BadRequest("Empty file.");

                // 2️⃣ Validate file extension
                if (!file.FileName.EndsWith(".xlsx"))
                    return BadRequest("Only .xlsx files are supported.");

                int insertedCount = 0;

                // 3️⃣ Read Excel file
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

                    // 4️⃣ Insert records
                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row["Code"] == DBNull.Value || row["Title"] == DBNull.Value)
                            continue;

                        string code = row["Code"].ToString().Trim();
                        string title = row["Title"].ToString().Trim();

                        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(title))
                            continue;

                        // Prevent duplicates
                        if (db.Courses.Any(c => c.code == code))
                            continue;

                        db.Courses.Add(new Course
                        {
                            code = code,
                            title = title
                        });

                        insertedCount++;
                    }

                    db.SaveChanges();
                }

                return Ok($"{insertedCount} courses uploaded successfully.");
            }
            catch (Exception ex)
            {
                // IIS-safe error response
                return InternalServerError(ex);
            }
        }

        // GET: api/course/ping
        [HttpGet]
        [Route("ping")]
        public IHttpActionResult PingCourse()
        {
            return Ok("Course API is alive");
        }
    }
}
