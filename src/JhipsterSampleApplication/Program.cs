using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Enrichers.CallerInfo;
using System;
using System.IO;
using System.Security.Authentication;
using JhipsterSampleApplication;
using JhipsterSampleApplication.Configuration;
using IStartup = JhipsterSampleApplication.IStartup;
using static JHipsterNet.Core.Boot.BannerPrinter;

PrintBanner(10 * 1000);

if (false && !System.Diagnostics.Debugger.IsAttached)
{
    Console.WriteLine($"PID: {Environment.ProcessId}");
    while (!System.Diagnostics.Debugger.IsAttached)
    {
        System.Threading.Thread.Sleep(100);  // Wait for debugger to attach
    }
}

try
{
    var webAppOptions = new WebApplicationOptions
    {
        ContentRootPath = Directory.GetCurrentDirectory(),
        EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "ClientApp", "dist"),
        Args = args
    };
    var builder = WebApplication.CreateBuilder(webAppOptions);

    // Use Serilog with host integration (auto-registers DiagnosticContext etc.)
    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        var configuration = ctx.Configuration;
        var port = 6514;
        if (int.TryParse(configuration.GetSection("Serilog")["SyslogPort"], out var portFromConf)) port = portFromConf;
        var url = configuration.GetSection("Serilog")["SyslogUrl"] ?? "localhost";
        var appName = configuration.GetSection("Serilog")["SyslogAppName"] ?? "JhipsterSampleApplicationApp";

        lc.Enrich.FromLogContext()
          .Enrich.With<JHipsterNet.Web.Logging.LoggerNameEnricher>()
          .Enrich.WithMachineName()
          .Enrich.WithEnvironmentName()
          .Enrich.WithProcessId()
          .Enrich.WithThreadId()
          .Enrich.WithProperty("service.name", appName)
          .Enrich.WithProperty("service.environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
          .Enrich.WithCallerInfo(includeFileInfo: true, assemblyPrefix: "JhipsterSampleApplication")
          .WriteTo.TcpSyslog(url, port, appName)
          .ReadFrom.Configuration(configuration);

        // Optional Elasticsearch sink with ECS
        var esUrl = configuration.GetValue<string>("Elasticsearch:Url");
        if (!string.IsNullOrWhiteSpace(esUrl))
        {
            var esUser = configuration.GetValue<string>("Elasticsearch:Username");
            var esPass = configuration.GetValue<string>("Elasticsearch:Password");
            var indexFormat = configuration.GetValue<string>("Serilog:Elasticsearch:IndexFormat") ?? "app-logs";
            var esOptions = new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(esUrl))
            {
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = Serilog.Sinks.Elasticsearch.AutoRegisterTemplateVersion.ESv8,
                IndexFormat = indexFormat,
                DetectElasticsearchVersion = true,
                CustomFormatter = new Elastic.CommonSchema.Serilog.EcsTextFormatter(),
                FailureCallback = (logEvent, ex) => Console.Error.WriteLine($"[Serilog-ES] {ex?.Message}"),
                BufferBaseFilename = Path.Combine(AppContext.BaseDirectory, "logs", "es-buffer")
            };
            if (!string.IsNullOrWhiteSpace(esUser) && !string.IsNullOrWhiteSpace(esPass))
            {
                esOptions.ModifyConnectionSettings = c => c.BasicAuthentication(esUser, esPass);
            }
            lc.WriteTo.Elasticsearch(esOptions);
        }
    });

    IStartup startup = new Startup();

    startup.Configure(builder.Configuration, builder.Services);
    startup.ConfigureServices(builder.Services, builder.Environment);

    WebApplication app = builder.Build();

    startup.ConfigureMiddleware(app, app.Environment);
    startup.ConfigureEndpoints(app, app.Environment);

    app
        .MapGet("/",
            () =>
            "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

    app.Run();

    return 0;
}
catch (Exception ex)
{
    // Use ForContext to give a context to this static environment (for Serilog LoggerNameEnricher).
    Log.ForContext<Program>().Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
