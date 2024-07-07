//using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using eLogin.Data;
using eLogin.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using Serilog;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace eLogin
{
    public class LicenseCheck
    {
        private readonly DatabaseContext _context;
        private IHostingEnvironment hostingEnv;

        public LicenseCheck(DatabaseContext context, IHostingEnvironment env)
        {
            _context = context;
            this.hostingEnv = env;
        }

        enum ValidationResult
        {
            
        }

        private static readonly string publicKey = @"-----BEGIN PUBLIC KEY-----
MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEApPCE3CIQ55l/idEr1D9e
8hjj7oCuI40oW+zWwytw9UXCq6m0dhO6GZv7HBjdpU3s/oV5pcJfXAx2eo6FjkK0
DES1G8L0EGW03d6MoNOW18QWKoNZfLXzjdjl6RigBKO0324hm8WjVGEQhtZto0nC
Fb+z9P6WKFP67JjR1OkCmZ54Xn9JVLE1hrtIA2hFu0r+yci519BVRoqu2DX1XlmG
qPvrTJaSqXYJfr/KDzO75WrZiacG97qi2pW9U7y8WAf4DOeBET/PasUQ0TJCdQCC
d4RzKjemKf4FT8/Pmia4smTIbR91GSb0GM9AGbXFoKOm7NLCUK3mHlja5MENS+xT
DeUv//li9ZBc8uXNnHKphP/rAziGp1HTx824VcUXf9deByVU0ELOweOG7OKwnql6
Jxz7KJhkYXiCUHP7U4hY4/HfxE9fRcdke5HI5hw16N7YNf1Ab6q3rEfG+eLCt0N3
3IGdOvoXH10FBtA+OCWQzY7Id9OfoyOvVsYufnLD7uEKMW8q60d/agi7XzpRv8fz
/8wsx5CzziesZvIIxHNHv5VuNNefvSgwQWlYzQNuNmoSyBpUYZaPCa+1E/9xrDnY
Orehq0xoK22Da9rbfNTOx0ey71ONjPSSt0I2ivVsJlFb0HRTFuKW0328x3Cb2MA+
NEyHY6BwdV8ejMJ7pr0MDW8CAwEAAQ==
-----END PUBLIC KEY-----";

        public LicenseValidationResult Check()
        {
            Log.Information("LicenseCheck.Check is called");
            LicenseValidationResult licenseValidationResult = new LicenseValidationResult();
            Log.Debug("Trying to read license file");
            try
            {
                var txtreader = new StringReader(publicKey);
                var keyPair = (AsymmetricKeyParameter)new PemReader(txtreader).ReadObject();
                License license;

                try
                {
                    StreamReader xr = new StreamReader(hostingEnv.WebRootPath + $@"\License\eLogin.lic");
                    xr.Close();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception while trying to read license file");
                    Log.Error("No license file detected. Please contact Expertflow to obtain demo or permenant license.");
                    licenseValidationResult.message = "No license file detected. Please contact Expertflow to obtain demo or permenant license.";
                    licenseValidationResult.code = 404;
                    return(licenseValidationResult);
                }

                StreamReader r = new StreamReader(hostingEnv.WebRootPath + $@"\License\eLogin.lic");
                string json = r.ReadToEnd();
                r.Close();
                Log.Debug("Deserializing license file");
                license = JsonConvert.DeserializeObject<License>(json);
                Log.Debug("Decrypting license signature");
                string decryptedJson = RsaDecrypt(license.Signature, keyPair);
                License decryptedLicense = JsonConvert.DeserializeObject<License>(decryptedJson);
                licenseValidationResult.license = decryptedLicense;
                Log.Debug("Veifying license file");
                if (license.CustomerName == decryptedLicense.CustomerName && license.ExpiryDate == decryptedLicense.ExpiryDate && license.MaxChannels == decryptedLicense.MaxChannels && license.CustomerRepository == decryptedLicense.CustomerRepository && license.MaxUsers == decryptedLicense.MaxUsers)
                {

                    if (license.CustomerRepository)
                    {
                        licenseValidationResult.isCustomerRepository = true;
                    }
                    else
                    {
                        licenseValidationResult.isCustomerRepository = false;
                    }
                    if (DateTime.Now > license.ExpiryDate && license.LicenseType!="Perpetual")
                    {
                        licenseValidationResult.code = 101;
                        if (!licenseValidationResult.message.IsNullOrEmpty()) licenseValidationResult.message = licenseValidationResult + Environment.NewLine;
                        licenseValidationResult.message = licenseValidationResult.message + "License validity expired! Please contact ExpertFlow for renewal.";
                        return (licenseValidationResult);
                    }
                    else
                    {
                        int ic = _context.IdentificationChannel.Count();
                        int logincount = _context.UserSession.Where(us => us.Action == "Login" && us.TimeStamp >= DateTime.UtcNow.AddMinutes(-1)).Count();
                        int logoutcount = _context.UserSession.Where(us => us.Action == "Logout" && us.TimeStamp >= DateTime.UtcNow.AddMinutes(-1)).Count();
                        int u = logincount - logoutcount;
                        int checkCode = 0;
                        if (ic >= license.MaxChannels)
                        {
                            checkCode = checkCode + 100;
                            if (!licenseValidationResult.message.IsNullOrEmpty()) licenseValidationResult.message = licenseValidationResult.message + Environment.NewLine;
                            licenseValidationResult.message = licenseValidationResult.message + "Note: Maximum license utilization reached for identification channels!";
                        }
                        //if (c >= license.CustomerRepository)
                        //{
                        //    checkCode = checkCode + 10;
                        //    if (!licenseValidationResult.message.IsNullOrEmpty()) licenseValidationResult.message = licenseValidationResult.message + Environment.NewLine;
                        //    licenseValidationResult.message = licenseValidationResult.message + "Maximum license utilization reached for customer repository!";
                        //}
                        if (u >= license.MaxUsers)
                        {
                            checkCode = checkCode + 1;
                            if (!licenseValidationResult.message.IsNullOrEmpty()) licenseValidationResult.message = licenseValidationResult.message + Environment.NewLine;
                            licenseValidationResult.message = licenseValidationResult.message + "Note: Maximum license utilization reached for eLogin users!";
                        }
                        if (checkCode > 0)
                        {
                            checkCode = checkCode + 1000;
                            licenseValidationResult.code = checkCode;
                            if (ic <= license.MaxChannels /*&& c <= license.CustomerRepository*/ && u <= license.MaxUsers) licenseValidationResult.isValid = true;
                            return (licenseValidationResult);
                        }
                    }
                    licenseValidationResult.isValid = true;
                    licenseValidationResult.code = 200;
                    
                    return (licenseValidationResult);
                }
                licenseValidationResult.message = "License file modification detected! Restored genuine license. Please contact Expertflow!";
                licenseValidationResult.code = 100;
                return (licenseValidationResult);
            }
            catch
            {
                licenseValidationResult.message = "License file modification detected! Restored genuine license. Please contact Expertflow!";
                licenseValidationResult.code = 100;
                return (licenseValidationResult);
            }
        }

        public static string RsaDecrypt(string base64Input, AsymmetricKeyParameter publickey)
        {
            Log.Information("LicenseCheck.RsaDecrypt is called");
            var bytesToDecrypt = Convert.FromBase64String(base64Input);

            //get a stream from the string
            AsymmetricCipherKeyPair keyPair;
            var decryptEngine = new Pkcs1Encoding(new RsaEngine());

            decryptEngine.Init(false, publickey);


            var decrypted = Encoding.UTF8.GetString(decryptEngine.ProcessBlock(bytesToDecrypt, 0, bytesToDecrypt.Length));
            return decrypted;
        }
    }
}
