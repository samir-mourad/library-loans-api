using Library.Api.Infrastructure.Actor;
using Library.Api.Infrastructure.Correlation;
using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddStructuredLogging();

builder.Services
    .AddErrorHandling()
    .AddCorrelation()
    .AddActor()
    .AddObservability(builder.Configuration, builder.Environment)
    .AddHealthChecks();

var app = builder.Build();

app.UseCorrelation();
app.UseErrorHandling();
app.UseActor();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });

app.Run();

/// <summary>
/// Application entry point. Exposed as public partial so integration tests can host it via WebApplicationFactory.
/// </summary>
public partial class Program;
