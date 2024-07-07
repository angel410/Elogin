using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog;
using Microsoft.EntityFrameworkCore;
using eLogin.Identity;
using eLogin.Models.Identity;
using eLogin.Models;
using eLogin.Services;
using eLogin.Settings;
using DNTCaptcha.Core;
using eLogin.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using eLogin;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;
builder.Logging.ClearProviders();
var configuration = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json")
    .Build();

builder.Services.AddControllers().AddXmlSerializerFormatters();

//builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
//    .AddEntityFrameworkStores<AmbulanceAgentsContext>();



builder.Services.AddIdentity<User, Role>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // Adjust this based on your requirements

})
        .AddRoles<Role>()
        .AddEntityFrameworkStores<DatabaseContext>()
        .AddDefaultTokenProviders();


builder.Services.AddDbContext<DatabaseContext>(options =>
               options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")).UseLazyLoadingProxies(), ServiceLifetime.Transient);



builder.Services.Configure<LdapSettings>(configuration.GetSection("LdapSettings"));


builder.Services.AddScoped<DatabaseContext>();
builder.Services.AddScoped<PasswordService>();

builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<LdapSignInManager>();
builder.Services.AddScoped<LicenseCheck>();




builder.Services.AddDNTCaptcha(options =>
              options.UseCookieStorageProvider()
                  .ShowThousandsSeparators(false)
          );
builder.Services.AddCors(c =>
{
    c.AddPolicy("AllowOrigin", options => options.AllowAnyOrigin());
});
builder.Services.ConfigureApplicationCookie(options =>
              {
                  options.Cookie.Name = "eLogin";
                  options.Cookie.HttpOnly = true;
                  options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                  //options.LoginPath = "/Account/Signin"; // If the LoginPath is not set here, ASP.NET Core will default to /Account/Login
                  //options.LogoutPath = "/Account/Signout"; // If the LogoutPath is not set here, ASP.NET Core will default to /Account/Logout
                  //options.AccessDeniedPath = "/Account/AccessDenied"; // If the AccessDeniedPath is not set here, ASP.NET Core will default to /Account/AccessDenied
                  options.SlidingExpiration = true;
                  options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
              });

builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdGroup-DomainAdmins", policy =>
                    policy.RequireClaim("AdGroup", "Domain Admins"));
    options.AddPolicy("eLoginAdmin", policy =>
                   policy.RequireClaim("AdGroup", "eLoginAdmin"));
});

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    //options.Password.RequireDigit = true;
    //options.Password.RequireLowercase = true;
    //options.Password.RequireNonAlphanumeric = true;
    //options.Password.RequireUppercase = true;
    //options.Password.RequiredLength = 6;
    //options.Password.RequiredUniqueChars = 1;

    // Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings.
    //options.User.AllowedUserNameCharacters =
    //"Mgo+DSMBMAY9C3t2V1hhQlJAfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hSn5bdk1jWn5adX1URGBV";
    options.User.RequireUniqueEmail = false;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews(config => {
    var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
    config.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddControllersWithViews()
            // Maintain property names during serialization. See:
            // https://github.com/aspnet/Announcements/issues/194
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // This lambda determines whether user consent for non-essential cookies is needed for a given request.
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".AdventureWorks.Session";
    options.IdleTimeout = TimeSpan.FromDays(1);
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Host.UseSerilog((hostContext, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(hostContext.Configuration) // Read other Serilog settings from appsettings.json

        .WriteTo.File("C:\\EFLogs\\logElogin.txt", rollingInterval: RollingInterval.Day)
        .WriteTo.Console();
});
//Log.Logger = new LoggerConfiguration()


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseDeveloperExceptionPage();
app.UseHostFiltering();
//app.UseSerilogRequestLogging(); // This logs HTTP request information

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();
app.MapDefaultControllerRoute();


app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});


app.Run();

