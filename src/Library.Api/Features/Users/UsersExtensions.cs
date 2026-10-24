namespace Library.Api.Features.Users;

public static class UsersExtensions
{
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<GetUserHandler>();
        return services;
    }
}
