using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using EPAMS.Models;

namespace EPAMS.Controllers.Director
{
    [RoutePrefix("api/OverallPerformance")]
    public class OverallPerformanceController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        //[HttpGet]
        //[Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]

        [HttpGet]
        [Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]
        public IHttpActionResult GetTeacherPerformanceAnalytics(string teacherId, int sessionId, int? kpiId = null)
        {
            try
            {
                // session ki tasdeeq karo
                var currentSession = db.Sessions.FirstOrDefault(s => s.id == sessionId);
                if (currentSession == null) return BadRequest("Invalid Session ID.");

                // ustad ka record dhundo
                var teacherData = db.Teachers.FirstOrDefault(t => t.userID == teacherId);
                if (teacherData == null) return BadRequest("Teacher not found.");

                // check karo k ustad society ka rukn hai ya nahi
                var isSocietyMember = db.SocietyAssignments
                    .Any(sa => sa.TeacherId == teacherId && sa.SessionId == sessionId);

                // is session ke active KPIs nikalo
                var activeKPIs = db.EmployeSessionKPIs
                    .Where(esk => esk.SessionID == sessionId &&
                          (kpiId == null || esk.KPIID == kpiId))
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPIs.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPIs.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    })
                    .ToList();

                // agar koi KPI nahi mili to khali jawab wapis karo
                if (!activeKPIs.Any())
                    return Ok(new { Status = "Empty", Message = "No KPIs configured for this session." });

                // pehla qadam: filter karne se PEHLE tamam KPIs ki total weight save karo
                // yeh is liye zaroori hai taake baad mein scale factor bana sakain
                double totalConfiguredWeight = 0;
                foreach (var item in activeKPIs)
                {
                    var w = db.SessionKPIWeights.FirstOrDefault(wt =>
                        wt.SessionID == sessionId &&
                        wt.KPIID == item.KPIID &&
                        wt.SubKPIID == item.SubKPIID);
                    totalConfiguredWeight += w?.Weight ?? 0;
                }

                // doosra qadam: jo KPIs is ustad par apply nahi hoti unhe list se nikalo
                // mustaqbil mein koi bhi nayi shart yahan add kar sakte hain
                activeKPIs = activeKPIs.Where(item =>
                {
                    string subName = (item.SubKPIName ?? "").ToLower();

                    // agar ustad society member nahi to society KPI hata do
                    if (subName.Contains("society") && !isSocietyMember)
                        return false;

                    return true;
                }).ToList();

                // teesra qadam: filter ke baad baqi bachi KPIs ki total weight nikalo
                double activeTotalWeight = 0;
                foreach (var item in activeKPIs)
                {
                    var w = db.SessionKPIWeights.FirstOrDefault(wt =>
                        wt.SessionID == sessionId &&
                        wt.KPIID == item.KPIID &&
                        wt.SubKPIID == item.SubKPIID);
                    activeTotalWeight += w?.Weight ?? 0;
                }

                // chautha qadam: scale factor banao
                // jo weight skip hui usse baaki KPIs mein proportion se distribute karo
                // taake percentage hamesha 100 mein se aaye — chahe koi bhi KPI skip ho
                // misaal: configured=100, active=70 — scaleFactor=1.428
                double scaleFactor = activeTotalWeight > 0
                    ? totalConfiguredWeight / activeTotalWeight
                    : 1.0;

                // student evaluation ka average nikalo
                var studentAvg = db.StudentEvaluations
                    .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
                    .Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;

                // peer evaluation ka average nikalo
                var peerAvg = db.PeerEvaluations
                    .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
                    .Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;

                // society evaluation ka average nikalo
                var societyAvg = db.SocietyEvaluations
                    .Where(se => se.EvaluateeId == teacherId && se.SessionId == sessionId)
                    .Select(x => (double?)x.Score).DefaultIfEmpty().Average() ?? 0;

                // CHR ka raw data nikalo
                var chrRawData = db.CHRs
                    .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
                    .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                    .ToList();

