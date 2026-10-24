using Scalar.AspNetCore;

namespace Library.Api.Infrastructure.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    /// <summary>
    /// Exposes <c>/openapi/v1.json</c> and the Scalar UI at <c>/scalar</c> only in Development.
    /// </summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
