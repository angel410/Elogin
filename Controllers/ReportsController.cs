using eLogin.Data;
using Microsoft.AspNetCore.Mvc;
using eLogin.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Text;
using Syncfusion.EJ2.Base;
using System.Collections;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class ReportsController : Controller
    {

        private readonly DatabaseContext _context;

        public ReportsController(DatabaseContext Context)
        {
            this._context = Context;
        }

        public static IEnumerable AuditDataSource;


        public async Task<IActionResult> CustomerLoginAttempts()
        {
            ViewBag.DataSource = _context.CustomerLoginAttempt.ToArray();

            var CustomerLoginAttempts = await _context.CustomerLoginAttempt
                                                     .ToListAsync();

            var customerLoginAttemptsRecords = new List<CustomerLoginAttemptRecord>();

            foreach (var CustomerLoginAttempt in CustomerLoginAttempts)
            {
                customerLoginAttemptsRecords.Add(new CustomerLoginAttemptRecord()
                {
                    
                    IsSuccess = CustomerLoginAttempt.IsSuccess,
                    IncorrectPassword = CustomerLoginAttempt.IncorrectPassword,
                    IsExpired = CustomerLoginAttempt.IsExpired,
                    LockedAccount = CustomerLoginAttempt.LockedAccount,
                    DateTime = CustomerLoginAttempt.DateTime,
                    CustomerID = CustomerLoginAttempt.CustomerId,
                    IdentificationChannel = CustomerLoginAttempt.IdentificationChannel.Channel
                }); ;
                
            }

            ViewBag.DataSource = customerLoginAttemptsRecords;

            return View();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Audit()
        {
            ViewBag.DataSource = _context.Audit.ToArray();
            return View();
        }

        public async Task<IActionResult> AuditServer()
        {
            AuditDataSource = _context.Audit;
            return View();
        }

        public async Task<IActionResult> LockedCustomers()
        {
            var Customers = await _context.Customer
                .Where(c => c.IsLocked == true)
                                    .Include(Customer => Customer.EntitieInstances)
                                    .ThenInclude(Entity => Entity.CustomerInfoValues)
                                    .ToListAsync();

            var customerRecords = new List<CustomerRecord>();



            foreach (Customer Customer in Customers)
            {
                // Adding Customer
                CustomerRecord cr = new CustomerRecord();
                cr.id = Customer.Id;
                cr.parentId = null;
                // Obtaining the primary property from System Settings
                SystemSetting systemSetting = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "Customer Primary Property");
                Guid primaryProperty = new Guid();
                if (systemSetting.Value != "")
                    Guid.Parse(systemSetting.Value);
                // Obtaining the corresponding customer value of the primary property
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
                }
                else
                {
                    // Customer primary property value not found
                    cr.classifiedName = "CustomerId: " + Customer.Id;
                }
                cr.instanceId = null;
                cr.CustomerId = Customer.Id;
                // Adding customer record
                customerRecords.Add(cr);

                // Creating a list of virtual entity categories to store virtualCategories
                List<VirtualEntityCategory> VirtualCategories = new List<VirtualEntityCategory>();
                // Creating a list of virtual entities to store entities
                List<VirtualEntity> entities = new List<VirtualEntity>();

                // Looping over customer entity instances
                foreach (var EntityInstance in Customer.EntitieInstances)
                {
                    if (EntityInstance.Entity != null && EntityInstance.Entity.EntityCategory != null)
                    {
                        //Adding Categories
                        //Before adding the entity's category, we look for it in the virtualCategories list, to avoid placing duplicate categories
                        VirtualEntityCategory vc = VirtualCategories.SingleOrDefault(pc => pc.originalId == EntityInstance.Entity.EntityCategoryId);
                        if (vc == null)
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
                                if (virtualEntityCategory == null)
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
                            if (!customerRecords.Any(c => c.id == crW.id)) customerRecords.Add(crW);
                        }



                        //Adding Entity
                        VirtualEntity ve = entities.SingleOrDefault(e => e.originalId == EntityInstance.EntityId);
                        if (ve == null)
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
                        cr2.CustomerId = EntityInstance.CustomerId;
                        if (!customerRecords.Any(c => c.id == cr2.id)) customerRecords.Add(cr2);


                        //Adding Instance
                        CustomerRecord cr1 = new CustomerRecord();
                        cr1.id = EntityInstance.Id;
                        cr1.parentId = ve.id;
                        cr1.classifiedName = EntityInstance.EntityInstanceName;
                        cr1.instanceId = EntityInstance.Id;
                        cr1.entityId = EntityInstance.EntityId;
                        cr1.CustomerId = EntityInstance.CustomerId;
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
                        crV.valueId = InfoValue.Id;
                        crV.property = InfoValue.Property.Name;
                        crV.value = Value;
                        if (!customerRecords.Any(c => c.id == crV.id)) customerRecords.Add(crV);


                    }
                }

            }

            ViewBag.DataSource = customerRecords;
            // ViewBag.DataSource = _context.Customer.Where(c => c.IsLocked == true).ToListAsync();
            return View();
        }

        public IActionResult EntityCategories()
        {
            ViewBag.DataSource = _context.EntityCategory.ToArray();
            return View();
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
            public Guid categoryId { get; set; }
            public Guid parentCategoryId { get; set; }

           
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

        public class CustomerLoginAttemptRecord
        {
            public DateTime DateTime { get; set; }
            public bool IsSuccess { get; set; }
            public bool IncorrectPassword { get; set; }
            public bool IsExpired { get; set; }
            public bool LockedAccount { get; set; }
            

            public Guid CustomerID { get; set; }
            //public string EntityCategory { get; set; }
            //public Guid EntityID { get; set; }
            //public string EntityName { get; set; }
            //public Guid ValueID { get; set; }
            //public string Value { get; set; }
            //public string PropertyName { get; set; }

            public string IdentificationChannel { get; set; }
            
        }

        public async Task<IActionResult> Customers()
        {
            var Customers = await _context.Customer
                                                     .Include(Customer => Customer.EntitieInstances)
                                                     .ThenInclude(Entity => Entity.CustomerInfoValues)
                                                     .ToListAsync();

            var customerRecords = new List<CustomerRecord>();



            foreach (Customer Customer in Customers)
            {
                // Adding Customer
                CustomerRecord cr = new CustomerRecord();
                cr.id = Customer.Id;
                cr.parentId = null;
                // Obtaining the primary property from System Settings
                SystemSetting systemSetting = await _context.SystemSetting.SingleOrDefaultAsync(ss => ss.SettingName == "Customer Primary Property");
                Guid primaryProperty=new Guid();
                if (systemSetting.Value != "")
                    Guid.Parse(systemSetting.Value);
                // Obtaining the corresponding customer value of the primary property
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
                }
                else
                {
                    // Customer primary property value not found
                    cr.classifiedName = "CustomerId: " + Customer.Id;
                }
                cr.instanceId = null;
                cr.CustomerId = Customer.Id;
                // Adding customer record
                customerRecords.Add(cr);

                // Creating a list of virtual entity categories to store virtualCategories
                List<VirtualEntityCategory> VirtualCategories = new List<VirtualEntityCategory>();
                // Creating a list of virtual entities to store entities
                List<VirtualEntity> entities = new List<VirtualEntity>();

                // Looping over customer entity instances
                foreach (var EntityInstance in Customer.EntitieInstances)
                {
                    if (EntityInstance.Entity != null && EntityInstance.Entity.EntityCategory != null)
                    {
                        //Adding Categories
                        //Before adding the entity's category, we look for it in the virtualCategories list, to avoid placing duplicate categories
                        VirtualEntityCategory vc = VirtualCategories.SingleOrDefault(pc => pc.originalId == EntityInstance.Entity.EntityCategoryId);
                        if (vc == null)
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
                                if (virtualEntityCategory == null)
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
                            if (!customerRecords.Any(c => c.id == crW.id)) customerRecords.Add(crW);
                        }



                        //Adding Entity
                        VirtualEntity ve = entities.SingleOrDefault(e => e.originalId == EntityInstance.EntityId);
                        if (ve == null)
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
                        cr2.CustomerId = EntityInstance.CustomerId;
                        if (!customerRecords.Any(c => c.id == cr2.id)) customerRecords.Add(cr2);


                        //Adding Instance
                        CustomerRecord cr1 = new CustomerRecord();
                        cr1.id = EntityInstance.Id;
                        cr1.parentId = ve.id;
                        cr1.classifiedName = EntityInstance.EntityInstanceName;
                        cr1.instanceId = EntityInstance.Id;
                        cr1.entityId = EntityInstance.EntityId;
                        cr1.CustomerId = EntityInstance.CustomerId;
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
                        crV.valueId = InfoValue.Id;
                        crV.property = InfoValue.Property.Name;
                        crV.value = Value;
                        if (!customerRecords.Any(c => c.id == crV.id)) customerRecords.Add(crV);


                    }
                }

            }

            ViewBag.DataSource = customerRecords;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditData(Guid valueId, string? newValue)
        {
            if (valueId == null || newValue == null)
            {
                return NotFound();
            }

            CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Id == valueId);
            if(customerInfoValue == null)
            {
                return NotFound();
            }
            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == customerInfoValue.PropertyId);
            if (!String.IsNullOrEmpty(property.ValidationRegex))
            {
                Regex regex = new Regex(property.ValidationRegex);

                if (!regex.IsMatch(newValue))
                {
                    return Json("Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint);
                }
            }
            if (property.IsEncrypted.Value)
            {
                newValue = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(newValue)), Cryptography.Operation.Encrypt);
            }
            else if (property.IsHashed.Value)
            {
                newValue = Cryptography.Hash(newValue);
            }

            if (property.IsUniqueIdentifier.Value)
            {
                CustomerInfoValue customerInfoValueDoublicateCheck = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == newValue);

                if (customerInfoValueDoublicateCheck != null && customerInfoValueDoublicateCheck.CustomerId != customerInfoValue.CustomerId)
                {
                    return Json("Cannot insert duplicate value of a unique identifier for two different customers");
                }
            }
            customerInfoValue.Value = newValue;
            await _context.SaveChangesAsync(HttpContext.User.Identity.Name);





            return Json("Success");

        }

        public IActionResult UrlDatasource([FromBody] DataManagerRequest dm)
        {
            IEnumerable DataSource = _context.Audit;
            DataOperations operation = new DataOperations();
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);  //Search
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0) //Sorting
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            if (dm.Where != null && dm.Where.Count > 0) //Filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, dm.Where[0].Operator);
            }
            int count = DataSource.Cast<Audit>().Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }
            return dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);
        }


    }
}