                // late aane aur jaldi jaane ke hisaab se CHR score calculate karo
                var chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x =>
                    {
                        int total = x.LateIn + x.LeftEarly;
                        if (total >= 10) return 0.0; // bohat zyada — zero score
                        if (total >= 6) return 3.0; // thora zyada
                        if (total >= 1) return 4.0; // thori kami
                        return 5.0;                  // bilkul theek
                    }).Average()
                    : 0.0;

                // confidential scores database se nikalo
                var confScores = db.KPIScores
                    .Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();

                // KPIs ko group karo aur breakdown tayyar karo
                var groupedKPIs = activeKPIs.GroupBy(k => new { k.KPIID, k.KPIName });
                var finalBreakdown = new List<object>();
                double totalAchieved = 0;
                double totalWeight = 0;

                foreach (var kpiGroup in groupedKPIs)
                {
                    var subDetails = new List<object>();
                    double kpiAchieved = 0;
                    double kpiWeight = 0;

                    foreach (var item in kpiGroup)
                    {
                        var weightEntry = db.SessionKPIWeights.FirstOrDefault(w =>
                            w.SessionID == sessionId &&
                            w.KPIID == item.KPIID &&
                            w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;

                        // paanchwa qadam: har SubKPI ki weight par scale factor lagao
                        // is se skip hone wali weight automatically baaki mein divide ho jati hai
                        double scaledWeight = Math.Round(weight * scaleFactor, 2);

                        string subName = (item.SubKPIName ?? "").ToLower();
                        double multiplier = 0;
                        double maxScale = 4.0;

                        // SubKPI ke naam ke hisaab se sahi score assign karo
                        if (subName.Contains("student"))
                            multiplier = studentAvg;
                        else if (subName.Contains("peer"))
                            multiplier = peerAvg;
                        else if (subName.Contains("society"))
                            multiplier = isSocietyMember ? societyAvg : 0;
                        else if (subName.Contains("confidential"))
                            multiplier = 0; // confidential score frontend se aata hai (Dexie)
                        else if (subName.Contains("chr") || subName.Contains("class held report"))
                        {
                            multiplier = chrAvg;
                            maxScale = 5.0; // CHR ka scale 5 hai
                        }
                        else
                        {
                            // baaki KPIs ka score database se nikalo
                            var specificScore = confScores
                                .Where(cs => cs.empKPIID == item.id)
                                .Average(cs => (double?)cs.score);
                            multiplier = specificScore ?? 0;
                            maxScale = 5.0;
                        }

                        // scaled weight se achieved score calculate karo
                        double achieved = Math.Round((multiplier / maxScale) * scaledWeight, 2);

                        subDetails.Add(new
                        {
                            SubName = item.SubKPIName,
                            SubMax = scaledWeight,   // frontend ko scaled weight milegi
                            SubAchieved = achieved,
                            MaxScale = maxScale,
                            RawScore = multiplier,
                            IsSociety = subName.Contains("society") && isSocietyMember,
                            IsCHR = subName.Contains("chr") || subName.Contains("class held report")
                        });

                        kpiAchieved += achieved;
                        kpiWeight += scaledWeight; // scaled weight use karo
                    }

                    finalBreakdown.Add(new
                    {
                        KPIName = kpiGroup.Key.KPIName,
                        KPIWeight = Math.Round(kpiWeight, 2),
                        KPIAchieved = Math.Round(kpiAchieved, 2),
                        SubDetails = subDetails
                    });

                    totalAchieved += kpiAchieved;
                    totalWeight += kpiWeight;
                }

                // aakhri hisaab — yeh hamesha 100 mein se hogi
                double overallPercentage = totalWeight > 0
                    ? Math.Round((totalAchieved / totalWeight) * 100, 2)
                    : 0;

                return Ok(new
                {
                    Status = "Success",
                    TeacherName = teacherData?.name,
                    Department = teacherData?.department,
                    SessionName = currentSession.name,
                    IsSocietyMember = isSocietyMember,
                    OverallPercentage = overallPercentage,
                    ChrAvgScore = Math.Round(chrAvg, 2),
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]

        [Route("GetKpiTypesBySession/{sessionId}")]
        public IHttpActionResult GetKpiTypesBySession(int sessionId)
        {
            var types = db.EmployeSessionKPIs
                .Where(esk => esk.SessionID == sessionId)
                .Select(esk => new {
                    id = esk.KPIID,                    // ✅ EmployeetypeID ki jagah KPIID
                    name = db.KPIs
                        .Where(k => k.id == esk.KPIID) // ✅ KPI table se naam
                        .Select(k => k.name)
                        .FirstOrDefault()
                })
                .Distinct()
                .ToList()
                .GroupBy(x => x.id)                    // ✅ Duplicate KPIs hata
                .Select(g => new { id = g.Key, name = g.First().name })
                .ToList();

            return Ok(types);
        }


        //teacher aginst seesion
        [HttpGet]
 [Route("GetTeachersBySession/{sessionId}")]
     public IHttpActionResult GetTeachersBySession(int sessionId)
        {
            try
            {
                var enrolledTeachers = db.Enrollments
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => new {
                        UserID = e.Teacher.userID,
                        Name = e.Teacher.name
                    })
                    .Distinct()
                    .ToList();

                return Ok(enrolledTeachers);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }


      //  session
 [HttpGet]
 [Route("list")]
 public IHttpActionResult GetAll()
        {
            try
            {
                var sessions = db.Sessions
                    .Select(s => new
                    {
                        s.id,
                        s.name
                    }).ToList();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
