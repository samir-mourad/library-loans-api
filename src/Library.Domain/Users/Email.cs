using System.Text.RegularExpressions;
using Library.Domain.Common;

namespace Library.Domain.Users;

/// <summary>
/// E-mail address normalized to lower case so uniqueness does not depend on capitalization.
/// Validation is pragmatic (shape only); proving ownership requires sending a message, which is out of scope.
/// </summary>
public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Result<Email> Create(string? raw)
    {
        var candidate = raw?.Trim().ToLowerInvariant() ?? string.Empty;

        return candidate.Length <= MaxLength && ValidShape().IsMatch(candidate)
            ? new Email(candidate)
            : UserErrors.InvalidEmail;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$")]
    private static partial Regex ValidShape();
}
