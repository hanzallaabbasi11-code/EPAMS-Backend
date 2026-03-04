using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.Director
{
    [RoutePrefix("api/email")]
    public class EmailController : ApiController
    {
        private EPAMSEntities db = new EPAMSEntities();

        // ============================================
        // 1️⃣ GET ALL EMAILS
        // ============================================
        [HttpGet]
        [Route("getall")]
        public IHttpActionResult GetAllEmails()
        {
            var emails = db.Emails
                           .OrderByDescending(x => x.id)
                           .ToList();

            return Ok(emails);
        }

        // ============================================
        // 2️⃣ GET ACTIVE EMAIL (isActive = 1)
        // ============================================
        [HttpGet]
        [Route("active")]
        public IHttpActionResult GetActiveEmail()
        {
            var activeEmail = db.Emails
                                .FirstOrDefault(x => x.isActive == true);

            if (activeEmail == null)
                return NotFound();

            return Ok(activeEmail);
        }

        // ============================================
        // 3️⃣ ADD NEW EMAIL
        // ============================================
        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddEmail(Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail))
                return BadRequest("Email is required.");

            model.isActive = false; // Always add as inactive

            db.Emails.Add(model);
            db.SaveChanges();

            return Ok(model);
        }

        // ============================================
        // 4️⃣ DELETE EMAIL
        // ============================================
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult DeleteEmail(int id)
        {
            var email = db.Emails.Find(id);

            if (email == null)
                return NotFound();

            db.Emails.Remove(email);
            db.SaveChanges();

            return Ok("Deleted Successfully");
        }

        // ============================================
        // 5️⃣ ACTIVATE EMAIL (ONLY ONE ACTIVE ALLOWED)
        // ============================================
        [HttpPut]
        [Route("activate/{id}")]
        public IHttpActionResult ActivateEmail(int id)
        {
            var emailToActivate = db.Emails.Find(id);

            if (emailToActivate == null)
                return NotFound();

            // Check if another email is already active
            var alreadyActive = db.Emails
                                  .FirstOrDefault(x => x.isActive == true);

            if (alreadyActive != null && alreadyActive.id != id)
            {
                return Content(HttpStatusCode.BadRequest,
                    "Another email is already active. Please deactivate it first.");
            }

            emailToActivate.isActive = true;
            db.SaveChanges();

            return Ok("Email Activated");
        }

        // ============================================
        // 6️⃣ DEACTIVATE EMAIL
        // ============================================
        [HttpPut]
        [Route("deactivate/{id}")]
        public IHttpActionResult DeactivateEmail(int id)
        {
            var email = db.Emails.Find(id);

            if (email == null)
                return NotFound();

            email.isActive = false;
            db.SaveChanges();

            return Ok("Email Deactivated");
        }
    }
    }
