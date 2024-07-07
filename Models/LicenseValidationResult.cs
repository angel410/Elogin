using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.Models
{
    public class LicenseValidationResult
    {
        public bool isValid { get; set; }
        public int code { get; set; }
        public string message { get; set; }
        public License license { get; set; }
        public bool isCustomerRepository { get; set; }
    }
}
