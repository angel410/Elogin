using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using eLogin.Data;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace eLogin.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly PasswordService _passwordService;
        private readonly DatabaseContext _context;

        public ChangePasswordModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<ChangePasswordModel> logger,
            PasswordService passwordService,
            DatabaseContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _passwordService = passwordService;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }
        [BindProperty]
        public string Identifier { get; set; }
        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }
           
            [Required]
            //[StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string identifier)
        {
            Identifier = identifier; // Set the Identifier property
            ModelState.AddModelError(string.Empty, "Your password has expired. Please change your password.");

            var user = await _userManager.FindByNameAsync(Identifier);
            if (user == null)
            {
                return NotFound($"Unable to load user with Id '{_userManager.GetUserId(User)}'.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByNameAsync(Identifier);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var settings = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "PasswordHistoryLimit");

            if (await _passwordService.IsPasswordInHistory(user, Input.NewPassword, Convert.ToInt32( settings.Value)))
            {
                ModelState.AddModelError(string.Empty, "You cannot use one of your last passwords.");
                return Page();
            }
            var regexData = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "PasswordRegex");
            var regex = new Regex(regexData.Value);

            if (!regex.IsMatch(Input.NewPassword))
            {
                ModelState.AddModelError(string.Empty, "Password does not match criteria.");
                return Page();
            }
            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _passwordService.UpdatePasswordHistory(user, Input.NewPassword);

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User changed their password successfully.");
            return RedirectToPage("./Login");
        }
    }
}
