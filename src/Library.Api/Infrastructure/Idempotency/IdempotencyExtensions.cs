namespace Library.Api.Infrastructure.Idempotency;

public static class IdempotencyExtensions
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services)
    {
        services.AddScoped<IdempotencyStore>();
        return services;
    }
}
