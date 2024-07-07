using Microsoft.AspNetCore.Hosting;

[assembly: HostingStartup(typeof(eLogin.Areas.Identity.IdentityHostingStartup))]
namespace eLogin.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
            });
        }
    }
}