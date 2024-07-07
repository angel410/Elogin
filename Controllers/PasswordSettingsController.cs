using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using eLogin.Models.Roles;
using Microsoft.AspNetCore.Authorization;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class PasswordSettingsController : Controller
    {
        private readonly DatabaseContext _context;

        public PasswordSettingsController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: PasswordSettings
        public async Task<IActionResult> Index()
        {
            return View(await _context.PasswordSettings.ToListAsync());
        }

        // GET: PasswordSettings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var passwordSettings = await _context.PasswordSettings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (passwordSettings == null)
            {
                return NotFound();
            }

            return View(passwordSettings);
        }

        // GET: PasswordSettings/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: PasswordSettings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,PasswordExpirationDays,PasswordHistoryLimit")] PasswordSettings passwordSettings)
        {
            if (ModelState.IsValid)
            {
                _context.Add(passwordSettings);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(passwordSettings);
        }

        // GET: PasswordSettings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var passwordSettings = await _context.PasswordSettings.FindAsync(id);
            if (passwordSettings == null)
            {
                return NotFound();
            }
            return View(passwordSettings);
        }

        // POST: PasswordSettings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PasswordExpirationDays,PasswordHistoryLimit")] PasswordSettings passwordSettings)
        {
            if (id != passwordSettings.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(passwordSettings);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PasswordSettingsExists(passwordSettings.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(passwordSettings);
        }

        // GET: PasswordSettings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var passwordSettings = await _context.PasswordSettings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (passwordSettings == null)
            {
                return NotFound();
            }

            return View(passwordSettings);
        }

        // POST: PasswordSettings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var passwordSettings = await _context.PasswordSettings.FindAsync(id);
            if (passwordSettings != null)
            {
                _context.PasswordSettings.Remove(passwordSettings);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PasswordSettingsExists(int id)
        {
            return _context.PasswordSettings.Any(e => e.Id == id);
        }
    }
}
