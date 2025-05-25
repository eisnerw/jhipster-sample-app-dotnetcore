using JhipsterSampleApplication.Infrastructure.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using JhipsterSampleApplication.Infrastructure.Data;
using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;

namespace JhipsterSampleApplication.Test.Setup;

public class TestStartup : Startup
{
    public override void Configure(IConfiguration configuration, IServiceCollection services)
    {
        base.Configure(configuration, services);
    }

    public override void ConfigureServices(IServiceCollection services, IHostEnvironment environment)
    {
        base.ConfigureServices(services, environment);

        if (ShouldSuppressOutput())
        {
            // Disable all logging output
            services.AddLogging(logging =>
            {
                logging.ClearProviders(); // Remove Console, Debug, etc.
            });
        }
    }

    public override void ConfigureMiddleware(IApplicationBuilder app, IHostEnvironment environment)
    {
        if (ShouldSuppressOutput())
        {
            Console.WriteLine($"Output is suppressed by environment variable SuppressOutput='{Environment.GetEnvironmentVariable("SuppressOutput")}'");
            // Suppress Console.WriteLine
            Console.SetOut(TextWriter.Null);
        } else {
            Console.WriteLine($"Output is NOT suppressed by environment variable SuppressOutput='{Environment.GetEnvironmentVariable("SuppressOutput")}'");
        }

        base.ConfigureMiddleware(app, environment);
    }

    public override void ConfigureEndpoints(IApplicationBuilder app, IHostEnvironment environment)
    {
        base.ConfigureEndpoints(app, environment);
    }

    protected override void AddDatabase(IConfiguration configuration, IServiceCollection services)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:"
        }.ToString());

        services.AddDbContext<ApplicationDatabaseContext>(context => context.UseSqlite(connection));
        services.AddScoped<DbContext>(provider => provider.GetService<ApplicationDatabaseContext>());
    }
    private static bool ShouldSuppressOutput()
    {
        var suppress = Environment.GetEnvironmentVariable("SuppressOutput");
        return string.Equals(suppress, "true", StringComparison.OrdinalIgnoreCase);
    }
}