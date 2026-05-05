using EPAMS.Models;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace EPAMS.Controllers.Teacher
{
    [RoutePrefix("api/teacher/performance")]
    public class OwnPerformanceController : ApiController
    {
        int employeeTypeId;
        EPAMSEntities db = new EPAMSEntities();

        [HttpGet]
        [Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]
        public IHttpActionResult GetTeacherPerformanceAnalytics(string teacherId, int sessionId)
        {
            try
            {
                // 1. Session + Teacher
                var currentSession = db.Sessions.FirstOrDefault(s => s.id == sessionId);
                if (currentSession == null) return BadRequest("Invalid Session ID.");

                var teacherData = db.Teachers.FirstOrDefault(t => t.userID == teacherId);
                if (teacherData == null) return BadRequest("Teacher not found.");

                // ================= SOCIETY CHECK /// same project =================
                var isSocietyMember = db.SocietyAssignments
                    .Any(sa => sa.TeacherId == teacherId && sa.SessionId == sessionId);


                // 2. Active KPIs
                var activeKPIs = db.EmployeSessionKPIs
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPIs.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPIs.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    })
                    .ToList();

                if (!activeKPIs.Any())
                    return Ok(new { Status = "Empty", Message = "No KPIs configured for this session." });

                // ================= FILTER SOCIETY KPI =================
                activeKPIs = activeKPIs.Where(item =>
                {
                    string subName = (item.SubKPIName ?? "").ToLower();

                    if (subName.Contains("society") && !isSocietyMember)
                        return false;
                    ////else project specific KPIs can also be filtered here if needed by checking subName for certain keywords and validating against teacher's involvement in those projects
                    return true;
                }).ToList();

                // 3. Averages
                var studentAvg = db.StudentEvaluations
                    .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                var peerAvg = db.PeerEvaluations
                    .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                var societyAvg = db.SocietyEvaluations
                    .Where(se => se.EvaluateeId == teacherId && se.SessionId == sessionId)
                    .Select(x => (double?)x.Score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;////same project

                // 3. CHR Average Score — Session filter ke saath
                // Sirf us session ki CHR records consider hongi
                var chrAvg = 0.0;

                var chrRawData = db.CHRs
                    .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
                    .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                    .ToList();

                chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x => {
                        int total = x.LateIn + x.LeftEarly;
                        if (total >= 10) return 0.0;
                        if (total >= 6) return 3.0;
                        if (total >= 1) return 4.0;
                        return 5.0;
                    }).Average()
                    : 0.0;

                var confScores = db.KPIScores
                    .Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();



                // 4. Breakdown
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
                        string subName = (item.SubKPIName ?? "").ToLower();

                        double multiplier = 0;
                        double maxScale = 4.0;

                        // ================= SCORE LOGIC =================
                        if (subName.Contains("student") || subName.Contains("Student Evalution"))
                        {
                            multiplier = studentAvg;
                        }
                        else if (subName.Contains("peer") || subName.Contains("Peer Evalution"))
                        {
                            multiplier = peerAvg;
                        }
                        else if (subName.Contains("society") || subName.Contains("Society Management"))
                        {
                            multiplier = isSocietyMember ? societyAvg : 0;
                        }
                        else if (subName.Contains("confidential") || subName.Contains("Confidential Evalution"))
                        {
                            multiplier = 0;
                        }
                        else if (subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report"))
                        {
                            multiplier = chrAvg;   // ← CHR score 0-5
                            maxScale = 5.0;
                        }
                        else
                        {
                            var specificScore = confScores
                                .Where(cs => cs.empKPIID == item.id)
                                .Average(cs => (double?)cs.score);

                            multiplier = specificScore ?? 0;
                            maxScale = 5.0;
                        }

                        double achieved = Math.Round((multiplier / maxScale) * weight, 2);

                        subDetails.Add(new
                        {
                            SubName = item.SubKPIName,
                            SubMax = weight,
                            SubAchieved = achieved,
                            MaxScale = maxScale,
                            RawScore = multiplier,
                            IsSociety = subName.Contains("society") || subName.Contains("society Management") && isSocietyMember,
                            IsCHR = subName.Contains("chr") || subName.Contains("CHR") || subName.Contains("class held report")

                        });

                        kpiAchieved += achieved;
                        kpiWeight += weight;
                    }

                    finalBreakdown.Add(new
                    {
                        KPIName = kpiGroup.Key.KPIName,
                        KPIWeight = kpiWeight,
                        KPIAchieved = Math.Round(kpiAchieved, 2),
                        SubDetails = subDetails
                    });

                    totalAchieved += kpiAchieved;
                    totalWeight += kpiWeight;
                }

                // 5. FINAL SCORE
                double overallPercentage = totalWeight > 0
                    ? Math.Round((totalAchieved / totalWeight) * 100, 2)
                    : 0;

                // 6. RESPONSE
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
    }



}
