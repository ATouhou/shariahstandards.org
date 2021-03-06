using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Newtonsoft.Json.Serialization;

namespace api.core
{
  public class Startup
  {
    public Startup(IConfiguration configuration)
    {
      Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        //.AddJsonOptions(options => {
        //    options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        //});

      Services.ServiceRegistrationConfig.RegisterAllUniqueServices(services);
      services.AddDbContext<DataModel.ApplicationContext>(options =>
      {
        options.UseSqlServer(Configuration.GetConnectionString("DefaultDatabase"));
        options.UseLazyLoadingProxies();
      });

      // In production, the Angular files will be served from this directory
      services.AddSpaStaticFiles(configuration =>
            {
              configuration.RootPath = "ClientApp/dist/shariahstandards";
            });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      if (env.IsDevelopment())
      {
        app.UseDeveloperExceptionPage();
        app.UseStaticFiles(
          new StaticFileOptions
          {
            FileProvider = new PhysicalFileProvider(
              Path.Combine(Directory.GetCurrentDirectory(), "ClientApp/src/assets")),
              RequestPath = "/assets"
          }
        );
      }
      else
      {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
        app.UseStaticFiles(
          new StaticFileOptions
          {
            FileProvider = new PhysicalFileProvider(
              Path.Combine(Directory.GetCurrentDirectory(), "ClientApp/dist/shariahstandards/assets")),
            RequestPath = "/assets"
          }
        );
      }

      app.UseHttpsRedirection();
     
      app.UseSpaStaticFiles();

      app.UseMvc(routes =>
      {
        routes.MapRoute(
                  name: "default",
                  template: "{controller}/{action=Index}/{id?}");
      });

      app.UseSpa(spa =>
      {
              // To learn more about options for serving an Angular SPA from ASP.NET Core,
              // see https://go.microsoft.com/fwlink/?linkid=864501

              spa.Options.SourcePath = "ClientApp";

        if (env.IsDevelopment())
        {
              spa.UseAngularCliServer(npmScript: "start");
        }
      });
    }
  }
}
