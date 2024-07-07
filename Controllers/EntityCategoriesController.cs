using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class EntityCategoriesController : Controller
    {
        private readonly DatabaseContext _context;

        public EntityCategoriesController(DatabaseContext context)
        {
            _context = context;
        }

        

        // GET: EntityCategories
        public async Task<IActionResult> Index()
        {
            List<EntityCategory> entityCategories = await _context.EntityCategory.ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            foreach(EntityCategory category in entityCategories )
            {
                EntityCategoryListItem ecli = new EntityCategoryListItem();
                ecli.Id = category.Id;
                ecli.CategoryName = category.CategoryName;
                ecli.ParentEntityCategoryId = category.ParentEntityCategoryId;
                if (category.ChildEntityCategories.Count != 0) ecli.HasChildren = true;
                ecl.Add(ecli);
            }
            ViewBag.dataSource = ecl;
           
            return View(entityCategories);
        }

        // GET: EntityCategories/Details/5
        public async Task<IActionResult> Details(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var entityCategory = await _context.EntityCategory
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (entityCategory == null)
            {
                return NotFound();
            }
            string ParentCategoryName = "No Parent Category";
            if (entityCategory.ParentEntityCategoryId != null)
            {
                EntityCategory entityParentCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.Id == entityCategory.ParentEntityCategoryId);
                ParentCategoryName = entityParentCategory.CategoryName;
            }


            ViewData.Add("ParentCategoryName", ParentCategoryName);

            return View(entityCategory);
        }

        // GET: EntityCategories/Create
        public async Task<IActionResult> Create()
        {
            var Categories = await _context.EntityCategory.ToArrayAsync();

            var CategoriesList = new List<SelectListItem>();

            foreach (var Category in Categories)
            {
                CategoriesList.Add(new SelectListItem(Category.CategoryName, Category.Id.ToString()));
            }

            ViewData.Add("Categories", CategoriesList);
            ViewBag.Categories = CategoriesList;

            return View();
        }

        // POST: EntityCategories/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CategoryName,ParentEntityCategoryId")] EntityCategory entityCategory)
        {
            if (ModelState.IsValid)
            {
                _context.Add(entityCategory);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                return RedirectToAction(nameof(Index));
            }
            return View(entityCategory);
        }

        // GET: EntityCategories/Edit/5
        public async Task<IActionResult> Edit(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var entityCategory = await _context.EntityCategory.FindAsync(Id);
            if (entityCategory == null)
            {
                return NotFound();
            }
            var Categories = await _context.EntityCategory.ToArrayAsync();

            var CategoriesList = new List<SelectListItem>();

            foreach (var Category in Categories)
            {
                CategoriesList.Add(new SelectListItem(Category.CategoryName, Category.Id.ToString()));
            }

            ViewData.Add("Categories", CategoriesList);
            ViewBag.Categories = CategoriesList;

            return View(entityCategory);
        }

        // POST: EntityCategories/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid Id, [Bind("Id,CategoryName,ParentEntityCategoryId")] EntityCategory entityCategory)
        {
            if (Id != entityCategory.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(entityCategory);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EntityCategoryExists(entityCategory.Id))
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
            return View(entityCategory);
        }

        // GET: EntityCategories/Delete/5
        public async Task<IActionResult> Delete(Guid Id)
        {
            if (Id == null)
            {
                return NotFound();
            }

            var entityCategory = await _context.EntityCategory
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (entityCategory == null)
            {
                return NotFound();
            }
            string ParentCategoryName = "No Parent Category";
            if (entityCategory.ParentEntityCategoryId != null)
            {
                EntityCategory entityParentCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.Id == entityCategory.ParentEntityCategoryId);
                ParentCategoryName = entityParentCategory.CategoryName;
            }
            

            ViewData.Add("ParentCategoryName", ParentCategoryName);
            
            return View(entityCategory);
        }

        // POST: EntityCategories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid Id)
        {
            var entityCategory = await _context.EntityCategory.FindAsync(Id);
            //_context.EntityCategory.Remove(entityCategory);
            entityCategory.IsDeleted = true;
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return RedirectToAction(nameof(Index));
        }

        private bool EntityCategoryExists(Guid Id)
        {
            return _context.EntityCategory.Any(e => e.Id == Id);
        }

        public class EntityCategoryListItem
        {
            public Guid Id { get; set; }
            public string CategoryName { get; set; }
            public Guid? ParentEntityCategoryId { get; set; }
            public bool HasChildren { get; set; } = false;
        }

    }
}
