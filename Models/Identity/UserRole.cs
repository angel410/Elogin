using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("UserRoles")]
    public class UserRole : IdentityUserRole<Guid>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
