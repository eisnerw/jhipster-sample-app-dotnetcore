using Scrutor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services;
using JhipsterSampleApplication.Domain.Services.Interfaces;

namespace JhipsterSampleApplication.Configuration;

public static class ServiceStartup
{
    public static IServiceCollection AddServiceModule(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromAssembliesOf(typeof(ServicesInterfacesAssemblyHelper), typeof(ServicesClassesAssemblyHelper))
                // Find services and register its matching interfaces/implementations.
                // For example: JobService matches IJobService, EmployeeService matches IEmployeeService, etc...
                .AddClasses(classes => classes.InNamespaces(ServicesClassesAssemblyHelper.Namespace, "JhipsterSampleApplication.Domain.Services"))
                    .UsingRegistrationStrategy(RegistrationStrategy.Replace(ReplacementBehavior.ServiceType))
                    .AsMatchingInterface()
                    .WithScopedLifetime()

                // Now find services with class name ending with 'ExtendedService' and register it to interfaces
                // it implements.
                // For example: if JobExtendedService class is present and implements IJobService, then register
                // it as the implementation for IJobService, replacing the generated service class (JobService).
                .AddClasses(classes => classes.Where(type => (type.Namespace.Equals(ServicesClassesAssemblyHelper.Namespace) || 
                                                            type.Namespace.Equals("JhipsterSampleApplication.Domain.Services")) &&
                                                            type.Name.EndsWith("ExtendedService")))
                    .UsingRegistrationStrategy(RegistrationStrategy.Replace(ReplacementBehavior.ServiceType))
                    .AsImplementedInterfaces()
                    .WithScopedLifetime()
        );
        services.AddScoped<IBqlService<Birthday>>(sp => new BqlService<Birthday>(
            sp.GetRequiredService<ILogger<BqlService<Birthday>>>(),
            sp.GetRequiredService<INamedQueryService>(),
            BqlService<Birthday>.LoadSpec("birthday"),
            "birthdays"));

        services.AddScoped<IBqlService<Movie>>(sp => new BqlService<Movie>(
            sp.GetRequiredService<ILogger<BqlService<Movie>>>(),
            sp.GetRequiredService<INamedQueryService>(),
            BqlService<Movie>.LoadSpec("movie"),
            "movies"));

        services.AddScoped<IBqlService<Supreme>>(sp => new BqlService<Supreme>(
            sp.GetRequiredService<ILogger<BqlService<Supreme>>>(),
            sp.GetRequiredService<INamedQueryService>(),
            BqlService<Supreme>.LoadSpec("supreme"),
            "supreme"));

        return services;
    }
}
