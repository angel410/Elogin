using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("UserLogins")]
    public class UserLogin : IdentityUserLogin<Guid>
    {

    }
}
