using System.ComponentModel.DataAnnotations;
using eLogin.Data;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using eLogin.Models.Roles;
using eLogin.Models;
using System.Text.RegularExpressions;

namespace eLogin.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> SignInManager;
        private readonly UserManager<User> UserManager;
        private readonly ILogger<RegisterModel> Logger;
        private readonly DatabaseContext _context;
        private LicenseCheck LC;


        public RegisterModel(UserManager<User> userManager, SignInManager<User> signInManager, ILogger<RegisterModel> logger, DatabaseContext DatabaseContext, LicenseCheck licenseCheck)
        {
            UserManager = userManager;
            SignInManager = signInManager;
            Logger = logger;
            this._context = DatabaseContext;
            LC = licenseCheck;
        }

        [BindProperty]
        public RegisterationModel Registeration { get; set; }

        public string ReturnUrl { get; set; }

        public class RegisterationModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            //[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

        }

        public async Task<IActionResult> OnGetAsync(string ReturnUrl = null)
        {
            

            this.ReturnUrl = ReturnUrl;
            
            //if (!LC.Check().isValid) return Redirect("~/LicenseManager/LicenseValidationResult");
            int code = LC.Check().code;
            if (code > 1000)
            {
                if (code % 10 == 1) return Redirect("~/LicenseManager/LicenseValidationResult");
            }
            
            
            return null;
        }

        

        public async Task<IActionResult> OnPostAsync(string ReturnUrl = null)
        {
            bool isFirstLoginAttempt = false;

            var currentUser = await _context.Users.FirstOrDefaultAsync();

            if(currentUser == null)
            {
                isFirstLoginAttempt = true;
            }
            
            ReturnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid) return Page();

            

            var User = new User { UserName = Registeration.Email, Email = Registeration.Email, EmailAddress = Registeration.Email };

            var Result = await UserManager.CreateAsync(User, Registeration.Password);

            var regexData = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "PasswordRegex");
            var regex = new Regex(regexData.Value);

            if (!regex.IsMatch(Registeration.Password))
            {
                ModelState.AddModelError(string.Empty, "Password does not match criteria.");
                return Page();
            }
            if (Result.Succeeded)
            {
                Logger.LogInformation("User created a new account with password.");
                
                if(isFirstLoginAttempt)
                {
                    await UserManager.AddToRoleAsync(User, nameof(eLoginAdmin));
                }
                else
                {
                    await UserManager.AddToRoleAsync(User, nameof(eLoginGuest));
                }
                

                await SignInManager.SignInAsync(User, isPersistent: false);

                UserSession userSession = new UserSession();
                userSession.Identifier = User.UserName;
                userSession.Action = "Login";
                await _context.AddAsync(userSession);
                await _context.SaveChangesAsync();

                return LocalRedirect(ReturnUrl);
            }

            foreach (var error in Result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
