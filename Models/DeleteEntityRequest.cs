using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace eLogin.Models
{
    public class DeleteEntityRequest
    {
        public Guid Id { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [Required]
        public long UnixTime { get; set; }

        [Required]
        public Guid EntityId { get; set; }

        public DateTime DateTime { get; set; } = DateTime.UtcNow;
    }
}
