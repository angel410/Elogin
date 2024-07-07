using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace eLogin.Models
{
    public class CreateEntityRequest
    {
        public Guid Id { get; set; }

        [Required]
        public Guid SessionId { get; set; }

        [Required]
        public long UnixTime { get; set; }

        [Required]
        public string EntityName { get; set; }

        [Required]
        public Guid EntityCategoryId { get; set; }

        public DateTime DateTime { get; set; } = DateTime.UtcNow;
    }
}
