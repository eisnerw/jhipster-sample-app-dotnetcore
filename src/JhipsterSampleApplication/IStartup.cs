using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JhipsterSampleApplication;

public interface IStartup
{
    void Configure(IConfiguration configuration, IServiceCollection services);
    void ConfigureServices(IServiceCollection services, IHostEnvironment environment);
    void ConfigureMiddleware(IApplicationBuilder app, IHostEnvironment environment);
    void ConfigureEndpoints(IApplicationBuilder app, IHostEnvironment environment);
}
