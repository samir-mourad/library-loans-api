namespace Library.Api.Infrastructure.Actor;

public static class ActorExtensions
{
    public static IServiceCollection AddActor(this IServiceCollection services)
    {
        services.AddScoped<ActorContext>();
        services.AddScoped<IActorAccessor>(provider => provider.GetRequiredService<ActorContext>());
        return services;
    }

    public static WebApplication UseActor(this WebApplication app)
    {
        app.UseMiddleware<ActorMiddleware>();
        return app;
    }
}
