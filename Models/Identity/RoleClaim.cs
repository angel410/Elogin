using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("RoleClaims")]
    public class RoleClaim : IdentityRoleClaim<Guid>
    {

    }


}
