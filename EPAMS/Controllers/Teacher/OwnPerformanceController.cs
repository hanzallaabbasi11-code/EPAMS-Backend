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
        [Route("SeeOwnPerformance")]
        public IHttpActionResult GetTeacherPerformance(string userId, int sessionId)
        {
            var response = new PerformanceDto();
            var kpiList = new List<KpiDto>();

            // 🔹 STEP 1: Get Employee Type from Teacher Department
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

            // 🔹 STEP 2: Get KPI IDs for session and employee type
            var kpiIds = db.EmployeSessionKPIs
                .Where(e => e.SessionID == sessionId && e.EmployeetypeID == employeeTypeId)
                .Select(e => e.KPIID)
                .Distinct()
                .ToList();

            // 🔹 STEP 3: Get KPI Records
            var kpis = db.KPIs
                .Where(k => kpiIds.Contains(k.id))
                .ToList();

            int overallScore = 0;
            int overallWeight = 0;

            foreach (var kpi in kpis)
            {
                // 🔹 Get Sub KPI IDs
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

                    // 🔹 STUDENT EVALUATION
                    if (sub.name.Trim() == "Student Evaluation")
                    {
                        var scores = db.StudentEvaluations
                            .Join(db.Enrollments,
                                se => se.enrollmentID,
                                e => e.id,
                                (se, e) => new { se, e })
                            .Where(x => x.se.SessionID == sessionId &&
                                        x.e.teacherID == userId)
                            .Select(x => (int?)x.se.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 PEER EVALUATION
                    else if (sub.name.Trim() == "Peer Evaluation")
                    {
                        var scores = db.PeerEvaluations
                            .Where(pe => pe.evaluateeID == userId)
                            .Select(pe => (int?)pe.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 OTHER KPIs
                    else
                    {
                        var scores = db.KPIScores
                            .Where(s => s.empID == userId && s.empKPIID == sub.id)
                            .Select(s => (int?)s.score)
                            .ToList();

                        avg = scores.Count > 0 ? scores.Average() ?? 0 : 0;
                    }

                    // 🔹 Sub KPI Weight
                    int weight = db.SessionKPIWeights
                        .Where(w => w.SubKPIID == sub.id && w.SessionID == sessionId)
                        .Select(w => w.Weight)
                        .FirstOrDefault() ?? 0;

                    // 🔹 Convert score (max scale = 4)
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

                // 🔹 Main KPI Weight
                int kpiWeight = db.SessionKPIWeights
                    .Where(w => w.KPIID == kpi.id &&
                                w.SessionID == sessionId &&
                                w.SubKPIID == null)
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