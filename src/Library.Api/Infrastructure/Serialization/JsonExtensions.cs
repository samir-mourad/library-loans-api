using System.Text.Json;
using System.Text.Json.Serialization;

namespace Library.Api.Infrastructure.Serialization;

public static class JsonExtensions
{
    /// <summary>
    /// Shared JSON settings for audit payloads: web casing and enums as names, matching the HTTP contract.
    /// </summary>
    public static readonly JsonSerializerOptions AuditSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Enums travel as names (e.g. <c>"Active"</c>) so clients and the OpenAPI document are self-explanatory.
    /// </summary>
    public static IServiceCollection AddJsonConventions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }
}
