using EPAMS.Models;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.HOD
{
    [RoutePrefix("api/PeerEvaluator")]
    public class PeerEvaluatorsController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        [HttpGet]
        [Route("Teachers")]
        public IHttpActionResult GetTeachers()
        {
            var teachers = db.Teachers
                .Select(t => new
                {
                    t.userID,
                    t.name,
                    t.department
                })
                .ToList();

            return Ok(teachers);
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


        [HttpPost]
        [Route("Add")]
        public IHttpActionResult AddPeerEvaluators(AddPeerEvaluatorDto model)
        {
            if (model == null || model.TeacherIds == null || model.TeacherIds.Count == 0)
                return BadRequest("Invalid data");

            int sessionId = model.SessionId;

            foreach (var teacherId in model.TeacherIds)
            {
                string teacherIdStr = teacherId.ToString();

                bool alreadyExists = db.PeerEvaluators.Any(pe =>
                    pe.teacherID == teacherIdStr &&
                    pe.sessionID == sessionId
                );

                if (!alreadyExists)
                {
                    db.PeerEvaluators.Add(new PeerEvaluator
                    {
                        teacherID = teacherIdStr,
                        sessionID = sessionId
                    });
                }
            }

            db.SaveChanges();
            return Ok("Peer evaluators added successfully");
        }


        [HttpGet]
        [Route("BySession/{sessionId}")]
        public IHttpActionResult GetPeerEvaluatorsBySession(int sessionId)
        {
            var evaluators = (from pe in db.PeerEvaluators
                              join t in db.Teachers on pe.teacherID equals t.userID
                              where pe.sessionID == sessionId
                              select new
                              {
                                  t.userID,
                                  t.name,
                                  t.department
                              }).ToList();


            if (evaluators.Count == 0) return BadRequest("No Teacher Found ");

            return Ok(evaluators);
        }



        // 7. Toggle Permanent Status (Add or Remove from Permanent list)
        // Isay Add aur Edit dono ke liye use kiya ja sakta hai
        [HttpPost]
        [Route("TogglePermanent")]
        public IHttpActionResult TogglePermanentStatus(TogglePermanentDto model)
        {
            if (model == null || string.IsNullOrEmpty(model.UserID))
                return BadRequest("Invalid user data.");

            try
            {
                var teacher = db.Teachers.FirstOrDefault(t => t.userID == model.UserID);
                if (teacher == null) return NotFound();

                // Status update karein: 1 for Permanent, 0 for Normal
                teacher.isPermanentEvaluator = model.IsPermanent ? 1 : 0;

                // Agar kisi ko Permanent banaya hai, toh usey PeerEvaluator table (manual list) se hatayein
                // Kyunke wo ab globally available hoga
                if (model.IsPermanent)
                {
                    var manualAssignments = db.PeerEvaluators.Where(pe => pe.teacherID == model.UserID).ToList();
                    if (manualAssignments.Any())
                    {
                        db.PeerEvaluators.RemoveRange(manualAssignments);
                    }
                }

                db.SaveChanges();

                string status = model.IsPermanent ? "Permanent" : "Normal";
                return Ok(new { message = $"Teacher marked as {status} evaluator successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 8. Bulk Permanent Assignment (Extra: Multiple teachers ke liye)
        [HttpPost]
[Route("SetBulkPermanent")]
public IHttpActionResult SetBulkPermanent(BulkPermanentDto model)
{
    if (model == null || model.UserIDs == null || !model.UserIDs.Any())
        return BadRequest("No User IDs provided.");

    try
    {
        var teachers = db.Teachers.Where(t => model.UserIDs.Contains(t.userID)).ToList();

        foreach (var t in teachers)
        {
            t.isPermanentEvaluator = 1;
        }

        db.SaveChanges();

        return Ok(new { message = "Selected teachers are now Permanent Evaluators." });
    }
    catch (Exception ex)
    {
        return InternalServerError(ex);
    }
}
    }


}

