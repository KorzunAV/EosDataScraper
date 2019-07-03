using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.IO;
using EosDataScraper.Common;
using EosDataScraper.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EosDataScraper
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServices();

            var path = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot");
            var physicalProvider = new PhysicalFileProvider(path);
            services.AddSingleton<IFileProvider>(physicalProvider);

            services.AddMvc(o =>
                {
                    o.MaxModelValidationErrors = 50;
                    o.ValueProviderFactories.Insert(0, new SnakeCaseValueProviderFactory());
                })
                .AddJsonOptions(o =>
                {
                    o.SerializerSettings.Formatting = Formatting.Indented;
                    o.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
                    o.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                    o.SerializerSettings.ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new SnakeCaseNamingStrategy(),
                    };
                });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.AddLogger();

                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.AddLogger();

                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.RunOnceServices();
            app.AddServicesToQueue();

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
