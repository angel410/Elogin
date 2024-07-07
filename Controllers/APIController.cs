using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Syncfusion.EJ2.Linq;

namespace eLogin.Controllers
{
    [AllowAnonymous]
    [Route("api")] // api/[controller]
    [ApiController]
    public class APIController : ControllerBase
    {
        private readonly DatabaseContext _context;
        private LicenseCheck LC;
        private readonly ILogger<APIController> _logger;

        //Constracture
        public APIController(DatabaseContext context, LicenseCheck licenseCheck, ILogger<APIController> logger)
        {
            _context = context;
            LC = licenseCheck;
            _logger = logger;
        }

        public long UnixTimeNow()
        {
            _logger.LogDebug("API.UnixTimeNow is called");
            var UnixTimeNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            _logger.LogDebug("UnixTimeNow is {UnixTimeNow}", UnixTimeNow);
            return UnixTimeNow;
        }

        public long UnixTimeGap(long cepoc)
        {
            _logger.LogDebug("API.UnixTimeGap is called with {cepoc}", cepoc);
            var EpocGap = cepoc - UnixTimeNow();
            if (EpocGap < 0) EpocGap = UnixTimeNow() - cepoc;
            _logger.LogDebug("EpocGap is {EpocGap}", EpocGap);
            return EpocGap;
        }

        public bool UnixGapValidator(long requestTime, int allowedDelay)
        {
            _logger.LogDebug("API.UnixGapValidator is called with {requestTime} and {allowedDelay} ", requestTime, allowedDelay);
            var isUnixGapValid = UnixTimeGap(requestTime) <= allowedDelay && UnixTimeGap(requestTime) >= 0;
            _logger.LogInformation("UnixGapValidator output is {isUnixGapValid}", isUnixGapValid);
            if (isUnixGapValid) return true;
            return false;
        }

