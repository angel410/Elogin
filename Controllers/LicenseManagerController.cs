using System.Net.Http.Headers;
//using System.Runtime.InteropServices.WindowsRuntime;
using eLogin.Data;
using eLogin.Models;
using eLogin.Models.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class LicenseManagerController : Controller
    {
        private readonly DatabaseContext _context;
        private IHostingEnvironment hostingEnv;
        private readonly ILogger<ImportCustomersController> _logger;

        public LicenseManagerController(DatabaseContext context, IHostingEnvironment env, ILogger<ImportCustomersController> logger)
        {
            _context = context;
            this.hostingEnv = env;
            _logger = logger;
        }

        public async Task<ActionResult> Index()
        {
            ViewBag.Error = "";
            LicenseCheck LC = new LicenseCheck(_context, hostingEnv);

            LicenseValidationResult LVR = LC.Check(); 
            if(!LVR.message.IsNullOrEmpty()) ViewBag.Error = LVR.message.Replace("eLogin.Models.LicenseValidationResult", "");
            return View(LC.Check().license);
        }
        
        [AllowAnonymous]
        public async Task<ActionResult> LicenseValidationResult()
        {
            ViewBag.Error = "";
            LicenseCheck LC = new LicenseCheck(_context, hostingEnv);
            LicenseValidationResult LVR = LC.Check();
            LVR = LC.Check();
            if (!LVR.message.IsNullOrEmpty()) ViewBag.Error = LVR.message.Replace("eLogin.Models.LicenseValidationResult", "");
            return View();
        }

        [AcceptVerbs("Post")]
        public IActionResult Save(IList<IFormFile> UploadFiles)
        {
            try
            {
                foreach (var file in UploadFiles)
                {
                    var filename = ContentDispositionHeaderValue
                                        .Parse(file.ContentDisposition)
                                        .FileName
                                        .Trim('"');
                    filename = hostingEnv.WebRootPath + $@"\License\eLogin.lic";
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
                        System.IO.File.Delete(hostingEnv.WebRootPath + $@"\License\eLogin.lic");
                        if (!System.IO.File.Exists(filename))
                        {
                            using (FileStream fs = System.IO.File.Create(filename))
                            {
                                file.CopyTo(fs);
                                fs.Flush();
                            }
                        }
                    }
                    return RedirectToAction(nameof(Index), "LicenseManager");
                    //return RedirectToPage();

                    

                }
            }
            catch (Exception e)
            {
                Response.Clear();
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File failed to upload";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;

            }
            return RedirectToAction("Index");
        }

        

        

        

    }
}
