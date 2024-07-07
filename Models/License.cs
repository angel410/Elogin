using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.Models
{
    public class License
    {
        public string CustomerName { get; set; }
        public bool CustomerRepository { get; set; }
        public int MaxChannels { get; set; }
        public int MaxUsers { get; set; }
        public string LicenseType { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Signature { get; set; }
    }
}
