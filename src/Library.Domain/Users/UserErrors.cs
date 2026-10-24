using Library.Domain.Common;

namespace Library.Domain.Users;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("user.not_found", "The user was not found.");

    public static readonly Error DuplicateEmail = Error.Conflict("user.email_duplicate", "A user with this e-mail already exists.");

    public static readonly Error InvalidEmail = Error.Validation("user.email_invalid", "The e-mail address is not valid.");

    public static readonly Error InvalidName = Error.Validation(
        "user.name_invalid",
        $"The name must have 1 to {User.NameMaxLength} characters.");
}
