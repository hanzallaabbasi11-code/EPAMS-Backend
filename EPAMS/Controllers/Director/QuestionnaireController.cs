using EPAMS.Models;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity; // for Include

namespace EPAMS.Controllers.Director
{
    [RoutePrefix("api/Questionnaire")]
    public class QuestionnaireController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        [HttpPost]
        [Route("Create")]
        public IHttpActionResult CreateQuestionnaire(QuestionCreateDto model)
        {
            if (model == null || model.Questions == null || model.Questions.Count == 0)
            {
                return BadRequest("Invalid data");
            }

            // 1️⃣ Create Questionnaire
            var questionnaire = new Questionare
            {
                type = model.EvaluationType,// OR map to text if needed
                flag = "0" // DEFAULT — DO NOT CHANGE
            };

            db.Questionares.Add(questionnaire);
            db.SaveChanges(); // 🔥 ID generated here

            // 2️⃣ Insert Questions
            foreach (var q in model.Questions)
            {
                var question = new Question
                {
                    QuestionareID = questionnaire.id,
                    QuestionText = q
                };

                db.Questions.Add(question);
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Questionnaire saved successfully",
                QuestionnaireId = questionnaire.id
            });
        }

        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll()
        {
            var data = db.Questionares
                .Select(q => new QuestionnaireListDto
                {
                    Id = q.id,
                    Type = q.type,
                    Flag = q.flag,
                    QuestionCount = q.Questions.Count()
                })
                .ToList();

            return Ok(data);
        }



        [HttpPost]
        [Route("Toggle")]
        public IHttpActionResult ToggleQuestionnaire(ToggleQuestionnaireDto model)
        {
            var questionnaire = db.Questionares.Find(model.QuestionnaireId);

            if (questionnaire == null)
                return NotFound();

            if (model.TurnOn)
            {
                // ❌ Check if same type is already ON
                bool alreadyActive = db.Questionares.Any(q =>
                    q.type == questionnaire.type &&
                    q.flag == "1" &&
                    q.id != questionnaire.id
                );

                if (alreadyActive)
                {
                    return BadRequest("Another evaluation of this type is already active.");
                }

                questionnaire.flag = "1";
            }
            else
            {
                questionnaire.flag = "0";
            }

            db.SaveChanges();

            return Ok(new { message = "Status updated successfully" });
        }

        [HttpPost]
        [Route("SaveAllChanges")]
        public IHttpActionResult SaveAllChanges(SaveQuestionnaireChangesDto model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            // 1️⃣ DELETE REMOVED QUESTIONS
            if (model.DeletedIds != null && model.DeletedIds.Count > 0)
            {
                var deleteQuestions = db.Questions
                    .Where(q => model.DeletedIds.Contains(q.QuestionID))
                    .ToList();

                 foreach (var q in deleteQuestions)
    {
        db.Questions.Remove(q);
    }
            }

            // 2️⃣ ADD & UPDATE QUESTIONS
            foreach (var q in model.Questions)
            {
                if (q.Id == 0)
                {
                    // ➕ NEW QUESTION
                    var newQuestion = new Question
                    {
                        QuestionareID = model.QuestionnaireId,
                        QuestionText = q.QuestionText
                    };
                    db.Questions.Add(newQuestion);
                }
                else
                {
                    // ✏️ UPDATE EXISTING QUESTION
                    var existing = db.Questions.Find(q.Id);
                    if (existing != null)
                    {
                        existing.QuestionText = q.QuestionText;
                    }
                }
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Questionnaire updated successfully"
            });
        }



      

[HttpGet]
    [Route("GetById/{id}")]
    public IHttpActionResult GetById(int id)
    {
        try
        {
            // 1️⃣ Load questionnaire with questions
            var questionnaire = db.Questionares
                .Include(q => q.Questions) // load questions explicitly
                .FirstOrDefault(q => q.id == id);

            if (questionnaire == null)
                return NotFound();

            // 2️⃣ Project in memory
            var result = new
            {
                id = questionnaire.id,
                title = questionnaire.type,          // using type as title
                evaluationType = questionnaire.type,
                questions = questionnaire.Questions
                    .Select(qq => new
                    {
                        id = qq.QuestionID,
                        questionText = qq.QuestionText
                    })
                    .ToList() // ✅ safe now
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            // return the actual exception to debug
            return InternalServerError(ex);
        }
    }




}

}


