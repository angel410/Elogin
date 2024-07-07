using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("Roles")]
    public class Role : IdentityRole<Guid>
    {

    }
}
