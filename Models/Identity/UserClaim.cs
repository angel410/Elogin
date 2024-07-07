using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("UserClaims")]
    public class UserClaim : IdentityUserClaim<Guid>
    {

    }
}