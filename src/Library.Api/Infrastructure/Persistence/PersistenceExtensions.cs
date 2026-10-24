using Library.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Library.Api.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<LibraryDbContext>((provider, options) =>
            LibraryDbContextOptions.Configure(
                options,
                provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Postgres));

        return services;
    }
}
