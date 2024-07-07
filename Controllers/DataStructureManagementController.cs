using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class DataStructureManagementController : Controller
    {
        private readonly DatabaseContext _context;
        private LicenseCheck LC;
        private readonly eLoginSettings _eLoginSettings;
        private readonly ILogger<DataStructureManagementController> _logger;

        public DataStructureManagementController(DatabaseContext context, IOptions<eLoginSettings> eLoginSettings, LicenseCheck licenseCheck, ILogger<DataStructureManagementController> logger)
        {
            _context = context;
            LC = licenseCheck;
            _eLoginSettings = eLoginSettings.Value;
            _logger = logger;
        }

        public class EntityCategoryListItem
        {
            public Guid id { get; set; }
            public string categoryName { get; set; }
            public Guid? parentId { get; set; }
            public bool hasChild { get; set; }
        }

        public class EnitityListItem
        {
            public Guid id { get; set; }
            public string entityName { get; set; }
            public bool isRequired { get; set; } = false;
            public Guid entityCategoryId { get; set; }
        }

        public class PropertiesListItem
        {
            public Guid id { get; set; }
            public string propertyName { get; set; }
            public string? validationRegex { get; set; }
            public string? validationHint { get; set; }
            public bool isEncrypted { get; set; } = false;
            public bool isHashed { get; set; } = false;
            public bool isUniqueIdentifier { get; set; }
            public bool isRequired { get; set; } = false;
            public Guid entityId { get; set; }

        }

        public class ChannelEntityListItem
        {
            public Guid id { get; set; }
            public Guid entityId { get; set; }
            public string entityName { get; set; }
            public bool isRequired { get; set; } = false;
            public Guid identificationChannelId { get; set; }
            public string channel { get; set; }
        }

        public class ChannelListItem
        {
            public Guid id { get; set; }
            public string channel { get; set; }
        }

        public class ChannelLoginPropertyListItem
        {
            public Guid id { get; set; }
            public Guid channelId { get; set; }
            public string channel { get; set; }
            public Guid propertyId { get; set; }
            public string propertyName { get; set; }
            public Guid entityId { get; set; }
            public string entityName { get; set; }
            public Guid categoryId { get; set; }
            public string categoryName { get; set; }
        }

        public class InstanceProperty
        {
            public Guid propertyId { get; set; }
            public string propertyName { get; set; }
            public string value { get; set; }
           
        }

        public class EntityListItem
        {
            public Guid id { get; set; }
            public string entityName { get; set; }
        }

        public async Task<List<PropertiesListItem>> PropertiesList()
        {
            _logger.LogInformation("DataStructureManagement.PropertiesList is called");
            List<Property> properties = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
            List<PropertiesListItem> pl = new List<PropertiesListItem>();

            if (properties != null)
            {
                foreach (Property property1 in properties)
                {
                    PropertiesListItem pli = new PropertiesListItem();
                    pli.id = property1.Id;
                    pli.propertyName = property1.Name;
                    pli.validationRegex = property1.ValidationRegex;
                    pli.validationHint = property1.ValidationHint;
                    pli.isEncrypted = property1.IsEncrypted.Value;
                    pli.isHashed = property1.IsHashed.Value;
                    pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                    pli.isRequired = property1.IsRequired.Value;

                    _logger.LogDebug("Getting related entities");
                    List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                    if (entityProperties1 != null)
                    {
                        foreach (EntityProperty entityProperty in entityProperties1)
                        {
                            pli.entityId = entityProperty.EntityId.Value;
                            pl.Add(pli);
                        }
                    }
                }
            }

            return pl;
        }

        // GET: EntityProperties
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("DataStructureManagement.Index is called");
            List<EntityCategory> entityCategories = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            _logger.LogDebug("Preparing entityCategoriesDataSource");
            if (entityCategories != null)
            {
                foreach (EntityCategory category in entityCategories)
                {
                    EntityCategoryListItem ecli = new EntityCategoryListItem();
                    ecli.id = category.Id;
                    ecli.categoryName = category.CategoryName;
                    ecli.parentId = category.ParentEntityCategoryId;
                    //if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                    ecl.Add(ecli);
                }
            }

            ViewBag.entityCategoriesDataSource = ecl;

            List<Entity> entities = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
            List<EnitityListItem> cel = new List<EnitityListItem>();

            _logger.LogDebug("Preparing entitiesDataSource");
            if (entities != null)
            {
                foreach (Entity entity in entities)
                {
                    EnitityListItem celi = new EnitityListItem();
                    celi.id = entity.Id;
                    celi.entityName = entity.EntityName;
                    celi.isRequired = entity.IsRequired;
                    celi.entityCategoryId = entity.EntityCategoryId;
                    cel.Add(celi);

                }
            }

            ViewBag.entitiesDataSource = cel;

            List<Property> properties = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
            List<PropertiesListItem> pl = new List<PropertiesListItem>();

            _logger.LogDebug("Preparing propertiesDataSource");
            if (properties != null)
            {
                foreach (Property property in properties)
                {
                    PropertiesListItem pli = new PropertiesListItem();
                    pli.id = property.Id;
                    pli.propertyName = property.Name;
                    pli.validationRegex = property.ValidationRegex;
                    pli.validationHint = property.ValidationHint;
                    pli.isEncrypted = property.IsEncrypted.Value;
                    pli.isHashed = property.IsHashed.Value;
                    pli.isUniqueIdentifier = property.IsUniqueIdentifier.Value;
                    pli.isRequired = property.IsRequired.Value;
                    List<EntityProperty> entityProperties = await _context.EntityProperty.Where(ep => ep.PropertyId == property.Id).ToListAsync();
                    if (entityProperties != null)
                    {
                        foreach (EntityProperty entityProperty in entityProperties)
                        {
                            pli.entityId = entityProperty.EntityId.Value;
                            pl.Add(pli);
                        }
                    }
                }
            }

            ViewBag.propertiesDataSource = pl;

            List<ChannelEntity> channelEntities = await _context.ChannelEntity.Where(ce => ce.Entity.IsDeleted == false && ce.IdentificationChannel.IsDeleted == false).ToListAsync();
            List<ChannelEntityListItem> channelEnityList = new List<ChannelEntityListItem>();

            _logger.LogDebug("Preparing channelEntityDataSource");
            if (channelEntities != null)
            {
                foreach (ChannelEntity channelEntity in channelEntities)
                {
                    ChannelEntityListItem channelEntityListItem = new ChannelEntityListItem();
                    channelEntityListItem.id = channelEntity.Id;
                    channelEntityListItem.entityId = channelEntity.EntityId;
                    channelEntityListItem.entityName = channelEntity.Entity.EntityName;
                    channelEntityListItem.isRequired = channelEntity.Entity.IsRequired;
                    channelEntityListItem.identificationChannelId = channelEntity.IdentificationChannelId;
                    channelEntityListItem.channel = channelEntity.IdentificationChannel.Channel;
                    channelEnityList.Add(channelEntityListItem);
                }
            }
            ViewBag.channelEntityDataSource = channelEnityList;

            List<IdentificationChannel> identificationChannels = await _context.IdentificationChannel.Where(ic => ic.IsDeleted == false).ToListAsync();
            List<ChannelListItem> channelListItems = new List<ChannelListItem>();

            _logger.LogDebug("Preparing channelDatasource");
            foreach (IdentificationChannel channel in identificationChannels)
            {
                ChannelListItem channelListItem = new ChannelListItem();
                channelListItem.id = channel.Id;
                channelListItem.channel = channel.Channel;
                channelListItems.Add(channelListItem);
            }
            ViewBag.channelDatasource = channelListItems;

            List<EntityCategoryListItem> categories = new List<EntityCategoryListItem>();
            List<EntityCategory> entityCategories1 = await _context.EntityCategory.ToListAsync();

            _logger.LogDebug("Preparing categoriesDataSource");
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

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> GetChannelLoginProperties(Guid id)
        {
            _logger.LogInformation("DataStructureManagement.GetChannelLoginProperties is called with {id}", id);
            if (id == null)
            {
                _logger.LogError("id == null");
                return NotFound();
            }

            List<ChannelLoginProperty> channelLoginProperties = await _context.ChannelLoginProperty.Where(clp => clp.IdentificationChannelId == id).ToListAsync();
            List<ChannelLoginPropertyListItem> channelLoginPropertyListItems = new List<ChannelLoginPropertyListItem>();
            foreach (ChannelLoginProperty channelLoginProperty in channelLoginProperties)
            {
                ChannelLoginPropertyListItem channelLoginPropertyListItem = new ChannelLoginPropertyListItem();
                channelLoginPropertyListItem.id = channelLoginProperty.Id;
                channelLoginPropertyListItem.channelId = channelLoginProperty.IdentificationChannelId;
                channelLoginPropertyListItem.channel = channelLoginProperty.IdentificationChannel.Channel;
                channelLoginPropertyListItem.propertyId = channelLoginProperty.PropertyId;
                channelLoginPropertyListItem.propertyName = channelLoginProperty.Property.Name;
                List<EntityProperty> entityProperties = await _context.EntityProperty.Where(ep => ep.PropertyId == channelLoginProperty.PropertyId).ToListAsync();
                foreach (EntityProperty entityProperty in entityProperties)
                {
                    channelLoginPropertyListItem.entityId = entityProperty.EntityId.Value;
                    channelLoginPropertyListItem.entityName = entityProperty.Entity.EntityName;
                    channelLoginPropertyListItem.categoryId = entityProperty.Entity.EntityCategoryId;
                    channelLoginPropertyListItem.categoryName = entityProperty.Entity.EntityCategory.CategoryName;
                    channelLoginPropertyListItems.Add(channelLoginPropertyListItem);
                }

            }



            return Json(channelLoginPropertyListItems);

        }


        [HttpPost]
        public async Task<IActionResult> AddChannelLoginProperty(Guid propertyid, Guid channelid)
        {
            _logger.LogInformation("DataStructureManagement.AddChannelLoginProperty is called with {propertyid} and {channelid}", propertyid, channelid);
            if (propertyid == null || channelid == null)
            {
                _logger.LogError("propertyid == null || channelid == null");
                return NotFound();
            }

            Property property = await _context.Property.SingleOrDefaultAsync(e => e.Id == propertyid);
            IdentificationChannel identificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == channelid);
            if (property == null || identificationChannel == null)
            {
                _logger.LogError("property == null || identificationChannel == null");
                return NotFound();
            }
            if (!property.IsUniqueIdentifier.Value)
            {
                _logger.LogError("Property is not a unique identifier");
                return BadRequest();
            }

            int countCurrentChannelLoginProperties = await _context.ChannelLoginProperty.Where(c => c.IdentificationChannelId == channelid).CountAsync();
            if (countCurrentChannelLoginProperties >= _eLoginSettings.MaxPropertiesUsedForLoginPerChannel)
            {
                _logger.LogError("countCurrentChannelLoginProperties >= _eLoginSettings.MaxPropertiesUsedForLoginPerChannel");
                return BadRequest();
            }

            ChannelLoginProperty channelLoginProperty1 = await _context.ChannelLoginProperty.SingleOrDefaultAsync(c => c.IdentificationChannelId == channelid && c.PropertyId == propertyid);
            if (channelLoginProperty1 == null)
            {
                ChannelLoginProperty newChannelLoginProperty = new ChannelLoginProperty();
                newChannelLoginProperty.IdentificationChannelId = identificationChannel.Id;
                newChannelLoginProperty.PropertyId = property.Id;
                _context.Add(newChannelLoginProperty);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            }

            
            return Ok();

        }

        [HttpPost]
        public async Task<IActionResult> RemoveChannelLoginProperty(Guid id)
        {
            _logger.LogInformation("DataStructureManagement.RemoveChannelLoginProperty is called with {id}", id);
            if (id == null)
            {
                _logger.LogError("id == null");
                return NotFound();
            }



            ChannelLoginProperty channelLoginProperty = await _context.ChannelLoginProperty.SingleOrDefaultAsync(c => c.Id == id);
            if (channelLoginProperty == null)
            {
                _logger.LogError("ChannelLoginProperty not found");
                return NotFound();
            }
            else
            {
                _context.ChannelLoginProperty.Remove(channelLoginProperty);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            }


            return Ok();

        }

        // Refresh Assigned Channels
        [HttpPost]
        public async Task<IActionResult> RefreshAssignedChannels(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.RemoveChannelLoginProperty is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return Json("");
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == Id);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return Json("");
            }


            List<IdentificationChannel> allChannels = await _context.IdentificationChannel.ToListAsync();
            List<ChannelListItem> unAssignedChannelList = new List<ChannelListItem>();

            _logger.LogDebug("Preparing unAssignedChannelsDataSource using all channels");
            foreach (IdentificationChannel channel1 in allChannels)
            {
                ChannelListItem channelListItem = new ChannelListItem();
                channelListItem.id = channel1.Id;
                channelListItem.channel = channel1.Channel;
                unAssignedChannelList.Add(channelListItem);
            }

            List<ChannelListItem> assignedChannelList = new List<ChannelListItem>();
            List<IdentificationChannel> assignedChannels = await _context.IdentificationChannel.Where(ic => ic.ChannelEntities.Any(ce => ce.EntityId == Id)).ToListAsync();

            _logger.LogDebug("Preparing AssignedChannelsDataSource and removing them from unAssignedChannelsDataSource");
            if (assignedChannels != null)
            {
                foreach (IdentificationChannel channel in assignedChannels)
                {
                    ChannelListItem channelListItem = new ChannelListItem();
                    channelListItem.id = channel.Id;
                    channelListItem.channel = channel.Channel;
                    assignedChannelList.Add(channelListItem);

                    ChannelListItem channelListItem1 = unAssignedChannelList.SingleOrDefault(u => u.id == channel.Id);
                    unAssignedChannelList.Remove(channelListItem1);

                }
            }
            ViewBag.assignedChannelsDataSource = assignedChannelList;
            ViewBag.unAssignedChannelsDataSource = unAssignedChannelList;



            return Json(assignedChannelList);

        }

        // Refresh UnAssigned Channels
        [HttpPost]
        public async Task<IActionResult> RefreshUnAssignedChannels(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.RefreshUnAssignedChannels is called with {Id}", Id);
            if (Id == null)
            {
                _logger.LogError("Id == null");
                return Json("");
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == Id);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return Json("");
            }


            List<IdentificationChannel> allChannels = await _context.IdentificationChannel.ToListAsync();
            List<ChannelListItem> unAssignedChannelList = new List<ChannelListItem>();

            _logger.LogDebug("Preparing unAssignedChannelsDataSource using all channels");
            foreach (IdentificationChannel channel1 in allChannels)
            {
                ChannelListItem channelListItem = new ChannelListItem();
                channelListItem.id = channel1.Id;
                channelListItem.channel = channel1.Channel;
                unAssignedChannelList.Add(channelListItem);
            }

            List<ChannelListItem> assignedChannelList = new List<ChannelListItem>();
            List<IdentificationChannel> assignedChannels = await _context.IdentificationChannel.Where(ic => ic.ChannelEntities.Any(ce => ce.EntityId == Id)).ToListAsync();

            _logger.LogDebug("Preparing AssignedChannelsDataSource and removing them from unAssignedChannelsDataSource");
            if (assignedChannels != null)
            {
                foreach (IdentificationChannel channel in assignedChannels)
                {
                    ChannelListItem channelListItem = new ChannelListItem();
                    channelListItem.id = channel.Id;
                    channelListItem.channel = channel.Channel;
                    assignedChannelList.Add(channelListItem);

                    ChannelListItem channelListItem1 = unAssignedChannelList.SingleOrDefault(u => u.id == channel.Id);
                    unAssignedChannelList.Remove(channelListItem1);

                }
            }
            ViewBag.assignedChannelsDataSource = assignedChannelList;
            ViewBag.unAssignedChannelsDataSource = unAssignedChannelList;



            return Json(unAssignedChannelList);

        }

        // Assign Channels
        [HttpPost]
        public async Task<IActionResult> AssignChannels(Guid channelid, Guid entityid)
        {
            _logger.LogInformation("DataStructureManagement.AssignChannels is called with {channelid} and {entityid}", channelid, entityid);
            if (entityid == null)
            {
                _logger.LogError("entityid == null");
                return NotFound();
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityid);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }

            if (channelid == null)
            {
                _logger.LogError("channelid == null");
                return NotFound();
            }
            IdentificationChannel channel = await _context.IdentificationChannel.SingleOrDefaultAsync(c => c.Id == channelid);
            if (channel == null)
            {
                _logger.LogError("channel == null");
                return NotFound();
            }

            ChannelEntity existingChannelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(e => e.IdentificationChannelId == channelid && e.EntityId == entityid);
            if (existingChannelEntity == null)
            {
                _logger.LogDebug("Adding new ChannelEntity");
                ChannelEntity channelEntity = new ChannelEntity();
                channelEntity.IdentificationChannelId = channel.Id;
                channelEntity.EntityId = entity.Id;
                _context.Add(channelEntity);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            }


            return Ok();

        }

        // Unassign Channels
        [HttpPost]
        public async Task<IActionResult> UnAssignChannels(Guid channelid, Guid entityid)
        {
            _logger.LogInformation("DataStructureManagement.UnAssignChannels is called with {channelid} and {entityid}", channelid, entityid);
            if (entityid == null)
            {
                _logger.LogError("entityid == null");
                return NotFound();
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityid);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }

            if (channelid == null)
            {
                _logger.LogError("channelid == null");
                return NotFound();
            }
            IdentificationChannel channel = await _context.IdentificationChannel.SingleOrDefaultAsync(c => c.Id == channelid);
            if (channel == null)
            {
                _logger.LogError("channel == null");
                return NotFound();
            }

            ChannelEntity existingChannelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(e => e.IdentificationChannelId == channelid && e.EntityId == entityid);
            if (existingChannelEntity != null)
            {
                _logger.LogDebug("Removing ChannelEntity");
                _context.ChannelEntity.Remove(existingChannelEntity);
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            }


            return Ok();

        }

        // Assign All Channels
        [HttpPost]
        public async Task<IActionResult> AssignAllChannels(Guid entityid)
        {
            _logger.LogInformation("DataStructureManagement.AssignAllChannels is called with {entityid}", entityid);
            if (entityid == null)
            {
                _logger.LogError("entityid == null");
                return NotFound();
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityid);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }


            List<IdentificationChannel> channels = await _context.IdentificationChannel.ToListAsync();
            foreach (IdentificationChannel channel in channels)
            {
                ChannelEntity existingChannelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(e => e.IdentificationChannelId == channel.Id && e.EntityId == entityid);
                if (existingChannelEntity == null)
                {
                    _logger.LogDebug("Adding entity {entity.EntityName} to channel {channel.Channel}", entity.EntityName, channel.Channel);
                    ChannelEntity channelEntity = new ChannelEntity();
                    channelEntity.IdentificationChannelId = channel.Id;
                    channelEntity.EntityId = entity.Id;
                    _context.Add(channelEntity);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
            }

            return Ok();

        }

        // UnAssign All Channels
        [HttpPost]
        public async Task<IActionResult> UnAssignAllChannels(Guid entityid)
        {
            _logger.LogInformation("DataStructureManagement.UnAssignAllChannels is called with {entityid}", entityid);
            if (entityid == null)
            {
                _logger.LogError("entityid == null");
                return NotFound();
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityid);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }


            List<IdentificationChannel> channels = await _context.IdentificationChannel.ToListAsync();
            foreach (IdentificationChannel channel in channels)
            {
                ChannelEntity existingChannelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(e => e.IdentificationChannelId == channel.Id && e.EntityId == entityid);
                if (existingChannelEntity != null)
                {
                    _logger.LogDebug("Removing entity {entity.EntityName} from channel {channel.Channel}", entity.EntityName, channel.Channel);
                    _context.ChannelEntity.Remove(existingChannelEntity);
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
            }

            return Ok();

        }

       
        private bool EntityCategoryExists(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.EntityCategoryExists is called with {Id}", Id);
            return _context.EntityCategory.Any(e => e.Id == Id);
        }

       
        public async Task<IActionResult> DeleteCategory(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.DeleteCategory is called with {Id}", Id);
            if (Id != null)
            {
                var entityCategory = await _context.EntityCategory
                .FirstOrDefaultAsync(m => m.Id == Id);
                if (entityCategory != null)
                {
                    entityCategory.IsDeleted = true;
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                
            }

            

            List<EntityCategory> entityCategories = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            if (entityCategories != null)
            {
                foreach (EntityCategory category in entityCategories)
                {
                    EntityCategoryListItem ecli = new EntityCategoryListItem();
                    ecli.id = category.Id;
                    ecli.categoryName = category.CategoryName;
                    ecli.parentId = category.ParentEntityCategoryId;
                    //if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                    ecl.Add(ecli);
                }
            }

            return Json(ecl);
        }

        public async Task<IActionResult> EditEntity(Guid id)
        {
            _logger.LogInformation("DataStructureManagement.EditEntity is called with {id}", id);
            if (id == null)
            {
                _logger.LogError("id == null");
                return NotFound();
            }

            var entity = await _context.Entity.FindAsync(id);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return NotFound();
            }

            var Categories = await _context.EntityCategory.Where(e => e.IsDeleted == false).ToArrayAsync();

            _logger.LogDebug("Preparing Categories viewbag");
            var CategoriesList = new List<SelectListItem>();

            foreach (var Category in Categories)
            {
                CategoriesList.Add(new SelectListItem(Category.CategoryName, Category.Id.ToString()));
            }
            ViewData.Add("Categories", CategoriesList);
            ViewBag.Categories = CategoriesList;

            return View(entity);
        }

        // POST: Entities/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEntity(Guid id, [Bind("Id,EntityName,IsDeleted,EntityCategoryId")] Entity entity)
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
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
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
        public async Task<IActionResult> DeleteEntity(Guid id)
        {
            if (id != null)
            {
                var entity = await _context.Entity
                .Include(e => e.EntityCategory)
                .FirstOrDefaultAsync(m => m.Id == id);
                if (entity != null)
                {
                    entity.IsDeleted = true;
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
                
            }


            List<Entity> entities = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
            List<EnitityListItem> cel = new List<EnitityListItem>();

            if (entities != null)
            {
                foreach (Entity entity in entities)
                {
                    EnitityListItem celi = new EnitityListItem();
                    celi.id = entity.Id;
                    celi.entityName = entity.EntityName;
                    celi.isRequired = entity.IsRequired;
                    celi.entityCategoryId = entity.EntityCategoryId;
                    cel.Add(celi);

                }
            }

            return Json(cel);
        }

        private bool EntityExists(Guid id)
        {
            _logger.LogInformation("DataStructureManagement.EntityExists is called with {id}", id);
            return _context.Entity.Any(e => e.Id == id);
        }

        public async Task<IActionResult> EditProperty(Guid Id)
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
        public async Task<IActionResult> EditProperty(Guid Id, [Bind("Id,Name,ValidationRegex,ValidationHint,IsEncrypted,IsHashed,IsUniqueIdentifier,IsDeleted")] Property Property)
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
        public async Task<IActionResult> DeleteProperty(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.DeleteProperty is called with {Id}", Id);
            if (Id != null)
            {
                var Property = await _context.Property
                .FirstOrDefaultAsync(m => m.Id == Id);
                if (Property != null)
                {
                    Property.IsDeleted = true;
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                }
            }

            _logger.LogDebug("Preparing properties list");
            List<Property> properties = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
            List<PropertiesListItem> pl = new List<PropertiesListItem>();

            if (properties != null)
            {
                foreach (Property property in properties)
                {
                    PropertiesListItem pli = new PropertiesListItem();
                    pli.id = property.Id;
                    pli.propertyName = property.Name;
                    pli.validationRegex = property.ValidationRegex;
                    pli.validationHint = property.ValidationHint;
                    pli.isEncrypted = property.IsEncrypted.Value;
                    pli.isHashed = property.IsHashed.Value;
                    pli.isUniqueIdentifier = property.IsUniqueIdentifier.Value;
                    pli.isRequired = property.IsRequired.Value;
                    List<EntityProperty> entityProperties = await _context.EntityProperty.Where(ep => ep.PropertyId == property.Id).ToListAsync();
                    if (entityProperties != null)
                    {
                        foreach (EntityProperty entityProperty in entityProperties)
                        {
                            pli.entityId = entityProperty.EntityId.Value;
                            pl.Add(pli);
                        }
                    }
                }
            }

            return Json(pl);
        }

        //// POST: Properties/Delete/5
        //[HttpPost, ActionName("DeleteProperty")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeletePropertyConfirmed(Guid Id)
        //{
        //    var Property = await _context.Property.FindAsync(Id);
        //    //_context.Property.Remove(Property);
        //    Property.IsDeleted = true;
        //    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
        //    return RedirectToAction(nameof(Index));
        //}

        private bool PropertyExists(Guid Id)
        {
            _logger.LogInformation("DataStructureManagement.PropertyExists is called with {Id}", Id);
            return _context.Property.Any(e => e.Id == Id);
        }

        public class CustomerRecord
        {
            public Guid CustomerID { get; set; }
            public string EntityCategory { get; set; }
            public string EntityName { get; set; }
            public Guid EntityInstanceID { get; set; }
            public string EntityInstanceName { get; set; }
            public Guid ValueID { get; set; }
            public string Value { get; set; }
            public string PropertyName { get; set; }
            public Guid ParentEntityCategoryId { get; set; }
            public string PCategoryName { get; set; }
            public string PPCategoryName { get; set; }
        }



        //public async Task<IActionResult> SearchCustomer(string? searchValue)
        //{
        //    _logger.LogInformation("DataStructureManagement.SearchCustomer is called with {searchValue}", searchValue);
        //    if (searchValue.IsNullOrEmpty())
        //    {
        //        _logger.LogError("searchValue IsNullOrEmpty");
        //        return NotFound();
        //    }

        //    _logger.LogDebug("Searching into the database for the value in plain text");
        //    List<CustomerInfoValue> customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.Value.Contains(searchValue)).ToListAsync();

        //    if (searchValue != null)
        //    {
        //        _logger.LogDebug("Encrypting the search text");
        //        string encryptedSearchValue = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(searchValue)), Cryptography.Operation.Encrypt);
        //        _logger.LogDebug("Searching for the encrypted text");
        //        List<CustomerInfoValue> customerEncryptedInfoValues = await _context.CustomerInfoValue.Where(c => c.Value.Equals(encryptedSearchValue)).ToListAsync();

        //        _logger.LogDebug("Adding encrypted matching values to the search result");
        //        if (customerEncryptedInfoValues != null)
        //        {
        //            foreach (CustomerInfoValue customerEncryptedInfoValue in customerEncryptedInfoValues)
        //            {
        //                customerInfoValues1.Add(customerEncryptedInfoValue);
        //            }
        //        }

        //    }
        //    _logger.LogDebug("Creating a new list of matching customers");
        //    List<Customer> matchingCustomers = new List<Customer>();
        //    _logger.LogDebug("Looping over all matched values and adding the customer to the list");
        //    foreach (CustomerInfoValue civ in customerInfoValues1)
        //    {
        //        Customer c = new Customer();
        //        c = civ.Customer;
        //        matchingCustomers.Add(c);
        //    }
        //    _logger.LogDebug("filtering the list to distinct customers");
        //    matchingCustomers = matchingCustomers.Distinct().ToList();

        //    _logger.LogDebug("Creating a list of records to be previewed to the user");
        //    var customerRecords = new List<CustomerRecord>();

        //    foreach (Customer Customer in matchingCustomers)
        //    {
        //        foreach (var EntityInstance in Customer.EntitieInstances)
        //        {
        //            foreach (var InfoValue in EntityInstance.CustomerInfoValues)
        //            {
        //                string Value;
        //                if (InfoValue.Property.IsEncrypted)
        //                {
        //                    Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, InfoValue.Value, Cryptography.Operation.Decrypt);
        //                    if (Value.Length > 7)
        //                    {
        //                        Regex regex = new Regex("[^ -](?=.{4})");
        //                        Value = regex.Replace(Value, "X");
        //                    }
        //                    else
        //                    {
        //                        Regex regex = new Regex(".");
        //                        Value = regex.Replace(Value, "X");
        //                    }

        //                }
        //                else Value = InfoValue.Value;
        //                if (EntityInstance.Entity != null && EntityInstance.Entity.EntityCategory != null)
        //                {
        //                    if (EntityInstance.Entity.EntityCategory.ParentEntityCategory != null)
        //                    {
        //                        if (EntityInstance.Entity.EntityCategory.ParentEntityCategory.ParentEntityCategory != null)
        //                        {
        //                            customerRecords.Add(new CustomerRecord()
        //                            {
        //                                CustomerID = Customer.Id,
        //                                EntityCategory = EntityInstance.Entity.EntityCategory.CategoryName,
        //                                EntityName = EntityInstance.Entity.EntityName,
        //                                EntityInstanceID = EntityInstance.Id,
        //                                EntityInstanceName = EntityInstance.EntityInstanceName,
        //                                ValueID = InfoValue.Id,
        //                                Value = Value,
        //                                PropertyName = InfoValue.Property.Name,
        //                                ParentEntityCategoryId = EntityInstance.Entity.EntityCategory.ParentEntityCategory.Id,
        //                                PCategoryName = EntityInstance.Entity.EntityCategory.ParentEntityCategory.CategoryName,
        //                                PPCategoryName = EntityInstance.Entity.EntityCategory.ParentEntityCategory.ParentEntityCategory.CategoryName
        //                            }); ;
        //                        }
        //                        else
        //                        {
        //                            customerRecords.Add(new CustomerRecord()
        //                            {
        //                                CustomerID = Customer.Id,
        //                                PCategoryName = EntityInstance.Entity.EntityCategory.CategoryName,
        //                                EntityName = EntityInstance.Entity.EntityName,
        //                                EntityInstanceID = EntityInstance.Id,
        //                                EntityInstanceName = EntityInstance.EntityInstanceName,
        //                                ValueID = InfoValue.Id,
        //                                Value = Value,
        //                                PropertyName = InfoValue.Property.Name,
        //                                ParentEntityCategoryId = EntityInstance.Entity.EntityCategory.ParentEntityCategory.Id,
        //                                PPCategoryName = EntityInstance.Entity.EntityCategory.ParentEntityCategory.CategoryName

        //                            }); ;
        //                        }

        //                    }
        //                    else
        //                    {
        //                        customerRecords.Add(new CustomerRecord()
        //                        {
        //                            CustomerID = Customer.Id,
        //                            PPCategoryName = EntityInstance.Entity.EntityCategory.CategoryName,
        //                            EntityName = EntityInstance.Entity.EntityName,
        //                            EntityInstanceID = EntityInstance.Id,
        //                            EntityInstanceName = EntityInstance.EntityInstanceName,
        //                            ValueID = InfoValue.Id,
        //                            Value = Value,
        //                            PropertyName = InfoValue.Property.Name
        //                        }); ;
        //                    }
        //                }


        //            }
        //        }

        //    }

        //    //ViewBag.DataSource = customerRecords;

        //    return Json(customerRecords);
        //}

        //public async Task<IActionResult> AddNewCustomerData(List<InstanceProperty>? values, string? instanceName, Guid entityId)
        //{
        //    if (values.IsNullOrEmpty())
        //    {
        //        return BadRequest();
        //    }
        //    if (entityId == null)
        //    {
        //        return BadRequest();
        //    }
        //    Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            
        //    if (instanceName.IsNullOrEmpty())
        //    {
        //        instanceName = entity.EntityName;
        //    }
        //    Customer customer = new Customer();
        //    customer.IsLocked = false;

        //    await _context.AddAsync(customer);

        //    EntityInstance entityInstance = new EntityInstance();
        //    entityInstance.CustomerId = customer.Id;
        //    entityInstance.EntityId = entity.Id;
        //    entityInstance.EntityInstanceName = instanceName;

        //    await _context.AddAsync(entityInstance);

        //    foreach (InstanceProperty instanceProperty in values)
        //    {
        //        if(!instanceProperty.value.IsNullOrEmpty())
        //        {
        //            if (instanceProperty.propertyId == null)
        //            {
        //                return BadRequest();
        //            }
        //            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == instanceProperty.propertyId);
        //            if (entity == null || property == null)
        //            {
        //                return BadRequest();
        //            }
        //            EntityProperty entityProperty = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.EntityId == entity.Id && ep.PropertyId == property.Id);
        //            if (entityProperty == null)
        //            {
        //                return Json("Invalid property: " + property.Name + " for entity: " + entity.EntityName);
        //            }

        //            if (!String.IsNullOrEmpty(property.ValidationRegex))
        //            {
        //                Regex regex = new Regex(property.ValidationRegex);

        //                if (!regex.IsMatch(instanceProperty.value))
        //                {
        //                    return Json("In valid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
        //                }
        //            }

        //            if (property.IsEncrypted)
        //            {
        //                instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
        //            }
        //            else if (property.IsHashed)
        //            {
        //                instanceProperty.value = Cryptography.Hash(instanceProperty.value);
        //            }

        //            if (property.IsUniqueIdentifier)
        //            {
        //                CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
        //                if (customerInfoValue1 != null)
        //                {
        //                    return Json("Can not insert a duplicate record for a unique identifier");
        //                }
        //            }

        //            CustomerInfoValue customerInfoValue = new CustomerInfoValue();
        //            customerInfoValue.CustomerId = customer.Id;
        //            customerInfoValue.EntityInstanceId = entityInstance.Id;

        //            customerInfoValue.PropertyId = property.Id;

        //            customerInfoValue.Value = instanceProperty.value;


        //            await _context.AddAsync(customerInfoValue);



        //        }

        //    }


        //    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);



        //    return Json("Success");
        //}

        //public async Task<IActionResult> AddExistingCustomerNewInstanceData(List<InstanceProperty>? values, string? instanceName, Guid customerId, Guid entityId)
        //{
        //    if (values.IsNullOrEmpty())
        //    {
        //        return BadRequest();
        //    }
        //    if (entityId == null || customerId == null)
        //    {
        //        return BadRequest();
        //    }
        //    Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            
        //    Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerId);
        //    if (entity == null || customer == null)
        //    {
        //        return BadRequest();
        //    }
        //    if (instanceName.IsNullOrEmpty())
        //    {
        //        instanceName = entity.EntityName;
        //    }

        //    EntityInstance existingInstance = await _context.EntityInstance.FirstOrDefaultAsync(ei => ei.CustomerId == customerId && ei.EntityId == entity.Id && ei.EntityInstanceName == instanceName);
        //    if(existingInstance != null)
        //    {
        //        return Json("Another Instance with the same name already exists for the same customer.");

        //    }

        //    EntityInstance entityInstance = new EntityInstance();
        //    entityInstance.CustomerId = customer.Id;
        //    entityInstance.EntityId = entity.Id;
        //    entityInstance.EntityInstanceName = instanceName;

        //    await _context.AddAsync(entityInstance);

        //    foreach (InstanceProperty instanceProperty in values)
        //    {
        //        if (!instanceProperty.value.IsNullOrEmpty())
        //        {
        //            if (instanceProperty.propertyId == null)
        //            {
        //                return BadRequest();
        //            }
        //            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == instanceProperty.propertyId);
        //            if (entity == null || property == null)
        //            {
        //                return BadRequest();
        //            }

        //            EntityProperty entityProperty = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.EntityId == entity.Id && ep.PropertyId == property.Id);
        //            if (entityProperty == null)
        //            {
        //                return Json("Invalid property: " + property.Name + " for entity: " + entity.EntityName);
        //            }

        //            if (!String.IsNullOrEmpty(property.ValidationRegex))
        //            {
        //                Regex regex = new Regex(property.ValidationRegex);

        //                if (!regex.IsMatch(instanceProperty.value))
        //                {
        //                    return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
        //                }
        //            }

        //            if (property.IsEncrypted)
        //            {
        //                instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
        //            }
        //            else if (property.IsHashed)
        //            {
        //                instanceProperty.value = Cryptography.Hash(instanceProperty.value);
        //            }

        //            if (property.IsUniqueIdentifier)
        //            {
        //                CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
        //                if (customerInfoValue1 != null)
        //                {
        //                    return Json("Can not insert a duplicate record for a unique identifier");
        //                }
        //            }

        //            CustomerInfoValue customerInfoValue = new CustomerInfoValue();
        //            customerInfoValue.CustomerId = customer.Id;
        //            customerInfoValue.EntityInstanceId = entityInstance.Id;

        //            customerInfoValue.PropertyId = property.Id;

        //            customerInfoValue.Value = instanceProperty.value;


        //            await _context.AddAsync(customerInfoValue);
        //        }
        //    }

                    
            


        //    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);


        //    return Json("Success");
        //}

        //public async Task<IActionResult> AddExistingCustomerExistiongInstanceData(List<InstanceProperty>? values, Guid instanceId)
        //{
        //    if (values.IsNullOrEmpty())
        //    {
        //        return BadRequest();
        //    }
        //    if ( instanceId == null)
        //    {
        //        return BadRequest();
        //    }
        //    EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(ei => ei.Id == instanceId);
            

        //    if (entityInstance == null)
        //    {
        //        return BadRequest();
        //    }

        //    Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == entityInstance.CustomerId);
        //    Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityInstance.EntityId);

        //    foreach (InstanceProperty instanceProperty in values)
        //    {
        //        if (!instanceProperty.value.IsNullOrEmpty())
        //        {
        //            if (instanceProperty.propertyId == null)
        //            {
        //                return BadRequest();
        //            }
        //            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == instanceProperty.propertyId);
        //            if (entity == null || property == null)
        //            {
        //                return BadRequest();
        //            }

        //            EntityProperty entityProperty = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.EntityId == entity.Id && ep.PropertyId == property.Id);
        //            if (entityProperty == null)
        //            {
        //                return Json("Invalid property: " + property.Name + " for entity: " + entity.EntityName);
        //            }

        //            CustomerInfoValue customerInfoValue2 = await _context.CustomerInfoValue.FirstOrDefaultAsync(civ => civ.EntityInstanceId == entityInstance.Id && civ.PropertyId == property.Id);
        //            if (customerInfoValue2 != null)
        //            {
        //                if (!String.IsNullOrEmpty(property.ValidationRegex))
        //                {
        //                    Regex regex = new Regex(property.ValidationRegex);

        //                    if (!regex.IsMatch(instanceProperty.value))
        //                    {
        //                        return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
        //                    }
        //                }

        //                if (property.IsEncrypted)
        //                {
        //                    instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
        //                }
        //                else if (property.IsHashed)
        //                {
        //                    instanceProperty.value = Cryptography.Hash(instanceProperty.value);
        //                }

        //                if (property.IsUniqueIdentifier)
        //                {
        //                    CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
        //                    if (customerInfoValue1 != null)
        //                    {
        //                        return Json("Can not insert a duplicate record for a unique identifier");
        //                    }
        //                }
        //                customerInfoValue2.Value = instanceProperty.value;
        //            }
        //            else
        //            {
        //                if (!String.IsNullOrEmpty(property.ValidationRegex))
        //                {
        //                    Regex regex = new Regex(property.ValidationRegex);

        //                    if (!regex.IsMatch(instanceProperty.value))
        //                    {
        //                        return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
        //                    }
        //                }

        //                if (property.IsEncrypted)
        //                {
        //                    instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
        //                }
        //                else if (property.IsHashed)
        //                {
        //                    instanceProperty.value = Cryptography.Hash(instanceProperty.value);
        //                }

        //                if (property.IsUniqueIdentifier)
        //                {
        //                    CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
        //                    if (customerInfoValue1 != null)
        //                    {
        //                        return Json("Can not insert a duplicate record for a unique identifier");
        //                    }
        //                }

        //                CustomerInfoValue customerInfoValue = new CustomerInfoValue();
        //                customerInfoValue.CustomerId = customer.Id;
        //                customerInfoValue.EntityInstanceId = entityInstance.Id;

        //                customerInfoValue.PropertyId = property.Id;

        //                customerInfoValue.Value = instanceProperty.value;


        //                await _context.AddAsync(customerInfoValue);
        //            }


                    
        //        }
        //    }

                    
            


        //    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);


        //    return Json("Success");
        //}

        //public async Task<IActionResult> RemoveData(Guid valueId)
        //{
        //    if (valueId == null)
        //    {
        //        return BadRequest();
        //    }

        //    CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(civ => civ.Id == valueId);
        //    if(customerInfoValue == null)
        //    {
        //        return BadRequest();
        //    }
        //    customerInfoValue.IsDeleted = true;
        //    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
        //    return Json("Success");
        //}

        public async Task<IActionResult> AuditPerformanceTest()
        {
            _logger.LogInformation("DataStructureManagement.AuditPerformanceTest is called");
            for (int x = 0; x < 100; x++)
            {
                for (int i = 0; i < 100000; i++)
                {
                    Audit audit = new Audit();
                    audit.action = "PerformanceTest";
                    audit.dateTime = DateTime.UtcNow.AddDays(-2);
                    audit.tableName = "table " + i;
                    audit.recordId = Guid.NewGuid();
                    audit.parameter = "parameter " + i;
                    audit.fromValue = "from " + i;
                    audit.toValue = "to " + i;
                    audit.performedBy = "Albert";
                    await _context.AddAsync(audit);
                }
                await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                Console.WriteLine(x);
            }
            
            return Ok();
        }

        /// <summary>
        /// Used to update Category tree structure (parent and child)
        /// </summary>
        /// <param name="tree"></param>
        /// <returns></returns>
        public async Task<IActionResult> UpdateCategoryParentId(List<EntityCategoryListItem>? tree)
        {
            _logger.LogInformation("DataStructureManagement.UpdateCategoryParentId is called with {tree}", tree);
            if (tree == null)
            {
                _logger.LogDebug("tree == null");
                List<EntityCategory> entityCategories1 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                List<EntityCategoryListItem> ecl1 = new List<EntityCategoryListItem>();

                if (entityCategories1 != null)
                {
                    _logger.LogDebug("entityCategories1 != null");
                    foreach (EntityCategory category in entityCategories1)
                    {
                        EntityCategoryListItem ecli = new EntityCategoryListItem();
                        ecli.id = category.Id;
                        ecli.categoryName = category.CategoryName;
                        ecli.parentId = category.ParentEntityCategoryId;
                        //if(category.ChildEntityCategories != null)
                        //{
                        //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                        //}

                        ecl1.Add(ecli);
                    }
                }

                return Json(ecl1);
            }

            foreach(EntityCategoryListItem item in tree)
            {
                EntityCategory originalItem = await _context.EntityCategory.SingleOrDefaultAsync(e => e.Id == item.id);
                if(originalItem == null)
                {
                    _logger.LogDebug("originalItem == null");
                    List<EntityCategory> entityCategories2 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                    List<EntityCategoryListItem> ecl2 = new List<EntityCategoryListItem>();

                    if (entityCategories2 != null)
                    {
                        _logger.LogDebug("entityCategories2 != null");
                        foreach (EntityCategory category in entityCategories2)
                        {
                            EntityCategoryListItem ecli = new EntityCategoryListItem();
                            ecli.id = category.Id;
                            ecli.categoryName = category.CategoryName;
                            ecli.parentId = category.ParentEntityCategoryId;
                            //if(category.ChildEntityCategories != null)
                            //{
                            //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                            //}

                            ecl2.Add(ecli);
                        }
                    }

                    return Json(ecl2);
                }
                if (originalItem.ParentEntityCategoryId != item.parentId) originalItem.ParentEntityCategoryId = item.parentId;
                
            }
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            List<EntityCategory> entityCategories = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            if (entityCategories != null)
            {
                _logger.LogDebug("entityCategories != null");
                foreach (EntityCategory category in entityCategories)
                {
                    EntityCategoryListItem ecli = new EntityCategoryListItem();
                    ecli.id = category.Id;
                    ecli.categoryName = category.CategoryName;
                    ecli.parentId = category.ParentEntityCategoryId;
                    //if(category.ChildEntityCategories != null)
                    //{
                    //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                    //}

                    ecl.Add(ecli);
                }
            }

            return Json(ecl);
        }

        public async Task<IActionResult> UpdateCategoryName(Guid categoryId, string? newName)
        {
            _logger.LogInformation("DataStructureManagement.UpdateCategoryName is called with {categoryId} and {newName}", categoryId, newName);
            if (categoryId == null)
            {
                _logger.LogDebug("categoryId == null");
                List<EntityCategory> entityCategories1 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                List<EntityCategoryListItem> ecl1 = new List<EntityCategoryListItem>();

                if (entityCategories1 != null)
                {
                    foreach (EntityCategory category in entityCategories1)
                    {
                        EntityCategoryListItem ecli = new EntityCategoryListItem();
                        ecli.id = category.Id;
                        ecli.categoryName = category.CategoryName;
                        ecli.parentId = category.ParentEntityCategoryId;
                        //if(category.ChildEntityCategories != null)
                        //{
                        //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                        //}

                        ecl1.Add(ecli);
                    }
                }

                return Json(ecl1);
            }

            EntityCategory originalItem = await _context.EntityCategory.SingleOrDefaultAsync(e => e.Id == categoryId);
            if (originalItem == null)
            {
                _logger.LogDebug("originalItem == null");
                List<EntityCategory> entityCategories2 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                List<EntityCategoryListItem> ecl2 = new List<EntityCategoryListItem>();

                if (entityCategories2 != null)
                {
                    foreach (EntityCategory category in entityCategories2)
                    {
                        EntityCategoryListItem ecli = new EntityCategoryListItem();
                        ecli.id = category.Id;
                        ecli.categoryName = category.CategoryName;
                        ecli.parentId = category.ParentEntityCategoryId;
                        //if(category.ChildEntityCategories != null)
                        //{
                        //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                        //}

                        ecl2.Add(ecli);
                    }
                }

                return Json(ecl2);
            }

            _logger.LogDebug("Updating Category Name");
            originalItem.CategoryName = newName;

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
            List<EntityCategory> entityCategories = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            if (entityCategories != null)
            {
                foreach (EntityCategory category in entityCategories)
                {
                    EntityCategoryListItem ecli = new EntityCategoryListItem();
                    ecli.id = category.Id;
                    ecli.categoryName = category.CategoryName;
                    ecli.parentId = category.ParentEntityCategoryId;
                    //if(category.ChildEntityCategories != null)
                    //{
                    //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                    //}

                    ecl.Add(ecli);
                }
            }

            return Json(ecl);
        }

        public async Task<IActionResult> CreateCategory(string? newCategoryName)
        {
            _logger.LogInformation("DataStructureManagement.CreateCategory is called with {newCategoryName}", newCategoryName);
            if (newCategoryName.IsNullOrEmpty())
            {
                _logger.LogDebug("newCategoryName.IsNullOrEmpty()");
                List<EntityCategory> entityCategories1 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                List<EntityCategoryListItem> ecl1 = new List<EntityCategoryListItem>();

                if (entityCategories1 != null)
                {
                    foreach (EntityCategory category in entityCategories1)
                    {
                        EntityCategoryListItem ecli = new EntityCategoryListItem();
                        ecli.id = category.Id;
                        ecli.categoryName = category.CategoryName;
                        ecli.parentId = category.ParentEntityCategoryId;
                        //if(category.ChildEntityCategories != null)
                        //{
                        //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                        //}

                        ecl1.Add(ecli);
                    }
                }

                return Json(ecl1);
            }

            EntityCategory existingCategory = await _context.EntityCategory.SingleOrDefaultAsync(e => e.CategoryName == newCategoryName && e.IsDeleted == false);
            if (existingCategory != null)
            {
                _logger.LogDebug("existingCategory != null");
                List<EntityCategory> entityCategories2 = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
                List<EntityCategoryListItem> ecl2 = new List<EntityCategoryListItem>();

                if (entityCategories2 != null)
                {
                    foreach (EntityCategory category in entityCategories2)
                    {
                        EntityCategoryListItem ecli = new EntityCategoryListItem();
                        ecli.id = category.Id;
                        ecli.categoryName = category.CategoryName;
                        ecli.parentId = category.ParentEntityCategoryId;
                        //if(category.ChildEntityCategories != null)
                        //{
                        //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                        //}

                        ecl2.Add(ecli);
                    }
                }

                return Json(ecl2);
            }

            _logger.LogDebug("Adding new EntityCategory");
            EntityCategory entityCategory = new EntityCategory();
            entityCategory.CategoryName = newCategoryName;
            _context.Add(entityCategory);
            

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            List<EntityCategory> entityCategories = await _context.EntityCategory.Where(c => c.IsDeleted == false).ToListAsync();
            List<EntityCategoryListItem> ecl = new List<EntityCategoryListItem>();

            if (entityCategories != null)
            {
                foreach (EntityCategory category in entityCategories)
                {
                    EntityCategoryListItem ecli = new EntityCategoryListItem();
                    ecli.id = category.Id;
                    ecli.categoryName = category.CategoryName;
                    ecli.parentId = category.ParentEntityCategoryId;
                    //if(category.ChildEntityCategories != null)
                    //{
                    //    if (category.ChildEntityCategories.Count != 0) ecli.hasChildren = true;
                    //}
                    
                    ecl.Add(ecli);
                }
            }

            return Json(ecl);
        }

        public async Task<IActionResult> UpdateEntity(Guid entityId, string? newName, bool isRequired)
        {
            _logger.LogInformation("DataStructureManagement.UpdateEntity is called with {entityId}, {newName} and {isRequired}", entityId, newName, isRequired);
            if (entityId == null)
            {
                _logger.LogDebug("entityId == null");
                List<Entity> entities2 = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
                List<EnitityListItem> cel2 = new List<EnitityListItem>();

                if (entities2 != null)
                {
                    foreach (Entity entity1 in entities2)
                    {
                        EnitityListItem celi = new EnitityListItem();
                        celi.id = entity1.Id;
                        celi.entityName = entity1.EntityName;
                        celi.isRequired = entity1.IsRequired;
                        celi.entityCategoryId = entity1.EntityCategoryId;
                        cel2.Add(celi);

                    }
                }

                return Json(cel2);
            }

            Entity originalItem = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            if (originalItem == null)
            {
                _logger.LogDebug("originalItem == null");
                List<Entity> entities1 = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
                List<EnitityListItem> cel1 = new List<EnitityListItem>();

                if (entities1 != null)
                {
                    foreach (Entity entity1 in entities1)
                    {
                        EnitityListItem celi = new EnitityListItem();
                        celi.id = entity1.Id;
                        celi.entityName = entity1.EntityName;
                        celi.isRequired = entity1.IsRequired;
                        celi.entityCategoryId = entity1.EntityCategoryId;
                        cel1.Add(celi);

                    }
                }

                return Json(cel1);
            }

            _logger.LogDebug("Updating Entity");
            originalItem.EntityName = newName;
            originalItem.IsRequired = isRequired;

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            _logger.LogDebug("Preparing updated list of entities");
            List<Entity> entities = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
            List<EnitityListItem> cel = new List<EnitityListItem>();

            if (entities != null)
            {
                foreach (Entity entity1 in entities)
                {
                    EnitityListItem celi = new EnitityListItem();
                    celi.id = entity1.Id;
                    celi.entityName = entity1.EntityName;
                    celi.isRequired = entity1.IsRequired;
                    celi.entityCategoryId = entity1.EntityCategoryId;
                    cel.Add(celi);

                }
            }

            return Json(cel);
        }

        public async Task<IActionResult> CreateEntity(string? newEntityName, Guid entityCategoryId)
        {
            _logger.LogInformation("DataStructureManagement.CreateEntity is called with {newEntityName}, {entityCategoryId}", newEntityName, entityCategoryId);
            if (newEntityName.IsNullOrEmpty() || entityCategoryId == null)
            {
                _logger.LogDebug("newEntityName.IsNullOrEmpty() || entityCategoryId == null");
                List<Entity> entities1 = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
                List<EnitityListItem> cel1 = new List<EnitityListItem>();

                if (entities1 != null)
                {
                    foreach (Entity entity1 in entities1)
                    {
                        EnitityListItem celi = new EnitityListItem();
                        celi.id = entity1.Id;
                        celi.entityName = entity1.EntityName;
                        celi.isRequired = entity1.IsRequired;
                        celi.entityCategoryId = entity1.EntityCategoryId;
                        cel1.Add(celi);

                    }
                }

                return Json(cel1);
            }

            EntityCategory existingCategory = await _context.EntityCategory.SingleOrDefaultAsync(e => e.Id == entityCategoryId && e.IsDeleted == false);
            if (existingCategory == null)
            {
                _logger.LogDebug("existingCategory == null");
                List<Entity> entities2 = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
                List<EnitityListItem> cel2 = new List<EnitityListItem>();

                if (entities2 != null)
                {
                    foreach (Entity entity1 in entities2)
                    {
                        EnitityListItem celi = new EnitityListItem();
                        celi.id = entity1.Id;
                        celi.entityName = entity1.EntityName;
                        celi.isRequired = entity1.IsRequired;
                        celi.entityCategoryId = entity1.EntityCategoryId;
                        cel2.Add(celi);

                    }
                }

                return Json(cel2);
            }

            Entity existingEntity = await _context.Entity.SingleOrDefaultAsync(e => e.EntityName == newEntityName && e.IsDeleted == false && e.EntityCategoryId == entityCategoryId);
            if (existingEntity != null)
            {
                _logger.LogDebug("existingEntity != null");
                List<Entity> entities3 = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
                List<EnitityListItem> cel3 = new List<EnitityListItem>();

                if (entities3 != null)
                {
                    foreach (Entity entity1 in entities3)
                    {
                        EnitityListItem celi = new EnitityListItem();
                        celi.id = entity1.Id;
                        celi.entityName = entity1.EntityName;
                        celi.isRequired = entity1.IsRequired;
                        celi.entityCategoryId = entity1.EntityCategoryId;
                        cel3.Add(celi);

                    }
                }

                return Json(cel3);
            }

            _logger.LogDebug("Adding new Entity");
            Entity entity = new Entity();
            entity.EntityName = newEntityName;
            entity.EntityCategoryId = existingCategory.Id;
            _context.Add(entity);


            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            _logger.LogDebug("Preparing updated list of entities");
            List<Entity> entities = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
            List<EnitityListItem> cel = new List<EnitityListItem>();

            if (entities != null)
            {
                foreach (Entity entity1 in entities)
                {
                    EnitityListItem celi = new EnitityListItem();
                    celi.id = entity1.Id;
                    celi.entityName = entity1.EntityName;
                    celi.isRequired = entity1.IsRequired;
                    celi.entityCategoryId = entity1.EntityCategoryId;
                    cel.Add(celi);

                }
            }

            return Json(cel);
        }

        public async Task<IActionResult> UpdateProperty(Guid propertyId, string? propertyName, string? validationRegex, string? validationHint, bool isEncrypted, bool isHashed, bool isUniqueIdentifier, bool isRequired)
        {
            _logger.LogInformation("DataStructureManagement.UpdateProperty is called with {propertyId}, {propertyName}, {validationRegex}, {validationHint}, {isEncrypted}, {isHashed}, {isUniqueIdentifier}, {isRequired}", propertyId, propertyName, validationRegex, validationHint, isEncrypted, isUniqueIdentifier, isRequired);
            if (propertyId == null || propertyName.IsNullOrEmpty() || (!LC.Check().isCustomerRepository && !isUniqueIdentifier))
            {
                _logger.LogDebug("propertyId == null || propertyName.IsNullOrEmpty() || (!LC.Check().isCustomerRepository && !isUniqueIdentifier)");
                List<Property> properties2 = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
                List<PropertiesListItem> pl2 = new List<PropertiesListItem>();

                if (properties2 != null)
                {
                    foreach (Property property1 in properties2)
                    {
                        PropertiesListItem pli = new PropertiesListItem();
                        pli.id = property1.Id;
                        pli.propertyName = property1.Name;
                        pli.validationRegex = property1.ValidationRegex;
                        pli.validationHint = property1.ValidationHint;
                        pli.isEncrypted = property1.IsEncrypted.Value;
                        pli.isHashed = property1.IsHashed.Value;
                        pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                        pli.isRequired = property1.IsRequired.Value;
                        List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                        if (entityProperties1 != null)
                        {
                            foreach (EntityProperty entityProperty in entityProperties1)
                            {
                                pli.entityId = entityProperty.EntityId.Value;
                                pl2.Add(pli);
                            }
                        }
                    }
                }



                return Json(pl2);
            }

            Property originalItem = await _context.Property.SingleOrDefaultAsync(e => e.Id == propertyId);
            if (originalItem == null)
            {
                _logger.LogDebug("originalItem == null");
                List<Property> properties3 = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
                List<PropertiesListItem> pl3 = new List<PropertiesListItem>();

                if (properties3 != null)
                {
                    foreach (Property property1 in properties3)
                    {
                        PropertiesListItem pli = new PropertiesListItem();
                        pli.id = property1.Id;
                        pli.propertyName = property1.Name;
                        pli.validationRegex = property1.ValidationRegex;
                        pli.validationHint = property1.ValidationHint;
                        pli.isEncrypted = property1.IsEncrypted.Value;
                        pli.isHashed = property1.IsHashed.Value;
                        pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                        pli.isRequired = property1.IsRequired.Value;
                        List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                        if (entityProperties1 != null)
                        {
                            foreach (EntityProperty entityProperty in entityProperties1)
                            {
                                pli.entityId = entityProperty.EntityId.Value;
                                pl3.Add(pli);
                            }
                        }
                    }
                }



                return Json(pl3);
            }

            _logger.LogDebug("Updating property");

            originalItem.Name = propertyName;
            originalItem.ValidationRegex = validationRegex;
            originalItem.ValidationHint = validationHint;
            originalItem.IsEncrypted = isEncrypted;
            originalItem.IsHashed = isHashed;
            originalItem.IsUniqueIdentifier = isUniqueIdentifier;
            originalItem.IsRequired = isRequired;

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            _logger.LogDebug("Returning updated list of properties");
            List<Property> properties = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
            List<PropertiesListItem> pl = new List<PropertiesListItem>();

            if (properties != null)
            {
                foreach (Property property1 in properties)
                {
                    PropertiesListItem pli = new PropertiesListItem();
                    pli.id = property1.Id;
                    pli.propertyName = property1.Name;
                    pli.validationRegex = property1.ValidationRegex;
                    pli.validationHint = property1.ValidationHint;
                    pli.isEncrypted = property1.IsEncrypted.Value;
                    pli.isHashed = property1.IsHashed.Value;
                    pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                    pli.isRequired = property1.IsRequired.Value;
                    List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                    if (entityProperties1 != null)
                    {
                        foreach (EntityProperty entityProperty in entityProperties1)
                        {
                            pli.entityId = entityProperty.EntityId.Value;
                            pl.Add(pli);
                        }
                    }
                }
            }



            return Json(pl);
        }

        public async Task<IActionResult> CreateProperty(string? propertyName, string? validationRegex, string? validationHint, bool isEncrypted, bool isHashed, bool isUniqueIdentifier, Guid entityId)
        {
            _logger.LogInformation("DataStructureManagement.CreateProperty is called with {propertyName}, {validationRegex}, {validationHint}, {isEncrypted}, {isHashed}, {isUniqueIdentifier}, {entityId}", propertyName, validationRegex, validationHint, isEncrypted, isUniqueIdentifier, entityId);
            if (propertyName.IsNullOrEmpty() || entityId == null)
            {
                _logger.LogDebug("propertyName.IsNullOrEmpty() || entityId == null");
                List<Property> properties1 = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
                List<PropertiesListItem> pl1 = new List<PropertiesListItem>();

                if (properties1 != null)
                {
                    foreach (Property property1 in properties1)
                    {
                        PropertiesListItem pli = new PropertiesListItem();
                        pli.id = property1.Id;
                        pli.propertyName = property1.Name;
                        pli.validationRegex = property1.ValidationRegex;
                        pli.validationHint = property1.ValidationHint;
                        pli.isEncrypted = property1.IsEncrypted.Value;
                        pli.isHashed = property1.IsHashed.Value;
                        pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                        pli.isRequired = property1.IsRequired.Value;
                        List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                        if (entityProperties1 != null)
                        {
                            foreach (EntityProperty entityProperty in entityProperties1)
                            {
                                pli.entityId = entityProperty.EntityId.Value;
                                pl1.Add(pli);
                            }
                        }
                    }
                }



                return Json(pl1);
            }

            if (!LC.Check().isCustomerRepository && !isUniqueIdentifier)
            {
                _logger.LogDebug("!LC.Check().isCustomerRepository && !isUniqueIdentifier");
                List<Property> properties2 = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
                List<PropertiesListItem> pl2 = new List<PropertiesListItem>();

                if (properties2 != null)
                {
                    foreach (Property property1 in properties2)
                    {
                        PropertiesListItem pli = new PropertiesListItem();
                        pli.id = property1.Id;
                        pli.propertyName = property1.Name;
                        pli.validationRegex = property1.ValidationRegex;
                        pli.validationHint = property1.ValidationHint;
                        pli.isEncrypted = property1.IsEncrypted.Value;
                        pli.isHashed = property1.IsHashed.Value;
                        pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                        pli.isRequired = property1.IsRequired.Value;
                        List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                        if (entityProperties1 != null)
                        {
                            foreach (EntityProperty entityProperty in entityProperties1)
                            {
                                pli.entityId = entityProperty.EntityId.Value;
                                pl2.Add(pli);
                            }
                        }
                    }
                }



                return Json(pl2);
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            if (entity == null) 
            {
                _logger.LogError("entity == null");
                return BadRequest(); 
            }

            List<EntityProperty> entityProperties = await _context.EntityProperty.Where(ep => ep.EntityId == entityId).ToListAsync();
            foreach (EntityProperty entityProperty in entityProperties)
            {
                Property property1 = await _context.Property.SingleOrDefaultAsync(p => p.Id == entityProperty.PropertyId);
                if(property1 != null && property1.Name == propertyName)
                {
                    List<Property> properties3 = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
                    List<PropertiesListItem> pl3 = new List<PropertiesListItem>();

                    if (properties3 != null)
                    {
                        foreach (Property property3 in properties3)
                        {
                            PropertiesListItem pli = new PropertiesListItem();
                            pli.id = property3.Id;
                            pli.propertyName = property3.Name;
                            pli.validationRegex = property3.ValidationRegex;
                            pli.validationHint = property3.ValidationHint;
                            pli.isEncrypted = property3.IsEncrypted.Value;
                            pli.isHashed = property3.IsHashed.Value;
                            pli.isUniqueIdentifier = property3.IsUniqueIdentifier.Value;
                            pli.isRequired = property3.IsRequired.Value;
                            List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property3.Id).ToListAsync();
                            if (entityProperties1 != null)
                            {
                                foreach (EntityProperty entityProperty3 in entityProperties1)
                                {
                                    pli.entityId = entityProperty3.EntityId.Value;
                                    pl3.Add(pli);
                                }
                            }
                        }
                    }



                    return Json(pl3);
                }
                
            }

            _logger.LogDebug("Adding property");

            Property property = new Property();
            property.Name = propertyName;
            property.ValidationRegex = validationRegex;
            property.ValidationHint = validationHint;
            property.IsEncrypted = isEncrypted;
            property.IsHashed = isHashed;
            property.IsUniqueIdentifier = isUniqueIdentifier;

            _context.Add(property);

            _logger.LogDebug("Adding EntityProperty");
            EntityProperty entityProperty1 = new EntityProperty();
            entityProperty1.EntityId = entity.Id;
            entityProperty1.PropertyId = property.Id;

            _context.Add(entityProperty1);

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);


            _logger.LogDebug("Returning updated list of properties");
            List<Property> properties = await _context.Property.Where(c => c.IsDeleted == false).ToListAsync();
            List<PropertiesListItem> pl = new List<PropertiesListItem>();

            if (properties != null)
            {
                foreach (Property property1 in properties)
                {
                    PropertiesListItem pli = new PropertiesListItem();
                    pli.id = property1.Id;
                    pli.propertyName = property1.Name;
                    pli.validationRegex = property1.ValidationRegex;
                    pli.validationHint = property1.ValidationHint;
                    pli.isEncrypted = property1.IsEncrypted.Value;
                    pli.isHashed = property1.IsHashed.Value;
                    pli.isUniqueIdentifier = property1.IsUniqueIdentifier.Value;
                    pli.isRequired = property1.IsRequired.Value;
                    List<EntityProperty> entityProperties1 = await _context.EntityProperty.Where(ep => ep.PropertyId == property1.Id).ToListAsync();
                    if (entityProperties1 != null)
                    {
                        foreach (EntityProperty entityProperty in entityProperties1)
                        {
                            pli.entityId = entityProperty.EntityId.Value;
                            pl.Add(pli);
                        }
                    }
                }
            }

            

            return Json(pl);
        }

        public async Task<IActionResult> GetEntityInstanceProperties(Guid id)
        {
            _logger.LogInformation("DataStructureManagement.GetEntityInstanceProperties is called with {id}", id);
            if (id == null)
            {
                _logger.LogError("id == null");
                List<InstanceProperty> instanceProperties = new List<InstanceProperty>();
                return Json(instanceProperties);
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == id);
            if(entity == null)
            {
                _logger.LogError("entity == null");
                List<InstanceProperty> instanceProperties = new List<InstanceProperty>();
                return Json(instanceProperties);
            }

            List<EntityProperty> entityProperties = await _context.EntityProperty.Where(e => e.EntityId == id).ToListAsync();
            List<InstanceProperty> instanceProperties1 = new List<InstanceProperty>();

            foreach(EntityProperty entityProperty in entityProperties)
            {
                InstanceProperty instanceProperty = new InstanceProperty();
                instanceProperty.propertyId = entityProperty.PropertyId.Value;
                instanceProperty.propertyName = entityProperty.Property.Name;
                instanceProperty.value = "";
                instanceProperties1.Add(instanceProperty);
            }

            return Json(instanceProperties1);
        }

        public async Task<IActionResult> ValidateInput(Guid propertyId, string? value)
        {
            _logger.LogInformation("DataStructureManagement.ValidateInput is called with {propertyId} and {value}", propertyId, value);
            if (propertyId == null)
            {
                _logger.LogError("propertyId == null");
                return Json("Invalid propery");
            }
            if(!value.IsNullOrEmpty())
            {
                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == propertyId);
                if (property == null)
                {
                    _logger.LogError("Invalid propery");
                    return Json("Invalid propery");
                }
                if (!String.IsNullOrEmpty(property.ValidationRegex))
                {
                    Regex regex = new Regex(property.ValidationRegex);

                    if (!regex.IsMatch(value))
                    {
                        _logger.LogError("In value for property {property.Name}. Validation Hint: {property.validationHint}", property.Name, property.ValidationHint);
                        return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
                    }
                }

                return Json("Success");
            }
            return Json("Success");
        }

        public async Task<IActionResult> GetEntities(Guid categoryId)
        {
            _logger.LogInformation("DataStructureManagement.GetEntities is called with {categoryId}", categoryId);
            List<EntityListItem> entities = new List<EntityListItem>();
            if (categoryId == null) return Json(entities);
            List<Entity> entities2 = await _context.Entity.Where(e => e.EntityCategoryId == categoryId).ToListAsync();
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



    }

    
}
