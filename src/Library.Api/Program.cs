using Library.Api.Infrastructure.Actor;
using Library.Api.Infrastructure.Correlation;
using Library.Api.Infrastructure.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddErrorHandling()
    .AddCorrelation()
    .AddActor()
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
