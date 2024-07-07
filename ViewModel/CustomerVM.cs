using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.ViewModel
{
    public class CustomerVM
    {
        public int Id { get; set; }
        public bool IsLocked { get; set; }

        
        //public List<CustomerInfoValue> CustomerInfoValues { get; set; }
        //public List<Entity> Entities { get; set; }
        //public List<CustomerLoginAttempt> CustomerLoginAttempts { get; set; }
        //public List<CustomerPassword> CustomerPasswords { get; set; }
    }
}
