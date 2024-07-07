using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("UserTokens")]
    public class UserToken : IdentityUserToken<Guid>
    {

    }
}
