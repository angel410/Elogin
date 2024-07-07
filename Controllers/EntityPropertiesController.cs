using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using eLogin.ViewModel;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class EntityPropertiesController : Controller
    {
        private readonly DatabaseContext _context;

        public EntityPropertiesController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: EntityProperties
        public async Task<IActionResult> Index()
        {
            var List = await (from ecit in _context.EntityProperty
                              join it in _context.Property on ecit.PropertyId equals it.Id
                              join ec in _context.Entity on ecit.EntityId equals ec.Id
                              select new EntityPropertyVM {Id = ecit.Id, PropertyName = it.Name, EntityName = ec.EntityName}
                              ).ToListAsync();
                              
            
            return View(List);
        }

        // GET: EntityProperties/Details/5
        public async Task<IActionResult> Details(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var EntityCategoryPropertyVM = await (from ecit in _context.EntityProperty
                                                          join it in _context.Property on ecit.PropertyId equals it.Id
                                                          join ec in _context.Entity on ecit.EntityId equals ec.Id
                                                          select new EntityPropertyVM { Id = ecit.Id, PropertyName = it.Name, EntityName = ec.EntityName }
                              ).FirstOrDefaultAsync(m => m.Id == Id);
            if (EntityCategoryPropertyVM == null)
            {
                return NotFound();
            }

            return View(EntityCategoryPropertyVM);
        }

        // GET: EntityCategoryProperties/Create
        public async Task<IActionResult> Create()
        {
            var Entities = await _context.Entity.ToArrayAsync();

            var EntityList = new List<SelectListItem>();

            foreach (var Entity in Entities)
            {
                EntityList.Add(new SelectListItem(Entity.EntityName, Entity.Id.ToString()));
            }

            ViewData.Add("Entities", EntityList);
            ViewBag.Entities = EntityList;

            var Properties = await _context.Property.ToArrayAsync();

            var PropertyList = new List<SelectListItem>();

            foreach (var Property in Properties)
            {
                PropertyList.Add(new SelectListItem(Property.Name, Property.Id.ToString()));
            }

            ViewData.Add("Properties", PropertyList);
            ViewBag.Properties = PropertyList;

            return View();
        }

        // POST: EntityCategoryProperties/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,EntityId,PropertyId")] EntityProperty EntityProperty)
        {
            if (ModelState.IsValid)
            {
                _context.Add(EntityProperty);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                return RedirectToAction(nameof(Index));
            }
            return View(EntityProperty);
        }

        // GET: EntityCategoryProperties/Edit/5
        public async Task<IActionResult> Edit(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var EntityProperty = await _context.EntityProperty.FindAsync(Id);
            if (EntityProperty == null)
            {
                return NotFound();
            }

            var Entities = await _context.Entity.ToArrayAsync();

            var EntityList = new List<SelectListItem>();

            foreach (var Entity in Entities)
            {
                EntityList.Add(new SelectListItem(Entity.EntityName, Entity.Id.ToString()));
            }

            ViewData.Add("Entities", EntityList);
            ViewBag.Entities = EntityList;

            var Properties = await _context.Property.ToArrayAsync();

            var PropertyList = new List<SelectListItem>();

            foreach (var Property in Properties)
            {
                PropertyList.Add(new SelectListItem(Property.Name, Property.Id.ToString()));
            }

            ViewData.Add("Properties", PropertyList);
            ViewBag.Properties = PropertyList; 

            return View(EntityProperty);
        }

        // POST: EntityCategoryProperties/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid Id, [Bind("Id,EntityId,PropertyId")] EntityProperty EntityProperty)
        {
            if (Id != EntityProperty.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(EntityProperty);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EntityPropertyExists(EntityProperty.Id))
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
            return View(EntityProperty);
        }

        // GET: EntityCategoryProperties/Delete/5
        public async Task<IActionResult> Delete(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var EntityPropertyVM = await (from ecit in _context.EntityProperty
                                                       join it in _context.Property on ecit.PropertyId equals it.Id
                                                       join ec in _context.Entity on ecit.EntityId equals ec.Id
                                                       select new EntityPropertyVM { Id = ecit.Id, PropertyName = it.Name, EntityName = ec.EntityName}
                              ).FirstOrDefaultAsync(m => m.Id == Id);
            if (EntityPropertyVM == null)
            {
                return NotFound();
            }
            

            return View(EntityPropertyVM);
        }

        // POST: EntityCategoryProperties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid Id)
        {
            var EntityProperty = await _context.EntityProperty.FindAsync(Id);
            _context.EntityProperty.Remove(EntityProperty);
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return RedirectToAction(nameof(Index));
        }

        private bool EntityPropertyExists(Guid Id)
        {
            return _context.EntityProperty.Any(e => e.Id == Id);
        }
    }
}
