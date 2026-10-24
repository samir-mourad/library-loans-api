namespace Library.Domain;

/// <summary>
/// Assembly marker. The domain project holds pure business rules with no infrastructure dependencies,
/// so they can be unit tested in isolation.
/// </summary>
public static class DomainAssembly
{
    public const string Name = "Library.Domain";
}
