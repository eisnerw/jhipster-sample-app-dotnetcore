using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Enrichers.CallerInfo;
using Elastic.CommonSchema.Serilog;
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
    // Global dynamic level control
    var globalLevel = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);
    // Default file level to Information so the on-demand file has content by default
    var fileLevel = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);
    var rootLevel = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);

    // Register typed holders so DI can distinguish the two switches
    builder.Services.AddSingleton(new JhipsterSampleApplication.Services.GlobalLogLevel(globalLevel));
    builder.Services.AddSingleton(new JhipsterSampleApplication.Services.FileLogLevel(fileLevel));
    builder.Services.AddSingleton(new JhipsterSampleApplication.Services.RootLogLevel(rootLevel));

    builder.Host.UseSerilog((ctx, services, lc) =>
    {
        var configuration = ctx.Configuration;
        var port = 6514;
        if (int.TryParse(configuration.GetSection("Serilog")["SyslogPort"], out var portFromConf)) port = portFromConf;
        var url = configuration.GetSection("Serilog")["SyslogUrl"] ?? "localhost";
        var appName = configuration.GetSection("Serilog")["SyslogAppName"] ?? "JhipsterSampleApplicationApp";

        // Helper: demote Microsoft/System Information & Debug to Verbose
        bool ShouldDemote(Serilog.Events.LogEvent e)
        {
            if (e.Level != Serilog.Events.LogEventLevel.Information && e.Level != Serilog.Events.LogEventLevel.Debug) return false;
            static bool IsMsCategory(string s)
                => s.StartsWith("Microsoft.") || s.StartsWith("System.");

            // Prefer SourceContext, but fall back to Serilog logger and LoggerName enricher
            if (e.Properties.TryGetValue("SourceContext", out var sc))
            {
                var s = sc.ToString().Trim('"');
                if (s.StartsWith("Serilog.AspNetCore")) return false; // keep request timing at Information
                if (IsMsCategory(s)) return true;
            }
            if (e.Properties.TryGetValue("log.logger", out var ll))
            {
                var s = ll.ToString().Trim('"');
                if (s.StartsWith("Serilog.AspNetCore")) return false;
                if (IsMsCategory(s)) return true;
            }
            if (e.Properties.TryGetValue("LoggerName", out var ln))
            {
                var s = ln.ToString().Trim('"');
                if (s.StartsWith("Serilog.AspNetCore")) return false;
                if (IsMsCategory(s)) return true;
            }
            return false;
        }

        // First apply configuration-based settings so our dynamic controls can override them below
        lc.ReadFrom.Configuration(configuration)
          .MinimumLevel.ControlledBy(rootLevel)
          // Do not set category overrides here; allow root minimum to govern event creation
          .Enrich.FromLogContext()
          .Enrich.With<JHipsterNet.Web.Logging.LoggerNameEnricher>()
          .Enrich.With(new JhipsterSampleApplication.Logging.LevelStampingEnricher())
          .Enrich.WithMachineName()
          .Enrich.WithEnvironmentName()
          .Enrich.WithProcessId()
          .Enrich.WithThreadId()
          .Enrich.WithCallerInfo(includeFileInfo: true, assemblyPrefix: "JhipsterSampleApplication")
          .Enrich.WithProperty("service.name", appName)
          .Enrich.WithProperty("service.environment", configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
          .WriteTo.Logger(sys => sys
              .MinimumLevel.Verbose()
              .WriteTo.Sink(new JhipsterSampleApplication.Logging.LevelThresholdSink(
                   new JhipsterSampleApplication.Logging.ForwardToLoggerSink(
                       new LoggerConfiguration().WriteTo.TcpSyslog(url, port, appName).CreateLogger()
                   ),
                   () => globalLevel.MinimumLevel))
          );

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
                CustomFormatter = new EcsTextFormatter(new EcsTextFormatterConfiguration
                {
                    // Ensure logger and origin function are always populated in ECS fields
                    MapCustom = (doc, evt) =>
                    {
                        if (evt.Properties.TryGetValue("user.name", out var un))
                        {
                            if (doc.User == null) doc.User = new Elastic.CommonSchema.User();
                            doc.User.Name = un.ToString().Trim('"');
                        }
                        if (evt.Properties.TryGetValue("SourceContext", out var sc))
                        {
                            doc.Log ??= new Elastic.CommonSchema.Log();
                            var val = sc.ToString().Trim('"');
                            doc.Log.Logger = val;
                            if (doc.Labels == null) doc.Labels = new Elastic.CommonSchema.Labels();
                            doc.Labels["SourceContext"] = val;
                        }
                        if (evt.Properties.TryGetValue("CallerMemberName", out var cm))
                        {
                            if (doc.Labels == null) doc.Labels = new Elastic.CommonSchema.Labels();
                            doc.Labels["CallerMemberName"] = cm.ToString().Trim('"');
                        }
                        if (evt.Properties.TryGetValue("CallerFilePath", out var fp))
                        {
                            if (doc.Labels == null) doc.Labels = new Elastic.CommonSchema.Labels();
                            doc.Labels["CallerFilePath"] = fp.ToString().Trim('"');
                        }
                        if (evt.Properties.TryGetValue("CallerLineNumber", out var ln))
                        {
                            if (doc.Labels == null) doc.Labels = new Elastic.CommonSchema.Labels();
                            doc.Labels["CallerLineNumber"] = ln.ToString().Trim('"');
                        }
                        return doc;
                    }
                }),
                FailureCallback = (logEvent, ex) => Console.Error.WriteLine($"[Serilog-ES] {ex?.Message}"),
                BufferBaseFilename = Path.Combine(AppContext.BaseDirectory, "logs", "es-buffer"),
                BatchPostingLimit = 1000,
                Period = TimeSpan.FromSeconds(2)
            };
            if (!string.IsNullOrWhiteSpace(esUser) && !string.IsNullOrWhiteSpace(esPass))
            {
                esOptions.ModifyConnectionSettings = c => c.BasicAuthentication(esUser, esPass);
            }
            // Child logger used by the buffering sink to forward events
            // Forward logger must accept all levels; otherwise Debug/Verbose get dropped here
            var forwardLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(a => a.Elasticsearch(esOptions), blockWhenFull: true)
                .CreateLogger();

            // Build buffering sink and wrap it with a demoter that treats Microsoft.* Information/Debug as Verbose
            var bufferSink = new JhipsterSampleApplication.Logging.BufferOnErrorSink(
                new JhipsterSampleApplication.Logging.ForwardToLoggerSink(forwardLogger),
                capacity: 200,
                ttl: TimeSpan.FromSeconds(60),
                bufferInformation: false,
                // Auto-bypass buffering when global level is set to Debug or Verbose
                isBufferingEnabled: () => globalLevel.MinimumLevel > Serilog.Events.LogEventLevel.Debug);

            lc.WriteTo.Logger(eslog => eslog
                .MinimumLevel.Verbose()
                .WriteTo.Sink(new JhipsterSampleApplication.Logging.LevelDemotionSink(
                    new JhipsterSampleApplication.Logging.LevelThresholdSink(
                        bufferSink,
                        () => globalLevel.MinimumLevel),
                    ShouldDemote,
                    Serilog.Events.LogEventLevel.Verbose))
            );
        }

        // On-demand file logging with demotion of Microsoft/System to Verbose, then threshold by fileLevel
        lc.WriteTo.Logger(fileLog =>
        {
            var fileTarget = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(
                    path: Path.Combine(AppContext.BaseDirectory, "logs", "on-demand", "log-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {SourceContext} {UserName} : {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            fileLog.MinimumLevel.Verbose()
                .WriteTo.Sink(new JhipsterSampleApplication.Logging.LevelDemotionSink(
                    new JhipsterSampleApplication.Logging.LevelThresholdSink(
                        new JhipsterSampleApplication.Logging.ForwardToLoggerSink(fileTarget),
                        () => fileLevel.MinimumLevel),
                    ShouldDemote,
                    Serilog.Events.LogEventLevel.Verbose));
        });
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
    Serilog.Log.ForContext<Program>().Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Serilog.Log.CloseAndFlush();
}
