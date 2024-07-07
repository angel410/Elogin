using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.ViewModel
{
    public class AssignedProperty
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? ValidationRegex { get; set; }
        public string? ValidationHint { get; set; }
        public bool IsEncrypted { get; set; } = false;
        public bool IsHashed { get; set; } = false;
        public bool IsUniqueIdentifier { get; set; }
        public bool IsDeleted { get; set; }
        public Guid EntityId { get; set; }
        public bool isCustomerRepository { get; set; }
    }
}
