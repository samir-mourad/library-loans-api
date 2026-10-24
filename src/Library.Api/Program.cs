using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Actor;
using Library.Api.Infrastructure.Configuration;
using Library.Api.Infrastructure.Correlation;
using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Health;
using Library.Api.Infrastructure.Observability;
using Library.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddStructuredLogging();

builder.Services
    .AddApplicationOptions()
    .AddClock()
    .AddPersistence()
    .AddErrorHandling()
    .AddCorrelation()
    .AddActor()
    .AddObservability(builder.Configuration, builder.Environment)
    .AddHealth()
    .AddAudit();

var app = builder.Build();

app.UseCorrelation();
app.UseErrorHandling();
app.UseActor();

app.MapHealthEndpoints();

app.Run();

/// <summary>
/// Application entry point. Exposed as public partial so integration tests can host it via WebApplicationFactory.
/// </summary>
public partial class Program;
