namespace Library.Api.Infrastructure.Correlation;

public static class CorrelationExtensions
{
    public static IServiceCollection AddCorrelation(this IServiceCollection services)
    {
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<CorrelationContext>());
        return services;
    }

    /// <summary>
    /// Must run before any middleware that logs or writes error responses, so they carry the correlation id.
    /// </summary>
    public static WebApplication UseCorrelation(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }
}
