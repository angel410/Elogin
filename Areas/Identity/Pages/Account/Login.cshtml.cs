using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using eLogin.Identity;
using eLogin.Settings;
using Microsoft.Extensions.Options;
using eLogin.Models;
using eLogin.Data;
using Microsoft.EntityFrameworkCore;

namespace eLogin.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly LdapSignInManager _LdapSignInManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly LdapSettings _ldapSettings;
        private readonly DatabaseContext _context;
        private readonly LicenseCheck LC;
        private readonly PasswordService _passwordService;
        public LoginModel(SignInManager<User> signInManager, 
            ILogger<LoginModel> logger,
            UserManager<User> userManager, LdapSignInManager ldapSignInManager, IOptions<LdapSettings> ldapSettings,
            DatabaseContext context, LicenseCheck licenseCheck, PasswordService passwordService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _LdapSignInManager = ldapSignInManager;
            _logger = logger;
            _ldapSettings = ldapSettings.Value;
            _context = context;
            LC = licenseCheck;
            _passwordService = passwordService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            //[Display(Name = "User name")]
            //[Required(ErrorMessage = "You must enter your username!")]
            //public string UserName { get; set; }

            //[Required]
            //[EmailAddress]
            //public string Email { get; set; }

            [Display(Name = "UserName / Email")]
            [Required]
            public string Identifier { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }

            //[Required]
            //[DataType(DataType.Text)]
            //[Display(Name = "Login Method")]
            //public string LoginMethod { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if(_ldapSettings.LdapEnabled)
            {
                ViewData.Add("LoginTitle", "Use LDAP account to log in.");
                ViewData.Add("Identifier", "User Name");
            }
            else
            {
                ViewData.Add("LoginTitle", "Use a local account to log in.");
                ViewData.Add("Identifier", "Email");
            }
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            //List<string> LoginMethod = new List<string>();
            //LoginMethod.Add("Local User");
            //LoginMethod.Add("Ldap");

            //ViewData.Add("LoginMethod", new SelectList(LoginMethod));
            
            


            
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var user = await _userManager.FindByNameAsync(Input.Identifier);

            //if (!LC.Check().isValid) return Redirect("~/LicenseManager/LicenseValidationResult");
            int code = LC.Check().code;
            if (code > 1000)
            {
                if (code % 10 == 1) return Redirect("~/LicenseManager/LicenseValidationResult");
            }

            if (_ldapSettings.LdapEnabled)
            {
                ViewData.Add("LoginTitle", "Use LDAP account to log in.");
                ViewData.Add("Identifier", "User Name");
            }
            else
            {
                ViewData.Add("LoginTitle", "Use a local account to log in.");
                ViewData.Add("Identifier", "Email");
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            if (ModelState.IsValid)
            {
                if(!_ldapSettings.LdapEnabled)
                {
                    if (user != null)
                    {
                        var settings = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "PasswordExpirationDays");
                        if (await _passwordService.IsPasswordExpired(user, Convert.ToInt32(settings.Value)))
                        {
                            ModelState.AddModelError(string.Empty, "Your password has expired. Please change your password.");

                            return RedirectToPage("./ChangePassword", new { Identifier = Input.Identifier });

                        }

                        var result = await _signInManager.PasswordSignInAsync(Input.Identifier, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                        if (result.Succeeded)
                        {
                            UserSession userSession = new UserSession();
                            userSession.Identifier = Input.Identifier;
                            userSession.Action = "Login";
                            await _context.AddAsync(userSession);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("User logged in.");
                            return LocalRedirect(returnUrl);
                        }
                        if (result.RequiresTwoFactor)
                        {
                            return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                        }
                        if (result.IsLockedOut)
                        {
                            _logger.LogWarning("User account locked out.");
                            return RedirectToPage("./Lockout");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                            return Page();
                        }
                    }

                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        return Page();
                   
                }
                else
                {
                    // This doesn't count login failures towards account lockout
                    // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                    var result = await _LdapSignInManager.PasswordSignInAsync(Input.Identifier, Input.Password, Input.RememberMe, false);
                    if (result.Succeeded)
                    {
                        UserSession userSession = new UserSession();
                        userSession.Identifier = Input.Identifier;
                        userSession.Action = "Login";
                        await _context.AddAsync(userSession);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("User logged in.");
                        return LocalRedirect(returnUrl);
                    }
                    if (result.RequiresTwoFactor)
                    {
                        return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                    }
                    if (result.IsLockedOut)
                    {
                        _logger.LogWarning("User account locked out.");
                        return RedirectToPage("./Lockout");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                        return Page();
                    }
                }
                
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