        public async Task<bool> SessionValid(Session session, DateTime requestTime)
        {
            _logger.LogInformation("API.SessionValid is called with {@session} and {requestTime} ", session, requestTime);
            int sessionTimeout = 60;
            _logger.LogDebug("Default sessionTimeout is {sessionTimeout}", sessionTimeout);
            _logger.LogDebug("Obtaining sessionTimeout from systemSettings");
          //  var  systemSettings = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Session Timeout").;
            var systemSettings= _context.SystemSetting.Where(s => s.SettingName == "Session Timeout").Select(t => t.Value).SingleOrDefaultAsync();



            _logger.LogDebug("systemSettings sessionTimeout is {systemSettings.Value}", systemSettings);
            //if (systemSettings != null)
            //{
            //    sessionTimeout = Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettings.Value, Cryptography.Operation.Decrypt));
            //    _logger.LogDebug("sessionTimeout is {sessionTimeout}", sessionTimeout);
            //}
            if ((requestTime - session.LastActivity).Seconds <= sessionTimeout)
            {
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Called at the beginning of every session to verify the channel and obtain a session id
        /// </summary>
        /// <param name="request"></param>
        /// <returns>session id</returns>
        [HttpPost]
        [Route("Handshake")]
        public async ValueTask<IActionResult> Handshake([FromBody] Request<HandshakeRequest> request)
        {
            _logger.LogInformation("Received Handshake Request");
            
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);

                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }

                _logger.LogDebug("Saving request in database");
                await _context.AddAsync(request);

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                _logger.LogDebug("ICKey="+ ICKey);

                request.Decrypt(ICKey);
                _logger.LogDebug("request.Payload= "+ request.Payload);
                // logWrite.LogMessageToFile("item" + JsonSerializer.Serialize(item));

                HandshakeRequest handshakeRequest = request.Payload;

                _logger.LogDebug("Verifying request channel id with decrypted payload channel id");
                if (request.ChannelId != handshakeRequest.ChannelId)
                {
                    _logger.LogError("Request channel id does not match decrypted payload channel id");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }
                _logger.LogDebug("Request channel id matches decrypted payload channel id");

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                int AllowedRequestDelay = Convert.ToInt32( await _context.SystemSetting.Where(s => s.SettingName == "Allowed Request Delay").Select(t => t.Value).SingleOrDefaultAsync()) ;
                //if (AllowedRequestDelay != null)
                //{
                //    allowedDelay = Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, AllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                //    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                //}

                _logger.LogDebug("Validating unix gap using request UnixTime {handshakeRequest.UnixTime} and allowedDelay {allowedDelay}", handshakeRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(handshakeRequest.UnixTime, AllowedRequestDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogInformation("Request is valid");
                _logger.LogInformation("Opening a new session");
                // request is valId. will open session
                Session session = new Session();
                session.IdentificationChannelId = handshakeRequest.ChannelId;
                session.DateTime = DateTime.UtcNow;
                session.LastActivity = DateTime.UtcNow;

                await _context.AddAsync(session);
                await _context.SaveChangesAsync();

                var serializerSettings = new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.None };

                //_logger.LogDebug("Session info is {@session}", JsonConvert.SerializeObject(session, serializerSettings));
                _logger.LogDebug("Session info is {@session}", session);
                //_logger.LogDebug("Session info is {session}", JsonConvert.SerializeObject(session));

                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                //SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                var recordDecryptedRequests = await _context.SystemSetting.Where(s => s.SettingName == "Record Decrypted Requests").Select(t=>t.Value).SingleOrDefaultAsync();

                if (recordDecryptedRequests != null)
                {
                    //if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    //{
                    //    _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                    //    request.Payload = null;
                    //}
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<HandshakeResponse> response = new Response<HandshakeResponse>();
                Response<HandshakeResponse> savedResponse = new Response<HandshakeResponse>();
                HandshakeResponse handshakeResponse = new HandshakeResponse();

                handshakeResponse.SessionId = session.Id;
                handshakeResponse.RequestId = handshakeRequest.Id;
                


                response.RequestId = request.Id;
                response.Payload = handshakeResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;

                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    //if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    //{
                    //    _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                    //    savedResponse.Payload = null;
                    //}
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Returning response {@response}", response);
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Exception in Handshake");
                return BadRequest();
            }
        }

        /// <summary>
        /// Called to register a new customer
        /// </summary>
        /// <param name="request"></param>
        /// <returns>customerId</returns>
        [HttpPost]
        [Route("Register")]//reviewed
        public async Task<IActionResult> Register([FromBody] Request<RegisterRequest> request)
        {
            _logger.LogInformation("Received Register Request");
            try
            {
                var systemSettingsAllowedRequestDelay = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");

                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating RegisterRequest from decypted Payload");
                RegisterRequest registerRequest = request.Payload;

                _logger.LogDebug("Initializing isPasswordValid with false");
                bool isPasswordValid = false;

                _logger.LogDebug("Checking if Channel Password has a regex.");
                if (!string.IsNullOrEmpty(IdentificationChannel.PasswordValidationRegex))
                {
                    _logger.LogDebug("Channel Password has a regex of value: {PasswordValidationRegex}", IdentificationChannel.PasswordValidationRegex);
                    Regex regex = new Regex(IdentificationChannel.PasswordValidationRegex);
                    _logger.LogDebug("Checking if password matched PasswordValidationRegex");
                    if (regex.IsMatch(registerRequest.Password))
                    {
                        isPasswordValid = true;
                        _logger.LogDebug("PasswordValidationRegex matched changed isPasswordValid to {isPasswordValid}", isPasswordValid);
                    }
                }
                else isPasswordValid = true;
                _logger.LogDebug("Channel Password does not have a regex value, hence changed isPasswordValid to {isPasswordValid}", isPasswordValid);

                _logger.LogDebug("Hashing Password and overwritting it with the Hash value");
                registerRequest.Password = Cryptography.Hash(registerRequest.Password);
                request.Payload.Password = registerRequest.Password;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = registerRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
               // SystemSetting systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32( systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {registerRequest.UnixTime} and allowedDelay {allowedDelay}", registerRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(registerRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

               string recordDecryptedRequests = Convert.ToString(  _context.SystemSetting.Where(s => s.SettingName == "Record Decrypted Requests").Select(t => t.Value).SingleOrDefaultAsync());

                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<RegisterResponse> response = new Response<RegisterResponse>();
                Response<RegisterResponse> savedResponse = new Response<RegisterResponse>();
                RegisterResponse registerResponse = new RegisterResponse();

                registerResponse.RequestId = registerRequest.Id;

                if (!LC.Check().isValid)
                {
                    _logger.LogError("License Validation failed.");
                    _logger.LogError("Rejecting request");
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "eLogin License Validation Failed";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }
                

                if (registerRequest.IdentifierEntityId == null)
                {
                    _logger.LogInformation("IdentifierEntityId is not set in request. Will use DefaultIdentifierEntityId: {IdentificationChannel.DefaultIdentifierEntityId}", IdentificationChannel.DefaultIdentifierEntityId);
                    registerRequest.IdentifierEntityId = IdentificationChannel.DefaultIdentifierEntityId;
                }

                Guid entityId = (Guid)registerRequest.IdentifierEntityId;
                Entity entity = await _context.Entity.SingleOrDefaultAsync(p => p.Id == entityId);

                if (entity == null)
                {
                    _logger.LogError("Failed to find a matching entity for {entityId}", entityId);
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Invalid Identification Entity";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (registerRequest.IdentifierPropertyId == null)
                {
                    _logger.LogInformation("IdentifierPropertyId is not set in request. Will use IdentifierPropertyId: {IdentificationChannel.DefaultIdentifierPropertyId}", IdentificationChannel.DefaultIdentifierPropertyId);
                    registerRequest.IdentifierPropertyId = IdentificationChannel.DefaultIdentifierPropertyId;
                }

                Guid propertyId = (Guid) registerRequest.IdentifierPropertyId;
                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == propertyId);

                if (property == null)
                {
                    _logger.LogError("Failed to find a matching property for {propertyId}", propertyId);
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Invalid Identification Property";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                ChannelLoginProperty channelLoginProperty = await _context.ChannelLoginProperty.SingleOrDefaultAsync(clp => clp.IdentificationChannelId == IdentificationChannel.Id && clp.PropertyId == propertyId);

                if (channelLoginProperty == null)
                {
                    _logger.LogError("Invalid Login Property for {IdentificationChannel.Channel}", IdentificationChannel.Channel);
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Invalid Login Property for " + IdentificationChannel.Channel;


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                EntityProperty entityProperty = await _context.EntityProperty.SingleOrDefaultAsync(ep => ep.EntityId == entityId && ep.PropertyId == propertyId);

                if (entityProperty == null)
                {
                    _logger.LogError("Property does not belong to entity.");
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Property does not belong to entity.";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!property.IsUniqueIdentifier.Value)
                {
                    _logger.LogError("Identifier Property is not a Unique Identifier");
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Identifier Property is not a Unique Identifier";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (registerRequest.EntityInstanceName == null)
                {
                    registerRequest.EntityInstanceName = entity.EntityName + " Default Alias";
                    _logger.LogDebug("EntityInstanceName not set. Setting it to {registerRequest.EntityInstanceName}", registerRequest.EntityInstanceName);
                }

                if (!String.IsNullOrEmpty(property.ValidationRegex))
                {
                    Regex regex = new Regex(property.ValidationRegex);

                    if (!regex.IsMatch(registerRequest.Identifier))
                    {
                        _logger.LogError("Invalid value for {property.Name}. Validation Hint: {property.ValidationHint}", property.Name, property.ValidationHint);
                        registerResponse.IsSuccess = false;
                        registerResponse.FailureReason = "Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint;


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = registerResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                        if (recordDecryptedRequests != null)
                        {
                            if (recordDecryptedRequests != "true")
                            {
                                _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                                savedResponse.Payload = null;
                            }
                            else
                            {
                                _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }

                }

                if (property.IsEncrypted.Value)
                {
                    registerRequest.Identifier = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(registerRequest.Identifier)), Cryptography.Operation.Encrypt);
                }
                else if (property.IsHashed.Value)
                {
                    registerRequest.Identifier = Cryptography.Hash(registerRequest.Identifier);
                }

                CustomerInfoValue customerInfoValue1 = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == registerRequest.Identifier);
                if(customerInfoValue1 != null)
                {
                    _logger.LogError("Identifier already used");
                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Identifier already used";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!isPasswordValid)
                {

                    registerResponse.IsSuccess = false;
                    registerResponse.FailureReason = "Invalid Password. " + IdentificationChannel.PasswordValidationHint;


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = registerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Customer customer = new Customer();
                customer.IsLocked = false;

                _logger.LogDebug("Adding new customer record: {customer}", customer);

                await _context.AddAsync(customer);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Registeration request adding new customer record.");
                EntityInstance entityInstance = new EntityInstance();
                entityInstance.CustomerId = customer.Id;
                entityInstance.EntityId = (Guid) registerRequest.IdentifierEntityId;
                entityInstance.EntityInstanceName = registerRequest.EntityInstanceName;

                _logger.LogDebug("Adding new entityInstance record: {entityInstance}", entityInstance);

                await _context.AddAsync(entityInstance);
                await _context.SaveChangesAsync();

                CustomerInfoValue customerInfoValue = new CustomerInfoValue();
                customerInfoValue.CustomerId = customer.Id;
                customerInfoValue.EntityInstanceId = entityInstance.Id;

                customerInfoValue.PropertyId = propertyId;

                customerInfoValue.Value = registerRequest.Identifier;

                _logger.LogDebug("Adding new customerInfoValue record: {customerInfoValue}", customerInfoValue);

                await _context.AddAsync(customerInfoValue);
                await _context.SaveChangesAsync();

                CustomerPassword customerPassword = new CustomerPassword();
                customerPassword.CustomerId = customer.Id;
                customerPassword.IdentificationChannelId = IdentificationChannel.Id;
                customerPassword.Password = registerRequest.Password;
                if(IdentificationChannel.PasswordValidityDays > 0)
                {
                    customerPassword.ExpiryDateTime = DateTime.UtcNow.AddDays(IdentificationChannel.PasswordValidityDays);
                }

                _logger.LogDebug("Adding new customerPassword record: {customerPassword}", customerPassword);

                await _context.AddAsync(customerPassword);
                await _context.SaveChangesAsync();

                registerResponse.IsSuccess = true;
                registerResponse.FailureReason = "";
                registerResponse.CustomerId = customer.Id;


                response.RequestId = request.Id;
                response.Payload = registerResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();


                _logger.LogInformation("Registrations succeeded. Returning {response}", response);
                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in Registration");
                return BadRequest();
            }
            
        }


        /// <summary>
        /// Used to login
        /// </summary>
        /// <param name="request"></param>
        /// <returns>customerId</returns>
        [HttpPost]
        [Route("Login")] //reviewed
        public async ValueTask<IActionResult> Login([FromBody] Request<LoginRequest> request)
        {
            _logger.LogInformation("Received Login Request");
            try
            {
                var systemSettingsAllowedRequestDelay = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");

                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating LoginRequest from decypted Payload");
                LoginRequest loginRequest = request.Payload;
                _logger.LogDebug("hashing password");
                loginRequest.Password = Cryptography.Hash(loginRequest.Password);


                _logger.LogDebug("Verifying Session");
                Guid sessionId = loginRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
               // var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {registerRequest.UnixTime} and allowedDelay {allowedDelay}", loginRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(loginRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

               // SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                var recordDecryptedRequests = Convert.ToString(await _context.SystemSetting.Where(s => s.SettingName == "Record Decrypted Requests").Select(t => t.Value).SingleOrDefaultAsync());

                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse loginResponse = new GeneralResponse();

                loginResponse.RequestId = loginRequest.Id;

                loginResponse.RequestType = "Login";

                _logger.LogInformation("Looking for a matching encrypted identifier");
                
                

                var EncryptedIdentifier = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(loginRequest.Identifier)), Cryptography.Operation.Encrypt);
                CustomerInfoValue customerInfoValue = await (from civ in _context.CustomerInfoValue
                                                                      join p in _context.Property on civ.PropertyId equals p.Id
                                                                      where p.IsUniqueIdentifier == true && p.IsEncrypted == true

                                                                      select new CustomerInfoValue { Id = civ.Id, CustomerId = civ.CustomerId, EntityInstanceId = civ.EntityInstanceId, IsDeleted = civ.IsDeleted, PropertyId = civ.PropertyId, Value = civ.Value }
                                                         ).SingleOrDefaultAsync(c => c.Value == EncryptedIdentifier);
                if(customerInfoValue == null)
                {
                    _logger.LogInformation("Looking for a matching plain text identifier");
                    CustomerInfoValue customerInfoValuePlain = await (from civ in _context.CustomerInfoValue
                                                                      join p in _context.Property on civ.PropertyId equals p.Id
                                                                      where p.IsUniqueIdentifier == true && p.IsEncrypted == false && p.IsHashed == false

                                                                      select new CustomerInfoValue { Id = civ.Id, CustomerId = civ.CustomerId, EntityInstanceId = civ.EntityInstanceId, IsDeleted = civ.IsDeleted, PropertyId = civ.PropertyId, Value = civ.Value }
                                                             ).SingleOrDefaultAsync(c => c.Value == loginRequest.Identifier);
                    if(customerInfoValuePlain != null)
                    {
                        customerInfoValue = customerInfoValuePlain;
                    }
                    else
                    {
                        _logger.LogInformation("Looking for a matching hashed identifier");
                        var HashedIdentifier = Cryptography.Hash(loginRequest.Identifier);
                        CustomerInfoValue customerInfoValueHashed = await (from civ in _context.CustomerInfoValue
                                                                           join p in _context.Property on civ.PropertyId equals p.Id
                                                                           where p.IsUniqueIdentifier == true && p.IsHashed == true

                                                                           select new CustomerInfoValue { Id = civ.Id, CustomerId = civ.CustomerId, EntityInstanceId = civ.EntityInstanceId, IsDeleted = civ.IsDeleted, PropertyId = civ.PropertyId, Value = civ.Value }
                                                             ).SingleOrDefaultAsync(c => c.Value == EncryptedIdentifier);
                        if (customerInfoValueHashed != null)
                        {
                            customerInfoValue = customerInfoValueHashed;
                        }
                    }
                }

                
                if (customerInfoValue == null)
                {
                    _logger.LogError("Could not finad a matching identifier");
                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Invalid Identifier or Password";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    
                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Checking if channel have access to the property");

                ChannelLoginProperty channelLoginProperty = await _context.ChannelLoginProperty.SingleOrDefaultAsync(clp => clp.IdentificationChannelId == IdentificationChannel.Id && clp.PropertyId == customerInfoValue.PropertyId);

                if (channelLoginProperty == null)
                {
                    _logger.LogError("Invalid Login Property for  {IdentificationChannel.Channel}", IdentificationChannel.Channel);
                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Invalid Login Property for " + IdentificationChannel.Channel;


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerInfoValue.CustomerId);

                CustomerPassword customerPassword = await _context.CustomerPassword.Where(c => c.CustomerId == customer.Id && c.IdentificationChannelId == request.ChannelId).OrderByDescending(c=>c.Id).FirstOrDefaultAsync();
                _logger.LogDebug("Checking if password is set for the matching customerId and the given channel");

                if (customerPassword == null)
                {
                    _logger.LogError("Password is not set for the matching customerId and the given channel");
                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Password not set for " + IdentificationChannel.Channel;


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Validating password validity");
                if((IdentificationChannel.PasswordValidityDays > 0 && (DateTime.UtcNow >= customerPassword.ExpiryDateTime)) || customerPassword.IsExpired)
                {
                    _logger.LogError("Password expired");
                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Password expired";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                CustomerLoginAttempt customerLoginAttempt = new CustomerLoginAttempt();
                _logger.LogDebug("Checking if customer is locked");
                if (customer.IsLocked)
                {
                    _logger.LogError("Customer account is locked");
                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Customer Account is Locked";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    customerLoginAttempt.IdentificationChannelId = request.ChannelId;
                    customerLoginAttempt.CustomerId = customer.Id;
                    customerLoginAttempt.IsSuccess = false;
                    customerLoginAttempt.LockedAccount = true;

                    await _context.AddAsync(customerLoginAttempt);
                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                //if (customerPassword.IsExpired)
                //{
                //    loginResponse.IsSuccess = false;
                //    loginResponse.FailureReason = "Password Expired";


                //    session.LastActivity = DateTime.UtcNow;
                //    await _context.SaveChangesAsync();

                //    response.RequestId = request.Id;
                //    response.Payload = loginResponse;
                //    response.IV = Convert.ToBase64String(responseCipher.IV);
                //    response.Encrypt(ICKey);

                //    savedResponse = response;
                //    if (recordDecryptedRequests != null)
                //    {
                //        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //        {
                //            savedResponse.Payload = null;
                //        }
                //    }
                //    else savedResponse.Payload = null;
                    
                //    customerLoginAttempt.IdentificationChannelId = request.ChannelId;
                //    customerLoginAttempt.CustomerId = customer.Id;
                //    customerLoginAttempt.IsSuccess = false;
                //    customerLoginAttempt.IsExpired = true;

                //    await _context.AddAsync(customerLoginAttempt);
                //    await _context.AddAsync(savedResponse);
                //    await _context.SaveChangesAsync();

                //    return Ok(JsonConvert.SerializeObject(response));
                //}

                if (customerPassword.Password != loginRequest.Password)
                {
                    _logger.LogError("Password mismatched");
                    SystemSetting DailyAllowedIncorrectPasswordAttempts = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Daily Allowed Incorrect Password Attempts");
                    if(DailyAllowedIncorrectPasswordAttempts != null)
                    {
                        int allowedDailyAttempts = Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, DailyAllowedIncorrectPasswordAttempts.Value, Cryptography.Operation.Decrypt));
                        _logger.LogDebug("Daily Allowed Incorrect Password Attempts is {allowedDailyAttempts}", allowedDailyAttempts);
                        int failedAttemptsToday = await _context.CustomerLoginAttempt.CountAsync(c => c.DateTime.Date == DateTime.UtcNow.Date && c.IncorrectPassword == true);
                        _logger.LogDebug("Todays Incorrect Password Attempts is {failedAttemptsToday}", failedAttemptsToday);
                        if (failedAttemptsToday >= allowedDailyAttempts)
                        {
                            _logger.LogError("Maximum daily limit of incorrect password attempts has reached. Locking customer account.");
                            customer.IsLocked = true;
                        }
                    }

                    SystemSetting WeeklyAllowedIncorrectPasswordAttempts = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Weekly Allowed Incorrect Password Attempts");
                    if (WeeklyAllowedIncorrectPasswordAttempts != null)
                    {
                        int allowedWeeklyAttempts = Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, WeeklyAllowedIncorrectPasswordAttempts.Value, Cryptography.Operation.Decrypt));
                        _logger.LogDebug("Weekly Allowed Incorrect Password Attempts is {allowedWeeklyAttempts}", allowedWeeklyAttempts);
                        int failedAttemptsThisWeek = await _context.CustomerLoginAttempt.CountAsync(c => c.DateTime >= DateTime.UtcNow.AddDays(-7) && c.IncorrectPassword == true);
                        _logger.LogDebug("This week's Incorrect Password Attempts is {failedAttemptsThisWeek}", failedAttemptsThisWeek);
                        if (failedAttemptsThisWeek >= allowedWeeklyAttempts)
                        {
                            _logger.LogError("Maximum weekly limit of incorrect password attempts has reached. Locking customer account.");
                            customer.IsLocked = true;
                        }
                    }

                    loginResponse.IsSuccess = false;
                    loginResponse.FailureReason = "Invalid Identifier or Password";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = loginResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    customerLoginAttempt.IdentificationChannelId = request.ChannelId;
                    customerLoginAttempt.CustomerId = customer.Id;
                    customerLoginAttempt.IsSuccess = false;
                    customerLoginAttempt.IncorrectPassword = true;

                    await _context.AddAsync(customerLoginAttempt);
                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogInformation("Login succeeded");

                loginResponse.IsSuccess = true;
                loginResponse.FailureReason = "";
                loginResponse.CustomerId = customer.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = loginResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                customerLoginAttempt.IdentificationChannelId = request.ChannelId;
                customerLoginAttempt.CustomerId = customer.Id;
                customerLoginAttempt.IsSuccess = true;

                await _context.AddAsync(customerLoginAttempt);
                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in Login");
                return BadRequest();
            }
            
        }

        /// <summary>
        /// Used to Reset Customer Channel Password without knowing old password
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("ResetCustomerChannelPassword")]
        public async ValueTask<IActionResult> ResetCustomerChannelPassword([FromBody] Request<ResetCustomerChannelPasswordRequest> request)
        {
            _logger.LogInformation("Received ResetCustomerChannelPassword Request");
            try
            {
                var systemSettingsAllowedRequestDelay = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");
                var systemSettingDifferentRecentPasswords = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Different Recent Passwords");

                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating ResetCustomerChannelPasswordRequest from decypted Payload");
                ResetCustomerChannelPasswordRequest resetCustomerChannelPasswordRequest = request.Payload;

                _logger.LogDebug("Checking if password is valid");
                bool isPasswordValid = false;
                _logger.LogDebug("resetCustomerChannelPasswordRequest.Password " + resetCustomerChannelPasswordRequest.Password);
                _logger.LogDebug("IdentificationChannel.PasswordValidationRegex " + IdentificationChannel.PasswordValidationRegex);
                if (IdentificationChannel.PasswordValidationRegex != null)
                {
                   

                    Regex regex = new Regex(IdentificationChannel.PasswordValidationRegex);
                    if (regex.IsMatch(resetCustomerChannelPasswordRequest.Password))
                    {
                        isPasswordValid = true;
                    }
                }

                if(isPasswordValid)
                {
                    _logger.LogDebug("Password is valid");
                }
                else
                {
                    _logger.LogError("Password is not valid");
                }

                _logger.LogDebug("Hashing password");
                resetCustomerChannelPasswordRequest.Password = Cryptography.Hash(resetCustomerChannelPasswordRequest.Password);

                _logger.LogDebug("Verifying Session");
                Guid sessionId = resetCustomerChannelPasswordRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                //var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {registerRequest.UnixTime} and allowedDelay {allowedDelay}", resetCustomerChannelPasswordRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(resetCustomerChannelPasswordRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                var recordDecryptedRequests = await _context.SystemSetting.Where(s => s.SettingName == "Record Decrypted Requests").Select(t=>t.Value).SingleOrDefaultAsync();
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = resetCustomerChannelPasswordRequest.Id;

                generalResponse.RequestType = "ResetCustomerChannelPasswordRequest";

                _logger.LogDebug("Searching for customer with id = {resetCustomerChannelPasswordRequest.CustomerId}", resetCustomerChannelPasswordRequest.CustomerId);
                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == resetCustomerChannelPasswordRequest.CustomerId);

                if (customer == null)
                {
                    _logger.LogError("Invalid Customer Id");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Obtaining old passwords");
                List<CustomerPassword> oldPasswords = await _context.CustomerPassword.Where(c => c.CustomerId == customer.Id && c.IdentificationChannelId == request.ChannelId).OrderByDescending(c=>c.CreationDateTime).ToListAsync();

                int differentRecentPasswords = 1;
                _logger.LogDebug("Initialized Different Recent Passwords value as {differentRecentPasswords}", differentRecentPasswords);

                _logger.LogDebug("Trying to obtain Different Recent Passwords value from db.systemSetting");
                if (systemSettingDifferentRecentPasswords != null)
                {
                    differentRecentPasswords = Convert.ToInt32( systemSettingDifferentRecentPasswords.Value);// Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingDifferentRecentPasswords.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {differentRecentPasswords}", differentRecentPasswords);
                }

                _logger.LogDebug("Checking if customer is locked");
                if (customer.IsLocked)
                {
                    _logger.LogError("Customer account is locked");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Customer Account is Locked";


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!isPasswordValid)
                {
                    _logger.LogError("Password is not valid");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Password. " + IdentificationChannel.PasswordValidationHint;


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                for (int i = 0; i < differentRecentPasswords; i++)
                {
                    if (resetCustomerChannelPasswordRequest.Password == oldPasswords[i].Password)
                    {
                        _logger.LogError("Password is equivilant to old password");
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "New password must not be similar to old password.";


                        session.LastActivity = DateTime.UtcNow;
                        //await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                        if (recordDecryptedRequests != null)
                        {
                            if (recordDecryptedRequests != "true")
                            {
                                _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                                savedResponse.Payload = null;
                            }
                            else
                            {
                                _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }
                }


                if (oldPasswords != null)
                {
                    _logger.LogDebug("Expiring old passwords related for the same channel");
                    foreach (CustomerPassword oldPassword in oldPasswords)
                    {
                        if (!oldPassword.IsExpired)
                        {
                            oldPassword.IsExpired = true;
                            oldPassword.ExpiryDateTime = DateTime.UtcNow;
                        }
                    }
                    
                    //await _context.SaveChangesAsync();
                }

                CustomerPassword customerPassword = new CustomerPassword();
                customerPassword.CustomerId = resetCustomerChannelPasswordRequest.CustomerId;

                customerPassword.IdentificationChannelId = request.ChannelId;
                customerPassword.IsExpired = false;
                customerPassword.Password = resetCustomerChannelPasswordRequest.Password;
                if (IdentificationChannel.PasswordValidityDays > 0)
                {
                    customerPassword.ExpiryDateTime = DateTime.UtcNow.AddDays(IdentificationChannel.PasswordValidityDays);
                    _logger.LogDebug("Setting new password expiry date as {customerPassword.ExpiryDateTime}", customerPassword.ExpiryDateTime);
                }

                await _context.AddAsync(customerPassword);
                //await _context.SaveChangesAsync();

                _logger.LogInformation("ResetCustomerChannelPassword Succeeded");
                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";
                generalResponse.CustomerId = customer.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in ResetCustomerChannelPassword");
                return BadRequest();
            }
            
        }

        /// <summary>
        /// Used to Update Customer Channel Password using old password
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("UpdateCustomerChannelPassword")]
        public async ValueTask<IActionResult> UpdateCustomerChannelPassword([FromBody] Request<UpdateCustomerChannelPasswordRequest> request)
        {
            _logger.LogInformation("Received UpdateCustomerChannelPassword Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateCustomerChannelPasswordRequest from decypted Payload");
                UpdateCustomerChannelPasswordRequest updateCustomerChannelPasswordRequest = request.Payload;

                _logger.LogDebug("Checking if password is valid");
                bool isPasswordValid = false;

                if (!string.IsNullOrEmpty( IdentificationChannel.PasswordValidationRegex))
                {
                    Regex regex = new Regex(IdentificationChannel.PasswordValidationRegex);
                    if (regex.IsMatch(updateCustomerChannelPasswordRequest.Password))
                    {
                        isPasswordValid = true;
                    }
                }

                if (isPasswordValid)
                {
                    _logger.LogDebug("Password is valid");
                }
                else
                {
                    _logger.LogError("Password is not valid");
                }

                _logger.LogDebug("Hashing passwords");
                updateCustomerChannelPasswordRequest.Password = Cryptography.Hash(updateCustomerChannelPasswordRequest.Password);
                updateCustomerChannelPasswordRequest.OldP = Cryptography.Hash(updateCustomerChannelPasswordRequest.OldP);

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateCustomerChannelPasswordRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateCustomerChannelPasswordRequest.UnixTime} and allowedDelay {allowedDelay}", updateCustomerChannelPasswordRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateCustomerChannelPasswordRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateCustomerChannelPasswordRequest.Id;

                generalResponse.RequestType = "UpdateCustomerChannelPasswordRequest";

                _logger.LogDebug("Searching for customer with id = {updateCustomerChannelPasswordRequest.CustomerId}", updateCustomerChannelPasswordRequest.CustomerId);
                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == updateCustomerChannelPasswordRequest.CustomerId);

                if (customer == null)
                {
                    _logger.LogError("Invalid Customer Id");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Obtaining old passwords");
                List<CustomerPassword> oldPasswords = await _context.CustomerPassword.Where(c => c.CustomerId == customer.Id && c.IdentificationChannelId == request.ChannelId).OrderByDescending(c => c.CreationDateTime).ToListAsync();
                CustomerPassword oldPassword = oldPasswords.FirstOrDefault();

                int differentRecentPasswords = 1;
                _logger.LogDebug("Initialized Different Recent Passwords value as {differentRecentPasswords}", differentRecentPasswords);

                _logger.LogDebug("Trying to obtain Different Recent Passwords value from db.systemSetting");
                SystemSetting systemSettingDifferentRecentPasswords = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Different Recent Passwords");
                if (systemSettingDifferentRecentPasswords != null)
                {
                    differentRecentPasswords = Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingDifferentRecentPasswords.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {differentRecentPasswords}", differentRecentPasswords);
                }

                if (customer.IsLocked)
                {
                    _logger.LogError("Customer account is locked");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Customer Account is Locked";


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (oldPassword == null)
                {
                    _logger.LogError("Old password not found");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Old password not found";


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (oldPassword.Password != updateCustomerChannelPasswordRequest.OldP)
                {
                    _logger.LogError("Invalid old password");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid old password";


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!isPasswordValid)
                {
                    _logger.LogError("Password is not valid");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid new password. " + IdentificationChannel.PasswordValidationHint;


                    session.LastActivity = DateTime.UtcNow;
                    //await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                for (int i = 0; i < differentRecentPasswords; i++)
                {
                    if (updateCustomerChannelPasswordRequest.Password == oldPasswords[i].Password)
                    {
                        _logger.LogError("Password is equivilant to old password");
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "New password must not be similar to old password.";


                        session.LastActivity = DateTime.UtcNow;
                        //await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                                savedResponse.Payload = null;
                            }
                            else
                            {
                                _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }
                }

                if (oldPasswords != null)
                {
                    _logger.LogDebug("Expiring old passwords related for the same channel");
                    foreach (CustomerPassword OP in oldPasswords)
                    {
                        if (!OP.IsExpired)
                        {
                            OP.IsExpired = true;
                            OP.ExpiryDateTime = DateTime.UtcNow;
                        }
                    }

                    //await _context.SaveChangesAsync();
                }



                CustomerPassword customerPassword = new CustomerPassword();
                customerPassword.CustomerId = updateCustomerChannelPasswordRequest.CustomerId;

                customerPassword.IdentificationChannelId = request.ChannelId;
                customerPassword.IsExpired = false;
                customerPassword.Password = updateCustomerChannelPasswordRequest.Password;
                if (IdentificationChannel.PasswordValidityDays > 0)
                {
                    customerPassword.ExpiryDateTime = DateTime.UtcNow.AddDays(IdentificationChannel.PasswordValidityDays);
                    _logger.LogDebug("Setting new password expiry date as {customerPassword.ExpiryDateTime}", customerPassword.ExpiryDateTime);
                }

                await _context.AddAsync(customerPassword);
                //await _context.SaveChangesAsync();

                _logger.LogInformation("UpdateCustomerChannelPassword Succeeded");
                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";
                generalResponse.CustomerId = customer.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in UpdateCustomerChannelPassword");
                return BadRequest();
            }


        }

        /// <summary>
        /// Returns all customer information
        /// </summary>
        /// <param name="request"></param>
        /// <returns>custopmer info</returns>
        [HttpPost] //reviewed
        [Route("GetCustomerInfo")]
        public async ValueTask<IActionResult> GetCustomerInfo([FromBody] Request<GetCustomerInfoRequest> request)
        {
            _logger.LogInformation("Received GetCustomerInfo Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating GetCustomerInfoRequest from decypted Payload");
                GetCustomerInfoRequest getCustomerInfoRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = getCustomerInfoRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {getCustomerInfoRequest.UnixTime} and allowedDelay {allowedDelay}", getCustomerInfoRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(getCustomerInfoRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        request.Payload = null;
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GetCustomerInfoResponse> response = new Response<GetCustomerInfoResponse>();
                Response<GetCustomerInfoResponse> savedResponse = new Response<GetCustomerInfoResponse>();
                GetCustomerInfoResponse getCustomerInfoResponse = new GetCustomerInfoResponse();

                getCustomerInfoResponse.RequestId = getCustomerInfoRequest.Id;

                _logger.LogDebug("Searching for customer with id = {getCustomerInfoRequest.CustomerId}", getCustomerInfoRequest.CustomerId);
                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == getCustomerInfoRequest.CustomerId);

                if (customer == null)
                {
                    _logger.LogError("Invalid Customer Id");
                    getCustomerInfoResponse.IsSuccess = false;
                    getCustomerInfoResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = getCustomerInfoResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    //_logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    //if (recordDecryptedRequests != null)
                    //{
                    //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    //    {
                    //        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                    //        savedResponse.Payload = null;
                    //    }
                    //    else
                    //    {
                    //        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    //    }
                    //}
                    //else
                    //{
                    //    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    //    savedResponse.Payload = null;
                    //}
                    savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Obtaining customer info");
                getCustomerInfoResponse.IsLocked = customer.IsLocked;

                List<EntityInstance> entityInstances = await _context.EntityInstance.Where(e => e.CustomerId == getCustomerInfoRequest.CustomerId).ToListAsync();

                List<CustomerEntityInstance> customerEntityInstances = new List<CustomerEntityInstance>();

                foreach (EntityInstance entityInstance in entityInstances) 
                {
                    ChannelEntity channelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(ce => ce.IdentificationChannelId == IdentificationChannel.Id && ce.EntityId == entityInstance.EntityId);
                    if(channelEntity != null) // filtering customer info for each channel based on on ChannelEntity
                    {
                        CustomerEntityInstance customerEntityInstance = new CustomerEntityInstance();
                        customerEntityInstance.Id = entityInstance.Id;
                        customerEntityInstance.EntityInstanceName = entityInstance.EntityInstanceName;
                        customerEntityInstance.CategoryId = entityInstance.Entity.EntityCategoryId;
                        customerEntityInstance.CategoryName = entityInstance.Entity.EntityCategory.CategoryName;
                        customerEntityInstance.ParentEntityCategoryId = entityInstance.Entity.EntityCategory.ParentEntityCategoryId;

                        List<CustomerInfoValue> customerInfoValues = await _context.CustomerInfoValue.Where(c => c.CustomerId == getCustomerInfoRequest.CustomerId && c.EntityInstanceId == entityInstance.Id).ToListAsync();

                        List<EntityInstancePropertyValue> entityInstancePropertyValues = new List<EntityInstancePropertyValue>();

                        foreach (CustomerInfoValue customerInfoValue in customerInfoValues)
                        {
                            EntityInstancePropertyValue entityPropertyValue = new EntityInstancePropertyValue();
                            entityPropertyValue.CustomerInfoValueId = customerInfoValue.Id;
                            Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == customerInfoValue.PropertyId);
                            entityPropertyValue.Name = property.Name;
                            if (property.IsEncrypted.Value)
                            {
                                entityPropertyValue.Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, customerInfoValue.Value, Cryptography.Operation.Decrypt);
                            }
                            else entityPropertyValue.Value = customerInfoValue.Value;

                            entityInstancePropertyValues.Add(entityPropertyValue);
                        }
                        customerEntityInstance.entityInstancePropertyValues = entityInstancePropertyValues;
                        customerEntityInstances.Add(customerEntityInstance);
                    }
                    
                }

                getCustomerInfoResponse.CustomerEntities = customerEntityInstances;

                _logger.LogInformation("GetCustomerInfo Succeeded");

                getCustomerInfoResponse.IsSuccess = true;
                getCustomerInfoResponse.FailureReason = "";
                getCustomerInfoResponse.CustomerId = customer.Id;

                _logger.LogTrace("Returnining response payload of {getCustomerInfoResponse}", getCustomerInfoResponse);

                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = getCustomerInfoResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                //_logger.LogDebug("Checking if Record Decrypted Request is enabled");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                //        savedResponse.Payload = null;
                //    }
                //    else
                //    {
                //        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                //    }
                //}
                //else
                //{
                //    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                //    savedResponse.Payload = null;
                //}
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in GetCustomerInfo");
                return BadRequest();
            }


        }

        /// <summary>
        /// Used to Search Customer By Any related value
        /// </summary>
        /// <param name="request"></param>
        /// <returns>custopmer info</returns>
        [HttpPost] //reviewed
        [Route("SearchCustomerByAnyValue")]
        public async ValueTask<IActionResult> SearchCustomerByAnyValue([FromBody] Request<SearchCustomerByAnyValueRequest> request)
        {
            _logger.LogInformation("Received SearchCustomerByAnyValue Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating SearchCustomerByAnyValueRequest from decypted Payload");
                SearchCustomerByAnyValueRequest searchCustomerByAnyValueRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = searchCustomerByAnyValueRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {searchCustomerByAnyValueRequest.UnixTime} and allowedDelay {allowedDelay}", searchCustomerByAnyValueRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(searchCustomerByAnyValueRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        await _context.AddAsync(resetCustomerChannelPasswordRequest);
                //        await _context.SaveChangesAsync();
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<SearchCustomerResponse> response = new Response<SearchCustomerResponse>();
                Response<SearchCustomerResponse> savedResponse = new Response<SearchCustomerResponse>();
                SearchCustomerResponse searchCustomerResponse = new SearchCustomerResponse();

                searchCustomerResponse.RequestId = searchCustomerByAnyValueRequest.Id;

                searchCustomerResponse.RequestType = "SearchCustomerByAnyValueRequest";

                _logger.LogDebug("Searching in all plaintext values");

                List<CustomerInfoValue> customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.Value.Contains(searchCustomerByAnyValueRequest.CustomerInfoValue)).ToListAsync();

                if (searchCustomerByAnyValueRequest.CustomerInfoValue != null)
                {
                    _logger.LogDebug("Searching in encrypted values");
                    string encryptedSearchValue = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(searchCustomerByAnyValueRequest.CustomerInfoValue)), Cryptography.Operation.Encrypt);
                    List<CustomerInfoValue> customerEncryptedInfoValues = await _context.CustomerInfoValue.Where(c => c.Value.Equals(encryptedSearchValue)).ToListAsync();

                    if (customerEncryptedInfoValues != null)
                    {
                        foreach (CustomerInfoValue customerEncryptedInfoValue in customerEncryptedInfoValues)
                        {
                            customerInfoValues1.Add(customerEncryptedInfoValue);
                        }
                    }

                }

                _logger.LogDebug("Obtaining all matched customers info");
                List<MatchingCustomer> matchingCustomers = new List<MatchingCustomer>();

                foreach (CustomerInfoValue customerInfoValue1 in customerInfoValues1)
                {
                    MatchingCustomer matchingCustomer = new MatchingCustomer();
                    matchingCustomer.Id = customerInfoValue1.CustomerId;
                    Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerInfoValue1.CustomerId);
                    matchingCustomer.IsLocked = customer.IsLocked;

                    List<EntityInstance> entityInstances = await _context.EntityInstance.Where(e => e.CustomerId == customer.Id).ToListAsync();

                    List<CustomerEntityInstance> customerEntityInstances = new List<CustomerEntityInstance>();

                    foreach (EntityInstance entityInstance in entityInstances)
                    {
                        ChannelEntity channelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(ce => ce.IdentificationChannelId == IdentificationChannel.Id && ce.EntityId == entityInstance.EntityId);
                        if (channelEntity != null) // filtering customer info for each channel based on on ChannelEntity
                        {
                            CustomerEntityInstance customerEntityInstance = new CustomerEntityInstance();
                            customerEntityInstance.Id = entityInstance.Id;
                            customerEntityInstance.EntityInstanceName = entityInstance.EntityInstanceName;
                            customerEntityInstance.CategoryId = entityInstance.Entity.EntityCategoryId;

                            customerEntityInstance.CategoryName = entityInstance.Entity.EntityCategory.CategoryName;
                            customerEntityInstance.ParentEntityCategoryId = entityInstance.Entity.EntityCategory.ParentEntityCategoryId;

                            List<CustomerInfoValue> customerInfoValues = await _context.CustomerInfoValue.Where(c => c.CustomerId == customer.Id && c.EntityInstanceId == entityInstance.Id).ToListAsync();

                            List<EntityInstancePropertyValue> entityPropertyValues = new List<EntityInstancePropertyValue>();

                            foreach (CustomerInfoValue customerInfoValue in customerInfoValues)
                            {
                                EntityInstancePropertyValue entityPropertyValue = new EntityInstancePropertyValue();
                                entityPropertyValue.CustomerInfoValueId = customerInfoValue.Id;
                                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == customerInfoValue.PropertyId);
                                entityPropertyValue.Name = property.Name;
                                if (property.IsEncrypted.Value)
                                {
                                    entityPropertyValue.Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, customerInfoValue.Value, Cryptography.Operation.Decrypt);
                                }
                                else entityPropertyValue.Value = customerInfoValue.Value;

                                entityPropertyValues.Add(entityPropertyValue);
                            }
                            customerEntityInstance.entityInstancePropertyValues = entityPropertyValues;
                            customerEntityInstances.Add(customerEntityInstance);
                        }

                    }
                    matchingCustomer.CustomerEntityInstances = customerEntityInstances;
                    matchingCustomers.Add(matchingCustomer);


                }



