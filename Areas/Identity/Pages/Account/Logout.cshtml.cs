using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using eLogin.Data;
using eLogin.Models;

namespace eLogin.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private readonly DatabaseContext _context;

        public LogoutModel(SignInManager<User> signInManager, ILogger<LogoutModel> logger, DatabaseContext context)
        {
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            var a = _signInManager.Context.User.Identity.Name;
            UserSession userSession = new UserSession();
            userSession.Identifier = a;
            userSession.Action = "Logout";
            await _context.AddAsync(userSession);
            await _context.SaveChangesAsync();
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                return RedirectToPage();
            }
        }
    }
}
