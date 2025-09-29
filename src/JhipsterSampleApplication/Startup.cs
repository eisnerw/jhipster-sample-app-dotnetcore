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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Newtonsoft.Json;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Repositories;
using JhipsterSampleApplication.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Serilog;
using JhipsterSampleApplication.Middleware;
using Serilog.Extensions.Hosting;
using JhipsterSampleApplication.Services;

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

        // Logging control endpoints support
        services.AddSingleton<LoggingControlService>();

        // Serilog services are registered via Host.UseSerilog() in Program.cs

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
            // Enrich request with user before Serilog request logging runs
            .UseMiddleware<SerilogUserEnricherMiddleware>()
            .UseSerilogRequestLogging(opts =>
            {
                // Promote controller/method into SourceContext/CallerMemberName so module::method renders correctly
                opts.EnrichDiagnosticContext = (ctx, http) =>
                {
                    var endpoint = http.GetEndpoint();
                    var display = endpoint?.DisplayName;
                    // Try to get controller/action via metadata when available
                    var cad = endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
                    if (cad != null)
                    {
                        var cls = cad.ControllerTypeInfo?.FullName ?? cad.ControllerName;
                        var action = cad.ActionName;
                        if (!string.IsNullOrWhiteSpace(cls)) ctx.Set("SourceContext", cls);
                        if (!string.IsNullOrWhiteSpace(action)) ctx.Set("CallerMemberName", action);
                        ctx.Set("labels.SourceContext", cls);
                        ctx.Set("labels.CallerMemberName", action);
                    }
                    else if (!string.IsNullOrWhiteSpace(display))
                    {
                        ctx.Set("EndpointName", display);
                    }
                };
            })
            /*.Use(async (context, next) => {
                // This code will execute for every request.
                // Console.WriteLine("Request received: " + context.Request.Path + " from " + context.Request.Headers["Origin"]);
                await next(context); // Pass control to the next middleware.
            })*/;

        // Initialize named queries (views now come from entity specs)
        using (var scope = app.ApplicationServices.CreateScope())
        {
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
