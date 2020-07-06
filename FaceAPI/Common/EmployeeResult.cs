using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FaceAPI.Common
{
    public class EmployeeResult
    {
        public Guid FaceId { get; set; }
        public string EmployeeNo { get; set; }
        public string EmployeeName { get; set; }
        public string NickName { get; set; }
        public DateTime DateTime { get; set; }
        public double Confidence { get; set; }
    }
}
