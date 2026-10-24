using Library.Domain.Common;

namespace Library.Domain.Books;

/// <summary>
/// International Standard Book Number, always normalized to ISBN-13 so an ISBN-10 and its ISBN-13 equivalent are equal.
/// </summary>
public sealed record Isbn
{
    private const int Isbn10Length = 10;
    private const int Isbn13Length = 13;
    private const string Isbn10To13Prefix = "978";

    private Isbn(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Accepts ISBN-10 or ISBN-13 with optional hyphens or spaces and validates the check digit.
    /// </summary>
    public static Result<Isbn> Create(string? raw)
    {
        var candidate = Clean(raw);

        return candidate.Length switch
        {
            Isbn10Length when IsValidIsbn10(candidate) => new Isbn(ConvertToIsbn13(candidate)),
            Isbn13Length when IsValidIsbn13(candidate) => new Isbn(candidate),
            _ => BookErrors.InvalidIsbn
        };
    }

    public override string ToString() => Value;

    private static string Clean(string? raw) =>
        raw is null
            ? string.Empty
            : string.Concat(raw.Where(character => character is not ('-' or ' '))).ToUpperInvariant();

    private static bool IsValidIsbn10(string candidate)
    {
        if (!candidate[..9].All(char.IsAsciiDigit) || !(char.IsAsciiDigit(candidate[9]) || candidate[9] == 'X'))
        {
            return false;
        }

        var sum = 0;
        for (var position = 0; position < Isbn10Length; position++)
        {
            var digit = candidate[position] == 'X' ? 10 : candidate[position] - '0';
            sum += (Isbn10Length - position) * digit;
        }

        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string candidate) =>
        candidate.All(char.IsAsciiDigit) && Isbn13CheckDigit(candidate[..12]) == candidate[12] - '0';

    private static string ConvertToIsbn13(string isbn10)
    {
        var body = Isbn10To13Prefix + isbn10[..9];
        return body + Isbn13CheckDigit(body);
    }

    private static int Isbn13CheckDigit(string first12Digits)
    {
        var sum = 0;
        for (var position = 0; position < first12Digits.Length; position++)
        {
            var weight = position % 2 == 0 ? 1 : 3;
            sum += (first12Digits[position] - '0') * weight;
        }

        return (10 - sum % 10) % 10;
    }
}
