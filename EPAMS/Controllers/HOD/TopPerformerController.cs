using EPAMS.Models;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.HOD
{
    [RoutePrefix("api/Performer")]
    public class TopPerformerController : ApiController
    {
        EPAMSEntities db=new EPAMSEntities();

        [HttpGet]
        [Route("GetBestPerformerTeacher")]
        public IHttpActionResult GetBestPerformerTeacher(int sessionId)
        {
            try
            {
                // Get all teachers in session
                var teachers = db.Enrollments
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => new
                    {
                        e.teacherID,
                        TeacherName = e.Teacher.name,
                        Department = e.Teacher.department
                    })
                    .Distinct()
                    .ToList();

                if (!teachers.Any())
                    return Ok(new { Message = "No teachers found." });

                double highestScore = -1;

                object bestTeacher = null;

                foreach (var t in teachers)
                {
                    double percentage = CalculateOverallPerformance(
                        t.teacherID,
                        sessionId
                    );

                    if (percentage > highestScore)
                    {
                        highestScore = percentage;

                        bestTeacher = new
                        {
                            TeacherID = t.teacherID,
                            TeacherName = t.TeacherName,
                            Department = t.Department,
                            Percentage = Math.Round(percentage, 2)
                        };
                    }
                }

                return Ok(bestTeacher);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private double CalculateOverallPerformance(
    string teacherId,
    int sessionId
)
        {
            try
            {
                // =========================
                // Society Membership Check
                // =========================
                var isSocietyMember = db.SocietyAssignments
                    .Any(sa =>
                        sa.TeacherId == teacherId &&
                        sa.SessionId == sessionId);

                // =========================
                // Active KPIs
                // =========================
                var activeKPIs = db.EmployeSessionKPIs
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,

                        KPIName = db.KPIs
                            .Where(k => k.id == esk.KPIID)
                            .Select(k => k.name)
                            .FirstOrDefault(),

                        SubKPIName = db.SubKPIs
                            .Where(sk => sk.id == esk.SubKPIID)
                            .Select(sk => sk.name)
                            .FirstOrDefault()
                    })
                    .ToList();

                // =========================
                // Remove Society KPI
                // if teacher not society member
                // =========================
                activeKPIs = activeKPIs
                    .Where(item =>
                    {
                        string subName =
                            (item.SubKPIName ?? "").ToLower();

                        if (subName.Contains("society")
                            && !isSocietyMember)
                            return false;

                        return true;
                    })
                    .ToList();

                // =========================
                // Student Average
                // =========================
                var studentAvg = db.StudentEvaluations
                    .Where(se =>
                        se.Enrollment.teacherID == teacherId &&
                        se.Enrollment.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // Peer Average
                // =========================
                var peerAvg = db.PeerEvaluations
                    .Where(pe =>
                        pe.evaluateeID == teacherId &&
                        pe.PeerEvaluator.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // Society Average
                // =========================
                var societyAvg = db.SocietyEvaluations
                    .Where(se =>
                        se.EvaluateeId == teacherId &&
                        se.SessionId == sessionId)
                    .Select(x => (double?)x.Score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // CHR Average
                // =========================
                var chrRawData = db.CHRs
                    .Where(c =>
                        c.TeacherID == teacherId &&
                        c.sessionID == sessionId)
                    .Select(x => new
                    {
                        LateIn = x.LateIn ?? 0,
                        LeftEarly = x.LeftEarly ?? 0
                    })
                    .ToList();

                double chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x =>
                    {
                        int total =
                            x.LateIn + x.LeftEarly;

                        if (total >= 10) return 0.0;

                        if (total >= 6) return 3.0;

                        if (total >= 1) return 4.0;

                        return 5.0;

                    }).Average()
                    : 0.0;

                // =========================
                // KPI Scores
                // =========================
                var confScores = db.KPIScores
                    .Where(ks =>
                        ks.empID == teacherId &&
                        ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();

                // =========================
                // Final KPI Calculation
                // =========================
                double totalAchieved = 0;
                double totalWeight = 0;

                foreach (var item in activeKPIs)
                {
                    var weightEntry =
                        db.SessionKPIWeights
                        .FirstOrDefault(w =>
                            w.SessionID == sessionId &&
                            w.KPIID == item.KPIID &&
                            w.SubKPIID == item.SubKPIID);

                    double weight =
                        weightEntry?.Weight ?? 0;

                    string subName =
                        (item.SubKPIName ?? "")
                        .ToLower();

                    double multiplier = 0;
                    double maxScale = 4.0;

                    // =========================
                    // Student Evaluation
                    // =========================
                    if (subName.Contains("student"))
                    {
                        multiplier = studentAvg;
                    }

                    // =========================
                    // Peer Evaluation
                    // =========================
                    else if (subName.Contains("peer"))
                    {
                        multiplier = peerAvg;
                    }

                    // =========================
                    // Society
                    // =========================
                    else if (subName.Contains("society"))
                    {
                        multiplier = isSocietyMember
                            ? societyAvg
                            : 0;
                    }

                    // =========================
                    // Confidential
                    // =========================
                    else if (subName.Contains("confidential"))
                    {
                        multiplier = 0;
                    }

                    // =========================
                    // CHR
                    // =========================
                    else if (
                        subName.Contains("chr") ||
                        subName.Contains("class held report"))
                    {
                        multiplier = chrAvg;
                        maxScale = 5.0;
                    }

                    // =========================
                    // Other KPI Scores
                    // =========================
                    else
                    {
                        var specificScore = confScores
                            .Where(cs => cs.empKPIID == item.id)
                            .Average(cs => (double?)cs.score);

                        multiplier = specificScore ?? 0;

                        maxScale = 5.0;
                    }

                    // =========================
                    // Weighted Achieved
                    // =========================
                    double achieved =
                        (multiplier / maxScale)
                        * weight;

                    totalAchieved += achieved;
                    totalWeight += weight;
                }

                // =========================
                // Final Percentage
                // =========================
                double overallPercentage =
                    totalWeight > 0
                    ? (totalAchieved / totalWeight) * 100
                    : 0;

                return Math.Round(overallPercentage, 2);
            }
            catch
            {
                return 0;
            }
        }



        [HttpGet]
        [Route("GetTeachersCount")]
        public IHttpActionResult GetTeachersCount(int sessionId)
        {
            try
            {
                var totalTeachers = db.Enrollments
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => e.teacherID)
                    .Distinct()
                    .Count();

                return Ok(new
                {
                    SessionId = sessionId,
                    TotalTeachers = totalTeachers
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}









//        Explenation


//        STEP 1 — Get All Teachers of Session
//        var teachers = db.Enrollment
//            .Where(e => e.sessionID == sessionId)

//This gets all enrollments of that session.

//Example:

//Teacher Course  Session
//T1  CS101	15
//T2 CS102	15
//T1 CS103	15

//Then:

//.Select(e => new
//{
//    e.teacherID,
//    TeacherName = e.Teacher.name,
//    Department = e.Teacher.department
//    })
//.Distinct()
//.ToList();

//    This converts data into:

//[
//  {
//    "teacherID":"T1",
//    "TeacherName":"Ali"
//  },
//  {
//    "teacherID":"T2",
//    "TeacherName":"Ahmed"
//  }
//]

//Distinct() removes duplicates because one teacher may teach multiple courses.

//So now you have:

//✅ all unique teachers of that session

//STEP 2 — Check Empty Data
//if (!teachers.Any())
//    return Ok(new { Message = "No teachers found." });

//If session has no teachers:

//API returns:

//{
//    "Message":"No teachers found."
//}
//STEP 3 — Loop Through Every Teacher
//foreach (var t in teachers)

//Now API checks each teacher one by one.

//STEP 4 — Calculate Performance
//double percentage = CalculateOverallPerformance(
//    t.teacherID,
//    sessionId
//);

//This calls your helper method.

//That helper method calculates:

//Student Evaluation
//Peer Evaluation
//Society Evaluation
//CHR
//KPI Scores
//KPI Weights
//Final weighted percentage

//Example:

//Teacher Percentage
//T1	88
//T2	93
//T3	79
//STEP 5 — Compare Highest Score

//Initially:

//double highestScore = -1;

//Then:

//if (percentage > highestScore)

//Example:

//    First Teacher

//T1 = 88

//88 > -1

//TRUE

//So:

//highestScore = 88

//bestTeacher = T1

//Second Teacher

//T2 = 93

//93 > 88

//TRUE

//Now:

//highestScore = 93

//bestTeacher = T2

//Third Teacher

//T3 = 79

//79 > 93

//FALSE

//Ignore.

//At the end:

//bestTeacher = T2

//STEP 6 — Return Best Teacher
//return Ok(bestTeacher);

//Final response:

//{
//    "TeacherID": "T2",
//  "TeacherName": "Ahmed",
//  "Department": "CS",
//  "Percentage": 93
//}
//Visual Flow
//Get Session Teachers
//        ↓
//Loop All Teachers
//        ↓
//Calculate KPI Percentage
//        ↓
//Compare With Highest
//        ↓
//Store Best Teacher
//        ↓
//Return Final Best Performer
//    }
//}
