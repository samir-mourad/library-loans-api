using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Library.Domain.Common;

namespace Library.Api.Infrastructure.Idempotency;

public static partial class IdempotencyKey
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int MaxLength = 100;

    public static Result<string> Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return IdempotencyErrors.KeyMissing;
        }

        return ValidFormat().IsMatch(headerValue) ? headerValue : IdempotencyErrors.KeyInvalid;
    }

    /// <summary>
    /// SHA-256 of a canonical representation of the request, so formatting differences in the JSON body
    /// (whitespace, property order) never turn a legitimate retry into a key mismatch.
    /// </summary>
    public static string HashRequest(string canonicalRequest) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,100}$")]
    private static partial Regex ValidFormat();
}
