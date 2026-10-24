using Library.Api.Features.Loans;

namespace Library.Api.Infrastructure.Configuration;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Binds and validates typed options at startup, so missing or invalid configuration fails fast instead of on first use.
    /// </summary>
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services)
    {
        services.AddOptions<ConnectionStringsOptions>()
            .BindConfiguration(ConnectionStringsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LoansOptions>()
            .BindConfiguration(LoansOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddClock(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
