using eLogin.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using eLogin.Services;
using eLogin.Models.Identity;
using eLogin.Models;

namespace eLogin.Identity
{
    public class UserManager : UserManager<User>
    {
        private readonly ILdapService _ldapService;

        public UserManager(
            ILdapService ldapService,
            IUserStore<User> store,
            IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<User> passwordHasher,
            IEnumerable<IUserValidator<User>> userValidators,
            IEnumerable<IPasswordValidator<User>> passwordValidators,
            ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors,
            IServiceProvider services,
            ILogger<UserManager> logger)
            : base(
                store,
                optionsAccessor,
                passwordHasher,
                userValidators,
                passwordValidators,
                keyNormalizer,
                errors,
                services,
                logger)
        {
            this._ldapService = ldapService;
        }

        public User GetAdministrator()
        {
            return this._ldapService.GetAdministrator();
        }

        /// <summary>
        /// Checks the given password agains the configured LDAP server.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public override async Task<bool> CheckPasswordAsync(User user, string password) {
            return  this._ldapService.Authenticate(user.DistinguishedName, password);
        }

        public override Task<IList<string>> GetRolesAsync(User user) {
            if(user.MemberOfNameOnly == null)
            {
                List<string> defaultMembers = new List<string>();
                defaultMembers.Add("eLoginGuest");
                user.MemberOfNameOnly = defaultMembers.ToArray();
            }
            return Task.FromResult<IList<string>>(user.MemberOfNameOnly.ToList());
        }

        public override Task<IList<Claim>> GetClaimsAsync(User user) {
            var userClaims = new List<Claim>();
            if (user.MemberOfNameOnly == null)
            {
                userClaims.Add(new Claim("AdGroup", "eLoginGuest"));
            }
            else
            {
                // In testing, going much above 30 claims seems to cause the login to hang. 
                user.MemberOfNameOnly.ToList().Take(25).ToList().ForEach(g => userClaims.Add(new Claim("AdGroup", g)));
            }
            
           
            return Task.FromResult<IList<Claim>>(userClaims.ToList());
        }

        public override Task<bool> HasPasswordAsync(User user) {
            return Task.FromResult(true);
        }

        public override Task<User> FindByIdAsync(string userId)
        {
            return this.FindByNameAsync(userId);
        }

        public override Task<User> FindByNameAsync(string userName)
        {
            return Task.FromResult(this._ldapService.GetUserByUserName(userName));
        }
                
        public override Task<string> GetEmailAsync(User user)
        {
            return base.GetEmailAsync(user);
        }

        public override Task<string> GetUserIdAsync(User user)
        {
            return base.GetUserIdAsync(user);
        }

        public override Task<string> GetUserNameAsync(User user)
        {
            return base.GetUserNameAsync(user);
        }

        public override Task<string> GetPhoneNumberAsync(User user)
        {
            return base.GetPhoneNumberAsync(user);
        }

        public override IQueryable<User> Users => this._ldapService.GetAllUsers().AsQueryable();
    }
}
