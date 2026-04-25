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





        /////////////////////////////////////SOciety APIS////////////////////////////////




        // =========================
        [HttpPost]
        [Route("AddSociety")]
        public IHttpActionResult AddSociety([FromBody] EPAMS.Models.DTO.SocietyDTO model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            var society = new EPAMS.Models.Society
            {
                SocietyName = model.SocietyName,
                Description = model.Description
            };

            db.Societies.Add(society);
            db.SaveChanges();

            // OPTIONAL: if you want session mapping, add here

            return Ok(new { message = "Society added successfully" });
        }
        // =========================
        // 2. GET ALL SOCIETIES
        // =========================
        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Societies
                .Select(s => new
                {
                    s.SocietyId,
                    s.SocietyName,
                    s.Description,

                    // Chair count
                    ChairCount = db.SocietyAssignments
                        .Count(a => a.SocietyId == s.SocietyId && a.IsChairperson == true),

                    // Mentor count
                    MentorCount = db.SocietyAssignments
                        .Count(a => a.SocietyId == s.SocietyId && a.IsMentor == true),

                    // Chairperson names
                    Chairpersons = (from a in db.SocietyAssignments
                                    join t in db.Teachers on a.TeacherId equals t.userID
                                    where a.SocietyId == s.SocietyId
                                       && a.IsChairperson == true
                                    select t.name).ToList(),

                    // Mentor names (optional but useful)
                    Mentors = (from a in db.SocietyAssignments
                               join t in db.Teachers on a.TeacherId equals t.userID
                               where a.SocietyId == s.SocietyId
                                  && a.IsMentor == true
                               select t.name).ToList()
                })
                .ToList();

            return Ok(data);
        }

        // =========================
        // 3. GET SOCIETIES BY SESSION
        // =========================
        [HttpGet]
        [Route("GetBySession/{sessionId}")]
        public IHttpActionResult GetBySession(int sessionId)
        {
            var data = (from s in db.Societies
                        join a in db.SocietyAssignments
                        on s.SocietyId equals a.SocietyId
                        where a.SessionId == sessionId
                        select new
                        {
                            s.SocietyId,
                            s.SocietyName,
                            s.Description
                        }).Distinct().ToList();

            return Ok(data);
        }

        // =========================
        // 4. ASSIGN TEACHER (Chair / Mentor)
        // =========================
        [HttpPost]
        [Route("AssignTeacher")]
        public IHttpActionResult AssignTeacher([FromBody] EPAMS.Models.DTO.SocietyAssignment model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            // =========================================
            // STEP 1: REMOVE ALL OLD CHAIRPERSONS
            // (IMPORTANT: fixes duplicate problem permanently)
            // =========================================
            var oldChairs = db.SocietyAssignments
                .Where(x =>
                    x.SocietyId == model.SocietyId &&
                    x.SessionId == model.SessionId &&
                    x.IsChairperson == true)
                .ToList();

            if (oldChairs.Any())
            {
                db.SocietyAssignments.RemoveRange(oldChairs);
            }

            // =========================================
            // STEP 2: ADD NEW CHAIRPERSON
            // =========================================
            var newChair = new SocietyAssignment
            {
                TeacherId = model.TeacherId,
                SocietyId = model.SocietyId,
                SessionId = model.SessionId,
                IsChairperson = true,
                IsMentor = false
            };

            db.SocietyAssignments.Add(newChair);

            db.SaveChanges();

            return Ok(new { message = "Chairperson updated successfully" });
        }
        // =========================
        // 5. GET ASSIGNMENTS BY SOCIETY
        // =========================
        [HttpGet]
        [Route("GetAssignments/{societyId}")]
        public IHttpActionResult GetAssignments(int societyId)
        {
            var data = db.SocietyAssignments
                .Where(x => x.SocietyId == societyId)
                .ToList();

            return Ok(data);
        }

        // =========================
        // 6. GET CHAIRPERSONS
        // =========================
        [HttpGet]
        [Route("GetChairpersons/{societyId}/{sessionId}")]
        public IHttpActionResult GetChairpersons(int societyId, int sessionId)
        {
            var data = db.SocietyAssignments
                .Where(a => a.SocietyId == societyId
                         && a.SessionId == sessionId
                         && a.IsChairperson == true)
                .Join(db.Teachers,
                      a => a.TeacherId,
                      t => t.userID,
                      (a, t) => new
                      {
                          SocietyId = a.SocietyId,
                          SessionId = a.SessionId,
                          TeacherId = a.TeacherId,
                          TeacherName = t.name
                      })
                .FirstOrDefault(); // 👈 ONLY ONE CHAIRPERSON

            return Ok(data);
        }

        // =========================
        // 7. GET MENTORS
        // =========================
        [HttpGet]
        [Route("GetMentors/{societyId}")]
        public IHttpActionResult GetMentors(int societyId)
        {
            var data = db.SocietyAssignments
                .Where(x => x.SocietyId == societyId && x.IsMentor == true)
                .ToList();

            return Ok(data);
        }



        // =========================
        // UPDATE SOCIETY
        // =========================
        [HttpPut]
        [Route("UpdateSociety/{id}")]
        public IHttpActionResult UpdateSociety(int id, [FromBody] EPAMS.Models.DTO.SocietyDTO model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            var society = db.Societies.FirstOrDefault(x => x.SocietyId == id);

            if (society == null)
                return NotFound();

            society.SocietyName = model.SocietyName;
            society.Description = model.Description;

            db.SaveChanges();

            return Ok(new { message = "Society updated successfully" });
        }


        // =========================
        // GET ALL TEACHERS
        // =========================
        [HttpGet]
        [Route("GetTeachers")]
        public IHttpActionResult GetTeachers()
        {
            var data = db.Teachers
                .Select(t => new
                {
                    t.userID,
                    t.name,
                    //t.designation
                }).ToList();

            return Ok(data);
        }


        [HttpPost]
        [Route("AssignMentorsBulk")]
        public IHttpActionResult AssignMentorsBulk([FromBody] List<EPAMS.Models.DTO.SocietyAssignment> models)
        {
            if (models == null || !models.Any())
                return BadRequest("Invalid data");

            foreach (var model in models)
            {
                var exists = db.SocietyAssignments.FirstOrDefault(x =>
                    x.SocietyId == model.SocietyId &&
                    x.SessionId == model.SessionId &&
                    x.TeacherId == model.TeacherId &&
                    x.IsMentor == true);

                if (exists == null)
                {
                    db.SocietyAssignments.Add(new SocietyAssignment
                    {
                        TeacherId = model.TeacherId,
                        SocietyId = model.SocietyId,
                        SessionId = model.SessionId,
                        IsChairperson = false,
                        IsMentor = true
                    });
                }
            }

            db.SaveChanges();

            return Ok(new { message = "Mentors assigned successfully" });
        }



    }
}
