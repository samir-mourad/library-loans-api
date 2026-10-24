var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });

app.Run();

/// <summary>
/// Application entry point. Exposed as public partial so integration tests can host it via WebApplicationFactory.
/// </summary>
public partial class Program;
