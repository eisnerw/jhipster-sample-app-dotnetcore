using JHipsterNet.Web.Pagination.Binders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;
using JhipsterSampleApplication.Formatters;

namespace JhipsterSampleApplication.Configuration;

public static class WebConfiguration
{

    public static IServiceCollection AddWebModule(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddMvc();

        //https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-2.2
        services.AddHealthChecks();

        services.AddControllers(options => { 
            options.ModelBinderProviders.Insert(0, new PageableBinderProvider());
            options.InputFormatters.Insert(0, new PlainTextInputFormatter());
        })
        .AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            options.SerializerSettings.Formatting = Formatting.Indented;
            options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        })
        .AddControllersAsServices();

        services.AddSpaStaticFiles(configuration =>
        {
            configuration.RootPath = "ClientApp/dist";
        });

        return services;
    }

    public static IApplicationBuilder UseApplicationWeb(this IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseSpaStaticFiles();

        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });


        app.UseHealthChecks("/health");
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") &&
                !context.Request.Path.StartsWithSegments("/management") &&
                !context.Request.Path.StartsWithSegments("/swagger") &&
                !Path.HasExtension(context.Request.Path.Value))
            {
                context.Request.Path = "/";
            }

            await next();
        }); 
       app.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";
            // Static files for the SPA are served from ClientApp/dist via UseSpaStaticFiles
            // When running ng build --watch, changes will appear without restarting the server
        });

        return app;
    }

}
