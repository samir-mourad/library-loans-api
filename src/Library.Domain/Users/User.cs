using Library.Domain.Common;

namespace Library.Domain.Users;

/// <summary>
/// A registered reader. Holds the personal data of the system, kept out of the append-only audit trail.
/// </summary>
public sealed class User
{
    public const int NameMaxLength = 200;

    private User()
    {
        Name = string.Empty;
        Email = null!;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Email Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<User> Create(string name, Email email, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            return UserErrors.InvalidName;
        }

        return new User
        {
            Id = Guid.CreateVersion7(now),
            Name = name.Trim(),
            Email = email,
            CreatedAt = now
        };
    }
}
