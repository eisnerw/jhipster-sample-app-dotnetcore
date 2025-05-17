using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using JHipsterNet.Web.Pagination.Swagger;

namespace JhipsterSampleApplication.Configuration;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerModule(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v3", new OpenApiInfo { Title = "JhipsterSampleApplication API", Version = "0.0.1" });
            // c.OperationFilter<PageableModelFilter>(); // Removed to prevent unwanted parameters

            // Add support for URL-encoded parameters
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseApplicationSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger(c =>
        {
            c.RouteTemplate = "{documentName}/api-docs";
        });
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/v3/api-docs", "JhipsterSampleApplication API");
            c.RoutePrefix = "swagger";
        });
        return app;
    }
}
