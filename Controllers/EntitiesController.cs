using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using Microsoft.Extensions.Logging;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class EntitiesController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<EntitiesController> _logger;

        public EntitiesController(DatabaseContext context, ILogger<EntitiesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Entities
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Entities.Index is called");
            var databaseContext = _context.Entity.Include(e => e.EntityCategory);
            return View(await databaseContext.ToListAsync());
        }

        // GET: Entities/Details/5
        public async Task<IActionResult> Details(Guid id)
        {
            _logger.LogInformation("Entities.Details is called with {id}", id);
            if (id == null)
            {
                _logger.LogError("id == null");
                return NotFound();
            }

            var entity = await _context.Entity
                .Include(e => e.EntityCategory)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }

            return View(entity);
        }

        // GET: Entities/Create
        public IActionResult Create()
        {
            _logger.LogInformation("Entities.Create is called");
            ViewData["EntityCategoryId"] = new SelectList(_context.EntityCategory, "Id", "Id");
            return View();
        }

        // POST: Entities/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,EntityName,IsDeleted,EntityCategoryId")] Entity entity)
        {
            _logger.LogInformation("Entities.Create is called with {@entity}", entity);
            if (ModelState.IsValid)
            {
                _logger.LogDebug("Adding new Entity in DB");
                entity.Id = Guid.NewGuid();
                _context.Add(entity);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            else _logger.LogError("Model is invalid");
            _logger.LogDebug("Preparing ViewData.EntityCategoryId");
            ViewData["EntityCategoryId"] = new SelectList(_context.EntityCategory, "Id", "Id", entity.EntityCategoryId);
            return View(entity);
        }

        // GET: Entities/Edit/5
        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.Entity.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            ViewData["EntityCategoryId"] = new SelectList(_context.EntityCategory, "Id", "Id", entity.EntityCategoryId);
            return View(entity);
        }

        // POST: Entities/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,EntityName,IsDeleted,EntityCategoryId")] Entity entity)
        {
            if (id != entity.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(entity);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EntityExists(entity.Id))
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
            ViewData["EntityCategoryId"] = new SelectList(_context.EntityCategory, "Id", "Id", entity.EntityCategoryId);
            return View(entity);
        }

        // GET: Entities/Delete/5
        public async Task<IActionResult> Delete(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var entity = await _context.Entity
                .Include(e => e.EntityCategory)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            return View(entity);
        }

        // POST: Entities/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var entity = await _context.Entity.FindAsync(id);
            _context.Entity.Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EntityExists(Guid id)
        {
            return _context.Entity.Any(e => e.Id == id);
        }
    }
}
