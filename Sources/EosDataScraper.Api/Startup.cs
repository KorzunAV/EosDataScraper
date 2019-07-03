using System;
using System.IO;
using System.Reflection;
using EosDataScraper.Api.Contexts;
using EosDataScraper.Api.Extensions;
using EosDataScraper.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace EosDataScraper.Api
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

            var defaultConnection = Configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<PostgresDbContext>(options => options.UseNpgsql(defaultConnection));

            var path = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot");
            var physicalProvider = new PhysicalFileProvider(path);
            services.AddSingleton<IFileProvider>(physicalProvider);

            var jwtSettings = new JwtSettings(DateTime.UtcNow);
            Configuration.GetSection(nameof(JwtSettings)).Bind(jwtSettings);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,
                        //ValidateLifetime = true,
                        //LifetimeValidator = (before, expires, token, param) => { return expires > DateTime.UtcNow; },

                        IssuerSigningKey = jwtSettings.GetSymmetricSecurityKey()
                    };
                });

            services.AddMvc(o =>
                {
                    o.MaxModelValidationErrors = 50;
                    o.ValueProviderFactories.Insert(0, new SnakeCaseValueProviderFactory());
                })
                .AddRazorPagesOptions(options => { options.Conventions.AuthorizePage("/Auth"); })
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
            services.Configure<FormOptions>(
                x =>
                {
                    x.ValueLengthLimit = int.MaxValue;
                    x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
                });
            services.AddDistributedMemoryCache();

            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "EOS explorer API V1",
                    //Description = "A simple example ASP.NET Core Web API",
                    //TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Alex Korzun",
                        Email = "KorzunAV@gmail.com",
                        Url = new Uri("https://chainartsoft.com/"),
                    },
                    //License = new OpenApiLicense
                    //{
                    //    Name = "Use under LICX",
                    //    Url = new Uri("https://example.com/license"),
                    //}
                });
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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

            app.UseStaticFiles();
            app.UseSwagger();

            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "EosDataScraper API V1"); });


            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Auth}/{action=Index}/{id?}");
            });
        }
    }
}