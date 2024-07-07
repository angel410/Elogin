using eLogin.Models;
using eLogin.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace eLogin.Data
{
    public class PasswordService
    {
        private readonly UserManager<User> _userManager;
        private readonly DatabaseContext _context;

        public PasswordService(UserManager<User> userManager, DatabaseContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<bool> IsPasswordExpired(User user, int expirationDays)
        {
            var lastChanged = user.PasswordLastChangedDate;

            if (lastChanged.HasValue)
            {
                return (DateTime.UtcNow - lastChanged.Value).TotalDays > expirationDays;
            }
            else
            {
                // Handle the case where lastChanged is null
                // For example, you might consider the password as expired or not, based on your application's logic
                return false; // or true, depending on your requirements
            }
        }


        public async Task<bool> IsPasswordInHistory(User user, string newPassword, int historyLimit)
        {
            var passwordHistories = await _context.PasswordHistory
                .Where(ph => ph.UserId == user.Id)
                .OrderByDescending(ph => ph.DateCreated)
                .Take(historyLimit)
                .ToListAsync();

            foreach (var passwordHistory in passwordHistories)
            {
                if (await _userManager.CheckPasswordAsync(user, newPassword))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task UpdatePasswordHistory(User user, string newPassword)
        {
            var passwordHash = _userManager.PasswordHasher.HashPassword(user, newPassword);
            var passwordHistory = new PasswordHistory
            {
                UserId = user.Id,
                PasswordHash = passwordHash,
                DateCreated = DateTime.UtcNow,
                Id= Guid.NewGuid()
            };

            _context.PasswordHistory.Add(passwordHistory);
            await _context.SaveChangesAsync();

            user.PasswordLastChangedDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
    }

}
