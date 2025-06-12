using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using System.Security.Authentication;
using JhipsterSampleApplication;
using JhipsterSampleApplication.Configuration;
using IStartup = JhipsterSampleApplication.IStartup;
using static JHipsterNet.Core.Boot.BannerPrinter;
using Microsoft.Extensions.Hosting;
using JhipsterSampleApplication.Domain;
using System.Globalization;
using Nest;
using System.Numerics;
using System.Diagnostics;
using JhipsterSampleApplication.Domain.Entities;
using System.Threading;

PrintBanner(10 * 1000);

if (false && !Debugger.IsAttached)
{
    Console.WriteLine($"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
    while (!Debugger.IsAttached)
    {
        Thread.Sleep(100);  // Wait for debugger to attach
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

    builder.Logging.AddSerilog(builder.Configuration);

    var config = builder.Configuration;
    bool useBonsai = config.GetValue<bool>("Elasticsearch:UseBonsai");
    string url = useBonsai ? config.GetValue<string>("Bonsai:Url")! : config.GetValue<string>("Elasticsearch:Url")!;
    string username = useBonsai ? config.GetValue<string>("Bonsai:Username")! : config.GetValue<string>("Elasticsearch:Username")!;
    string password = useBonsai ? config.GetValue<string>("Bonsai:Password")! : config.GetValue<string>("Elasticsearch:Password")!;
    string defaultIndex = config.GetValue<string>("Elasticsearch:DefaultIndex")!;    

    var node = new Uri(url);
    var settings = new ConnectionSettings(node).DefaultIndex(defaultIndex);
    if (useBonsai && !string.IsNullOrEmpty(username))
    {
        settings = settings.BasicAuthentication(username, password);
    }
    var elastic = new ElasticClient(settings);
    var SearchResponse = elastic.Search<Birthday>(s => s
        .Query(q => q.MatchAll())
    );
    Console.WriteLine($"Got {SearchResponse.Hits.Count} hits");    

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
