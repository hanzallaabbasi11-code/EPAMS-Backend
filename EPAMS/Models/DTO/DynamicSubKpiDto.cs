using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class DynamicSubKpiDto
    {
        public int SessionId { get; set; }
        public int KpiId { get; set; }
        public string Name { get; set; }
        public int NewWeight { get; set; }
    }
}