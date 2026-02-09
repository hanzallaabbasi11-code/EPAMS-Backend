using EPAMS.Models;
//using EPAMS.Models.KPI;
using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/kpi")]
    public class KPIController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        // 1. CREATE KPI WITH WEIGHTS
        [HttpPost]
        [Route("create-with-weight")]
        public IHttpActionResult CreateWithWeight(AddKpiDto dto)
        {
            if (dto == null || dto.SubKPIs == null || dto.SubKPIs.Count == 0)
                return BadRequest("Data incomplete.");

            decimal mainKpiTargetWeight = (decimal)dto.RequestedKPIWeight;
            decimal subKpiTotalInput = dto.SubKPIs.Sum(s => (decimal)s.Weight);

            if (mainKpiTargetWeight >= 100)
                return BadRequest("Main KPI weight must be less than 100.");

            try
            {
                using (var scope = new TransactionScope())
                {
                    KPI kpi = new KPI { name = dto.KPIName, KPI_Employeetype = dto.EmployeeTypeId };
                    db.KPIs.Add(kpi);
                    db.SaveChanges();

                    decimal subFactor = subKpiTotalInput > 0 ? mainKpiTargetWeight / subKpiTotalInput : 0;

                    foreach (var subDto in dto.SubKPIs)
                    {
                        var subObj = new SubKPI { KPIID = kpi.id, name = subDto.Name };
                        db.SubKPIs.Add(subObj);
                        db.SaveChanges();

                        decimal adjustedSubWeight = (decimal)subDto.Weight * subFactor;
                        db.SessionKPIWeights.Add(new SessionKPIWeight
                        {
                            SessionID = dto.SessionId,
                            KPIID = kpi.id,
                            SubKPIID = subObj.id,
                            Weight = (int)Math.Round(adjustedSubWeight, MidpointRounding.AwayFromZero)
                        });
                    }
                    db.SaveChanges();

                    // Local Rounding Correction
                    var currentKpiWeights = db.SessionKPIWeights.Where(w => w.SessionID == dto.SessionId && w.KPIID == kpi.id).ToList();
                    int currentKpiSum = currentKpiWeights.Sum(w => w.Weight ?? 0);
                    if (currentKpiSum != (int)mainKpiTargetWeight && currentKpiWeights.Any())
                    {
                        currentKpiWeights.First().Weight += ((int)mainKpiTargetWeight - currentKpiSum);
                        db.SaveChanges();
                    }

                    AdjustGlobalWeights(dto.SessionId, dto.EmployeeTypeId, kpi.id, mainKpiTargetWeight);

                    scope.Complete();
                    return Ok(new { Message = "KPI Saved and weights adjusted.", Status = "Success" });
                }
            }
            catch (Exception ex) { return InternalServerError(new Exception("Error: " + ex.Message)); }
        }

        // 2. ADD SUB-KPI TO EXISTING KPI (Dynamic Adjustment)
        [HttpPost]
        [Route("add-subkpi-dynamic")]
        public IHttpActionResult AddSubKpiDynamic(DynamicSubKpiDto dto)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var existingWeights = db.SessionKPIWeights
                        .Where(w => w.SessionID == dto.SessionId && w.KPIID == dto.KpiId).ToList();

                    if (!existingWeights.Any()) return BadRequest("KPI not found in this session.");

                    int mainKpiTotal = existingWeights.Sum(x => x.Weight ?? 0);
                    if (dto.NewWeight >= mainKpiTotal) return BadRequest("New Sub-KPI weight is too high.");

                    decimal remainingSpace = mainKpiTotal - dto.NewWeight;
                    decimal factor = remainingSpace / mainKpiTotal;

                    foreach (var w in existingWeights)
                    {
                        w.Weight = (int)Math.Round((w.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                    }

                    SubKPI newSub = new SubKPI { KPIID = dto.KpiId, name = dto.Name };
                    db.SubKPIs.Add(newSub);
                    db.SaveChanges();

                    db.SessionKPIWeights.Add(new SessionKPIWeight
                    {
                        SessionID = dto.SessionId,
                        KPIID = dto.KpiId,
                        SubKPIID = newSub.id,
                        Weight = dto.NewWeight
                    });
                    db.SaveChanges();

                    int finalSum = db.SessionKPIWeights.Where(w => w.SessionID == dto.SessionId && w.KPIID == dto.KpiId).Sum(x => x.Weight ?? 0);
                    if (finalSum != mainKpiTotal)
                    {
                        existingWeights.First().Weight += (mainKpiTotal - finalSum);
                        db.SaveChanges();
                    }

                    scope.Complete();
                    return Ok("Sub-KPI added and existing weights scaled down.");
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 3. DELETE SUB-KPI (With Auto-Adjustment)
        [HttpDelete]
        [Route("delete-subkpi/{sid}/{subid}")]
        public IHttpActionResult DeleteSubKpi(int sid, int subid)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var weightRec = db.SessionKPIWeights.FirstOrDefault(w => w.SubKPIID == subid && w.SessionID == sid);
                    if (weightRec == null) return NotFound();

                    int kpiId = (int)weightRec.KPIID;
                    int kpiTotalWeight = db.SessionKPIWeights.Where(w => w.KPIID == kpiId && w.SessionID == sid).Sum(x => x.Weight ?? 0);

                    db.SessionKPIWeights.Remove(weightRec);
                    var subDef = db.SubKPIs.Find(subid);
                    if (subDef != null) db.SubKPIs.Remove(subDef);
                    db.SaveChanges();

                    var remaining = db.SessionKPIWeights.Where(w => w.KPIID == kpiId && w.SessionID == sid).ToList();
                    if (remaining.Any())
                    {
                        decimal currentSum = remaining.Sum(x => (decimal)(x.Weight ?? 0));
                        if (currentSum > 0)
                        {
                            decimal factor = (decimal)kpiTotalWeight / currentSum;
                            foreach (var o in remaining)
                            {
                                o.Weight = (int)Math.Round((o.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                            }
                            db.SaveChanges();

                            int finalSum = remaining.Sum(x => x.Weight ?? 0);
                            if (finalSum != kpiTotalWeight)
                            {
                                remaining.First().Weight += (kpiTotalWeight - finalSum);
                                db.SaveChanges();
                            }
                        }
                    }
                    scope.Complete();
                    return Ok("Sub-KPI deleted and weights adjusted.");
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 4. DELETE MAIN KPI (Safe & Global 100%)
        [HttpDelete]
        [Route("delete-main-kpi/{sid}/{kpiid}")]
        public IHttpActionResult DeleteMainKpi(int sid, int kpiid)
        {
            try
            {
                using (var scope = new TransactionScope())
                {
                    var kpi = db.KPIs.Find(kpiid);
                    if (kpi == null) return NotFound();

                    int empTypeId = kpi.KPI_Employeetype ?? 0;

                    var weights = db.SessionKPIWeights.Where(w => w.KPIID == kpiid && w.SessionID == sid).ToList();
                    foreach (var w in weights) db.SessionKPIWeights.Remove(w);

                    var subs = db.SubKPIs.Where(s => s.KPIID == kpiid).ToList();
                    foreach (var s in subs) db.SubKPIs.Remove(s);

                    db.KPIs.Remove(kpi);
                    db.SaveChanges();

                    var bakiWeights = db.SessionKPIWeights.Where(w => w.SessionID == sid &&
                                      db.KPIs.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();

                    if (bakiWeights.Any())
                    {
                        decimal currentTotal = bakiWeights.Sum(x => (decimal)(x.Weight ?? 0));
                        if (currentTotal > 0)
                        {
                            decimal factor = 100m / currentTotal;
                            foreach (var bw in bakiWeights)
                            {
                                bw.Weight = (int)Math.Round((bw.Weight ?? 0) * factor, MidpointRounding.AwayFromZero);
                            }
                            db.SaveChanges();

                            int finalSum = bakiWeights.Sum(x => x.Weight ?? 0);
                            if (finalSum != 100)
                            {
                                bakiWeights.First().Weight += (100 - finalSum);
                                db.SaveChanges();
                            }
                        }
                    }
                    scope.Complete();
                    return Ok("KPI Deleted and Global weights adjusted.");
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 5. EDIT MAIN KPI NAME
        [HttpPut]
        [Route("edit-kpi-name/{id}")]
        public IHttpActionResult EditKpiName(int id, [FromBody] string newName)
        {
            if (string.IsNullOrEmpty(newName)) return BadRequest("Name required.");
            var kpi = db.KPIs.Find(id);
            if (kpi == null) return NotFound();
            kpi.name = newName;
            db.SaveChanges();
            return Ok("KPI updated.");
        }

        // 6. EDIT SUB-KPI NAME
        [HttpPut]
        [Route("edit-subkpi-name/{id}")]
        public IHttpActionResult EditSubKpiName(int id, [FromBody] string newName)
        {
            if (string.IsNullOrEmpty(newName)) return BadRequest("Name required.");
            var sub = db.SubKPIs.Find(id);
            if (sub == null) return NotFound();
            sub.name = newName;
            db.SaveChanges();
            return Ok("Sub-KPI updated.");
        }

        // 7. HELPER: Global Adjustment
        private void AdjustGlobalWeights(int sessionId, int empTypeId, int currentKpiId, decimal newKpiWeight)
        {
            var existingWeights = db.SessionKPIWeights
                .Where(w => w.SessionID == sessionId && w.KPIID != currentKpiId &&
                            db.KPIs.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();

            if (existingWeights.Any())
            {
                decimal currentOldTotal = existingWeights.Sum(w => (decimal)(w.Weight ?? 0));
                decimal targetForOld = 100m - newKpiWeight;

                if (currentOldTotal > 0)
                {
                    decimal globalFactor = targetForOld / currentOldTotal;
                    foreach (var w in existingWeights)
                    {
                        w.Weight = (int)Math.Round((w.Weight ?? 0) * globalFactor, MidpointRounding.AwayFromZero);
                    }
                    db.SaveChanges();
                }

                var all = db.SessionKPIWeights.Where(w => w.SessionID == sessionId &&
                            db.KPIs.Any(k => k.id == w.KPIID && k.KPI_Employeetype == empTypeId)).ToList();
                int totalSum = all.Sum(x => x.Weight ?? 0);
                if (totalSum != 100)
                {
                    existingWeights.First().Weight += (100 - totalSum);
                    db.SaveChanges();
                }
            }
        }

        // 8. GET METHODS
        [HttpGet]
        [Route("view-weights/{sid}/{eid}")]
        public IHttpActionResult GetWeights(int sid, int eid)
        {
            var res = db.KPIs.Where(k => k.KPI_Employeetype == eid).ToList()
                .Select(k => new
                {
                    kpiId = k.id,
                    kpiName = k.name,
                    totalKpiWeight = db.SessionKPIWeights.Where(w => w.SessionID == sid && w.KPIID == k.id).Sum(w => (int?)w.Weight) ?? 0,
                    subKpis = (from w in db.SessionKPIWeights
                               join s in db.SubKPIs on w.SubKPIID equals s.id
                               where w.SessionID == sid && w.KPIID == k.id
                               select new { subKpiId = s.id, subKpiName = s.name, weight = w.Weight }).ToList()
                }).Where(x => x.totalKpiWeight > 0).ToList();
            return Ok(res);
        }

        [HttpGet][Route("sessions")] public IHttpActionResult GetSessions() => Ok(db.Sessions.Select(s => new { s.id, s.name }).ToList());
        [HttpGet][Route("emptypes")] public IHttpActionResult GetEmpTypes() => Ok(db.EmployeeTypes.Select(e => new { e.id, e.type }).ToList());
    }
}