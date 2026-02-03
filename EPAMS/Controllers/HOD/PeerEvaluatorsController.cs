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


    }
}
