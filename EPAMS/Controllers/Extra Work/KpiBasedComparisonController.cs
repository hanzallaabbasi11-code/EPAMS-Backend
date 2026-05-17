using EPAMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EPAMS.Controllers.Extra_Work
{
    [RoutePrefix("api/KpiBaseComparision")]
    public class KpiBasedComparisonController : ApiController
    {

        private EPAMSEntities db = new EPAMSEntities();

        // 1. GET TEACHERS BY SESSION
        // Jab session select ho, to sirf wahi teachers aayenge jo active hain ya enrolled hain
        [HttpGet]
        [Route("GetTeachersBySession/{sessionId}")]
        public IHttpActionResult GetTeachersBySession(int sessionId)
        {
            try
            {
                // Un teachers ki list nikalen jo is session mein enrolled hain
                var enrolledTeacherIds = db.Enrollments
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => e.teacherID)
                    .Distinct()
                    .ToList();

                var teachers = db.Teachers
                    .Where(t => enrolledTeacherIds.Contains(t.userID))
                    .Select(t => new { id = t.userID, name = t.name, department = t.department })
                    .ToList();

                return Ok(teachers);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 2. CORE COMPARISON ENGINE
        [HttpGet]
        [Route("GetTeacherComparison")]
        public IHttpActionResult GetTeacherComparison(int sessionId, string teacher1Id, string teacher2Id, int? kpiId = null, int? subKpiId = null)
        {
            try
            {
                var sessionWeights = db.SessionKPIWeights.Where(w => w.SessionID == sessionId).ToList();
                var activeEsk = db.EmployeSessionKPIs.Where(esk => esk.SessionID == sessionId).ToList();

                // Filters Apply Karein
                if (subKpiId.HasValue && subKpiId.Value > 0)
                {
                    activeEsk = activeEsk.Where(e => e.SubKPIID == subKpiId.Value).ToList();
                }
                else if (kpiId.HasValue && kpiId.Value > 0)
                {
                    activeEsk = activeEsk.Where(e => e.KPIID == kpiId.Value).ToList();
                }

                if (!activeEsk.Any())
                    return Ok(new { Teacher1 = (object)null, Teacher2 = (object)null });

                // Process Data for Both Teachers
                var teacher1Data = ProcessSingleTeacherPerformance(teacher1Id, sessionId, activeEsk, sessionWeights);
                var teacher2Data = ProcessSingleTeacherPerformance(teacher2Id, sessionId, activeEsk, sessionWeights);

                return Ok(new
                {
                    Teacher1 = teacher1Data,
                    Teacher2 = teacher2Data
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Helper Method to extract performance payload uniquely
        private object ProcessSingleTeacherPerformance(string teacherId, int sessionId, List<EmployeSessionKPI> activeEsk, List<SessionKPIWeight> sessionWeights)
        {
            var teacher = db.Teachers.FirstOrDefault(t => t.userID == teacherId);
            if (teacher == null) return null;

            var breakdownList = new List<object>();
            double overallAccumulatedScore = 0.0;
            double totalWeightContext = 0.0;

            foreach (var config in activeEsk)
            {
                var subKpiObj = db.SubKPIs.FirstOrDefault(sk => sk.id == config.SubKPIID);
                if (subKpiObj == null) continue;

                string subName = (subKpiObj.name ?? "").ToLower().Trim();
                int currentSubKpiId = subKpiObj.id;

                double currentWeight = sessionWeights
                    .Where(w => w.SubKPIID == currentSubKpiId)
                    .Select(w => (double?)w.Weight)
                    .FirstOrDefault() ?? 100.0;

                // Eligibility Checks
                if (subName.Contains("society"))
                {
                    bool isSocietyMember = db.SocietyAssignments?.Any(sa => sa.TeacherId == teacherId && sa.SessionId == sessionId) ?? false;
                    if (!isSocietyMember) continue;
                }
                //if (subName.Contains("project"))
                //{
                //    bool isProjectMember = teacher.IsCommitteeMember ?? false;
                //    if (!isProjectMember) continue;
                //}

                // Score Calculation
                double rawScore = 0.0;
                double maxScale = 5.0;

                if (subName.Contains("student"))
                {
                    maxScale = 4.0;
                    rawScore = db.StudentEvaluations.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId).Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;
                }
                else if (subName.Contains("peer"))
                {
                    maxScale = 4.0;
                    rawScore = db.PeerEvaluations.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId).Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;
                }
                else if (subName.Contains("society"))
                {
                    maxScale = 4.0;
                    rawScore = db.SocietyEvaluations?.Where(se => se.EvaluateeId == teacherId && se.SessionId == sessionId).Select(x => (double?)x.Score).DefaultIfEmpty().Average() ?? 0;
                }
                //else if (subName.Contains("project"))
                //{
                //    maxScale = 4.0;
                //    rawScore = db.ProjectEvaluation?.Where(pe => pe.evaluateeID == teacherId && pe.sessionID == sessionId).Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;
                //}
                else if (subName.Contains("chr") || subName.Contains("class held"))
                {
                    maxScale = 5.0;
                    var chrData = db.CHRs?.Where(c => c.TeacherID == teacherId && c.sessionID == sessionId).Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 }).ToList();
                    rawScore = (chrData != null && chrData.Any()) ? chrData.Select(x => {
                        int total = x.LateIn + x.LeftEarly;
                        if (total >= 10) return 0.0;
                        if (total >= 6) return 3.0;
                        if (total >= 1) return 4.0;
                        return 5.0;
                    }).Average() : 0.0;
                }
                else
                {
                    maxScale = 5.0;
                    rawScore = db.KPIScores.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId && ks.EmployeSessionKPI.SubKPIID == currentSubKpiId).Select(x => (double?)x.score).DefaultIfEmpty().Average() ?? 0;
                }

                double normalizedScore = maxScale > 0 ? (rawScore / maxScale) * 100 : 0;
                double componentAchieved = Math.Round((normalizedScore * currentWeight) / 100, 2);

                breakdownList.Add(new
                {
                    SubKPI = subKpiObj.name,
                    Category = GetCategory(subName),
                    RawScore = Math.Round(rawScore, 2),
                    NormalizedScore = Math.Round(normalizedScore, 2),
                    SubKPITotalWeight = currentWeight,
                    SubKPIObtainedWeight = componentAchieved
                });

                overallAccumulatedScore += componentAchieved;
                totalWeightContext += currentWeight;
            }

            double finalPercentage = totalWeightContext > 0 ? Math.Round((overallAccumulatedScore / totalWeightContext) * 100, 2) : 0;

            return new
            {
                TeacherID = teacher.userID,
                TeacherName = teacher.name,
                Department = teacher.department,
                Designation = teacher.designation,
                OverallPercentage = finalPercentage,
                TotalSessionWeight = Math.Round(totalWeightContext, 2),
                TotalObtainedWeight = Math.Round(overallAccumulatedScore, 2),
                Breakdown = breakdownList
            };
        }

        private string GetCategory(string subName)
        {
            if (subName.Contains("student") || subName.Contains("peer") || subName.Contains("society") || subName.Contains("project"))
                return "Qualitative";
            if (subName.Contains("chr") || subName.Contains("class held"))
                return "Attendance";
            return "Quantitative";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { db.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
