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
using JhipsterSampleApplication.Infrastructure.Services;
using Nest;
using JhipsterSampleApplication.Domain.Entities;

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
        var settings = new ConnectionSettings(new Uri(elasticsearchSettings.Url))
            .BasicAuthentication(elasticsearchSettings.Username, elasticsearchSettings.Password)
            .DefaultIndex(elasticsearchSettings.DefaultIndex);
        var client = new ElasticClient(settings);
        services.AddSingleton<IElasticClient>(client);

        services.AddScoped<IGenericBirthdayService<Birthday>, BirthdayService>();
        services.AddScoped<Domain.Services.Interfaces.IQueryBuilder, BirthdayQueryBuilder>();

        services.AddScoped<IBirthdayService, BirthdayService>();
        services.AddScoped<BirthdayService>();
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
            .UseApplicationIdentity();
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
