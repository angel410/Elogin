using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class CustomerDataManagementController : Controller
    {
        private readonly DatabaseContext _context;
        private LicenseCheck LC;
        private readonly eLoginSettings _eLoginSettings;
        private readonly ILogger<CustomerDataManagementController> _logger;

        public CustomerDataManagementController(DatabaseContext context, IOptions<eLoginSettings> eLoginSettings, LicenseCheck licenseCheck, ILogger<CustomerDataManagementController> logger)
        {
            _context = context;
            LC = licenseCheck;
            _eLoginSettings = eLoginSettings.Value;
            _logger = logger;
        }

        public class EnitityListItem
        {
            public Guid entityId { get; set; }
            public Guid categoryId { get; set; }
            public Guid? parentCategoryId { get; set; }
            public string categoryName { get; set; }
            public string? entityName { get; set; }

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

        public class Instance
        {
            public Guid id { get; set; }
            public string instanceName { get; set; }
            public Guid entityId { get; set; }
            public List<InstanceProperty> properties { get; set; }
            public bool isValid { get; set; }
        }

        public static List<Instance> newInstances { get; set; }
        public static List<Instance> newInstancesEC { get; set; } //Existing Customer

        public class InstanceProperty
        {
            public Guid propertyId { get; set; }
            public string propertyName { get; set; }
            public string value { get; set; }
           
        }

        public class CustomerRecord
        {
            public Guid id { get; set; }
            public Guid? parentId { get; set; }
            public string classifiedName { get; set; } = "";
            public string property { get; set; } = "";
            public string value { get; set; } = "";
            public Guid? instanceId { get; set; }
            public Guid CustomerId { get; set; }
            public Guid valueId { get; set; }
            public Guid entityId { get; set; }
            public string? entityName { get; set; }
            public Guid categoryId { get; set; }
            public Guid parentCategoryId { get; set; }
            public string? primaryPropertyValue { get; set; }

            
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

        public class VirtualEntityCategory
        {
            public Guid id { get; set; }
            public Guid parentId { get; set; }
            public string categoryName { get; set; } = "";
            public Guid originalId { get; set; }
            public Guid? originalParentId { get; set; }
        }

        public class VirtualEntity
        {
            public Guid id { get; set; }
            public Guid categoryId { get; set; }
            public string entityName { get; set; } = "";
            public Guid originalId { get; set; }
            public Guid originalCategoryId { get; set; }
        }

        public async Task<List<PropertiesListItem>> PropertiesList()
        {
            _logger.LogInformation("CustomerDataManagement.PropertiesList is called");
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

        
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("CustomerDataManagement.Index is called");

            _logger.LogDebug("Initializing newInstances (server-side)");
            newInstances = new List<Instance>();
            List<Entity> entities = await _context.Entity.Where(c => c.IsDeleted == false && c.IsRequired == true).ToListAsync();
            List<EnitityListItem> cel = new List<EnitityListItem>();

            _logger.LogDebug("Preparing entitiesDataSource");
            if (entities != null)
            {
                foreach (Entity entity in entities)
                {
                    EnitityListItem celi = new EnitityListItem();
                    celi.entityId = entity.Id;
                    celi.entityName = entity.EntityName;
                    celi.categoryName = "";
                    celi.categoryId = entity.Id;
                    celi.parentCategoryId = entity.EntityCategoryId;
                    cel.Add(celi);

                    int i = 0;
                    List<EntityCategory> ParentCategories = new List<EntityCategory>();
                    ParentCategories.Add(entity.EntityCategory);
                    while(ParentCategories[i] != null)
                    {
                        EnitityListItem celiW = new EnitityListItem();
                        celiW.categoryName = ParentCategories[i].CategoryName;
                        celiW.categoryId = ParentCategories[i].Id;
                        celiW.parentCategoryId = ParentCategories[i].ParentEntityCategoryId;
                        if (!cel.Any(c => c.categoryId == celiW.categoryId)) cel.Add(celiW);
                        ParentCategories.Add(ParentCategories[i].ParentEntityCategory);
                        i++;
                    }

                    Instance instance = new Instance();
                    instance.entityId = entity.Id;
                    instance.instanceName = "";
                    instance.properties = new List<InstanceProperty>();
                    List<EntityProperty> entityProperties = await _context.EntityProperty.Where(e => e.EntityId == entity.Id).ToListAsync();

                    foreach (EntityProperty entityProperty in entityProperties)
                    {
                        InstanceProperty instanceProperty = new InstanceProperty();
                        instanceProperty.propertyId = entityProperty.PropertyId.Value;
                        instanceProperty.propertyName = entityProperty.Property.Name;
                        instanceProperty.value = "";
                        instance.properties.Add(instanceProperty);
                    }
                    newInstances.Add(instance);
                }
            }

            ViewBag.entitiesDataSource = cel;

            _logger.LogDebug("Initializing newInstancesEC (server-side)");
            newInstancesEC = new List<Instance>();
            List<Entity> entitiesEC = await _context.Entity.Where(c => c.IsDeleted == false).ToListAsync();
            foreach(Entity e in entitiesEC)
            {
                Instance instance = new Instance();
                instance.entityId = e.Id;
                instance.instanceName = "";
                instance.properties = new List<InstanceProperty>();
                List<EntityProperty> eps = await _context.EntityProperty.Where(ep => ep.EntityId == e.Id).ToListAsync();
                foreach (EntityProperty ep in eps)
                {
                    InstanceProperty ip = new InstanceProperty();
                    ip.propertyId = ep.PropertyId.Value;
                    ip.propertyName = ep.Property.Name;
                    ip.value = "";
                    instance.properties.Add(ip);
                }
                newInstancesEC.Add(instance);
            }

            _logger.LogDebug("Preparing categoriesDataSource");
            List<EntityCategoryListItem> categories = new List<EntityCategoryListItem>();
            List<EntityCategory> entityCategories = await _context.EntityCategory.ToListAsync();
            foreach(EntityCategory ec in entityCategories)
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

        public async Task<IActionResult> GetCustomerInstance (Guid instanceId)
        {
            _logger.LogInformation("CustomerDataManagement.GetCustomerInstance is called with {instanceId}", instanceId);
            Instance instance = new Instance();
            if (instanceId == null) return Json(instance);
            EntityInstance ei = await _context.EntityInstance.SingleOrDefaultAsync(ei => ei.Id == instanceId);
            if (ei == null) return Json(instance);
            instance.id = instanceId;
            instance.entityId = ei.EntityId;
            instance.instanceName = ei.EntityInstanceName;
            instance.isValid = true;
            instance.properties = new List<InstanceProperty>();
            _logger.LogDebug("Preparing a list of instance properties and their values");
            List<EntityProperty> eps = await _context.EntityProperty.Where(ep => ep.EntityId == ei.EntityId).ToListAsync();
            foreach (EntityProperty ep in eps)
            {
                InstanceProperty ip = new InstanceProperty();
                ip.propertyId = ep.PropertyId.Value;
                ip.propertyName = ep.Property.Name;
                CustomerInfoValue civ = await _context.CustomerInfoValue.FirstOrDefaultAsync(c => c.CustomerId == ei.CustomerId && c.EntityInstanceId == ei.Id && c.PropertyId == ep.PropertyId);
                if (civ != null)
                {
                    string Value;
                    if (civ.Property.IsEncrypted.Value)
                    {
                        Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, civ.Value, Cryptography.Operation.Decrypt);
                        if (Value.Length > 7)
                        {
                            _logger.LogDebug("Masking all string except the last four characters");
                            Regex regex = new Regex("[^ -](?=.{4})");
                            Value = regex.Replace(Value, "X");
                        }
                        else
                        {
                            _logger.LogDebug("Masking all string");
                            Regex regex = new Regex(".");
                            Value = regex.Replace(Value, "X");
                        }

                    }
                    else Value = civ.Value;
                    ip.value = Value;
                }
                else
                {
                    ip.value = "";
                }
                
                instance.properties.Add(ip);
            }
            return Json(instance);
        }


        public async Task<IActionResult> GetEntities(Guid categoryId)
        {
            _logger.LogInformation("CustomerDataManagement.GetEntities is called with {categoryId}", categoryId);
            List<EntityListItem> entities = new List<EntityListItem>();
            if (categoryId == null) return Json(entities);
            List<Entity> entities2 = await _context.Entity.Where(e => e.EntityCategoryId == categoryId).ToListAsync();
            if (entities2 == null) return Json(entities);
            foreach(Entity e in entities2)
            {
                EntityListItem eli = new EntityListItem();
                eli.id = e.Id;
                eli.entityName = e.EntityName;
                entities.Add(eli);
            }
            return Json(entities);
        }
        



        public async Task<IActionResult> SearchCustomer(string? searchValue)
        {
            _logger.LogInformation("CustomerDataManagement.SearchCustomer is called with {searchValue}", searchValue);
            // Checking if the search value is null or empty string
            if (searchValue.IsNullOrEmpty())
            {
                _logger.LogError("searchValue IsNullOrEmpty");
                return NotFound();
            }

            _logger.LogDebug("Searching into the database for the value in plain text");
            List<CustomerInfoValue> customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.Value.Contains(searchValue)).ToListAsync();

            if (searchValue != null)
            {
                _logger.LogDebug("Encrypting the search text");
                string encryptedSearchValue = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(searchValue)), Cryptography.Operation.Encrypt);
                _logger.LogDebug("Searching for the encrypted text");
                List<CustomerInfoValue> customerEncryptedInfoValues = await _context.CustomerInfoValue.Where(c => c.Value.Equals(encryptedSearchValue)).ToListAsync();

                _logger.LogDebug("Adding encrypted matching values to the search result");
                if (customerEncryptedInfoValues != null)
                {
                    foreach (CustomerInfoValue customerEncryptedInfoValue in customerEncryptedInfoValues)
                    {
                        customerInfoValues1.Add(customerEncryptedInfoValue);
                    }
                }

            }
            _logger.LogDebug("Creating a new list of matching customers");
            List <Customer> matchingCustomers = new List<Customer>();
            _logger.LogDebug("Looping over all matched values and adding the customer to the list");
            foreach (CustomerInfoValue civ in customerInfoValues1)
            {
                Customer c = new Customer();
                c = civ.Customer;
                matchingCustomers.Add(c);
            }
            _logger.LogDebug("filtering the list to distinct customers");
            matchingCustomers = matchingCustomers.Distinct().ToList();

            _logger.LogDebug("Creating a list of records to be previewed to the user");
            var customerRecords = new List<CustomerRecord>();

            foreach (Customer Customer in matchingCustomers)
            {
                _logger.LogDebug("Adding Customer");
                CustomerRecord cr = new CustomerRecord();
                cr.id = Customer.Id;
                cr.parentId = null;
                _logger.LogDebug("Obtaining the primary property from System Settings");
                SystemSetting systemSetting = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "Customer Primary Property");
               // string systemSetting_ = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(systemSetting.Value)), Cryptography.Operation.Decrypt);

                var primaryProperty = Guid.Parse(systemSetting.Value);
                _logger.LogDebug("Obtaining the corresponding customer value of the primary property");
                CustomerInfoValue civp = await _context.CustomerInfoValue.FirstOrDefaultAsync(civp => civp.CustomerId == Customer.Id && civp.PropertyId == primaryProperty);
                if (civp != null)
                {
                    string Value;
                    if (civp.Property.IsEncrypted.Value)
                    {
                        Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, civp.Value, Cryptography.Operation.Decrypt);
                        if (Value.Length > 7)
                        {
                            Regex regex = new Regex("[^ -](?=.{4})");
                            Value = regex.Replace(Value, "X");
                        }
                        else
                        {
                            Regex regex = new Regex(".");
                            Value = regex.Replace(Value, "X");
                        }

                    }
                    else Value = civp.Value;
                    cr.classifiedName = civp.Property.Name + ": " + Value + " - CustomerId: " + Customer.Id;
                    cr.primaryPropertyValue = Value;
                }
                else
                {
                    _logger.LogDebug("Customer primary property value not found");
                    cr.classifiedName = "CustomerId: " + Customer.Id;
                }
                cr.instanceId = null;
                cr.CustomerId = Customer.Id;
                _logger.LogDebug("Adding customer record");
                customerRecords.Add(cr);

                _logger.LogDebug("Creating a list of virtual entity categories to store virtualCategories");
                List <VirtualEntityCategory> VirtualCategories = new List<VirtualEntityCategory>();
                _logger.LogDebug("Creating a list of virtual entities to store entities");
                List <VirtualEntity> entities = new List<VirtualEntity>();

                _logger.LogDebug("Looping over customer entity instances");
                foreach (var EntityInstance in Customer.EntitieInstances)
                {
                    if (EntityInstance.Entity != null && EntityInstance.Entity.EntityCategory != null)
                    {
                        //Adding Categories
                        //Before adding the entity's category, we look for it in the virtualCategories list, to avoid placing duplicate categories
                        VirtualEntityCategory vc = VirtualCategories.SingleOrDefault(pc => pc.originalId == EntityInstance.Entity.EntityCategoryId);
                        if(vc == null)
                        {
                            //If not found, we add the entity's category to the list
                            vc = new VirtualEntityCategory();
                            //Generating a virtual Guid for the category record
                            vc.id = Guid.NewGuid();
                            vc.categoryName = EntityInstance.Entity.EntityCategory.CategoryName;
                            //Storing it's original id
                            vc.originalId = EntityInstance.Entity.EntityCategoryId;
                            //Checking if the category has a parent
                            if (EntityInstance.Entity.EntityCategory.ParentEntityCategoryId != null)
                            {
                                //Checking if the parent already exists in the virtual entity categories list
                                VirtualEntityCategory virtualEntityCategory = VirtualCategories.SingleOrDefault(pc => pc.originalId == EntityInstance.Entity.EntityCategory.ParentEntityCategoryId);
                                if(virtualEntityCategory == null)
                                {
                                    //If the category does not exist, set the parent id to a new Guid
                                    vc.parentId = Guid.NewGuid();
                                }
                                else
                                {
                                    // If the category exists, set the parent id as the id of the virtual category
                                    vc.parentId = virtualEntityCategory.id;
                                }
                                // Setting the original parent Id
                                vc.originalParentId = EntityInstance.Entity.EntityCategory.ParentEntityCategoryId;
                            }
                            else
                            {
                                //If category does not have a parent, set it's parent as the customer id
                                vc.parentId = Customer.Id;
                            }
                            // adding the category to the virtual category list
                            VirtualCategories.Add(vc);

                            int i = (VirtualCategories.Count()) - 1;

                            // Loop to add all parents of a category to the virtual categories list
                            while (VirtualCategories[i] != null)
                            {
                                // Adding a new customer record containing the category's info
                                CustomerRecord crW = new CustomerRecord();
                                crW.id = VirtualCategories[i].id;
                                crW.parentId = VirtualCategories[i].parentId;
                                crW.classifiedName = VirtualCategories[i].categoryName;
                                crW.instanceId = null;
                                crW.CustomerId = EntityInstance.CustomerId;
                                crW.primaryPropertyValue = cr.primaryPropertyValue;
                                crW.categoryId = VirtualCategories[i].originalId;
                                // Avoiding inserting duplicate records before adding customer record
                                if (!customerRecords.Any(c => c.id == crW.id)) customerRecords.Add(crW);
                                // Checking if the category has a parent
                                EntityCategory parentEntityCategory = await _context.EntityCategory.SingleOrDefaultAsync(ec => ec.Id == VirtualCategories[i].originalParentId);
                                if (parentEntityCategory != null)
                                {
                                    //Checking if the parent already exists in the virtual categories list
                                    VirtualEntityCategory pvc = VirtualCategories.SingleOrDefault(pc => pc.originalId == parentEntityCategory.Id);
                                    if (pvc == null)
                                    {
                                        pvc = new VirtualEntityCategory();
                                        pvc.id = (Guid)VirtualCategories[i].parentId;
                                        pvc.categoryName = parentEntityCategory.CategoryName;
                                        pvc.originalId = (Guid)parentEntityCategory.Id;
                                        if (parentEntityCategory.ParentEntityCategoryId != null)
                                        {
                                            VirtualEntityCategory virtualEntityCategory = VirtualCategories.SingleOrDefault(pc => pc.originalId == parentEntityCategory.ParentEntityCategoryId);
                                            if (virtualEntityCategory == null)
                                            {
                                                //If the category does not exist, set the parent id to a new Guid
                                                pvc.parentId = Guid.NewGuid();
                                            }
                                            else
                                            {
                                                // If the category exists, set the parent id as the id of the virtual category
                                                pvc.parentId = virtualEntityCategory.id;
                                            }
                                            // Setting the original parent Id
                                            pvc.originalParentId = parentEntityCategory.ParentEntityCategoryId;
                                        }
                                        else
                                        {
                                            // If category does not have a parent, set its parent as customer id
                                            pvc.parentId = Customer.Id;
                                        }
                                        VirtualCategories.Add(pvc);
                                        i++;
                                    }
                                    else
                                    {
                                        // If parent category already exists, consequently all its parent hirarichy already exists and hence we break here.
                                        break;
                                    }


                                }
                                else
                                {
                                    // No more categories to add
                                    break;
                                }

                                
                            }
                        }
                        else
                        {
                            //Category exists and hence and the new customer record with reference to it.
                            CustomerRecord crW = new CustomerRecord();
                            crW.id = vc.id;
                            crW.parentId = vc.parentId;
                            crW.classifiedName = vc.categoryName;
                            crW.instanceId = null;
                            crW.CustomerId = EntityInstance.CustomerId;
                            crW.primaryPropertyValue = cr.primaryPropertyValue;
                            crW.categoryId = EntityInstance.Entity.EntityCategoryId;
                            if (!customerRecords.Any(c => c.id == crW.id)) customerRecords.Add(crW);
                        }



                        //Adding Entity
                        VirtualEntity ve = entities.SingleOrDefault(e => e.originalId == EntityInstance.EntityId);
                        if(ve == null)
                        {
                            ve = new VirtualEntity();
                            ve.id = Guid.NewGuid();
                            ve.entityName = EntityInstance.Entity.EntityName;
                            ve.originalId = EntityInstance.EntityId;
                            ve.originalCategoryId = EntityInstance.Entity.EntityCategoryId;
                            ve.categoryId = VirtualCategories.SingleOrDefault(pc => pc.originalId == ve.originalCategoryId).id;
                            entities.Add(ve);
                        }
                        
                        CustomerRecord cr2 = new CustomerRecord();
                        cr2.id = ve.id;
                        cr2.parentId = ve.categoryId;
                        cr2.classifiedName = ve.entityName;
                        cr2.instanceId = null;
                        cr2.entityId = ve.originalId;
                        cr2.categoryId = ve.originalCategoryId;
                        cr2.entityName = ve.entityName;
                        cr2.CustomerId = EntityInstance.CustomerId;
                        cr2.primaryPropertyValue = cr.primaryPropertyValue;
                        if (!customerRecords.Any(c => c.id == cr2.id)) customerRecords.Add(cr2);


                        //Adding Instance
                        CustomerRecord cr1 = new CustomerRecord();
                        cr1.id = EntityInstance.Id;
                        cr1.parentId = ve.id;
                        cr1.classifiedName = EntityInstance.EntityInstanceName;
                        cr1.instanceId = EntityInstance.Id;
                        cr1.entityId = EntityInstance.EntityId;
                        cr1.categoryId = EntityInstance.Entity.EntityCategoryId;
                        cr1.entityName = EntityInstance.Entity.EntityName;
                        cr1.CustomerId = EntityInstance.CustomerId;
                        cr1.primaryPropertyValue = cr.primaryPropertyValue;
                        customerRecords.Add(cr1);

                        
                        
                    }

                    foreach (var InfoValue in EntityInstance.CustomerInfoValues)
                    {
                        string Value;
                        if (InfoValue.Property.IsEncrypted.Value)
                        {
                            Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, InfoValue.Value, Cryptography.Operation.Decrypt);
                            if (Value.Length > 7)
                            {
                                Regex regex = new Regex("[^ -](?=.{4})");
                                Value = regex.Replace(Value, "X");
                            }
                            else
                            {
                                Regex regex = new Regex(".");
                                Value = regex.Replace(Value, "X");
                            }

                        }
                        else Value = InfoValue.Value;
                        CustomerRecord crV = new CustomerRecord();
                        crV.id = InfoValue.Id;
                        crV.parentId = EntityInstance.Id;
                        crV.classifiedName = "";
                        crV.instanceId = EntityInstance.Id;
                        crV.CustomerId = EntityInstance.CustomerId;
                        crV.entityId = EntityInstance.EntityId;
                        crV.categoryId = EntityInstance.Entity.EntityCategoryId;
                        crV.entityName = EntityInstance.Entity.EntityName;
                        crV.valueId = InfoValue.Id;
                        crV.property = InfoValue.Property.Name;
                        crV.value = Value;
                        crV.primaryPropertyValue = cr.primaryPropertyValue;
                        if (!customerRecords.Any(c => c.id == crV.id)) customerRecords.Add(crV);

                        
                    }
                }

            }

            return Json(customerRecords);
        }



        

        public async Task<IActionResult> DeleteCustomerInstance(Guid instanceId)
        {
            _logger.LogInformation("CustomerDataManagement.DeleteCustomerInstance is called with {instanceId}", instanceId);
            if (instanceId == null)
            {
                _logger.LogError("instanceId == null");
                return BadRequest();
            }
            EntityInstance instance = await _context.EntityInstance.SingleOrDefaultAsync(i => i.Id == instanceId);
            if (instance == null)
            {
                _logger.LogError("instance == null");
                return BadRequest();
            }
            instance.IsDeleted = true;
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            return Json("Success");
        }

        public async Task<IActionResult> SaveExistingCustomerData(List<InstanceProperty>? values, string? instanceName, Guid customerId, Guid entityId, Guid instanceId)
        {
            _logger.LogInformation("CustomerDataManagement.SaveExistingCustomerData is called with {values}, {instanceName}, {customerId}, {entityId}, {instanceId}", values, instanceName, customerId, entityId, instanceId);
            if (values.IsNullOrEmpty() || entityId == null)
            {
                _logger.LogError("values.IsNullOrEmpty() || entityId == null");
                return BadRequest();
            }
            if (customerId == null)
            {
                _logger.LogError("Please select a customer");
                return Json("Please select a customer");
            }
            Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerId);
            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);

            if (entity == null || customer == null)
            {
                _logger.LogError("entity == null || customer == null");
                return BadRequest();
            }

            _logger.LogDebug("Checking whether instance is new or existing");
            EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(ei => ei.Id == instanceId);
            if (entityInstance == null)
            {
                _logger.LogDebug("Instance is new");


                entityInstance = new EntityInstance();
                entityInstance.CustomerId = customer.Id;
                entityInstance.EntityId = entity.Id;
                entityInstance.EntityInstanceName = instanceName;
                await _context.AddAsync(entityInstance);

                foreach (InstanceProperty instanceProperty in values)
                {
                    if (!instanceProperty.value.IsNullOrEmpty())
                    {
                        if (instanceProperty.propertyId == null)
                        {
                            _logger.LogError("instanceProperty.propertyId == null");
                            return BadRequest();
                        }
                        Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == instanceProperty.propertyId);
                        if (entity == null || property == null)
                        {
                            _logger.LogError("entity == null || property == null");
                            return BadRequest();
                        }

                        EntityProperty entityProperty = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.EntityId == entity.Id && ep.PropertyId == property.Id);
                        if (entityProperty == null)
                        {
                            _logger.LogError("Invalid property: {property.Name} for entity: {entity.EntityName}",property.Name, entity.EntityName);
                            return Json("Invalid property: " + property.Name + " for entity: " + entity.EntityName);
                        }

                        if (!String.IsNullOrEmpty(property.ValidationRegex))
                        {
                            Regex regex = new Regex(property.ValidationRegex);

                            if (!regex.IsMatch(instanceProperty.value))
                            {
                                _logger.LogError("Invalid value for  {property.Name}. Validation Hint: {property.ValidationHint}", property.Name, property.ValidationHint);
                                return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
                            }
                        }

                        if (property.IsEncrypted.HasValue)
                        {
                            instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
                        }
                        else if (property.IsHashed.Value)
                        {
                            instanceProperty.value = Cryptography.Hash(instanceProperty.value);
                        }

                        if (property.IsUniqueIdentifier.Value)
                        {
                            CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
                            if (customerInfoValue1 != null)
                            {
                                _logger.LogError("Can not insert a duplicate record for a unique identifier");
                                return Json("Can not insert a duplicate record for a unique identifier");
                            }
                        }

                        CustomerInfoValue customerInfoValue = new CustomerInfoValue();
                        customerInfoValue.CustomerId = customer.Id;
                        customerInfoValue.EntityInstanceId = entityInstance.Id;

                        customerInfoValue.PropertyId = property.Id;

                        customerInfoValue.Value = instanceProperty.value;


                        await _context.AddAsync(customerInfoValue);
                    }
                }
            }
            else
            {
                _logger.LogDebug("Instance already exists");
                entityInstance.EntityInstanceName = instanceName;

                foreach (InstanceProperty instanceProperty in values)
                {
                    if (!instanceProperty.value.IsNullOrEmpty())
                    {
                        if (instanceProperty.propertyId == null)
                        {
                            _logger.LogError("instanceProperty.propertyId == null");
                            return BadRequest();
                        }
                        Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == instanceProperty.propertyId);
                        if (entity == null || property == null)
                        {
                            _logger.LogError("entity == null || property == null");
                            return BadRequest();
                        }

                        EntityProperty entityProperty = await _context.EntityProperty.FirstOrDefaultAsync(ep => ep.EntityId == entity.Id && ep.PropertyId == property.Id);
                        if (entityProperty == null)
                        {
                            _logger.LogError("Invalid property: {property.Name} for entity: {entity.EntityName}", property.Name, entity.EntityName);
                            return Json("Invalid property: " + property.Name + " for entity: " + entity.EntityName);
                        }

                        CustomerInfoValue customerInfoValue2 = await _context.CustomerInfoValue.FirstOrDefaultAsync(civ => civ.EntityInstanceId == entityInstance.Id && civ.PropertyId == property.Id);
                        if (customerInfoValue2 != null)
                        {
                            if (!String.IsNullOrEmpty(property.ValidationRegex))
                            {
                                Regex regex = new Regex(property.ValidationRegex);

                                if (!regex.IsMatch(instanceProperty.value))
                                {
                                    _logger.LogError("Invalid value for  {property.Name}. Validation Hint: {property.ValidationHint}", property.Name, property.ValidationHint);
                                    return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
                                }
                            }

                            if (property.IsEncrypted.Value)
                            {
                                instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
                            }
                            else if (property.IsHashed.Value)
                            {
                                instanceProperty.value = Cryptography.Hash(instanceProperty.value);
                            }

                            if (property.IsUniqueIdentifier.Value)
                            {
                                CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value && c.EntityInstanceId != customerInfoValue2.EntityInstanceId);
                                if (customerInfoValue1 != null)
                                {
                                    _logger.LogError("Can not insert a duplicate record for a unique identifier");
                                    return Json("Can not insert a duplicate record for a unique identifier");
                                }
                            }
                            customerInfoValue2.Value = instanceProperty.value;
                        }
                        else
                        {
                            if (!String.IsNullOrEmpty(property.ValidationRegex))
                            {
                                Regex regex = new Regex(property.ValidationRegex);

                                if (!regex.IsMatch(instanceProperty.value))
                                {
                                    _logger.LogError("Invalid value for  {property.Name}. Validation Hint: {property.ValidationHint}", property.Name, property.ValidationHint);
                                    return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
                                }
                            }

                            if (property.IsEncrypted.Value)
                            {
                                instanceProperty.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(instanceProperty.value)), Cryptography.Operation.Encrypt);
                            }
                            else if (property.IsHashed.Value)
                            {
                                instanceProperty.value = Cryptography.Hash(instanceProperty.value);
                            }

                            if (property.IsUniqueIdentifier.Value)
                            {
                                CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == instanceProperty.value);
                                if (customerInfoValue1 != null)
                                {
                                    _logger.LogError("Can not insert a duplicate record for a unique identifier");
                                    return Json("Can not insert a duplicate record for a unique identifier");
                                }
                            }

                            CustomerInfoValue customerInfoValue = new CustomerInfoValue();
                            customerInfoValue.CustomerId = customer.Id;
                            customerInfoValue.EntityInstanceId = entityInstance.Id;

                            customerInfoValue.PropertyId = property.Id;

                            customerInfoValue.Value = instanceProperty.value;


                            await _context.AddAsync(customerInfoValue);
                        }



                    }
                }



            }

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);


            return Json("Success");
        }




        public async Task<IActionResult> GetEntityInstanceProperties(Guid entityId, bool existing)
        {
            _logger.LogInformation("CustomerDataManagement.GetEntityInstanceProperties is called with {entityId} and {existing}", entityId, existing);
            List<InstanceProperty> instanceProperties = new List<InstanceProperty>();
            if (entityId == null)
            {
                _logger.LogError("entityId == null");
                return Json(instanceProperties);
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            if(entity == null)
            {
                _logger.LogError("entity == null");
                return Json(instanceProperties);
            }

            Instance instance;
            if (existing)
            {
                instance = newInstancesEC.SingleOrDefault(ni => ni.entityId == entityId);
            }
            else
            {
                instance = newInstances.SingleOrDefault(ni => ni.entityId == entityId);
            }
            

            List<InstanceProperty> instanceProperties1 = instance.properties.ToList();

            return Json(instanceProperties1);
        }

        public async Task<IActionResult> GetInstanceName(Guid entityId, bool existing)
        {
            _logger.LogInformation("CustomerDataManagement.GetInstanceName is called with {entityId} and {existing}", entityId, existing);
            if (entityId == null)
            {
                _logger.LogError("entityId == null");
                return Json("");
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            if (entity == null)
            {
                _logger.LogError("entity == null");
                return Json("");
            }

            Instance instance;
            if (existing)
            {
                instance = newInstancesEC.SingleOrDefault(ni => ni.entityId == entityId);
            }
            else
            {
                instance = newInstances.SingleOrDefault(ni => ni.entityId == entityId);
            }

            return Json(instance.instanceName);
        }

        public async Task<IActionResult> UpdateInstanceInfo(Guid entityId, string? instanceName, List<InstanceProperty> instanceProperties)
        {
            _logger.LogInformation("CustomerDataManagement.UpdateInstanceInfo is called with {entityId}, {instanceName} and {instanceProperties}", entityId, instanceName, instanceProperties);
            if (entityId == null)
            {
                _logger.LogError("instanceId == null");
                return BadRequest();
            }

            Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == entityId);
            if (entity == null)
            {
                _logger.LogError("instance == null");
                return BadRequest();
            }

            Instance instance = newInstances.SingleOrDefault(ni => ni.entityId == entityId);

            if (!instanceName.IsNullOrEmpty()) instance.instanceName = instanceName;

            instance.properties = instanceProperties;
            instance.isValid = true;

            foreach(InstanceProperty ip in instanceProperties)
            {
                if (!await InternallyValidateInput(ip.propertyId, ip.value))
                {
                    instance.isValid = false;
                    return Json("SuccessInvalid");
                }
            }

            

            return Json("Success");
        }

        public async Task<IActionResult> ValidateInput(Guid propertyId, string? value)
        {
            _logger.LogInformation("CustomerDataManagement.ValidateInput is called with {propertyId} and {value}", propertyId, value);
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

        public async Task<bool> InternallyValidateInput(Guid propertyId, string? value)
        {
            _logger.LogInformation("CustomerDataManagement.InternallyValidateInput is called with {propertyId} and {value}", propertyId, value);
            if (propertyId == null)
            {
                _logger.LogError("propertyId == null");
                return false;
            }
            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == propertyId);
            if (property == null)
            {
                return false;
            }
            if (!value.IsNullOrEmpty())
            {
                if (!String.IsNullOrEmpty(property.ValidationRegex))
                {
                    Regex regex = new Regex(property.ValidationRegex);

                    if (!regex.IsMatch(value))
                    {
                        return false;
                    }
                }

                return true;
            }
            else
            {
                if (!property.IsRequired.Value) return true;
            }
            return false;
        }

        public async Task<IActionResult> Save()
        {
            _logger.LogInformation("CustomerDataManagement.Save is called");
            _logger.LogDebug("Creating new customer");
            Customer customer = new Customer();
            await _context.AddAsync(customer);

            _logger.LogDebug("Adding new instances");
            foreach (Instance instance in newInstances)
            {
                _logger.LogDebug("Adding instance {instance.instanceName}", instance.instanceName);
                Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == instance.entityId);
                if (!instance.isValid) 
                {
                    _logger.LogError("Please enter a valid {entity.EntityName}", entity.EntityName);
                    return Json("Please enter a valid " + entity.EntityName); 
                }
                EntityInstance entityInstance = new EntityInstance();
                entityInstance.CustomerId = customer.Id;
                entityInstance.EntityId = entity.Id;
                entityInstance.EntityInstanceName = instance.instanceName;
                await _context.AddAsync(entityInstance);

                _logger.LogDebug("Adding instance properties");
                foreach (InstanceProperty ip in instance.properties)
                {
                    _logger.LogDebug("Adding property {ip.propertyName}", ip.propertyName);
                    CustomerInfoValue customerInfoValue = new CustomerInfoValue();
                    customerInfoValue.CustomerId = customer.Id;
                    customerInfoValue.EntityInstanceId = entityInstance.Id;
                    customerInfoValue.PropertyId = ip.propertyId;
                    Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == ip.propertyId);
                    if (property == null)
                    {
                        _logger.LogError("Invalid property in {entity.EntityName}", entity.EntityName);
                        return Json("Invalid property in " + entity.EntityName);
                    }
                    if (property.IsEncrypted.Value)
                    {
                        ip.value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(ip.value)), Cryptography.Operation.Encrypt);
                    }
                    else if (property.IsHashed.Value)
                    {
                        ip.value = Cryptography.Hash(ip.value);
                    }

                    if (property.IsUniqueIdentifier.Value)
                    {
                        CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == ip.value);
                        if (customerInfoValue1 != null)
                        {
                            _logger.LogError("Can not insert a duplicate record for a unique identifier");
                            return Json("Can not insert a duplicate record for a unique identifier");
                        }
                    }
                    customerInfoValue.Value = ip.value;
                    await _context.AddAsync(customerInfoValue);
                }
            }

            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);

            foreach (Instance instance in newInstances)
            {
                instance.isValid = false;
                instance.instanceName = "";
                foreach(InstanceProperty ip in instance.properties)
                {
                    ip.value = "";
                }
            }
            return Json("Success");
        }


    }

    
}
