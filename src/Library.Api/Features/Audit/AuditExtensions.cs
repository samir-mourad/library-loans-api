namespace Library.Api.Features.Audit;

public static class AuditExtensions
{
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        services.AddScoped<IAuditTrail, AuditTrail>();
        return services;
    }
}
