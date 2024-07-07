using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using eLogin.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace eLogin.Models.Identity
{
    [Table("Users")]
    public class User : IdentityUser<Guid>, ILdapEntry
    {
      
        [NotMapped]
        public string ObjectSid { get; set; }

        [NotMapped]
        public string ObjectGuid { get; set; }

        [NotMapped]
        public string ObjectCategory { get; set; }

        [NotMapped]
        public string ObjectClass { get; set; }

        [NotMapped]
        public string Name { get; set; }

        [NotMapped]
        public string CommonName { get; set; }

        [NotMapped]
        public string DistinguishedName { get; set; }

        [NotMapped]
        public string SamAccountName { get; set; }

        [NotMapped]
        public int SamAccountType { get; set; }

        [NotMapped]
        public string[] MemberOf { get; set; }
        [NotMapped]
        public string[] MemberOfNameOnly { get; set; }

        [NotMapped]
        public bool IsDomainAdmin { get; set; }

        [NotMapped]
        public bool MustChangePasswordOnNextLogon { get; set; }

        [NotMapped]
        public string UserPrincipalName { get; set; }

        [NotMapped]
        public string DisplayName { get; set; }

        [NotMapped]
        //[Required(ErrorMessage = "You must enter your first name!")]
        public string FirstName { get; set; }

        [NotMapped]
        //[Required(ErrorMessage = "You must enter your last name!")]
        public string LastName { get; set; }

        [NotMapped]
        public string FullName => $"{this.FirstName} {this.LastName}";

        //[Required(ErrorMessage = "You must enter your email address!")]
        [EmailAddress(ErrorMessage = "You must enter a valid email address.")]
        public string EmailAddress { get; set; }

        [NotMapped]
        public string Description { get; set; }

        [NotMapped]
        public string Phone { get; set; }

        [NotMapped]
        public LdapAddress Address { get; set; }

        [NotMapped]
        public string[] Roles { get; set; }

        // New properties for password history and expiration
        public DateTime? PasswordLastChangedDate { get; set; } 

        public virtual ICollection<PasswordHistory> PasswordHistories { get; set; }

        // Constructor
        public User()
        {
            PasswordHistories = new List<PasswordHistory>();
        }
    }
   
    public class LdapAddress
    {
        public string Street { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string StateName { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
    }

}
