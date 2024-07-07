
using eLogin.Models.Identity;
using System.Collections.Generic;

namespace eLogin.Models
{
    public interface ILdapService
    {
        ICollection<LdapEntry> GetGroups(string groupName, bool getChildGroups = false);

        ICollection<User> GetUsersInGroup(string groupName);

        ICollection<User> GetUsersInGroups(ICollection<LdapEntry> groups = null);

        ICollection<User> GetUsersByEmailAddress(string emailAddress);

        ICollection<User> GetAllUsers();

        User GetAdministrator();

        User GetUserByUserName(string userName);
        bool Authenticate(string distinguishedName, string password);
    }
}
