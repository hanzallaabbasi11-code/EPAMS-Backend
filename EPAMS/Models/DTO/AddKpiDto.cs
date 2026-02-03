using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class AddKpiDto
    {
        public int SessionId { get; set; }
        public string KPIName { get; set; }
        public int EmployeeTypeId { get; set; }
        public int RequestedKPIWeight { get; set; }

        // SubKPI list
        public List<SubKPIDto> SubKPIs { get; set; }
    }

    public class SubKPIDto
    {
        public string Name { get; set; }
        public int Weight { get; set; }

    }
}