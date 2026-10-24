using System.ComponentModel.DataAnnotations;

namespace Library.Api.Infrastructure.Configuration;

/// <summary>
/// Connection strings supplied via configuration or environment variables (<c>ConnectionStrings__Postgres</c>,
/// <c>ConnectionStrings__Redis</c>). Never committed with credentials.
/// </summary>
public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    [Required]
    public string Postgres { get; init; } = string.Empty;

    [Required]
    public string Redis { get; init; } = string.Empty;
}
