namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Generates random valid ISBNs so each test owns its data and tests can share the database in parallel.
/// </summary>
internal static class TestIsbn
{
    public static string NewIsbn13() => NewPair().Isbn13;

    public static (string Isbn10, string Isbn13) NewPair()
    {
        var body = Random.Shared.NextInt64(0, 1_000_000_000).ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
        var isbn13Body = "978" + body;

        return (body + Isbn10CheckCharacter(body), isbn13Body + Isbn13CheckDigit(isbn13Body));
    }

    private static char Isbn10CheckCharacter(string nineDigits)
    {
        var sum = nineDigits.Select((digit, index) => (10 - index) * (digit - '0')).Sum();
        var check = (11 - sum % 11) % 11;
        return check == 10 ? 'X' : (char)('0' + check);
    }

    private static int Isbn13CheckDigit(string twelveDigits)
    {
        var sum = twelveDigits.Select((digit, index) => (digit - '0') * (index % 2 == 0 ? 1 : 3)).Sum();
        return (10 - sum % 10) % 10;
    }
}
