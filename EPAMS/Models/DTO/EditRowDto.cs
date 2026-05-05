using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EPAMS.Models.DTO
{
    public class EditRowDto
    {
        public string Remarks { get; set; }
        public int LateIn { get; set; }
        public int LeftEarly { get; set; }
    }
}