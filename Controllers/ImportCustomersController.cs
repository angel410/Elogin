using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using eLogin.Data;
using eLogin.Models;
using eLogin.Models.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.EJ2.Linq;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class ImportCustomersController : Controller
    {
        private readonly DatabaseContext _context;
        private Microsoft.AspNetCore.Hosting.IHostingEnvironment hostingEnv;
        private static string uploadedFileName;
        static string Log;
        private readonly eLoginSettings _eLoginSettings;
        private LicenseCheck LC;
        private readonly ILogger<ImportCustomersController> _logger;

        public ImportCustomersController(DatabaseContext context, IHostingEnvironment env, IOptions<eLoginSettings> eLoginSettings, LicenseCheck licenseCheck, ILogger<ImportCustomersController> logger)
        {
            _context = context;
            this.hostingEnv = env;
            _eLoginSettings = eLoginSettings.Value;
            LC = licenseCheck;
            _logger = logger;
        }

        [AcceptVerbs("Post")]
        public async Task<IActionResult> Save(IList<IFormFile> UploadFiles)
        {
            _logger.LogInformation("ImportCustomers.Save is called");
            try
            {
                foreach (var file in UploadFiles)
                {
                    var filename = ContentDispositionHeaderValue
                                        .Parse(file.ContentDisposition)
                                        .FileName
                                        .Trim('"');
                    filename = hostingEnv.WebRootPath + $@"\ImportedCustomers\ImportedCustomers.csv";
                    uploadedFileName = filename;
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
                        System.IO.File.Delete(hostingEnv.WebRootPath + $@"\ImportedCustomers\ImportedCustomers.csv");
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
                //if (await ImportValidation())
                //{
                //    await ImportValidationSucceeded();
                //    await ImportCustomers();
                //}
                //else await ImportValidationFailed();
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in ImportCustomers.Save");
                _logger.LogError(e, "Failed to upload and import customer data");
                Response.Clear();
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "Failed to upload and import customer data.";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;
                return StatusCode(204);
            }
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("ImportCustomers.Index is called");
                uploadedFileName = hostingEnv.WebRootPath + $@"\ImportedCustomers\customerdata.csv"; 
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Exception in ImportCustomers.Index");
            }
            
            return View();
            
        }

        
        private async Task<bool> ImportValidation(bool updateExisting)
        {
            _logger.LogInformation("ImportCustomers.ImportValidation is called with {updateExisting}", updateExisting);
            try
            {
                using (var reader = new StreamReader(uploadedFileName))
                {


                    string[] entityNames = new string[] { };
                    string[] entityIsRequired = new string[] { };
                    string[] entityInstanceNames = new string[] { };
                    string[] entityCategoryName = new string[] { };
                    string[] entityParentCategoryName = new string[] { };
                    string[] propertyName = new string[] { };
                    string[] propertyValidationRegex = new string[] { };
                    string[] propertyValidationHint = new string[] { };
                    string[] propertyIsEncrypted = new string[] { };
                    string[] propertyIsHashed = new string[] { };
                    string[] propertyIsUniqueIdentifier = new string[] { };
                    string[] propertyIsRequired = new string[] { };

                    string[] entityRelatedToChannels = new string[] { };
                    string[] propertyUsedToLoginForChannels = new string[] { };

                    Entity[] entities = new Entity[] { };
                    EntityInstance[] entityInstances = new EntityInstance[] { };
                    EntityCategory[] entityCategories = new EntityCategory[] { };
                    EntityCategory[] parentEntityCategories = new EntityCategory[] { };
                    Property[] properties = new Property[] { };
                    EntityProperty[] entityProperties = new EntityProperty[] { };
                    Customer[] customers = new Customer[] { };
                    CustomerInfoValue[] customerInfoValues = new CustomerInfoValue[] { };
                    ChannelEntity[] channelEntities = new ChannelEntity[] { };
                    List<ChannelLoginProperty> channelLoginProperties = new List<ChannelLoginProperty>();


                    int L = 1;
                    bool endOfStream = false;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                        foreach(string value in values)
                        {
                            if (!value.IsNullOrEmpty())
                            {
                                endOfStream = false;
                                break;
                            }
                            else
                            {
                                endOfStream = true;
                            }

                        }
                        if (endOfStream) break;
                        var vs = line.Split(',');
                        var columnHeaders = values;

                        if (L <= 15)
                        {
                            switch (L)
                            {
                                case 1:
                                    propertyName = values;
                                    break;
                                case 2:
                                    propertyValidationRegex = values;
                                    break;
                                case 3:
                                    propertyValidationHint = values;
                                    break;
                                case 4:
                                    propertyIsEncrypted = values;
                                    break;
                                case 5:
                                    propertyIsHashed = values;
                                    break;
                                case 6:
                                    propertyIsUniqueIdentifier = values;
                                    break;
                                case 7:
                                    propertyIsRequired = values;
                                    break;
                                case 8:
                                    entityInstanceNames = values;
                                    break;
                                case 9:
                                    entityNames = values;
                                    break;
                                case 10:
                                    entityIsRequired = values;
                                    break;
                                case 11:
                                    entityCategoryName = values;
                                    break;
                                case 12:
                                    entityParentCategoryName = values;
                                    break;
                                case 13:
                                    entityRelatedToChannels = values;
                                    break;
                                case 14:
                                    propertyUsedToLoginForChannels = values;
                                    break;
                                case 15:
                                    
                                    // Creating Parent Entity Categories

                                    int arraypointer = 0;
                                    foreach (string pec in entityParentCategoryName)
                                    {
                                        if (!pec.IsNullOrEmpty())
                                        {
                                            EntityCategory parentEntityCategory = await _context.EntityCategory.SingleOrDefaultAsync(p => p.CategoryName == pec);
                                            if (parentEntityCategory == null)
                                            {
                                                parentEntityCategory = parentEntityCategories.Where(en => en != null).FirstOrDefault(p => p.CategoryName == pec);
                                            }
                                            if (parentEntityCategory == null)
                                            {
                                                parentEntityCategory = entityCategories.Where(en => en != null).FirstOrDefault(p => p.CategoryName == pec);
                                            }
                                            if (parentEntityCategory == null && arraypointer != 0)
                                            {
                                                EntityCategory newParentEntityCategory = new EntityCategory();
                                                newParentEntityCategory.CategoryName = pec;

                                                await _context.AddAsync(newParentEntityCategory);
                                                

                                                parentEntityCategory = newParentEntityCategory;
                                            }
                                            Array.Resize(ref parentEntityCategories, parentEntityCategories.Length + 1);
                                            parentEntityCategories[arraypointer] = parentEntityCategory;
                                        }
                                        else
                                        {
                                            Array.Resize(ref parentEntityCategories, parentEntityCategories.Length + 1);
                                            parentEntityCategories[arraypointer] = null;
                                        }

                                        arraypointer++;

                                    }

                                    // Creating Child Entity Categories

                                    arraypointer = 0;
                                    foreach (string cec in entityCategoryName)
                                    {
                                        if (!cec.IsNullOrEmpty())
                                        {
                                            EntityCategory entityCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.CategoryName == cec);
                                            if (entityCategory == null)
                                            {
                                                entityCategory = parentEntityCategories.Where(en => en != null).FirstOrDefault(p => p.CategoryName == cec);
                                            }
                                            if (entityCategory == null)
                                            {
                                                entityCategory = entityCategories.Where(en => en != null).FirstOrDefault(p => p.CategoryName == cec);
                                            }
                                            if (entityCategory == null && arraypointer != 0)
                                            {
                                                EntityCategory newEntityCategory = new EntityCategory();
                                                newEntityCategory.CategoryName = cec;

                                                string parentName = entityParentCategoryName[arraypointer];
                                                if (!parentName.IsNullOrEmpty())
                                                {
                                                    EntityCategory parent = parentEntityCategories[arraypointer];
                                                    newEntityCategory.ParentEntityCategoryId = parent.Id;
                                                }

                                                await _context.AddAsync(newEntityCategory);
                                                
                                                entityCategory = newEntityCategory;
                                            }
                                            if (arraypointer != 0)
                                            {
                                                string parentName = entityParentCategoryName[arraypointer];
                                                if (!parentName.IsNullOrEmpty())
                                                {
                                                    EntityCategory parent = parentEntityCategories[arraypointer];
                                                    entityCategory.ParentEntityCategoryId = parent.Id;
                                                }
                                            }
                                            Array.Resize(ref entityCategories, entityCategories.Length + 1);
                                            entityCategories[arraypointer] = (entityCategory);
                                        }
                                        else
                                        {
                                            Array.Resize(ref entityCategories, entityCategories.Length + 1);
                                            entityCategories[arraypointer] = (null);
                                        }
                                        arraypointer++;

                                    }

                                    // Creating Entities

                                    arraypointer = 0;
                                    foreach (string e in entityNames)
                                    {
                                        if (!e.IsNullOrEmpty())
                                        {
                                            Entity entity = await _context.Entity.SingleOrDefaultAsync(c => c.EntityName == e);
                                            if (entity == null)
                                            {
                                                entity = entities.Where(en=>en != null).FirstOrDefault(ent=>ent.EntityName == e);
                                            }
                                            if (entity == null && arraypointer != 0)
                                            {
                                                Entity newEntity = new Entity();
                                                newEntity.EntityName = e;
                                                newEntity.IsRequired = Convert.ToBoolean(entityIsRequired[arraypointer]);

                                                string categoryName = entityCategoryName[arraypointer];
                                                if (!categoryName.IsNullOrEmpty())
                                                {
                                                    EntityCategory parent = entityCategories[arraypointer];
                                                    newEntity.EntityCategoryId = parent.Id;
                                                }

                                                await _context.AddAsync(newEntity);
                                                
                                                entity = newEntity;

                                                if(!String.IsNullOrEmpty(entityRelatedToChannels[arraypointer]))
                                                {
                                                    string[] ertc = entityRelatedToChannels[arraypointer].Replace("\"", "").Split(",");
                                                    foreach (string channelName in ertc)
                                                    {
                                                        IdentificationChannel identificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(c => c.Channel == channelName);
                                                        if (identificationChannel == null)
                                                        {
                                                            if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                            Log = Log + "Could not find a matching Channel for: " + channelName + " At Row Number: " + 11 + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                                        }
                                                        else
                                                        {
                                                            ChannelEntity channelEntity = new ChannelEntity();
                                                            channelEntity.IdentificationChannelId = identificationChannel.Id;
                                                            channelEntity.EntityId = entity.Id;

                                                            await _context.AddAsync(channelEntity);
                                                        }
                                                    }
                                                }
                                                
                                            }
                                            Array.Resize(ref entities, entities.Length + 1);
                                            entities[arraypointer] = (entity);
                                            

                                        }
                                        else
                                        {
                                            Array.Resize(ref entities, entities.Length + 1);
                                            entities[arraypointer] = (null);
                                        }
                                        arraypointer++;

                                    }

                                    // Creating Properties
                                    arraypointer = 0;
                                    foreach (string p in propertyName)
                                    {
                                        if(!p.IsNullOrEmpty())
                                        {
                                            Property property = null;

                                            EntityProperty entityProperty = await _context.EntityProperty.SingleOrDefaultAsync(ep => ep.Entity.EntityName == entityNames[arraypointer] && ep.Property.Name == p);
                                            if (entityProperty == null)
                                            {
                                                entityProperty = entityProperties.Where(ep => ep != null).SingleOrDefault(ep => ep.Entity.EntityName == entityNames[arraypointer] && ep.Property.Name == p);
                                            }

                                            if (entityProperty != null)
                                            {
                                                property = entityProperty.Property;
                                            }

                                            if (property == null && arraypointer != 0)
                                            {
                                                Property newProperty = new Property();
                                                newProperty.Name = propertyName[arraypointer];
                                                string vr = propertyValidationRegex[arraypointer];
                                                if (vr.StartsWith("\"") && vr.EndsWith("\""))
                                                {
                                                    vr = vr.Substring(1, propertyValidationRegex[arraypointer].Length - 2);
                                                    propertyValidationRegex[arraypointer] = vr;
                                                }
                                                newProperty.ValidationRegex = propertyValidationRegex[arraypointer];
                                                newProperty.ValidationHint = propertyValidationHint[arraypointer];
                                                newProperty.IsEncrypted = Convert.ToBoolean(propertyIsEncrypted[arraypointer]);
                                                newProperty.IsHashed = Convert.ToBoolean(propertyIsHashed[arraypointer]);
                                                newProperty.IsUniqueIdentifier = Convert.ToBoolean(propertyIsUniqueIdentifier[arraypointer]);
                                                newProperty.IsRequired = Convert.ToBoolean(propertyIsRequired[arraypointer]);

                                                await _context.AddAsync(newProperty);


                                                property = newProperty;

                                                if (LC.Check().isValid)
                                                {
                                                    if (LC.Check().isCustomerRepository)
                                                    {

                                                    }
                                                    else
                                                    {
                                                        if (!property.IsUniqueIdentifier.Value || propertyUsedToLoginForChannels[arraypointer].IsNullOrEmpty())
                                                        {
                                                            if (!Log.IsNullOrEmpty()) Log = null;
                                                            Log = Log + "Unfortunately, you do not have Customer Repository License rights and therefore, you can only store properties that are uniquie identifiers and you must also specify the channel name(s) to which this property is used as a login identifier.";
                                                            return false;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (!Log.IsNullOrEmpty()) Log = null;
                                                    Log = Log + "Invalid License. Please contact ExpertFlow.";
                                                    return false;
                                                }

                                                if (property.IsUniqueIdentifier.Value)
                                                {
                                                    if (!propertyUsedToLoginForChannels[arraypointer].IsNullOrEmpty())
                                                    {
                                                        string[] putlfc = propertyUsedToLoginForChannels[arraypointer].Split(",");
                                                        foreach (string channelName in putlfc)
                                                        {
                                                            IdentificationChannel identificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(c => c.Channel == channelName);
                                                            if (identificationChannel == null)
                                                            {
                                                                if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                                Log = Log + "Could not find a matching Channel for: " + channelName + " At Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                                            }
                                                            else
                                                            {
                                                                int counter = await _context.ChannelLoginProperty.Where(clp => clp.IdentificationChannelId == identificationChannel.Id).CountAsync();
                                                                int newCounter = channelLoginProperties.Where(clp => clp.IdentificationChannelId == identificationChannel.Id).Count();
                                                                counter = counter + newCounter;
                                                                if (counter >= _eLoginSettings.MaxPropertiesUsedForLoginPerChannel)
                                                                {
                                                                    if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                                    Log = Log + "Cannot set " + entities[arraypointer].EntityName + " " + property.Name + " as a login identifier for channel " + channelName + ". A maximum of " + _eLoginSettings.MaxPropertiesUsedForLoginPerChannel + " properties can be used as a login identifier per channel.";
                                                                }
                                                                else
                                                                {
                                                                    ChannelLoginProperty channelLoginProperty = new ChannelLoginProperty();
                                                                    channelLoginProperty.IdentificationChannelId = identificationChannel.Id;
                                                                    channelLoginProperty.PropertyId = property.Id;

                                                                    channelLoginProperties.Add(channelLoginProperty);

                                                                    await _context.AddAsync(channelLoginProperty);
                                                                }

                                                            }
                                                        }
                                                    }

                                                }

                                            }
                                            Array.Resize(ref properties, properties.Length + 1);
                                            properties[arraypointer] = property;

                                            EntityProperty entityProperty1 = null;
                                            if (entities[arraypointer] != null || properties[arraypointer] != null)
                                            {
                                                entityProperty1 = await _context.EntityProperty.SingleOrDefaultAsync(ep => ep.EntityId == entities[arraypointer].Id && ep.PropertyId == properties[arraypointer].Id);
                                            }

                                            if (entityProperty1 == null)
                                            {
                                                entityProperty1 = entityProperties.Where(ep => ep != null).SingleOrDefault(ep => ep.EntityId == entities[arraypointer].Id && ep.PropertyId == properties[arraypointer].Id);
                                            }

                                            if (entityProperty1 == null && arraypointer != 0)
                                            {
                                                EntityProperty newEntityProperty = new EntityProperty();
                                                newEntityProperty.EntityId = entities[arraypointer].Id;
                                                newEntityProperty.PropertyId = properties[arraypointer].Id;

                                                await _context.AddAsync(newEntityProperty);

                                                entityProperty1 = newEntityProperty;
                                            }

                                            Array.Resize(ref entityProperties, entityProperties.Length + 1);
                                            entityProperties[arraypointer] = (entityProperty1);

                                            
                                        }
                                        else
                                        {
                                            Array.Resize(ref entityProperties, entityProperties.Length + 1);
                                            entityProperties[arraypointer] = (null);
                                        }
                                        arraypointer++;
                                    }


                                    //Avoiding duplicates

                                    bool isExistingCustomer1 = false;
                                    arraypointer = 0;

                                    Customer customer1 = new Customer();

                                    foreach (string value in values)
                                    {
                                        if (!value.IsNullOrEmpty() && arraypointer != 0)
                                        {
                                            string v = value;
                                            if (properties[arraypointer].IsEncrypted.Value)
                                            {
                                                v = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(v)), Cryptography.Operation.Encrypt);
                                            }
                                            else if (properties[arraypointer].IsHashed.Value)
                                            {
                                                v = Cryptography.Hash(v);
                                            }

                                            CustomerInfoValue civ = await _context.CustomerInfoValue.FirstOrDefaultAsync(c => c.Value == v);
                                            if (properties[arraypointer].IsUniqueIdentifier == true && civ != null)
                                            {
                                                // ignoring duplicate value and setting customer id
                                                isExistingCustomer1 = true;
                                                customer1 = civ.Customer;
                                            }
                                        }
                                        arraypointer++;
                                    }

                                    if (isExistingCustomer1)
                                    {
                                        // Skipping this row because customer already exists
                                        //break;
                                        if(!updateExisting)
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        //Add Customer
                                        await _context.AddAsync(customer1);
                                        Array.Resize(ref customers, customers.Length + 1);
                                        customers[customers.Length - 1] = (customer1);
                                    }


                                    //Add Customer Entities and InfoValues
                                    arraypointer = 0;

                                    foreach (string value in values)
                                    {
                                        if (value.IsNullOrEmpty() && arraypointer != 0)
                                        {
                                            if (properties[arraypointer].IsRequired.Value && entities[arraypointer].IsRequired)
                                            {
                                                if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                Log = Log + properties[arraypointer].Name + " is required at Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                            }
                                            else
                                            {
                                                if (properties[arraypointer].IsRequired.Value)
                                                {
                                                    int[] allInstanceProperties = new int[] { };
                                                    int i = 0;
                                                    foreach (String instance in entityInstanceNames)
                                                    {
                                                        if (instance == entityInstanceNames[arraypointer])
                                                        {
                                                            Array.Resize(ref allInstanceProperties, allInstanceProperties.Length + 1);
                                                            allInstanceProperties[allInstanceProperties.Length - 1] = i;
                                                        }
                                                        i++;
                                                    }
                                                    foreach (int ii in allInstanceProperties)
                                                    {
                                                        if (!values[ii].IsNullOrEmpty())
                                                        {
                                                            if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                            Log = Log + properties[arraypointer].Name + " is required at Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (!value.IsNullOrEmpty() && arraypointer != 0)
                                        {
                                            string v = value;

                                            if (!String.IsNullOrEmpty(properties[arraypointer].ValidationRegex))
                                            {
                                                string vr = properties[arraypointer].ValidationRegex;
                                                //if (vr.StartsWith("\"") && vr.EndsWith("\""))
                                                //{
                                                //    vr = vr.Substring(1, properties[arraypointer].ValidationRegex.Length - 2);
                                                //}

                                                Regex regex = null;
                                                try
                                                {
                                                    regex = new Regex(vr);
                                                }
                                                catch
                                                {
                                                    if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                    Log = Log + "Invalid regex: " + vr + " At Column Number: " + ((arraypointer + 1).ToString()) + ".";

                                                }


                                                if (regex != null && !regex.IsMatch(v))
                                                {
                                                    if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                    Log = Log + "Data Validation failed. For: " + v + " At Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ". Validation Hint: " + properties[arraypointer].ValidationHint;

                                                }

                                            }


                                            EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(e => e.CustomerId == customer1.Id && e.EntityInstanceName == entityInstanceNames[arraypointer]);
                                            if (entityInstance == null)
                                            {
                                                entityInstance = entityInstances.FirstOrDefault(ei => ei.CustomerId == customer1.Id && ei.EntityInstanceName == entityInstanceNames[arraypointer]);
                                            }
                                            if (entityInstance == null)
                                            {
                                                EntityInstance newEntityInstance = new EntityInstance();
                                                newEntityInstance.EntityInstanceName = entityInstanceNames[arraypointer];
                                                newEntityInstance.CustomerId = customer1.Id;
                                                newEntityInstance.EntityId = entities[arraypointer].Id;

                                                await _context.AddAsync(newEntityInstance);
                                                

                                                entityInstance = newEntityInstance;

                                            }
                                            Array.Resize(ref entityInstances, entityInstances.Length + 1);
                                            entityInstances[entityInstances.Length - 1] = (entityInstance);
                                            
                                            bool isNewCustomerValue = false;

                                            CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(civ => civ.EntityInstanceId == entityInstance.Id && civ.PropertyId == properties[arraypointer].Id);

                                            if (customerInfoValue == null)
                                            {
                                                isNewCustomerValue = true;
                                                customerInfoValue = new CustomerInfoValue();
                                            }

                                            customerInfoValue.CustomerId = customer1.Id;
                                            customerInfoValue.EntityInstanceId = entityInstance.Id;
                                            customerInfoValue.PropertyId = properties[arraypointer].Id;

                                            if (properties[arraypointer].IsEncrypted.Value)
                                            {
                                                v = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(v)), Cryptography.Operation.Encrypt);
                                            }
                                            else if (properties[arraypointer].IsHashed.Value)
                                            {
                                                v = Cryptography.Hash(v);
                                            }

                                            
                                            customerInfoValue.Value = v;

                                            if(isNewCustomerValue) await _context.AddAsync(customerInfoValue);

                                            Array.Resize(ref customerInfoValues, customerInfoValues.Length + 1);
                                            customerInfoValues[customerInfoValues.Length - 1] = (customerInfoValue);

                                        }
                                        arraypointer++;
                                    }
                                    break;




                            }
                        }
                        else
                        {


                            //Avoiding duplicates

                            bool isExistingCustomer = false;
                            int arraypointer = 0;

                            Customer customer = new Customer();

                            foreach (string value in values)
                            {
                                if (!value.IsNullOrEmpty() && arraypointer != 0)
                                {
                                    if (properties[arraypointer].IsUniqueIdentifier.Value)
                                    {
                                        string v = value;
                                        if (properties[arraypointer].IsEncrypted.Value)
                                        {
                                            v = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(v)), Cryptography.Operation.Encrypt);
                                        }
                                        else if (properties[arraypointer].IsHashed.Value)
                                        {
                                            v = Cryptography.Hash(v);
                                        }

                                        CustomerInfoValue civ = await _context.CustomerInfoValue.FirstOrDefaultAsync(c => c.Value == v);
                                        if (properties[arraypointer].IsUniqueIdentifier == true && civ != null)
                                        {
                                            // ignoring duplicate value and setting customer id
                                            isExistingCustomer = true;
                                            customer = civ.Customer;
                                        }
                                    }

                                }
                                arraypointer++;
                            }

                            if (isExistingCustomer)
                            {
                                // Skipping this row because customer already exists
                                //break;
                                if (!updateExisting)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                //Add Customer
                                await _context.AddAsync(customer);
                                Array.Resize(ref customers, customers.Length + 1);
                                customers[customers.Length - 1] = customer;
                            }



                            //Add Customer Entities and InfoValues
                            arraypointer = 0;

                            foreach (string value in values)
                            {
                                if (value.IsNullOrEmpty() && arraypointer != 0)
                                {
                                    if (properties[arraypointer].IsRequired.Value && entities[arraypointer].IsRequired)
                                    {
                                        if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                        Log = Log + properties[arraypointer].Name + " is required at Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                    }
                                    else
                                    {
                                        if (properties[arraypointer].IsRequired.Value)
                                        {
                                            int[] allInstanceProperties = new int[] { };
                                            int i = 0;
                                            foreach (String instance in entityInstanceNames)
                                            {
                                                if (instance == entityInstanceNames[arraypointer])
                                                {
                                                    Array.Resize(ref allInstanceProperties, allInstanceProperties.Length + 1);
                                                    allInstanceProperties[allInstanceProperties.Length - 1] = i;
                                                }
                                                i++;
                                            }
                                            foreach (int ii in allInstanceProperties)
                                            {
                                                if (!values[ii].IsNullOrEmpty())
                                                {
                                                    if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                                    Log = Log + properties[arraypointer].Name + " is required at Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ".";
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (!value.IsNullOrEmpty() && arraypointer != 0)
                                {
                                    string v = value;

                                    if (!String.IsNullOrEmpty(properties[arraypointer].ValidationRegex))
                                    {
                                        string vr = properties[arraypointer].ValidationRegex;
                                        //if (vr.StartsWith("\"") && vr.EndsWith("\""))
                                        //{
                                        //    vr = vr.Substring(1, properties[arraypointer].ValidationRegex.Length - 2);
                                        //}

                                        Regex regex = null;
                                        try
                                        {
                                            regex = new Regex(vr);
                                        }
                                        catch
                                        {
                                            if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                            Log = Log + "Invalid regex: " + vr + " At Column Number: " + ((arraypointer + 1).ToString()) + ".";

                                        }


                                        if (regex != null && !regex.IsMatch(v))
                                        {
                                            if (!Log.IsNullOrEmpty()) Log = Log + Environment.NewLine;
                                            Log = Log + "Data Validation failed. For: " + v + " At Row Number: " + L + " and Column Number: " + ((arraypointer + 1).ToString()) + ". Validation Hint: " + properties[arraypointer].ValidationHint;

                                        }

                                    }


                                    EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(e => e.CustomerId == customer.Id && e.EntityInstanceName == entityInstanceNames[arraypointer]);
                                    if(entityInstance == null)
                                    {
                                        entityInstance = entityInstances.FirstOrDefault(ei => ei.CustomerId == customer.Id && ei.EntityInstanceName == entityInstanceNames[arraypointer]);
                                    }
                                    if (entityInstance == null)
                                    {
                                        EntityInstance newEntityInstance = new EntityInstance();
                                        newEntityInstance.EntityInstanceName = entityInstanceNames[arraypointer];
                                        newEntityInstance.CustomerId = customer.Id;
                                        newEntityInstance.EntityId = entities[arraypointer].Id;

                                        await _context.AddAsync(newEntityInstance);
                                            

                                        entityInstance = newEntityInstance;

                                    }
                                    Array.Resize(ref entityInstances, entityInstances.Length + 1);
                                    entityInstances[entityInstances.Length - 1] = (entityInstance);

                                    bool isNewCustomerValue = false;

                                    CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(civ => civ.EntityInstanceId == entityInstance.Id && civ.PropertyId == properties[arraypointer].Id);

                                    if (customerInfoValue == null)
                                    {
                                        isNewCustomerValue = true;
                                        customerInfoValue = new CustomerInfoValue();
                                    }

                                    customerInfoValue.CustomerId = customer.Id;
                                    customerInfoValue.EntityInstanceId = entityInstance.Id;
                                    customerInfoValue.PropertyId = properties[arraypointer].Id;

                                    if (properties[arraypointer].IsEncrypted.Value)
                                    {
                                        v = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(v)), Cryptography.Operation.Encrypt);
                                    }
                                    else if (properties[arraypointer].IsHashed.Value)
                                    {
                                        v = Cryptography.Hash(v);
                                    }

                                        
                                    customerInfoValue.Value = v;

                                    if (isNewCustomerValue) await _context.AddAsync(customerInfoValue);

                                    Array.Resize(ref customerInfoValues, customerInfoValues.Length + 1);
                                    customerInfoValues[customerInfoValues.Length - 1] = (customerInfoValue);

                                }
                                arraypointer++;
                            }

                            


                        }



                        L++;
                    }
                }
                if (Log.IsNullOrEmpty()) return true;
                else return false;
            }

            catch (Exception e)
            {
                _logger.LogError(e, "Exception in ImportCustomers.ImportValidation");
                Log = e.Message;
                return false;
            }
            
        }

        public async Task<ActionResult> ValidatingData(bool updateExisting)
        {
            _logger.LogInformation("ImportCustomers.ValidatingData is called with {updateExisting}", updateExisting);
            if (await ImportValidation(updateExisting))
            {
                Log = null;
                try
                {
                    await _context.SaveChangesAsync(HttpContext.User.Identity.Name);
                    //await ImportCustomers();
                    return RedirectToAction("ImportValidationSucceeded");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception in ImportCustomers.ValidatingData");
                    Log = e.Message;
                    return RedirectToAction("ImportValidationFailed");
                }
            }
            else return RedirectToAction("ImportValidationFailed");
        }
        
        public ActionResult ImportValidationFailed()
        {
            _logger.LogInformation("ImportCustomers.ImportValidationFailed is called");
            _logger.LogDebug("Import validation failure reason is {Log}", Log);
            ViewBag.Log = Log;
            Log = null;
            return View();
        }

        public ActionResult ImportValidationSucceeded()
        {
            _logger.LogInformation("ImportCustomers.ImportValidationSucceeded is called");
            Log = null;
            return View();
        }

        public async Task<IActionResult> DownloadExcelDocument()
        {
            _logger.LogInformation("ImportCustomers.DownloadExcelDocument is called");
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string fileName = "ImportDataTemplate.xlsx";
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    int propertiesCount = 1;
                    List<Entity> entities = await _context.Entity.ToListAsync();
                    foreach(Entity entity in entities)
                    {
                        int entityProperties = await _context.EntityProperty.Where(ep => ep.EntityId == entity.Id).CountAsync();
                        propertiesCount = propertiesCount + entityProperties;
                    }

                    


                    IXLWorksheet worksheet =
                    workbook.Worksheets.Add("eLoginDataTemplate");
                    worksheet.Cell(1, 1).Value = "Property";
                    worksheet.Cell(2, 1).Value = "PropertyValidationRegex";
                    worksheet.Cell(3, 1).Value = "PropertyValidationHint";
                    worksheet.Cell(4, 1).Value = "IsEncrypted";
                    worksheet.Cell(5, 1).Value = "IsHashed";
                    worksheet.Cell(6, 1).Value = "IsUniqueIdentifier";
                    worksheet.Cell(7, 1).Value = "IsRequired";
                    worksheet.Cell(8, 1).Value = "Alias";
                    worksheet.Cell(9, 1).Value = "EntityName";
                    worksheet.Cell(10, 1).Value = "IsRequired";
                    worksheet.Cell(11, 1).Value = "EntityCategoryName";
                    worksheet.Cell(12, 1).Value = "ParentEntityCategoryName";
                    worksheet.Cell(13, 1).Value = "EntityRelatedToChannels";
                    worksheet.Cell(14, 1).Value = "PropertyUsedToLoginForChannels";
                    worksheet.Cell(15, 1).Value = "Customer Data";
                    //worksheet.Cell(13, 1).Style.Fill.SetBackgroundColor(XLColor.Blue);
                    worksheet.Column(1).Style.Font.Bold = true;
                    worksheet.Column(1).Style.Font.FontColor = XLColor.White;
                    IXLRange propertyRange = worksheet.Range(worksheet.Cell(1, 2).Address, worksheet.Cell(1, propertiesCount).Address);
                    IXLRange propertyValidationRegexRange = worksheet.Range(worksheet.Cell(2, 2).Address, worksheet.Cell(2, propertiesCount).Address);
                    IXLRange propertyValidationHintRange = worksheet.Range(worksheet.Cell(3, 2).Address, worksheet.Cell(3, propertiesCount).Address);
                    IXLRange propertyIsEncryptedRange = worksheet.Range(worksheet.Cell(4, 2).Address, worksheet.Cell(4, propertiesCount).Address);
                    IXLRange propertyIsHashedRange = worksheet.Range(worksheet.Cell(5, 2).Address, worksheet.Cell(5, propertiesCount).Address);
                    IXLRange propertyIsUniqueIdentifierRange = worksheet.Range(worksheet.Cell(6, 2).Address, worksheet.Cell(6, propertiesCount).Address);
                    IXLRange propertyIsRequiredRange = worksheet.Range(worksheet.Cell(7, 2).Address, worksheet.Cell(7, propertiesCount).Address);
                    IXLRange customerInstanceNameRange = worksheet.Range(worksheet.Cell(8, 2).Address, worksheet.Cell(8, propertiesCount).Address);
                    IXLRange entityNameRange = worksheet.Range(worksheet.Cell(9, 2).Address, worksheet.Cell(9, propertiesCount).Address);
                    IXLRange entityIsRequiredRange = worksheet.Range(worksheet.Cell(10, 2).Address, worksheet.Cell(10, propertiesCount).Address);
                    IXLRange entityCategoryNameRange = worksheet.Range(worksheet.Cell(11, 2).Address, worksheet.Cell(11, propertiesCount).Address);
                    IXLRange parentEntityCategoryRange = worksheet.Range(worksheet.Cell(12, 2).Address, worksheet.Cell(12, propertiesCount).Address);
                    IXLRange entityRelatedToChannelsRange = worksheet.Range(worksheet.Cell(13, 2).Address, worksheet.Cell(13, propertiesCount).Address);
                    IXLRange propertyUsedToLoginForChannelsRange = worksheet.Range(worksheet.Cell(14, 2).Address, worksheet.Cell(14, propertiesCount).Address);
                    IXLRange customerDataRange = worksheet.Range(worksheet.Cell(15, 2).Address, worksheet.Cell(15, propertiesCount).Address);


                    XLColor blueAccent1 = XLColor.FromHtml("#4F81BD");
                    XLColor blueAccent1_D25 = XLColor.FromHtml("#366092");
                    XLColor blueAccent1_L80 = XLColor.FromHtml("#DCE6F1");
                    XLColor blueAccent1_L60 = XLColor.FromHtml("#B8CCE4");
                    
                    
                    XLColor redAccent2 = XLColor.FromHtml("#C0504D");
                    XLColor redAccent2_D25 = XLColor.FromHtml("#963634");
                    XLColor redAccent2_L80 = XLColor.FromHtml("#F2DCDB");
                    XLColor redAccent2_L60 = XLColor.FromHtml("#E6B8B7");
                    XLColor oliveGreenAccent3 = XLColor.FromHtml("#76933C");
                    XLColor oliveGreenAccent3_L80 = XLColor.FromHtml("#EBF1DE");
                    XLColor black_L25 = XLColor.FromHtml("#404040");
                   

                    propertyRange.Style.Fill.SetBackgroundColor(blueAccent1_L80);
                    propertyValidationRegexRange.Style.Fill.SetBackgroundColor(blueAccent1_L60);
                    propertyValidationHintRange.Style.Fill.SetBackgroundColor(blueAccent1_L80);
                    propertyIsEncryptedRange.Style.Fill.SetBackgroundColor(blueAccent1_L60);
                    propertyIsHashedRange.Style.Fill.SetBackgroundColor(blueAccent1_L80);
                    propertyIsUniqueIdentifierRange.Style.Fill.SetBackgroundColor(blueAccent1_L60);
                    propertyIsRequiredRange.Style.Fill.SetBackgroundColor(blueAccent1_L80);
                    customerInstanceNameRange.Style.Fill.SetBackgroundColor(redAccent2_L80);
                    entityNameRange.Style.Fill.SetBackgroundColor(redAccent2_L60);
                    entityIsRequiredRange.Style.Fill.SetBackgroundColor(redAccent2_L80);
                    entityCategoryNameRange.Style.Fill.SetBackgroundColor(redAccent2_L60);
                    parentEntityCategoryRange.Style.Fill.SetBackgroundColor(redAccent2_L80);
                    entityRelatedToChannelsRange.Style.Fill.SetBackgroundColor(redAccent2_L60);
                    propertyUsedToLoginForChannelsRange.Style.Fill.SetBackgroundColor(oliveGreenAccent3_L80);

                    worksheet.Cell(1, 1).Style.Fill.SetBackgroundColor(blueAccent1);
                    worksheet.Cell(2, 1).Style.Fill.SetBackgroundColor(blueAccent1_D25);
                    worksheet.Cell(3, 1).Style.Fill.SetBackgroundColor(blueAccent1);
                    worksheet.Cell(4, 1).Style.Fill.SetBackgroundColor(blueAccent1_D25);
                    worksheet.Cell(5, 1).Style.Fill.SetBackgroundColor(blueAccent1);
                    worksheet.Cell(6, 1).Style.Fill.SetBackgroundColor(blueAccent1_D25);
                    worksheet.Cell(7, 1).Style.Fill.SetBackgroundColor(blueAccent1);
                    worksheet.Cell(8, 1).Style.Fill.SetBackgroundColor(redAccent2);
                    worksheet.Cell(9, 1).Style.Fill.SetBackgroundColor(redAccent2_D25);
                    worksheet.Cell(10, 1).Style.Fill.SetBackgroundColor(redAccent2);
                    worksheet.Cell(11, 1).Style.Fill.SetBackgroundColor(redAccent2_D25);
                    worksheet.Cell(12, 1).Style.Fill.SetBackgroundColor(redAccent2);
                    worksheet.Cell(13, 1).Style.Fill.SetBackgroundColor(redAccent2_D25);
                    worksheet.Cell(14, 1).Style.Fill.SetBackgroundColor(oliveGreenAccent3);
                    worksheet.Cell(15, 1).Style.Fill.SetBackgroundColor(black_L25);

                    IXLRange Titles = worksheet.Range(worksheet.Cell(1, 1).Address, worksheet.Cell(15, 1).Address);
                    IXLRange Data = worksheet.Range(worksheet.Cell(1, 2).Address, worksheet.Cell(50000, propertiesCount).Address);
                    Titles.Style.Font.Bold = true;
                    Titles.Style.Font.FontColor = XLColor.White;

                    worksheet.Columns().Width = 30;

                    worksheet.Cells().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cells().Style.Border.OutsideBorderColor = XLColor.FromHtml("#A6A6A6");
                    Titles.Cells().Style.Border.OutsideBorder = XLBorderStyleValues.None;

                    
                    int index = 1;
                    if (entities != null)
                    {
                        foreach (Entity entity in entities)
                        {
                            List<EntityProperty> entityProperties = await _context.EntityProperty.Where(ep => ep.EntityId == entity.Id).ToListAsync();
                            for (int pindex = 1; pindex <= entityProperties.Count; pindex++)
                            {
                                index++;
                                worksheet.Cell(1, index).Value = entityProperties[pindex - 1].Property.Name;
                                worksheet.Cell(2, index).Value = entityProperties[pindex - 1].Property.ValidationRegex;
                                worksheet.Cell(3, index).Value = entityProperties[pindex - 1].Property.ValidationHint;
                                worksheet.Cell(4, index).Value = entityProperties[pindex - 1].Property.IsEncrypted.Value;
                                worksheet.Cell(5, index).Value = entityProperties[pindex - 1].Property.IsHashed.Value;
                                worksheet.Cell(6, index).Value = entityProperties[pindex - 1].Property.IsUniqueIdentifier.Value;
                                worksheet.Cell(7, index).Value = entityProperties[pindex - 1].Property.IsRequired.Value;
                                worksheet.Cell(8, index).Value = "Alias for " + entity.EntityName;
                                worksheet.Cell(9, index).Value = entity.EntityName;
                                worksheet.Cell(10, index).Value = entity.IsRequired;
                                worksheet.Cell(11, index).Value = entity.EntityCategory.CategoryName;
                                if (entity.EntityCategory.ParentEntityCategoryId != null) worksheet.Cell(12, pindex + 1).Value = entity.EntityCategory.ParentEntityCategory.CategoryName;
                                List<ChannelEntity> entityChannels = await _context.ChannelEntity.Where(ec => ec.EntityId == entity.Id).ToListAsync();
                                if (entityChannels.Count() != 0)
                                {
                                    string ERC = "";
                                    foreach (ChannelEntity entityChannel in entityChannels)
                                    {
                                        if (ERC == "") ERC = ERC + entityChannel.IdentificationChannel.Channel;
                                        else ERC = ERC + "," + entityChannel.IdentificationChannel.Channel;
                                    }
                                    worksheet.Cell(13, index).Value = ERC;
                                }
                                List<ChannelLoginProperty> channelLoginProperties = await _context.ChannelLoginProperty.Where(clp => clp.PropertyId == entityProperties[pindex - 1].PropertyId).ToListAsync();
                                if (channelLoginProperties.Count() != 0)
                                {
                                    string PUTLFC = "";
                                    foreach (ChannelLoginProperty channelLoginProperty in channelLoginProperties)
                                    {
                                        if (PUTLFC == "") PUTLFC = PUTLFC + channelLoginProperty.IdentificationChannel.Channel;
                                        else PUTLFC = PUTLFC + "," + channelLoginProperty.IdentificationChannel.Channel;
                                    }
                                    worksheet.Cell(14, index).Value = PUTLFC;
                                }

                            }

                        }
                    }

                    IXLWorksheet colorSchemeWorksheet = workbook.Worksheets.Add("Color Scheme");

                    colorSchemeWorksheet.Cell(1, 1).Value = "Property Declaration Properties";
                    colorSchemeWorksheet.Cell(1, 1).Style.Fill.SetBackgroundColor(blueAccent1);

                    colorSchemeWorksheet.Cell(1, 2).Value = "Blue";
                    colorSchemeWorksheet.Cell(1, 2).Style.Fill.SetBackgroundColor(blueAccent1_L80);

                    colorSchemeWorksheet.Cell(2, 1).Value = "Entity Declaration Properties";
                    colorSchemeWorksheet.Cell(2, 1).Style.Fill.SetBackgroundColor(redAccent2);

                    colorSchemeWorksheet.Cell(2, 2).Value = "Red";
                    colorSchemeWorksheet.Cell(2, 2).Style.Fill.SetBackgroundColor(redAccent2_L80);

                    colorSchemeWorksheet.Cell(3, 1).Value = "Used to Login For Channels";
                    colorSchemeWorksheet.Cell(3, 1).Style.Fill.SetBackgroundColor(oliveGreenAccent3);

                    colorSchemeWorksheet.Cell(3, 2).Value = "Olive Green";
                    colorSchemeWorksheet.Cell(3, 2).Style.Fill.SetBackgroundColor(oliveGreenAccent3_L80);

                    colorSchemeWorksheet.Cell(4, 1).Value = "Customer Data Begining Row";
                    colorSchemeWorksheet.Cell(4, 1).Style.Fill.SetBackgroundColor(black_L25);

                    colorSchemeWorksheet.Cell(4, 2).Value = "Light Black";
                    

                    colorSchemeWorksheet.Columns().Width = 30;
                    IXLRange Keys = colorSchemeWorksheet.Range(colorSchemeWorksheet.Cell(1, 1).Address, colorSchemeWorksheet.Cell(4, 1).Address);
                    Keys.Style.Font.Bold = true;
                    Keys.Style.Font.FontColor = XLColor.White;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content, contentType, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in ImportCustomers.DownloadExcelDocument");
                return null;
            }
        }
    }
}
