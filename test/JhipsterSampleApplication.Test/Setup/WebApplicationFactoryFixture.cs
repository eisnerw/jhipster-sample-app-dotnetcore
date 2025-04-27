using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using JhipsterSampleApplication;

namespace JhipsterSampleApplication.Test.Setup
{
    public class WebApplicationFactoryFixture : WebApplicationFactory<Startup>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Add any test-specific service configurations here
            });
        }

        public T GetRequiredService<T>() where T : notnull
        {
            return Services.GetRequiredService<T>();
        }
    }
} 