using eLogin.Identity.Models;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace eLogin.Identity
{
    public class LdapSignInManager : SignInManager<User>
    {
        public LdapSignInManager(
            UserManager ldapUserManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<User> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<LdapSignInManager> logger,
            IAuthenticationSchemeProvider schemes, 
            IUserConfirmation<User> confirmation)
            : base(
                ldapUserManager,
                contextAccessor,
                claimsFactory,
                optionsAccessor,
                logger,
                schemes,
                confirmation)
        {
        }

        public override async Task<SignInResult> PasswordSignInAsync(string userName, string password, bool rememberMe, bool lockOutOnFailure)
        {
            var user = await this.UserManager.FindByNameAsync(userName);
            if (user == null)
            {
                return SignInResult.Failed;
            }

            user.EmailAddress = userName;
            user.Email = userName;
            user.Id = Guid.NewGuid();
            user.SecurityStamp = Guid.NewGuid().ToString();
            


            return await this.PasswordSignInAsync(user, password, rememberMe, lockOutOnFailure);
        }
    }
}
