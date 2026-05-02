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

        [Route("SeeOwnPerformance")]
        public IHttpActionResult GetTeacherPerformance(string userId, int sessionId)
        {
            EPAMSEntities db = new EPAMSEntities();

            var response = new PerformanceDto();
            var kpiList = new List<KpiDto>();

            // 🔹 STEP 1: Get Employee Type ID from userId
            var role = db.Teachers
                .Where(u => u.userID == userId)
                .Select(u => u.department)
                .FirstOrDefault();

            if (role == "CS")
            {
                employeeTypeId = 1;
            }
            else if (role == "Non CS")
            {
                employeeTypeId = 2;
            }

            var kpiIds = db.EmployeSessionKPIs
            .Where(e => e.SessionID == sessionId && e.EmployeetypeID == employeeTypeId)
            .Select(e => e.KPIID)
            .Distinct()
            .ToList();


            // 🔹 STEP 2: Get KPIs for this employee type
            var kpis = db.KPIs
                    .Where(k => kpiIds.Contains(k.id))
                    .ToList();

            int overallScore = 0;
            int overallWeight = 0;

            foreach (var kpi in kpis)
            {
                var subKpiIds = db.EmployeSessionKPIs
                .Where(e => e.KPIID == kpi.id &&
                e.SessionID == sessionId &&
                e.EmployeetypeID == employeeTypeId)
                .Select(e => e.SubKPIID)
                .ToList();

                var subKpis = db.SubKPIs
                 .Where(s => subKpiIds.Contains(s.id))
                 .ToList();

                int kpiScore = 0;
                int kpiTotal = 0;

                var subKpiDtos = new List<SubKpiDto>();

                foreach (var sub in subKpis)
                {
                    double avg = 0;

                    // 🔹 STUDENT
                    if (sub.name.Trim().ToLower() == "student evaluation")
                    {
                        var scores = db.StudentEvaluations
                            .Where(se => se.SessionID == sessionId &&
                                db.Enrollments.Any(e =>
                                    e.id == se.enrollmentID &&
                                    e.teacherID == userId))
                            .Select(se => (int?)se.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 PEER
                    else if (sub.name.Trim().ToLower() == "peer evaluation")
                    {
                        var scores = db.PeerEvaluations
                            .Where(pe => pe.evaluateeID == userId && pe.SessionID == sessionId)
                            .Select(pe => (int?)pe.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 OTHER
                    else
                    {
                        var scores = db.KPIScores
                            .Where(s => s.empID == userId && s.empKPIID == sub.id)
                            .Select(s => (int?)s.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 Sub KPI weight
                    int weight = db.SessionKPIWeights
                         .Where(w => w.SubKPIID == sub.id && w.SessionID == sessionId)
                         .Select(w => w.Weight)
                         .FirstOrDefault() ?? 0;

                    // 🔹 Convert to marks (max = 4)
                    int finalScore = (int)Math.Round((avg / 4.0) * weight);

                    subKpiDtos.Add(new SubKpiDto
                    {
                        Name = sub.name,
                        Score = finalScore,
                        Total = weight
                    });

                    kpiScore += finalScore;
                    kpiTotal += weight;
                }

                // 🔹 Main KPI weight (80%, 20%)
                int kpiWeight = db.SessionKPIWeights
                    .Where(w => w.KPIID == kpi.id && w.SessionID == sessionId && w.SubKPIID == null)
                    .Select(w => w.Weight)
                    .FirstOrDefault() ?? 0;

                double kpiPercentage = kpiTotal > 0 ? (double)kpiScore / kpiTotal : 0;
                int weightedKpiScore = (int)Math.Round(kpiPercentage * kpiWeight);

                overallScore += kpiScore;
                overallWeight += kpiTotal;

                kpiList.Add(new KpiDto
                {
                    Name = kpi.name,
                    Score = kpiScore,
                    Total = kpiTotal,
                    SubKpis = subKpiDtos
                });
            }

            response.Kpis = kpiList;
            response.OverallPercentage = overallWeight > 0
                ? (int)Math.Round((double)overallScore * 100 / overallWeight)
                : 0;

            response.ObtainedPoints = overallScore;
            response.TotalPoints = overallWeight;

            return Ok(response);
        }
    }
}