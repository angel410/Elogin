using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using eLogin.ViewModel;
using System.Text;

namespace eLogin.Controllers
{
    //[Authorize(Roles = nameof(Admin))]
    //[Authorize(Roles = "eLoginAdmin")]
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class IdentificationChannelsController : Controller
    {
        private readonly DatabaseContext _context;
        private LicenseCheck LC;
        private readonly ILogger<IdentificationChannelsController> _logger;

        public IdentificationChannelsController(DatabaseContext context, LicenseCheck licenseCheck, ILogger<IdentificationChannelsController> logger)
        {
            _context = context;
            LC = licenseCheck;
            _logger = logger;
        }

        // GET: IdentificationChannels
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("IdentificationChannels.Index is called");
            var IdentificationChannelVM = await (from ic in _context.IdentificationChannel
                              select new IdentificationChannelVM { Id = ic.Id, Channel = ic.Channel, DefaultIdentifierEntityId = ic.DefaultIdentifierEntity.EntityName, DefaultIdentifierPropertyId = ic.DefaultIdentifierProperty.Name, IsEnabled = ic.IsEnabled, Key = ic.Key }
                              ).ToListAsync();
            return View(IdentificationChannelVM);
        }

        // GET: IdentificationChannels/Details/5
        public async Task<IActionResult> Details(Guid Id)
        {
            _logger.LogInformation("IdentificationChannels.Details is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }
            _logger.LogDebug("Preparing IdentificationChannelVM");
            var IdentificationChannelVM = await (from ic in _context.IdentificationChannel
                                               select new IdentificationChannelVM { Id = ic.Id, Channel = ic.Channel, DefaultIdentifierEntityId = ic.DefaultIdentifierEntity.EntityName, DefaultIdentifierPropertyId = ic.DefaultIdentifierProperty.Name, IsEnabled = ic.IsEnabled, Key = ic.Key }
                              ).FirstOrDefaultAsync(m => m.Id == Id);
            if (IdentificationChannelVM == null)
            {
                _logger.LogError("IdentificationChannelVM == null");
                return NotFound();
            }

            return View(IdentificationChannelVM);
        }

        // GET: IdentificationChannels/Create
        [Authorize(Roles = "eLoginAdmin")]
        public async Task<IActionResult> Create()
        {
            _logger.LogInformation("IdentificationChannels.Create is called");
            _logger.LogDebug("Checking License Validity");
            if (!LC.Check().isValid) return Redirect("~/LicenseManager/LicenseValidationResult");
            _logger.LogDebug("Checking if the number of identification channels is fully utilized or not");
            int code = LC.Check().code;
            if(code >1000)
            {
                code = code / 10;
                code = code / 10;
                if (code % 10 == 1) 
                {
                    _logger.LogError("Number of identification channels is fully utilized.");
                    return Redirect("~/LicenseManager/LicenseValidationResult"); 
                }
            }

            _logger.LogDebug("Creating new Identification Channel");
            IdentificationChannel IdentificationChannel = new IdentificationChannel();
            _logger.LogDebug("Generating Channel Key");
            IdentificationChannel.Key = Cryptography.GenerateKey();

            _logger.LogDebug("Preparing ViewBag.Entities");
            var Entities = await _context.Entity
                .ToListAsync();

            var EntityList = new List<SelectListItem>();

            foreach (var Entity in Entities)
            {
                EntityList.Add(new SelectListItem(Entity.EntityName, Entity.Id.ToString()));
            }

            ViewData.Add("Entities", EntityList);
            ViewBag.Entities = EntityList;

            _logger.LogDebug("Preparing ViewBag.Properties");
            var EntityProperties = await _context.EntityProperty
                .Include(ep => ep.Property)
                .ToListAsync();


            List<EntityPropertiesListItem> Properties = new List<EntityPropertiesListItem>();

            foreach (EntityProperty EP in EntityProperties)
            {
                EntityPropertiesListItem EPI = new EntityPropertiesListItem();
                EPI.PropertyId = EP.PropertyId.Value;
                EPI.PropertyName = EP.Property.Name;
                EPI.EntityId = EP.EntityId.Value;

                Properties.Add(EPI);
            }
            ViewBag.Properties = Properties;


            return View(IdentificationChannel);
        }

        
        // POST: IdentificationChannels/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Channel,Key,IsEnabled,DefaultIdentifierEntityId,DefaultIdentifierPropertyId,IsDeleted")] IdentificationChannel IdentificationChannel)
        {
            _logger.LogInformation("IdentificationChannels.Create is called with {@IdentificationChannel}", IdentificationChannel);
            if (ModelState.IsValid)
            {
                _logger.LogDebug("Adding new IdentificationChannel in DB");
                _logger.LogDebug("Encrypting Channel Key in db");
                IdentificationChannel.Key = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(IdentificationChannel.Key)), Cryptography.Operation.Encrypt);
                _context.Add(IdentificationChannel);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                return RedirectToAction(nameof(Index));
            }
            else _logger.LogError("Model is invalid");
            return View(IdentificationChannel);
        }

        // GET: IdentificationChannels/Edit/5
        public async Task<IActionResult> Edit(Guid Id)
        {
            _logger.LogInformation("IdentificationChannels.Edit is called with {@Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }

            var IdentificationChannel = await _context.IdentificationChannel.FindAsync(Id);
            if (IdentificationChannel == null)
            {
                _logger.LogError("IdentificationChannel == null");
                return NotFound();
            }


            _logger.LogDebug("Preparing ViewBag.Entities");
            var Entities = await _context.Entity
                .ToListAsync();

            var EntityList = new List<SelectListItem>();

            foreach (var Entity in Entities)
            {
                EntityList.Add(new SelectListItem(Entity.EntityName, Entity.Id.ToString()));
            }

            ViewData.Add("Entities", EntityList);
            ViewBag.Entities = EntityList;

            var EntityProperties = await _context.EntityProperty
                .Include(ep => ep.Property)
                .ToListAsync();

            _logger.LogDebug("Preparing ViewBag.Properties");
            List<EntityPropertiesListItem> Properties = new List<EntityPropertiesListItem>();

            foreach(EntityProperty EP in EntityProperties)
            {
                EntityPropertiesListItem EPI = new EntityPropertiesListItem();
                EPI.PropertyId = EP.PropertyId.Value;
                EPI.PropertyName = EP.Property.Name;
                EPI.EntityId = EP.EntityId.Value;

                Properties.Add(EPI);
            }
            ViewBag.Properties = Properties;

            return View(IdentificationChannel);
        }

        // POST: IdentificationChannels/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid Id, [Bind("Id,Channel,Token,IsEnabled,DefaultIdentifierEntityId,DefaultIdentifierPropertyId,PasswordValidationHint,PasswordValidationRegex,PasswordValidityDays,IsDeleted")] IdentificationChannel IdentificationChannel)
        {
            _logger.LogInformation("IdentificationChannels.Edit is called with {Id} and {@IdentificationChannel}", Id, IdentificationChannel);
            if (Id != IdentificationChannel.Id)
            {
                _logger.LogError("Id != IdentificationChannel.Id");
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogDebug("Updating IdentificationChannel");
                    IdentificationChannel idch = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == IdentificationChannel.Id);
                    idch.Channel = IdentificationChannel.Channel;
                    idch.IsEnabled = IdentificationChannel.IsEnabled;
                    idch.DefaultIdentifierEntityId = IdentificationChannel.DefaultIdentifierEntityId;
                    idch.DefaultIdentifierPropertyId = IdentificationChannel.DefaultIdentifierPropertyId;
                    idch.PasswordValidationHint = IdentificationChannel.PasswordValidationHint;
                    idch.PasswordValidationRegex = IdentificationChannel.PasswordValidationRegex;
                    idch.PasswordValidityDays = IdentificationChannel.PasswordValidityDays;
                    _context.Update(idch);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                catch (DbUpdateConcurrencyException e)
                {
                    if (!IdentificationChannelExists(IdentificationChannel.Id))
                    {
                        _logger.LogError("!IdentificationChannelExists(IdentificationChannel.Id)");
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError(e, "Exception in IdentificationChannels.Edit");
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            else _logger.LogError("Model is invalid");
            return View(IdentificationChannel);
        }

        // GET: IdentificationChannels/Delete/5
        public async Task<IActionResult> Delete(Guid Id)
        {
            _logger.LogInformation("IdentificationChannels.Delete is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return NotFound();
            }

            //var IdentificationChannelVM = await (from ic in _context.IdentificationChannel
            //                                     join it in _context.Property on ic.DefaultIdentifierPropertyId equals it.Id
            //                                     join ec in _context.Entity on ic.DefaultIdentifierEntityId equals ec.Id
            //                                     select new IdentificationChannelVM { Id = ic.Id, Channel = ic.Channel, DefaultIdentifierEntityId = ec.EntityName, DefaultIdentifierPropertyId = it.Name, IsEnabled = ic.IsEnabled, Key = ic.Key }
            //                  ).FirstOrDefaultAsync(m => m.Id == Id);
            var IdentificationChannelVM = await (from ic in _context.IdentificationChannel
                                                 select new IdentificationChannelVM { Id = ic.Id, Channel = ic.Channel, DefaultIdentifierEntityId = ic.DefaultIdentifierEntity.EntityName, DefaultIdentifierPropertyId = ic.DefaultIdentifierProperty.Name, IsEnabled = ic.IsEnabled, Key = ic.Key }
                              ).FirstOrDefaultAsync(m => m.Id == Id);
            if (IdentificationChannelVM == null)
            {
                _logger.LogError("IdentificationChannelVM == null");
                return NotFound();
            }

            return View(IdentificationChannelVM);
        }

        // POST: IdentificationChannels/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid Id)
        {
            _logger.LogInformation("IdentificationChannels.DeleteConfirmed is called with {Id}", Id);
            var IdentificationChannel = await _context.IdentificationChannel.FindAsync(Id);
            //_context.IdentificationChannel.Remove(IdentificationChannel);
            IdentificationChannel.IsDeleted = true;
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            return RedirectToAction(nameof(Index));
        }

        private bool IdentificationChannelExists(Guid Id)
        {
            _logger.LogInformation("IdentificationChannels.IdentificationChannelExists is called with {Id}", Id);
            return _context.IdentificationChannel.Any(e => e.Id == Id);
        }

        public class EntityPropertiesListItem
        {
            public Guid PropertyId { get; set; }
            public string PropertyName { get; set; }
            public Guid EntityId { get; set; }
        }
    }
}
