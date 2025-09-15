using System;
using JhipsterSampleApplication.Infrastructure.Data;
using JhipsterSampleApplication.Configuration;
using JhipsterSampleApplication.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using JhipsterSampleApplication.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

[assembly: ApiController]

namespace JhipsterSampleApplication;

public class Startup : IStartup
{
    public virtual void Configure(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddAppSettingsModule(configuration);

        AddDatabase(configuration, services);
    }

    public virtual void ConfigureServices(IServiceCollection services, IHostEnvironment environment)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        services
            .AddSecurityModule()
            .AddProblemDetailsModule()
            .AddAutoMapperModule()
            .AddSwaggerModule()
            .AddWebModule()
            .AddRepositoryModule()
            .AddServiceModule();

        // Add ElasticSearch services
        var elasticsearchSettings = configuration.GetSection("Elasticsearch").Get<ElasticsearchSettings>();
        string url = configuration.GetValue<string>("Elasticsearch:Url")!;
        string defaultIndex = configuration.GetValue<string>("Elasticsearch:DefaultIndex")!;    

        var node = new Uri(url);
        // New v8 client (Elastic.Clients.Elasticsearch)
        var settings = new ElasticsearchClientSettings(node)
            .DefaultIndex(defaultIndex);
        if (!string.IsNullOrWhiteSpace(elasticsearchSettings?.Username) && !string.IsNullOrWhiteSpace(elasticsearchSettings?.Password))
        {
            settings = settings.Authentication(new BasicAuthentication(elasticsearchSettings.Username, elasticsearchSettings.Password));
        }

        var esClient = new ElasticsearchClient(settings);
        services.AddSingleton(esClient);
        services.AddSingleton<JhipsterSampleApplication.Domain.Services.IEntitySpecRegistry, JhipsterSampleApplication.Domain.Services.EntitySpecRegistry>();
        services.AddScoped<IViewRepository, ViewRepository>();
        services.AddScoped<IViewService, ViewService>();
        services.AddScoped<ViewInitializationService>();
        services.AddScoped<NamedQueryInitializationService>();
        services.AddScoped<JhipsterSampleApplication.Domain.Services.EntityService>();
    }

    public virtual void ConfigureMiddleware(IApplicationBuilder app, IHostEnvironment environment)
    {
        IServiceProvider serviceProvider = app.ApplicationServices;
        var securitySettingsOptions = serviceProvider.GetRequiredService<IOptions<SecuritySettings>>();
        var securitySettings = securitySettingsOptions.Value;
        app
            .UseApplicationSecurity(securitySettings)
            .UseApplicationProblemDetails(environment)
            .UseApplicationDatabase(environment)
            .UseApplicationIdentity()
            /*.Use(async (context, next) => {
                // This code will execute for every request.
                // Console.WriteLine("Request received: " + context.Request.Path + " from " + context.Request.Headers["Origin"]);
                await next(context); // Pass control to the next middleware.
            })*/;

        // Initialize views from JSON files
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var viewInitializationService = scope.ServiceProvider.GetRequiredService<ViewInitializationService>();
            viewInitializationService.InitializeViewsAsync().GetAwaiter().GetResult();

            var namedQueryInitializationService = scope.ServiceProvider.GetRequiredService<NamedQueryInitializationService>();
            namedQueryInitializationService.InitializeNamedQueriesAsync().GetAwaiter().GetResult();
        }
    }

    public virtual void ConfigureEndpoints(IApplicationBuilder app, IHostEnvironment environment)
    {
        app
            .UseApplicationSwagger()
            .UseApplicationWeb(environment);
    }

    protected virtual void AddDatabase(IConfiguration configuration, IServiceCollection services)
    {
        services.AddDatabaseModule(configuration);
    }
}

public class ElasticsearchSettings
{
    public string Url { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string DefaultIndex { get; set; }
}
