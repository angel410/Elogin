using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class PropertiesController : Controller
    {
        private readonly DatabaseContext _context;

        public PropertiesController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: Properties
        public async Task<IActionResult> Index()
        {
            return View(await _context.Property.ToListAsync());
        }

        // GET: Properties/Details/5
        public async Task<IActionResult> Details(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var Property = await _context.Property
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (Property == null)
            {
                return NotFound();
            }

            return View(Property);
        }

        // GET: Properties/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Properties/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,ValidationRegex,ValidationHint,IsEncrypted,IsHashed,IsUniqueIdentifier,IsDeleted")] Property Property)
        {
            if (ModelState.IsValid)
            {
                _context.Add(Property);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                return RedirectToAction(nameof(Index));
            }
            return View(Property);
        }

        // GET: Properties/Edit/5
        public async Task<IActionResult> Edit(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var Property = await _context.Property.FindAsync(Id);
            if (Property == null)
            {
                return NotFound();
            }
            return View(Property);
        }

        // POST: Properties/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid Id, [Bind("Id,Name,ValidationRegex,ValidationHint,IsEncrypted,IsHashed,IsUniqueIdentifier,IsDeleted")] Property Property)
        {
            if (Id != Property.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(Property);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PropertyExists(Property.Id))
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
            return View(Property);
        }

        // GET: Properties/Delete/5
        public async Task<IActionResult> Delete(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var Property = await _context.Property
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (Property == null)
            {
                return NotFound();
            }

            return View(Property);
        }

        // POST: Properties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid Id)
        {
            var Property = await _context.Property.FindAsync(Id);
            //_context.Property.Remove(Property);
            Property.IsDeleted = true;
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return RedirectToAction(nameof(Index));
        }

        private bool PropertyExists(Guid Id)
        {
            return _context.Property.Any(e => e.Id == Id);
        }
    }
}
