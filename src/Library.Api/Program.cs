using Library.Api.Features.Audit;
using Library.Api.Features.Books;
using Library.Api.Features.Loans;
using Library.Api.Features.Users;
using Library.Api.Infrastructure.Actor;
using Library.Api.Infrastructure.Caching;
using Library.Api.Infrastructure.Configuration;
using Library.Api.Infrastructure.Correlation;
using Library.Api.Infrastructure.ErrorHandling;
using Library.Api.Infrastructure.Health;
using Library.Api.Infrastructure.Idempotency;
using Library.Api.Infrastructure.Observability;
using Library.Api.Infrastructure.OpenApi;
using Library.Api.Infrastructure.Persistence;
using Library.Api.Infrastructure.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddStructuredLogging();

builder.Services
    .AddApplicationOptions()
    .AddClock()
    .AddPersistence()
    .AddCaching()
    .AddJsonConventions()
    .AddErrorHandling()
    .AddCorrelation()
    .AddActor()
    .AddObservability(builder.Configuration, builder.Environment)
    .AddHealth()
    .AddApiDocumentation()
    .AddIdempotency()
    .AddAudit()
    .AddBooks()
    .AddUsers()
    .AddLoans();

var app = builder.Build();

app.UseCorrelation();
app.UseErrorHandling();
app.UseActor();

app.MapHealthEndpoints();
app.MapApiDocumentation();
app.MapBookEndpoints();
app.MapUserEndpoints();
app.MapLoanEndpoints();
app.MapAuditEndpoints();

app.Run();

/// <summary>
/// Application entry point. Exposed as public partial so integration tests can host it via WebApplicationFactory.
/// </summary>
public partial class Program;
