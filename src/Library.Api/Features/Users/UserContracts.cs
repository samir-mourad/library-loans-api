using System.ComponentModel.DataAnnotations;
using Library.Domain.Users;

namespace Library.Api.Features.Users;

public sealed class CreateUserRequest
{
    [Required, StringLength(User.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(Domain.Users.Email.MaxLength)]
    public string Email { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Name, string Email, DateTimeOffset CreatedAt)
{
    public static UserResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserResponse(user.Id, user.Name, user.Email.Value, user.CreatedAt);
    }
}
