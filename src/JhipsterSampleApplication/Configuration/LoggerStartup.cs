using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using JHipsterNet.Web.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Syslog;
using Serilog.Sinks.Elasticsearch;
using Elastic.CommonSchema.Serilog;
using Serilog.Context;
using Serilog.Enrichers.CallerInfo;

namespace JhipsterSampleApplication.Configuration;

public static class SerilogConfiguration
{
    const string SerilogSection = "Serilog";
    const string SyslogPort = "SyslogPort";
    const string SyslogUrl = "SyslogUrl";
    const string SyslogAppName = "SyslogAppName";

    /// <summary>
    /// Create application logger from configuration.
    /// </summary>
    /// <returns></returns>
    public static ILoggingBuilder AddSerilog(this ILoggingBuilder loggingBuilder, IConfiguration appConfiguration)
    {
        var port = 6514;

        // for logger configuration
        // https://github.com/serilog/serilog-settings-configuration
        if (appConfiguration.GetSection(SerilogSection)[SyslogPort] != null)
        {
            if (int.TryParse(appConfiguration.GetSection(SerilogSection)[SyslogPort], out var portFromConf))
            {
                port = portFromConf;
            }
        }

        var url = appConfiguration.GetSection(SerilogSection)[SyslogUrl] != null
            ? appConfiguration.GetSection(SerilogSection)[SyslogUrl]
            : "localhost";
        var appName = appConfiguration.GetSection(SerilogSection)[SyslogAppName] != null
            ? appConfiguration.GetSection(SerilogSection)[SyslogAppName]
            : "JhipsterSampleApplicationApp";
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With<LoggerNameEnricher>()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("service.name", appName)
            .Enrich.WithProperty("service.environment", appConfiguration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development")
            // Syslog is optional; enable only if explicitly configured
            .WriteTo.Logger(lc =>
            {
                bool syslogEnabled = string.Equals(appConfiguration.GetSection(SerilogSection)["SyslogEnabled"], "true", StringComparison.OrdinalIgnoreCase);
                if (syslogEnabled)
                {
                    lc.WriteTo.TcpSyslog(url, port, appName);
                }
            })
            .ReadFrom.Configuration(appConfiguration);

        // Optional: Elasticsearch sink with ECS formatter
        var esUrl = appConfiguration.GetValue<string>("Elasticsearch:Url");
        if (!string.IsNullOrWhiteSpace(esUrl))
        {
            var esUser = appConfiguration.GetValue<string>("Elasticsearch:Username");
            var esPass = appConfiguration.GetValue<string>("Elasticsearch:Password");
            var indexFormat = appConfiguration.GetValue<string>("Serilog:Elasticsearch:IndexFormat") ?? "app-logs-{0:yyyy.MM.dd}";

            var esOptions = new ElasticsearchSinkOptions(new Uri(esUrl))
            {
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                IndexFormat = indexFormat,
                DetectElasticsearchVersion = true,
                CustomFormatter = new EcsTextFormatter(),
                FailureCallback = (logEvent, ex) => Console.Error.WriteLine($"[Serilog-ES] {ex?.Message}"),
                BufferBaseFilename = Path.Combine(AppContext.BaseDirectory, "logs", "es-buffer")
            };
            if (!string.IsNullOrWhiteSpace(esUser) && !string.IsNullOrWhiteSpace(esPass))
            {
                esOptions.ModifyConnectionSettings = (c) => c.BasicAuthentication(esUser, esPass);
            }
            loggerConfiguration = loggerConfiguration.WriteTo.Elasticsearch(esOptions);
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        return loggingBuilder.AddSerilog(Log.Logger);
    }
}