                searchCustomerResponse.MatchingCustomers = matchingCustomers;

                _logger.LogInformation("SearchCustomerByAnyValue Succeeded");
                searchCustomerResponse.IsSuccess = true;
                searchCustomerResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = searchCustomerResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                //_logger.LogDebug("Checking if Record Decrypted Request is enabled");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                //        savedResponse.Payload = null;
                //    }
                //    else
                //    {
                //        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                //    }
                //}
                //else
                //{
                //    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                //    savedResponse.Payload = null;
                //}
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in SearchCustomerByAnyValue");
                return BadRequest();
            }


        }


        /// <summary>
        /// Used to Search Customer By using a specific property value
        /// </summary>
        /// <param name="request"></param>
        /// <returns>custopmer info</returns>
        [HttpPost] //reviewed
        [Route("SearchCustomerByPropertyValue")]
        public async Task<IActionResult> SearchCustomerByPropertyValue([FromBody] Request<SearchCustomerByPropertyValueRequest> request)
        {
            _logger.LogInformation("Received SearchCustomerByPropertyValue Request");
            try
            {
                var systemSettingsAllowedRequestDelay = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");
                var recordDecryptedRequests =  _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Record Decrypted Requests");

                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating SearchCustomerByPropertyValueRequest from decypted Payload");
                SearchCustomerByPropertyValueRequest searchCustomerByPropertyValueRequest = request.Payload;


                _logger.LogDebug("Verifying Session");
                Guid sessionId = searchCustomerByPropertyValueRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");

               // var systemSettingsAllowedRequestDelay =  _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

               

                _logger.LogDebug("Validating unix gap using request UnixTime {searchCustomerByPropertyValueRequest.UnixTime} and allowedDelay {allowedDelay}", searchCustomerByPropertyValueRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(searchCustomerByPropertyValueRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        await _context.AddAsync(resetCustomerChannelPasswordRequest);
                //        await _context.SaveChangesAsync();
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<SearchCustomerResponse> response = new Response<SearchCustomerResponse>();
                Response<SearchCustomerResponse> savedResponse = new Response<SearchCustomerResponse>();
                SearchCustomerResponse searchCustomerResponse = new SearchCustomerResponse();

                searchCustomerResponse.RequestId = searchCustomerByPropertyValueRequest.Id;

                searchCustomerResponse.RequestType = "SearchCustomerByPropertyValueRequest";

                _logger.LogDebug("Searching for property id {searchCustomerByPropertyValueRequest.PropertyId}", searchCustomerByPropertyValueRequest.PropertyId);
                Property property1 = await _context.Property.SingleOrDefaultAsync(p => p.Id == searchCustomerByPropertyValueRequest.PropertyId);
                _logger.LogError("searchCustomerByPropertyValueRequest.PropertyId "+ searchCustomerByPropertyValueRequest.PropertyId);

                if (property1 == null)
                {
                    _logger.LogError("Invalid Property Id");
                    _logger.LogError("responseCipher.IV " + responseCipher.IV);

                    searchCustomerResponse.IsSuccess = false;
                    searchCustomerResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = searchCustomerResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    _logger.LogError("ICKey "+ ICKey);

                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        _logger.LogError("recordDecryptedRequests "+ recordDecryptedRequests);

                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                List<CustomerInfoValue> customerInfoValues1 = new List<CustomerInfoValue>();

                if (searchCustomerByPropertyValueRequest.CustomerInfoValue != null)
                {
                    _logger.LogDebug("Checking where the property is encrypted or hashed");

                    if (property1.IsEncrypted.Value)
                    {
                        _logger.LogDebug("Property is encrypted");
                        searchCustomerByPropertyValueRequest.CustomerInfoValue = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(searchCustomerByPropertyValueRequest.CustomerInfoValue)), Cryptography.Operation.Encrypt);
                        customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.PropertyId == searchCustomerByPropertyValueRequest.PropertyId && c.Value.Contains(searchCustomerByPropertyValueRequest.CustomerInfoValue)).ToListAsync();
                    }
                    else if (property1.IsHashed.Value)
                    {
                        _logger.LogDebug("Property is hashed");
                        searchCustomerByPropertyValueRequest.CustomerInfoValue = Cryptography.Hash(searchCustomerByPropertyValueRequest.CustomerInfoValue);
                        customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.PropertyId == searchCustomerByPropertyValueRequest.PropertyId && c.Value.Equals(searchCustomerByPropertyValueRequest.CustomerInfoValue)).ToListAsync();
                    }
                    else
                    {
                        _logger.LogDebug("Property is neither encrypted nor hashed");
                        customerInfoValues1 = await _context.CustomerInfoValue.Where(c => c.PropertyId == searchCustomerByPropertyValueRequest.PropertyId && c.Value.Contains(searchCustomerByPropertyValueRequest.CustomerInfoValue)).ToListAsync();
                    }
                }


                _logger.LogDebug("Obtaining all matched customers info");

                List<MatchingCustomer> matchingCustomers = new List<MatchingCustomer>();

                foreach (CustomerInfoValue customerInfoValue1 in customerInfoValues1)
                {
                    MatchingCustomer matchingCustomer = new MatchingCustomer();
                    matchingCustomer.Id = customerInfoValue1.CustomerId;
                    Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerInfoValue1.CustomerId);
                    matchingCustomer.IsLocked = customer.IsLocked;

                    List<EntityInstance> entityInstances = await _context.EntityInstance.Where(e => e.CustomerId == customer.Id).ToListAsync();

                    List<CustomerEntityInstance> customerEntityInstances = new List<CustomerEntityInstance>();

                    foreach (EntityInstance entityInstance in entityInstances)
                    {
                        ChannelEntity channelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(ce => ce.IdentificationChannelId == IdentificationChannel.Id && ce.EntityId == entityInstance.EntityId);
                        if (channelEntity != null) // filtering customer info for each channel based on on ChannelEntity
                        {
                            CustomerEntityInstance customerEntityInstance = new CustomerEntityInstance();
                            customerEntityInstance.Id = entityInstance.Id;
                            customerEntityInstance.EntityInstanceName = entityInstance.EntityInstanceName;
                            customerEntityInstance.CategoryId = entityInstance.Entity.EntityCategoryId;
                            customerEntityInstance.CategoryName = entityInstance.Entity.EntityCategory.CategoryName;
                            customerEntityInstance.ParentEntityCategoryId = entityInstance.Entity.EntityCategory.ParentEntityCategoryId;

                            List<CustomerInfoValue> customerInfoValues = await _context.CustomerInfoValue.Where(c => c.CustomerId == customer.Id && c.EntityInstanceId == entityInstance.Id).ToListAsync();

                            List<EntityInstancePropertyValue> entityPropertyValues = new List<EntityInstancePropertyValue>();

                            foreach (CustomerInfoValue customerInfoValue in customerInfoValues)
                            {
                                EntityInstancePropertyValue entityPropertyValue = new EntityInstancePropertyValue();
                                entityPropertyValue.CustomerInfoValueId = customerInfoValue.Id;
                                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == customerInfoValue.PropertyId);
                                entityPropertyValue.Name = property.Name;
                                if (property.IsEncrypted.HasValue)
                                {
                                    entityPropertyValue.Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, customerInfoValue.Value, Cryptography.Operation.Decrypt);
                                }
                                else entityPropertyValue.Value = customerInfoValue.Value;

                                entityPropertyValues.Add(entityPropertyValue);
                            }
                            customerEntityInstance.entityInstancePropertyValues = entityPropertyValues;
                            customerEntityInstances.Add(customerEntityInstance);
                        }
                    }
                    matchingCustomer.CustomerEntityInstances = customerEntityInstances;
                    matchingCustomers.Add(matchingCustomer);

                }



                searchCustomerResponse.MatchingCustomers = matchingCustomers;

                _logger.LogInformation("SearchCustomerByPropertyValue Succeeded");
                searchCustomerResponse.IsSuccess = true;
                searchCustomerResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = searchCustomerResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                //        savedResponse.Payload = null;
                //    }
                //    else
                //    {
                //        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                //    }
                //}
                //else
                //{
                //    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                //    savedResponse.Payload = null;
                //}
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in SearchCustomerByPropertyValue");
                return BadRequest();
            }


        }

        /// <summary>
        /// Used to Lock/Unlock a customer
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("CustomerLock")]
        public async ValueTask<IActionResult> CustomerLock([FromBody] Request<CustomerLockRequest> request)
        {
            _logger.LogInformation("Received CustomerLock Request");
            try
            {
                var systemSettingsAllowedRequestDelay =  _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Allowed Request Delay");
                var recordDecryptedRequests = _context.SystemSetting.SingleOrDefault(s => s.SettingName == "Record Decrypted Requests");

                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating CustomerLockRequest from decypted Payload");
                CustomerLockRequest customerLockRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = customerLockRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {customerLockRequest.UnixTime} and allowedDelay {allowedDelay}", customerLockRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(customerLockRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests.Value != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = customerLockRequest.Id;

                generalResponse.RequestType = "CustomerLockRequest";

                _logger.LogDebug("Searching for customer with id = {customerLockRequest.CustomerId}", customerLockRequest.CustomerId);
                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == customerLockRequest.CustomerId);

                if (customer == null)
                {
                    _logger.LogError("Invalid Customer Id");
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (recordDecryptedRequests.Value  != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Setting Customer IsLocked as {customerLockRequest.IsLocked}", customerLockRequest.IsLocked);
                customer.IsLocked = customerLockRequest.IsLocked;
                await _context.SaveChangesAsync();

                _logger.LogInformation("CustomerLock Succeeded");
                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;

                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (recordDecryptedRequests.Value != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in CustomerLock");
                return BadRequest();
            }


        }

        /// <summary>
        /// Used to create a new entity
        /// </summary>
        /// <param name="request"></param>
        /// <returns>entityId</returns>
        [HttpPost] //reviewed
        [Route("CreateEntity")]
        public async ValueTask<IActionResult> CreateEntity([FromBody] Request<CreateEntityRequest> request)
        {
            _logger.LogInformation("Received CreateEntity Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating CreateEntityRequest from decypted Payload");
                CreateEntityRequest createEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = createEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {createEntityRequest.UnixTime} and allowedDelay {allowedDelay}", createEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(createEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<CreateEntityResponse> response = new Response<CreateEntityResponse>();
                Response<CreateEntityResponse> savedResponse = new Response<CreateEntityResponse>();
                CreateEntityResponse createEntityResponse = new CreateEntityResponse();

                createEntityResponse.RequestId = createEntityRequest.Id;

                _logger.LogDebug("Searching for Category {createEntityRequest.EntityCategoryId}", createEntityRequest.EntityCategoryId);
                EntityCategory entityCategory = await _context.EntityCategory.SingleOrDefaultAsync(ec => ec.Id == createEntityRequest.EntityCategoryId);

                if (entityCategory == null)
                {
                    _logger.LogError("Invalid Category Id");
                    createEntityResponse.IsSuccess = false;
                    createEntityResponse.FailureReason = "Invalid Category Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = createEntityResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                            savedResponse.Payload = null;
                        }
                        else
                        {
                            _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                        }
                    }
                    else
                    {
                        _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                _logger.LogDebug("Creating new Entity");

                Entity entity = new Entity();
                entity.EntityCategoryId = createEntityRequest.EntityCategoryId;
                entity.EntityName = createEntityRequest.EntityName;

                await _context.AddAsync(entity);
                await _context.SaveChangesAsync();


                _logger.LogDebug("Giving channel access to the newly created entity");
                ChannelEntity channelEntity = new ChannelEntity();
                channelEntity.EntityId = entity.Id;
                channelEntity.IdentificationChannelId = IdentificationChannel.Id;

                await _context.AddAsync(channelEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("CreateEntity Succeeded");
                createEntityResponse.IsSuccess = true;
                createEntityResponse.FailureReason = "";
                createEntityResponse.EntityId = entity.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = createEntityResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                _logger.LogDebug("Checking if Record Decrypted Request is enabled");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the saved Response Payload with null");
                        savedResponse.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Response payload is saved in database as follows {@savedResponse.payload}", savedResponse.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the saved Response Payload with null");
                    savedResponse.Payload = null;
                }

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in CreateEntity");
                return BadRequest();
            }


        }

        /// <summary>
        /// Used to update an existing entity
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost] //reviewed
        [Route("UpdateEntity")]
        public async ValueTask<IActionResult> UpdateEntity([FromBody] Request<UpdateEntityRequest> request)
        {
            _logger.LogInformation("Received UpdateEntity Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateEntityRequest from decypted Payload");
                UpdateEntityRequest updateEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateEntityRequest.UnixTime} and allowedDelay {allowedDelay}", updateEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateEntityRequest.Id;

                generalResponse.RequestType = "UpdateEntityCategoryRequest";



                Entity entity = await _context.Entity.SingleOrDefaultAsync(c => c.Id == updateEntityRequest.EntityId);

                if (entity == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                EntityCategory entityCategory = await _context.EntityCategory.SingleOrDefaultAsync(ec => ec.Id == updateEntityRequest.EntityCategoryId);

                if (entityCategory == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Category Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entity.EntityName = updateEntityRequest.EntityName;
                entity.EntityCategoryId = updateEntityRequest.EntityCategoryId;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to create an instance of an entity and related it to a specific customer
        /// </summary>
        /// <param name="request"></param>
        /// <returns>entityInstanceId</returns>
        [HttpPost] //reviewed
        [Route("CreateCustomerEntityInstance")]
        public async ValueTask<IActionResult> CreateCustomerEntityInstance([FromBody] Request<CreateCustomerEntityInstanceRequest> request)
        {
            _logger.LogInformation("Received CreateCustomerEntityInstance Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating CreateCustomerEntityInstanceRequest from decypted Payload");
                CreateCustomerEntityInstanceRequest createCustomerEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = createCustomerEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {createCustomerEntityRequest.UnixTime} and allowedDelay {allowedDelay}", createCustomerEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(createCustomerEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<CreateCustomerEntityInstanceResponse> response = new Response<CreateCustomerEntityInstanceResponse>();
                Response<CreateCustomerEntityInstanceResponse> savedResponse = new Response<CreateCustomerEntityInstanceResponse>();
                CreateCustomerEntityInstanceResponse createCustomerEntityResponse = new CreateCustomerEntityInstanceResponse();

                createCustomerEntityResponse.RequestId = createCustomerEntityRequest.Id;


                Entity entity = await _context.Entity.SingleOrDefaultAsync(eLogin => eLogin.Id == createCustomerEntityRequest.EntityId);

                if (entity == null)
                {
                    createCustomerEntityResponse.IsSuccess = false;
                    createCustomerEntityResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = createCustomerEntityResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                ChannelEntity channelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(ce => ce.EntityId == createCustomerEntityRequest.EntityId && ce.IdentificationChannelId == IdentificationChannel.Id);

                if (channelEntity == null)
                {
                    createCustomerEntityResponse.IsSuccess = false;
                    createCustomerEntityResponse.FailureReason = "Entity is not assigned to this Channel";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = createCustomerEntityResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == createCustomerEntityRequest.CustomerId);

                if (customer == null)
                {
                    createCustomerEntityResponse.IsSuccess = false;
                    createCustomerEntityResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = createCustomerEntityResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }



                EntityInstance entityInstance = new EntityInstance();
                entityInstance.CustomerId = createCustomerEntityRequest.CustomerId;
                entityInstance.EntityId = createCustomerEntityRequest.EntityId;
                entityInstance.EntityInstanceName = createCustomerEntityRequest.EntityInstanceName;

                await _context.AddAsync(entityInstance);
                await _context.SaveChangesAsync();

                createCustomerEntityResponse.IsSuccess = true;
                createCustomerEntityResponse.FailureReason = "";
                createCustomerEntityResponse.EntityInstanceId = entityInstance.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = createCustomerEntityResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to add new customer information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        //reviewed
        [HttpPost]
        [Route("AddCustomerInfo")]
        public async ValueTask<IActionResult> AddCustomerInfo([FromBody] Request<AddCustomerInfoRequest> request)
        {
            _logger.LogInformation("Received AddCustomerInfo Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating AddCustomerInfoRequest from decypted Payload");
                AddCustomerInfoRequest addCustomerInfoRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = addCustomerInfoRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {addCustomerInfoRequest.UnixTime} and allowedDelay {allowedDelay}", addCustomerInfoRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(addCustomerInfoRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = addCustomerInfoRequest.Id;

                generalResponse.RequestType = "AddCustomerInfoRequest";

                Customer customer = await _context.Customer.SingleOrDefaultAsync(c => c.Id == addCustomerInfoRequest.CustomerId);

                if (customer == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(e => e.Id == addCustomerInfoRequest.EntityInstanceId && e.CustomerId == addCustomerInfoRequest.CustomerId);

                if (entityInstance == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Instance Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == addCustomerInfoRequest.PropertyId);
                EntityProperty entityProperty = await _context.EntityProperty.SingleOrDefaultAsync(ep => ep.PropertyId == addCustomerInfoRequest.PropertyId && ep.EntityId == entityInstance.EntityId);
                if (property == null || entityProperty == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                CustomerInfoValue customerInfoValue2 = await _context.CustomerInfoValue.FirstOrDefaultAsync(civ => civ.EntityInstanceId == entityInstance.Id && civ.PropertyId == property.Id);
                if (customerInfoValue2 != null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = property.Name + " already exists in " + entityInstance.EntityInstanceName;


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!String.IsNullOrEmpty(property.ValidationRegex))
                {
                    Regex regex = new Regex(property.ValidationRegex);

                    if (!regex.IsMatch(addCustomerInfoRequest.Value))
                    {
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint;


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                savedResponse.Payload = null;
                            }
                        }
                        else savedResponse.Payload = null;

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }

                }

                if (property.IsEncrypted.Value)
                {
                    addCustomerInfoRequest.Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(addCustomerInfoRequest.Value)), Cryptography.Operation.Encrypt);
                }
                else if (property.IsHashed.Value)
                {
                    addCustomerInfoRequest.Value = Cryptography.Hash(addCustomerInfoRequest.Value);
                }

                if (property.IsUniqueIdentifier.Value)
                {
                    CustomerInfoValue customerInfoValueDoublicateCheck = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == addCustomerInfoRequest.Value);

                    if (customerInfoValueDoublicateCheck != null && customerInfoValueDoublicateCheck.CustomerId != addCustomerInfoRequest.CustomerId)
                    {
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "Cannot insert duplicate value of a unique identifier for two different customers";


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                savedResponse.Payload = null;
                            }
                        }
                        else savedResponse.Payload = null;

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }
                }





                CustomerInfoValue customerInfoValue = new CustomerInfoValue();
                customerInfoValue.CustomerId = customer.Id;
                customerInfoValue.EntityInstanceId = entityInstance.Id;
                customerInfoValue.PropertyId = property.Id;
                customerInfoValue.Value = addCustomerInfoRequest.Value;



                await _context.AddAsync(customerInfoValue);
                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to update customer information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("UpdateCustomerInfo")]
        public async ValueTask<IActionResult> UpdateCustomerInfo([FromBody] Request<UpdateCustomerInfoRequest> request)
        {
            _logger.LogInformation("Received UpdateCustomerInfo Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateCustomerInfoRequest from decypted Payload");
                UpdateCustomerInfoRequest updateCustomerInfoRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateCustomerInfoRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateCustomerInfoRequest.UnixTime} and allowedDelay {allowedDelay}", updateCustomerInfoRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateCustomerInfoRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateCustomerInfoRequest.Id;

                generalResponse.RequestType = "UpdateCustomerInfoRequest";



                CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Id == updateCustomerInfoRequest.CustomerInfoValueId);

                if (customerInfoValue == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Info Value Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == customerInfoValue.PropertyId);

                if (property == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                if (!String.IsNullOrEmpty(property.ValidationRegex))
                {
                    Regex regex = new Regex(property.ValidationRegex);

                    if (!regex.IsMatch(updateCustomerInfoRequest.Value))
                    {
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "Invalid value for " + property.Name + ". Validation Hint: " + property.ValidationHint;


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                savedResponse.Payload = null;
                            }
                        }
                        else savedResponse.Payload = null;

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }

                }

                if (property.IsEncrypted.Value)
                {
                    updateCustomerInfoRequest.Value = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, Convert.ToBase64String(Encoding.UTF8.GetBytes(updateCustomerInfoRequest.Value)), Cryptography.Operation.Encrypt);
                }
                else if (property.IsHashed.Value)
                {
                    updateCustomerInfoRequest.Value = Cryptography.Hash(updateCustomerInfoRequest.Value);
                }

                if (property.IsUniqueIdentifier.Value)
                {
                    CustomerInfoValue customerInfoValueDoublicateCheck = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Value == updateCustomerInfoRequest.Value);

                    if (customerInfoValueDoublicateCheck != null && customerInfoValueDoublicateCheck.CustomerId != customerInfoValue.CustomerId)
                    {
                        generalResponse.IsSuccess = false;
                        generalResponse.FailureReason = "Cannot insert duplicate value of a unique identifier for two different customers";


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = generalResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                savedResponse.Payload = null;
                            }
                        }
                        else savedResponse.Payload = null;

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }
                }

                customerInfoValue.Value = updateCustomerInfoRequest.Value;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to update an entity instance belonging to a specific customer
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("UpdateCustomerEntityInstance")]//reviewed
        public async ValueTask<IActionResult> UpdateCustomerEntityInstance([FromBody] Request<UpdateCustomerEntityInstanceRequest> request)
        {
            _logger.LogInformation("Received UpdateCustomerEntityInstance Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateCustomerEntityInstanceRequest from decypted Payload");
                UpdateCustomerEntityInstanceRequest updateCustomerEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateCustomerEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateCustomerEntityRequest.UnixTime} and allowedDelay {allowedDelay}", updateCustomerEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateCustomerEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateCustomerEntityRequest.Id;

                generalResponse.RequestType = "UpdateCustomerEntityRequest";



                EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(c => c.Id == updateCustomerEntityRequest.EntityInstanceId);

                if (entityInstance == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Entity Instance Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == updateCustomerEntityRequest.EntityId);
                if (entity == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entityInstance.EntityInstanceName = updateCustomerEntityRequest.EntityInstanceName;
                entityInstance.EntityId = updateCustomerEntityRequest.EntityId;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to delete customer information
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("DeleteCustomerInfo")]
        public async ValueTask<IActionResult> DeleteCustomerInfo([FromBody] Request<DeleteCustomerInfoRequest> request)
        {
            _logger.LogInformation("Received DeleteCustomerInfo Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating DeleteCustomerInfoRequest from decypted Payload");
                DeleteCustomerInfoRequest deleteCustomerInfoRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = deleteCustomerInfoRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {registerRequest.UnixTime} and allowedDelay {allowedDelay}", deleteCustomerInfoRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(deleteCustomerInfoRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = deleteCustomerInfoRequest.Id;

                generalResponse.RequestType = "DeleteCustomerInfoRequest";



                CustomerInfoValue customerInfoValue = await _context.CustomerInfoValue.SingleOrDefaultAsync(c => c.Id == deleteCustomerInfoRequest.CustomerInfoValueId);

                if (customerInfoValue == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Info Value Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                customerInfoValue.IsDeleted = true;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to delete an entity instance belonging to a specific customer
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost] //reviewed
        [Route("DeleteCustomerEntityInstance")]
        public async ValueTask<IActionResult> DeleteCustomerEntityInstance([FromBody] Request<DeleteCustomerEntityInstanceRequest> request)
        {
            _logger.LogInformation("Received DeleteCustomerEntityInstance Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating DeleteCustomerEntityInstanceRequest from decypted Payload");
                DeleteCustomerEntityInstanceRequest deleteCustomerEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = deleteCustomerEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {deleteCustomerEntityRequest.UnixTime} and allowedDelay {allowedDelay}", deleteCustomerEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(deleteCustomerEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = deleteCustomerEntityRequest.Id;

                generalResponse.RequestType = "DeleteCustomerEntityRequest";



                EntityInstance entityInstance = await _context.EntityInstance.SingleOrDefaultAsync(c => c.Id == deleteCustomerEntityRequest.EntityInstanceId);

                if (entityInstance == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Customer Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entityInstance.IsDeleted = true;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to get a list of all entities
        /// </summary>
        /// <param name="request"></param>
        /// <returns>list of entities</returns>
        [HttpPost] //reviewed
        [Route("GetEntities")]
        public async ValueTask<IActionResult> GetEntities([FromBody] Request<GetEntitiesRequest> request)
        {
            _logger.LogInformation("Received GetEntities Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating GetEntitiesRequest from decypted Payload");
                GetEntitiesRequest getEntitiesRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = getEntitiesRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {getEntitiesRequest.UnixTime} and allowedDelay {allowedDelay}", getEntitiesRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(getEntitiesRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        request.Payload = null;
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GetEntitiesResponse> response = new Response<GetEntitiesResponse>();
                Response<GetEntitiesResponse> savedResponse = new Response<GetEntitiesResponse>();
                GetEntitiesResponse getEntitiesResponse = new GetEntitiesResponse();

                getEntitiesResponse.RequestId = getEntitiesRequest.Id;



                List<Entity> entities = await _context.Entity.ToListAsync();



                getEntitiesResponse.Entities = entities;

                getEntitiesResponse.IsSuccess = true;
                getEntitiesResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = getEntitiesResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to get a list of all categories
        /// </summary>
        /// <param name="request"></param>
        /// <returns>list of categories</returns>
        [HttpPost]
        [Route("GetEntityCategories")]
        public async ValueTask<IActionResult> GetEntityCategories([FromBody] Request<GetEntityCategoriesRequest> request)
        {
            _logger.LogInformation("Received GetEntityCategories Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating GetEntityCategoriesRequest from decypted Payload");
                GetEntityCategoriesRequest getEntityCategoriesRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = getEntityCategoriesRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {getEntityCategoriesRequest.UnixTime} and allowedDelay {allowedDelay}", getEntityCategoriesRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(getEntityCategoriesRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        request.Payload = null;
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GetEntityCategoriesResponse> response = new Response<GetEntityCategoriesResponse>();
                Response<GetEntityCategoriesResponse> savedResponse = new Response<GetEntityCategoriesResponse>();
                GetEntityCategoriesResponse getEntityCategoriesResponse = new GetEntityCategoriesResponse();

                getEntityCategoriesResponse.RequestId = getEntityCategoriesRequest.Id;



                List<EntityCategory> entityCategories = await _context.EntityCategory.ToListAsync();



                getEntityCategoriesResponse.EntityCategories = entityCategories;

                getEntityCategoriesResponse.IsSuccess = true;
                getEntityCategoriesResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = getEntityCategoriesResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to geta list of all properties
        /// </summary>
        /// <param name="request"></param>
        /// <returns>list of properties</returns>
        [HttpPost]
        [Route("GetInfoProperties")]
        public async ValueTask<IActionResult> GetInfoProperties([FromBody] Request<GetInfoPropertiesRequest> request)
        {
            _logger.LogInformation("Received GetInfoProperties Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating GetInfoPropertiesRequest from decypted Payload");
                GetInfoPropertiesRequest getInfoPropertiesRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = getInfoPropertiesRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {getInfoPropertiesRequest.UnixTime} and allowedDelay {allowedDelay}", getInfoPropertiesRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(getInfoPropertiesRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        request.Payload = null;
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GetInfoPropertiesResponse> response = new Response<GetInfoPropertiesResponse>();
                Response<GetInfoPropertiesResponse> savedResponse = new Response<GetInfoPropertiesResponse>();
                GetInfoPropertiesResponse getInfoPropertiesResponse = new GetInfoPropertiesResponse();

                getInfoPropertiesResponse.RequestId = getInfoPropertiesRequest.Id;



                List<Property> properties = await _context.Property.ToListAsync();



                getInfoPropertiesResponse.Properties = properties;

                getInfoPropertiesResponse.IsSuccess = true;
                getInfoPropertiesResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = getInfoPropertiesResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to create a new category
        /// </summary>
        /// <param name="request"></param>
        /// <returns>categoryId</returns>
        [HttpPost] //reviewed
        [Route("CreateEntityCategory")]
        public async ValueTask<IActionResult> CreateEntityCategory([FromBody] Request<CreateEntityCategoryRequest> request)
        {
            _logger.LogInformation("Received CreateEntityCategory Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating CreateEntityCategoryRequest from decypted Payload");
                CreateEntityCategoryRequest createEntityCategoryRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = createEntityCategoryRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {createEntityCategoryRequest.UnixTime} and allowedDelay {allowedDelay}", createEntityCategoryRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(createEntityCategoryRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<CreateEntityCategoryResponse> response = new Response<CreateEntityCategoryResponse>();
                Response<CreateEntityCategoryResponse> savedResponse = new Response<CreateEntityCategoryResponse>();
                CreateEntityCategoryResponse createEntityCategoryResponse = new CreateEntityCategoryResponse();

                createEntityCategoryResponse.RequestId = createEntityCategoryRequest.Id;

                if (createEntityCategoryRequest.ParentEntityCategoryId != null)
                {
                    EntityCategory parentCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.Id == createEntityCategoryRequest.ParentEntityCategoryId);

                    if (parentCategory == null)
                    {
                        createEntityCategoryResponse.IsSuccess = false;
                        createEntityCategoryResponse.FailureReason = "Invalid Parent Category Id";


                        session.LastActivity = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        response.RequestId = request.Id;
                        response.Payload = createEntityCategoryResponse;
                        response.IV = Convert.ToBase64String(responseCipher.IV);
                        response.Encrypt(ICKey);

                        savedResponse = response;
                        if (recordDecryptedRequests != null)
                        {
                            if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                            {
                                savedResponse.Payload = null;
                            }
                        }
                        else savedResponse.Payload = null;

                        await _context.AddAsync(savedResponse);
                        await _context.SaveChangesAsync();

                        return Ok(JsonConvert.SerializeObject(response));
                    }
                }


                EntityCategory entityCategory = new EntityCategory();
                entityCategory.CategoryName = createEntityCategoryRequest.CategoryName;
                entityCategory.ParentEntityCategoryId = createEntityCategoryRequest.ParentEntityCategoryId;

                await _context.AddAsync(entityCategory);
                await _context.SaveChangesAsync();

                createEntityCategoryResponse.IsSuccess = true;
                createEntityCategoryResponse.FailureReason = "";
                createEntityCategoryResponse.EntityCategoryId = entityCategory.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = createEntityCategoryResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to create a new property
        /// </summary>
        /// <param name="request"></param>
        /// <returns>propertyId</returns>
        [HttpPost]
        [Route("CreateInfoProperty")]
        public async ValueTask<IActionResult> CreateInfoProperty([FromBody] Request<CreateInfoPropertyRequest> request)
        {
            _logger.LogInformation("Received CreateInfoProperty Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating CreateInfoPropertyRequest from decypted Payload");
                CreateInfoPropertyRequest createInfoPropertyRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = createInfoPropertyRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {createInfoPropertyRequest.UnixTime} and allowedDelay {allowedDelay}", createInfoPropertyRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(createInfoPropertyRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<CreateInfoPropertyResponse> response = new Response<CreateInfoPropertyResponse>();
                Response<CreateInfoPropertyResponse> savedResponse = new Response<CreateInfoPropertyResponse>();
                CreateInfoPropertyResponse createInfoPropertyResponse = new CreateInfoPropertyResponse();

                createInfoPropertyResponse.RequestId = createInfoPropertyRequest.Id;




                Property property = new Property();
                property.Name = createInfoPropertyRequest.Name;
                property.ValidationRegex = createInfoPropertyRequest.ValidationRegex;
                property.ValidationHint = createInfoPropertyRequest.ValidationHint;
                property.IsEncrypted = createInfoPropertyRequest.IsEncrypted;
                property.IsHashed = createInfoPropertyRequest.IsHashed;
                property.IsUniqueIdentifier = createInfoPropertyRequest.IsUniqueIdentifier;

                await _context.AddAsync(property);
                await _context.SaveChangesAsync();

                createInfoPropertyResponse.IsSuccess = true;
                createInfoPropertyResponse.FailureReason = "";
                createInfoPropertyResponse.PropertyId = property.Id;


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = createInfoPropertyResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to update an existing category
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("UpdateEntityCategory")]
        public async ValueTask<IActionResult> UpdateEntityCategory([FromBody] Request<UpdateEntityCategoryRequest> request)
        {
            _logger.LogInformation("Received UpdateEntityCategory Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateEntityCategoryRequest from decypted Payload");
                UpdateEntityCategoryRequest updateEntityCategoryRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateEntityCategoryRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateEntityCategoryRequest.UnixTime} and allowedDelay {allowedDelay}", updateEntityCategoryRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateEntityCategoryRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateEntityCategoryRequest.Id;

                generalResponse.RequestType = "UpdateEntityCategoryRequest";



                EntityCategory entityCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.Id == updateEntityCategoryRequest.EntityCategoryId);

                if (entityCategory == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Category Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entityCategory.CategoryName = updateEntityCategoryRequest.CategoryName;
                entityCategory.ParentEntityCategoryId = updateEntityCategoryRequest.ParentEntityCategoryId;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to update an existing property
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("UpdateInfoProperty")]
        public async ValueTask<IActionResult> UpdateInfoProperty([FromBody] Request<UpdateInfoPropertyRequest> request)
        {
            _logger.LogInformation("Received UpdateInfoProperty Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating UpdateInfoPropertyRequest from decypted Payload");
                UpdateInfoPropertyRequest updateInfoPropertyRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = updateInfoPropertyRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {updateInfoPropertyRequest.UnixTime} and allowedDelay {allowedDelay}", updateInfoPropertyRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(updateInfoPropertyRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = updateInfoPropertyRequest.Id;

                generalResponse.RequestType = "UpdateEntityCategoryRequest";



                Property property = await _context.Property.SingleOrDefaultAsync(c => c.Id == updateInfoPropertyRequest.PropertyId);

                if (property == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                property.Name = updateInfoPropertyRequest.Name;
                property.ValidationRegex = updateInfoPropertyRequest.ValidationRegex;
                property.ValidationHint = updateInfoPropertyRequest.ValidationHint;
                property.IsEncrypted = updateInfoPropertyRequest.IsEncrypted;
                property.IsHashed = updateInfoPropertyRequest.IsHashed;
                property.IsUniqueIdentifier = updateInfoPropertyRequest.IsUniqueIdentifier;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to delete a category
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("DeleteEntityCategory")]
        public async ValueTask<IActionResult> DeleteEntityCategory([FromBody] Request<DeleteEntityCategoryRequest> request)
        {
            _logger.LogInformation("Received DeleteEntityCategory Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating DeleteEntityCategoryRequest from decypted Payload");
                DeleteEntityCategoryRequest deleteEntityCategoryRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = deleteEntityCategoryRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {deleteEntityCategoryRequest.UnixTime} and allowedDelay {allowedDelay}", deleteEntityCategoryRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(deleteEntityCategoryRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = deleteEntityCategoryRequest.Id;

                generalResponse.RequestType = "DeleteEntityCategoryRequest";



                EntityCategory entityCategory = await _context.EntityCategory.SingleOrDefaultAsync(c => c.Id == deleteEntityCategoryRequest.EntityCategoryId);

                if (entityCategory == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Category Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entityCategory.IsDeleted = true;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to delete an entity
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("DeleteEntity")] //reviewed
        public async ValueTask<IActionResult> DeleteEntity([FromBody] Request<DeleteEntityRequest> request)
        {
            _logger.LogInformation("Received DeleteEntity Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating DeleteEntityRequest from decypted Payload");
                DeleteEntityRequest deleteEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = deleteEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {deleteEntityRequest.UnixTime} and allowedDelay {allowedDelay}", deleteEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(deleteEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = deleteEntityRequest.Id;

                generalResponse.RequestType = "DeleteEntityRequest";



                Entity entity = await _context.Entity.SingleOrDefaultAsync(c => c.Id == deleteEntityRequest.EntityId);

                if (entity == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                entity.IsDeleted = true;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to delete a property
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("DeleteInfoProperty")]
        public async ValueTask<IActionResult> DeleteInfoProperty([FromBody] Request<DeleteInfoPropertyRequest> request)
        {
            _logger.LogInformation("Received DeleteInfoProperty Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating DeleteInfoPropertyRequest from decypted Payload");
                DeleteInfoPropertyRequest deleteInfoPropertyRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = deleteInfoPropertyRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {deleteInfoPropertyRequest.UnixTime} and allowedDelay {allowedDelay}", deleteInfoPropertyRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(deleteInfoPropertyRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = deleteInfoPropertyRequest.Id;

                generalResponse.RequestType = "DeleteInfoPropertyRequest";



                Property property = await _context.Property.SingleOrDefaultAsync(c => c.Id == deleteInfoPropertyRequest.PropertyId);

                if (property == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                property.IsDeleted = true;

                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to assign a specific property to a specific entity
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost] //reviewed
        [Route("AssignPropertyToEntity")]
        public async ValueTask<IActionResult> AssignPropertyToEntity([FromBody] Request<AssignPropertyToEntityRequest> request)
        {
            _logger.LogInformation("Received AssignPropertyToEntity Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating AssignPropertyToEntityRequest from decypted Payload");
                AssignPropertyToEntityRequest assignPropertyToEntityRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = assignPropertyToEntityRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {assignPropertyToEntityRequest.UnixTime} and allowedDelay {allowedDelay}", assignPropertyToEntityRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(assignPropertyToEntityRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        _logger.LogDebug("Record Decrypted Request is disabled, setting the Payload with null");
                        request.Payload = null;
                    }
                    else
                    {
                        _logger.LogDebug("Record Decypted Request is enabled. Request decrypted payload is saved in database as follows {@request.Payload}", request.Payload);
                    }
                }
                else
                {
                    _logger.LogError("Failed to obtain the value of Record Decrypted Request from systemSettings, setting the Payload with null");
                    request.Payload = null;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Generating new Cipher for response using channel key");
                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GeneralResponse> response = new Response<GeneralResponse>();
                Response<GeneralResponse> savedResponse = new Response<GeneralResponse>();
                GeneralResponse generalResponse = new GeneralResponse();

                generalResponse.RequestId = assignPropertyToEntityRequest.Id;

                generalResponse.RequestType = "AssignPropertyToEntityRequest";

                ChannelEntity channelEntity = await _context.ChannelEntity.SingleOrDefaultAsync(ce => ce.EntityId == assignPropertyToEntityRequest.EntityId && ce.IdentificationChannelId == IdentificationChannel.Id);

                if (channelEntity == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Entity is not assigned to this Channel";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == assignPropertyToEntityRequest.PropertyId);

                if (property == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Property Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == assignPropertyToEntityRequest.EntityId);

                if (entity == null)
                {
                    generalResponse.IsSuccess = false;
                    generalResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = generalResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }

                EntityProperty entityProperty = new EntityProperty();
                entityProperty.EntityId = assignPropertyToEntityRequest.EntityId;
                entityProperty.PropertyId = assignPropertyToEntityRequest.PropertyId;

                await _context.AddAsync(entityProperty);
                await _context.SaveChangesAsync();

                generalResponse.IsSuccess = true;
                generalResponse.FailureReason = "";



                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = generalResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }

        /// <summary>
        /// Used to get a list of properties related to a specific entity
        /// </summary>
        /// <param name="request"></param>
        /// <returns>list of properties</returns>
        [HttpPost]
        [Route("GetEntityCategoryInfoProperties")]
        public async ValueTask<IActionResult> GetEntityProperties([FromBody] Request<GetEntityPropertiesRequest> request)
        {
            _logger.LogInformation("Received GetEntityProperties Request");
            try
            {
                if (request == null)
                {
                    _logger.LogError("Rejecting request because it is null");
                    return BadRequest();
                }
                _logger.LogDebug("Request received is {@request}", request);
                if (!ModelState.IsValid)
                {
                    _logger.LogError("Rejecting request because it is invalid");
                    //Invalid Request
                    return BadRequest();
                }
                _logger.LogDebug("Saving request in database");

                await _context.AddAsync(request);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Request saved in database as {@request}", request);

                _logger.LogDebug("Verifying Identification Channel");
                IdentificationChannel IdentificationChannel = await _context.IdentificationChannel.SingleOrDefaultAsync(ic => ic.Id == request.ChannelId);

                if (IdentificationChannel == null || !IdentificationChannel.IsEnabled)
                {
                    if (IdentificationChannel == null) _logger.LogError("Invalid Identification Channel");
                    if (!IdentificationChannel.IsEnabled) _logger.LogError("Identification Channel is not enabled");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                _logger.LogDebug("Identification Channel verified");

                _logger.LogDebug("Decrypting Channel Key using dbKey and dbIV");
                string ICKey = Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, IdentificationChannel.Key, Cryptography.Operation.Decrypt);
                _logger.LogDebug("Decrypting Payload using channel key and request IV");
                request.Decrypt(ICKey);


                _logger.LogDebug("Generating GetEntityPropertiesRequest from decypted Payload");
                GetEntityPropertiesRequest getEntityPropertiesRequest = request.Payload;

                _logger.LogDebug("Verifying Session");
                Guid sessionId = getEntityPropertiesRequest.SessionId;

                Session session = await _context.Session.SingleOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                {
                    _logger.LogError("Invalid session id, rejecting request.");
                    return BadRequest();
                }

                if (request.ChannelId != session.IdentificationChannelId)
                {
                    _logger.LogError("Session id does not belong to the same channel, rejecting request.");
                    return BadRequest();
                }
                if (!await SessionValid(session, DateTime.UtcNow))
                {
                    _logger.LogError("Session expired. Rejecting request.");
                    return BadRequest("(!await SessionValid(session, DateTime.UtcNow))");
                }

                int allowedDelay = 2;
                _logger.LogDebug("Initialized allowed request delay value as {allowedDelay}", allowedDelay);

                _logger.LogDebug("Trying to obtain allowed request delay value from db.systemSetting");
                var systemSettingsAllowedRequestDelay = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Allowed Request Delay");

                if (systemSettingsAllowedRequestDelay != null)
                {
                    allowedDelay = Convert.ToInt32(systemSettingsAllowedRequestDelay.Value); //Convert.ToInt32(Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, systemSettingsAllowedRequestDelay.Value, Cryptography.Operation.Decrypt));
                    _logger.LogDebug("Obtained allowed request delay value from db as {allowedDelay}", allowedDelay);
                }

                _logger.LogDebug("Validating unix gap using request UnixTime {getEntityPropertiesRequest.UnixTime} and allowedDelay {allowedDelay}", getEntityPropertiesRequest.UnixTime, allowedDelay);
                if (!UnixGapValidator(getEntityPropertiesRequest.UnixTime, allowedDelay))
                {
                    _logger.LogError("UnixGapValidator failed. Man in the middle attack suspected!");
                    _logger.LogError("Rejecting request");
                    return BadRequest();
                }

                // Request is Valid
                _logger.LogInformation("Request is valid");


                await _context.SaveChangesAsync();

                SystemSetting recordDecryptedRequests = await _context.SystemSetting.SingleOrDefaultAsync(s => s.SettingName == "Record Decrypted Requests");
                //if (recordDecryptedRequests != null)
                //{
                //    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                //    {
                //        request.Payload = null;
                //    }
                //}
                request.Payload = null;

                Aes responseCipher = Cryptography.CreateCipher(ICKey);

                Response<GetEntityPropertiesResponse> response = new Response<GetEntityPropertiesResponse>();
                Response<GetEntityPropertiesResponse> savedResponse = new Response<GetEntityPropertiesResponse>();
                GetEntityPropertiesResponse getEntityPropertiesResponse = new GetEntityPropertiesResponse();

                getEntityPropertiesResponse.RequestId = getEntityPropertiesRequest.Id;


                Entity entity = await _context.Entity.SingleOrDefaultAsync(e => e.Id == getEntityPropertiesRequest.EntityId);

                if (entity == null)
                {
                    getEntityPropertiesResponse.IsSuccess = false;
                    getEntityPropertiesResponse.FailureReason = "Invalid Entity Id";


                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    response.RequestId = request.Id;
                    response.Payload = getEntityPropertiesResponse;
                    response.IV = Convert.ToBase64String(responseCipher.IV);
                    response.Encrypt(ICKey);

                    savedResponse = response;
                    if (recordDecryptedRequests != null)
                    {
                        if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                        {
                            savedResponse.Payload = null;
                        }
                    }
                    else savedResponse.Payload = null;

                    await _context.AddAsync(savedResponse);
                    await _context.SaveChangesAsync();

                    return Ok(JsonConvert.SerializeObject(response));
                }


                List<Property> properties = new List<Property>();
                List<EntityProperty> entityProperties = await _context.EntityProperty.Where(e => e.EntityId == getEntityPropertiesRequest.EntityId).ToListAsync();

                foreach (EntityProperty entityCategoryProperty in entityProperties)
                {
                    Property property = await _context.Property.SingleOrDefaultAsync(p => p.Id == entityCategoryProperty.PropertyId);
                    properties.Add(property);
                }



                getEntityPropertiesResponse.Properties = properties;

                getEntityPropertiesResponse.IsSuccess = true;
                getEntityPropertiesResponse.FailureReason = "";


                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                response.RequestId = request.Id;
                response.Payload = getEntityPropertiesResponse;
                response.IV = Convert.ToBase64String(responseCipher.IV);
                response.Encrypt(ICKey);

                savedResponse = response;
                if (recordDecryptedRequests != null)
                {
                    if (Cryptography.AES(Cryptography.dbKey, Cryptography.dbIV, recordDecryptedRequests.Value, Cryptography.Operation.Decrypt).ToLower() != "true")
                    {
                        savedResponse.Payload = null;
                    }
                }
                else savedResponse.Payload = null;
                savedResponse.Payload = null;

                await _context.AddAsync(savedResponse);
                await _context.SaveChangesAsync();

                return Ok(JsonConvert.SerializeObject(response));
            }
            catch (Exception)
            {
                return BadRequest();
            }
            

        }
    }
}