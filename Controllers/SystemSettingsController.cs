using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using eLogin.Models.Roles;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.CodeAnalysis;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class SystemSettingController : Controller
    {
        private readonly DatabaseContext _context;
        private IHostingEnvironment hostingEnv;
        private readonly ILogger<SystemSettingController> _logger;

        public SystemSettingController(DatabaseContext context, IHostingEnvironment env, ILogger<SystemSettingController> logger)
        {
            _context = context;
            this.hostingEnv = env;
            _logger = logger;
        }

        public class EntityCategoryListItem
        {
            public Guid id { get; set; }
            public Guid? parentId { get; set; }
            public string categoryName { get; set; }
            public bool hasChild { get; set; }
        }

        public class EntityListItem
        {
            public Guid id { get; set; }
            public string entityName { get; set; }
        }

        public class PropertyListItem
        {
            public Guid id { get;set; }
            public string propertyName { get; set; }
        }

        // GET: SystemSetting
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("SystemSetting.Index is called");

            _logger.LogDebug("Preparing categoriesDataSource");
            List<EntityCategoryListItem> categories = new List<EntityCategoryListItem>();
            List<EntityCategory> entityCategories = await _context.EntityCategory.ToListAsync();
            foreach (EntityCategory ec in entityCategories)
            {
                EntityCategoryListItem vec = new EntityCategoryListItem();
                vec.categoryName = ec.CategoryName;
                vec.id = ec.Id;
                if (ec.ChildEntityCategories != null) vec.hasChild = true;
                vec.parentId = ec.ParentEntityCategoryId;
                categories.Add(vec);
            }

            ViewBag.categoriesDataSource = categories;

            
            _logger.LogDebug("Obtaining Customer Primary Property from DB");
            SystemSetting systemSetting = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "Customer Primary Property");

            Guid? propertyId = null;
            Guid? entityId = null;
            Guid? categoryId = null;

            _logger.LogDebug("Preparing categoriesDataSource");
            List<PropertyListItem> properties = new List<PropertyListItem>();
            List<EntityListItem> entities = new List<EntityListItem>();
            systemSetting.Value = systemSetting.Value;//Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSetting.Value, Cryptography.Operation.Decrypt);
            if (!string.IsNullOrEmpty(systemSetting.Value))
            {
                propertyId = Guid.Parse(systemSetting.Value);
                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == propertyId);
                EntityProperty ep = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.PropertyId == propertyId);

                entityId = ep.EntityId;
                categoryId = ep.Entity.EntityCategoryId;
                PropertyListItem p = new PropertyListItem();
                p.id = property.Id;
                p.propertyName = property.Name;
                EntityListItem e = new EntityListItem();
                e.id = ep.EntityId.Value;
                e.entityName = ep.Entity.EntityName;
                properties.Add(p);
                entities.Add(e);
            }

            
            ViewBag.propertyDataSource = properties;
            ViewBag.entityDataSource = entities;

            ViewBag.propertyId = propertyId;
            ViewBag.entityId = entityId;
            ViewBag.categoryId = categoryId;

            List<SystemSetting> systemSettings = await _context.SystemSetting.ToListAsync();
            foreach(SystemSetting ss in systemSettings)
            {
                ss.Value = ss.Value;//Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, ss.Value, Cryptography.Operation.Decrypt);
            }

            return View(systemSettings);
        }

        // GET: SystemSetting/Details/5
        public async Task<IActionResult> Details(Guid Id)
        {
            _logger.LogInformation("SystemSetting.Details is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }

            var systemSettings = await _context.SystemSetting
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (systemSettings == null)
            {
                _logger.LogError("systemSettings == null");
                return NotFound();
            }


            systemSettings.Value = systemSettings.Value;//Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettings.Value, Cryptography.Operation.Decrypt);
            return View(systemSettings);
        }

        // GET: SystemSetting/Create
        public IActionResult Create()
        {
            _logger.LogInformation("SystemSetting.Create is called");
            return View();
        }

        // POST: SystemSetting/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,SettingName,Value")] SystemSetting systemSettings)
        {
            _logger.LogInformation("SystemSetting.Details is called with {@systemSettings}", systemSettings);
            if (ModelState.IsValid)
            {
                systemSettings.Value = systemSettings.Value; //Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(systemSettings.Value)), Cryptography.Operation.Encrypt);
                _logger.LogDebug("Adding new system setting");
                _context.Add(systemSettings);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                return RedirectToAction(nameof(Index));
            }
            else _logger.LogError("Model is invalid");
            return View(systemSettings);
        }

        // GET: SystemSetting/Edit/5
        public async Task<IActionResult> Edit(Guid Id)
        {
            _logger.LogInformation("SystemSetting.Edit is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }

            var systemSettings = await _context.SystemSetting.FindAsync(Id);
            if (systemSettings == null)
            {
                _logger.LogError("systemSettings == null");
                return NotFound();
            }
            systemSettings.Value = systemSettings.Value;// Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettings.Value, Cryptography.Operation.Decrypt);
            return View(systemSettings);
        }

        // POST: SystemSetting/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid Id, [Bind("Id,SettingName,Value")] SystemSetting systemSettings)
        {
            _logger.LogInformation("SystemSetting.Edit is called with {@systemSettings}", systemSettings);
            if (Id != systemSettings.Id)
            {
                _logger.LogError("Id != systemSettings.Id");
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogDebug("Updating system setting value");
                    systemSettings.Value = systemSettings.Value;//Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(systemSettings.Value)), Cryptography.Operation.Encrypt);
                    _context.Update(systemSettings);

                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogError("Exception while updating system setting");
                    if (!SystemSettingExists(systemSettings.Id))
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
            else _logger.LogError("Model is invalid");
            return View(systemSettings);
        }

        // GET: SystemSetting/Delete/5
        public async Task<IActionResult> Delete(Guid Id)
        {
            _logger.LogInformation("SystemSetting.Delete is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }

            var systemSettings = await _context.SystemSetting
                .FirstOrDefaultAsync(m => m.Id == Id);
            if (systemSettings == null)
            {
                _logger.LogError("systemSettings == null");
                return NotFound();
            }

            return View(systemSettings);
        }

        // POST: SystemSetting/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid Id)
        {
            _logger.LogInformation("SystemSetting.DeleteConfirmed is called with {Id}", Id);
            var systemSettings = await _context.SystemSetting.FindAsync(Id);
            _context.SystemSetting.Remove(systemSettings);
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return RedirectToAction(nameof(Index));
        }

        private bool SystemSettingExists(Guid Id)
        {
            return _context.SystemSetting.Any(e => e.Id == Id);
        }

        [AcceptVerbs("Post")]
        public IActionResult UploadCustomerLogo(IList<IFormFile> uploadFiles)
        {
            _logger.LogInformation("SystemSetting.UploadCustomerLogo is called");
            try
            {
                foreach (var file in uploadFiles)
                {
                    var filename = ContentDispositionHeaderValue
                                        .Parse(file.ContentDisposition)
                                        .FileName
                                        .Trim('"');
                    filename = hostingEnv.WebRootPath + $@"\Theme\Customer.png";
                    long size = 0;
                    size += file.Length;
                    if (!System.IO.File.Exists(filename))
                    {
                        using (FileStream fs = System.IO.File.Create(filename))
                        {
                            file.CopyTo(fs);
                            fs.Flush();
                        }
                    }
                    else
                    {
                        System.IO.File.Delete(hostingEnv.WebRootPath + $@"\Theme\Customer.png");
                        if (!System.IO.File.Exists(filename))
                        {
                            using (FileStream fs = System.IO.File.Create(filename))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }
                        }
                    }
                   



                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in UploadCustomerLogo");
                Response.Clear();
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File failed to upload";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;

            }
            return RedirectToAction("Index");
        }

        [AcceptVerbs("Post")]
        public IActionResult UploadBackground(IList<IFormFile> uploadFiles2)
        {
            _logger.LogInformation("SystemSetting.UploadBackground is called");
            try
            {
                foreach (var file in uploadFiles2)
                {
                    var filename = ContentDispositionHeaderValue
                                        .Parse(file.ContentDisposition)
                                        .FileName
                                        .Trim('"');
                    filename = hostingEnv.WebRootPath + $@"\Theme\background.jpg";
                    long size = 0;
                    size += file.Length;
                    if (!System.IO.File.Exists(filename))
                    {
                        using (FileStream fs = System.IO.File.Create(filename))
                        {
                            file.CopyTo(fs);
                            fs.Flush();
                        }
                    }
                    else
                    {
                        System.IO.File.Delete(hostingEnv.WebRootPath + $@"\Theme\background.jpg");
                        if (!System.IO.File.Exists(filename))
                        {
                            using (FileStream fs = System.IO.File.Create(filename))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }
                        }
                    }




                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in UploadBackground");
                Response.Clear();
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File failed to upload";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;

            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> GetEntities(Guid categoryId)
        {
            _logger.LogInformation("SystemSetting.GetEntities is called with {categoryId}", categoryId);
            List<EntityListItem> entities = new List<EntityListItem>();
            if (categoryId == null) return Json(entities);
            List<Entity> entities2 = await _context.Entity.Where(e => e.EntityCategoryId == categoryId && e.IsRequired).ToListAsync();
            if (entities2 == null) return Json(entities);
            foreach (Entity e in entities2)
            {
                EntityListItem eli = new EntityListItem();
                eli.id = e.Id;
                eli.entityName = e.EntityName;
                entities.Add(eli);
            }
            return Json(entities);
        }

        public async Task<IActionResult> GetProperties(Guid entityId)
        {
            _logger.LogInformation("SystemSetting.GetProperties is called with {entityId}", entityId);
            List<PropertyListItem> properties = new List<PropertyListItem>();
            if (entityId == null) return Json(properties);
            List<EntityProperty> ep = await _context.EntityProperty.Where(e => e.EntityId == entityId && e.Property.IsRequired.Value).ToListAsync();
            _logger.LogDebug("Matching entity properties are {@ep}", ep);
            if (ep == null) return Json(properties);
            foreach (EntityProperty e in ep)
            {
                Property p = await _context.Property.SingleOrDefaultAsync(p => p.Id == e.PropertyId);
                PropertyListItem pli = new PropertyListItem();
                //pli.id = e.Property.Id;
                //pli.propertyName = e.Property.Name;
                pli.id = p.Id;
                pli.propertyName = p.Name;

                properties.Add(pli);
            }
            return Json(properties);
        }

        public async Task<IActionResult> SetPrimaryProperty(Guid propertyId)
        {
            _logger.LogInformation("SystemSetting.SetPrimaryProperty is called with {propertyId}", propertyId);
            if (propertyId == null) return Json("Invalid Property");
            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == propertyId && p.IsRequired.Value);
            if (property == null) return Json("Invalid Property");

            SystemSetting primaryProperty = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "Customer Primary Property");
            primaryProperty.Value = propertyId.ToString();// Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(propertyId.ToString())), Cryptography.Operation.Encrypt);

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return Json("Success");
        }
    }

    
}
