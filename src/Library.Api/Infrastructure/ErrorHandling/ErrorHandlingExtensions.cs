namespace Library.Api.Infrastructure.ErrorHandling;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsEnricher.Enrich);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddValidation();
        return services;
    }

    /// <summary>
    /// Converts unhandled exceptions and empty-body error status codes (404, 405...) into Problem Details.
    /// </summary>
    public static WebApplication UseErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
