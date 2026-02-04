using EPAMS.Models;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Transactions;

namespace EPAMS.Controllers.HOD
{
    [RoutePrefix("api/Kpi")]

    public class KPIController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();


        [HttpGet]
        [Route("getemployeetype")]
        public HttpResponseMessage GetEmployeeType()
        {
            var res = db.EmployeeTypes.ToList();

            if (res.Count == 0)
            {
                return Request.CreateResponse(HttpStatusCode.NoContent, "No Employee Type Found");
            }
            return Request.CreateResponse(HttpStatusCode.OK, res);

        }



        [HttpPost]
        [Route("create-with-weight")]
        public IHttpActionResult CreateWithWeight(AddKpiDto dto)
        {
            if (dto == null || dto.SubKPIs == null || dto.SubKPIs.Count == 0)
                return BadRequest("Data incomplete.");

            try
            {
                using (var scope = new TransactionScope())
                {
                    // =========================
                    // 1. CREATE MAIN KPI
                    // =========================
                    KPI kpi = new KPI
                    {
                        name = dto.KPIName,
                        KPI_Employeetype = dto.EmployeeTypeId
                    };

                    db.KPIs.Add(kpi);
                    db.SaveChanges(); // KPI ID generated

                    // =========================
                    // 2. CREATE SUB KPIs + INITIAL WEIGHTS
                    // =========================
                    foreach (var subDto in dto.SubKPIs)
                    {
                        var sub = new SubKPI
                        {
                            KPIID = kpi.id,
                            name = subDto.Name
                        };

                        db.SubKPIs.Add(sub);
                        db.SaveChanges(); // SubKPI ID generated

                        db.SessionKPIWeights.Add(new SessionKPIWeight
                        {
                            SessionID = dto.SessionId,
                            KPIID = kpi.id,
                            SubKPIID = sub.id,
                            Weight = (int)subDto.Weight // user-given weight
                        });
                    }

                    db.SaveChanges();

                    // =========================
                    // 3. KPI-LEVEL WEIGHT ADJUSTMENT (INCREMENTAL LOGIC)
                    // =========================

                    // All weights for this session + employee type
                    var allWeights = db.SessionKPIWeights
                        .Where(w => w.SessionID == dto.SessionId &&
                                    db.KPIs.Any(k =>
                                        k.id == w.KPIID &&
                                        k.KPI_Employeetype == dto.EmployeeTypeId))
                        .ToList();

                    // Weights of newly added KPI
                    var newKpiWeights = allWeights
                        .Where(w => w.KPIID == kpi.id)
                        .ToList();

                    int newKpiTotal = newKpiWeights.Sum(w => w.Weight ?? 0);

                    // =========================
                    // CASE 1: FIRST KPI FOR THIS CATEGORY
                    // =========================
                    if (allWeights.Count == newKpiWeights.Count)
                    {
                        // Force first KPI to 100%
                        foreach (var w in newKpiWeights)
                        {
                            w.Weight = (int)Math.Round(
                                (decimal)(w.Weight ?? 0) * 100 / newKpiTotal,
                                MidpointRounding.AwayFromZero
                            );
                        }

                        db.SaveChanges();
                    }
                    else
                    {
                        // =========================
                        // CASE 2: KPI ALREADY EXISTS → SCALE EXISTING
                        // =========================
                        var existingWeights = allWeights
                            .Where(w => w.KPIID != kpi.id)
                            .ToList();

                        int existingTotal = existingWeights.Sum(w => w.Weight ?? 0);
                        int remaining = 100 - newKpiTotal;

                        decimal factor = (decimal)remaining / existingTotal;

                        foreach (var w in existingWeights)
                        {
                            w.Weight = (int)Math.Round(
                                (decimal)(w.Weight ?? 0) * factor,
                                MidpointRounding.AwayFromZero
                            );
                        }

                        db.SaveChanges();
                    }

                    // =========================
                    // 4. FINAL ROUNDING FIX (GUARANTEE 100)
                    // =========================
                    var finalWeights = db.SessionKPIWeights
                        .Where(w => w.SessionID == dto.SessionId &&
                                    db.KPIs.Any(k =>
                                        k.id == w.KPIID &&
                                        k.KPI_Employeetype == dto.EmployeeTypeId))
                        .ToList();

                    int finalSum = finalWeights.Sum(w => w.Weight ?? 0);

                    if (finalSum != 100 && finalWeights.Count > 0)
                    {
                        finalWeights.First().Weight += (100 - finalSum);
                        db.SaveChanges();
                    }

                    scope.Complete();

                    return Ok(new
                    {
                        Message = "KPI saved and weights adjusted incrementally to exactly 100%.",
                        Status = "Success"
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception("Adjustment Failed: " + ex.Message)
                );
            }
        }




        [HttpGet]
        [Route("view-weights/{sid}/{eid}")]
        public IHttpActionResult GetWeights(int sid, int eid)
        {
            try
            {
                var res = db.KPIs
                    .Where(k => k.KPI_Employeetype == eid)
                    .ToList() // Memory mein laa kar mapping karein
                    .Select(k => new {
                        kpiId = k.id,
                        kpiName = k.name,
                        // Is KPI ke andar jitne sub-kpis hain unka total weight calculate karein
                        totalKpiWeight = db.SessionKPIWeights
                                         .Where(w => w.SessionID == sid && w.KPIID == k.id)
                                         .Sum(w => (int?)w.Weight) ?? 0,

                        subKpis = (from w in db.SessionKPIWeights
                                   join s in db.SubKPIs on w.SubKPIID equals s.id
                                   where w.SessionID == sid && w.KPIID == k.id
                                   select new
                                   {
                                       subKpiId = s.id,
                                       subKpiName = s.name,
                                       weight = w.Weight
                                   }).ToList()
                    })
                    .Where(x => x.totalKpiWeight > 0) // Sirf wo dikhayein jinka weight set hai
                    .ToList();

                return Ok(res);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        [HttpGet]
        [Route("sessions")]
        public IHttpActionResult GetSessions() => Ok(db.Sessions.Select(s => new { s.id, s.name }).ToList());







    }
}
