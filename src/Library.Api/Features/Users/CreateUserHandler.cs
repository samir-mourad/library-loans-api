using Library.Api.Features.Audit;
using Library.Api.Infrastructure.Persistence;
using Library.Domain.Common;
using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Users;

/// <summary>
/// Registers a reader and records a <c>UserCreated</c> event without personal data: the append-only trail cannot be
/// erased, so name and e-mail live only in <c>users</c>, where they can be anonymized on a data-erasure request.
/// </summary>
public sealed class CreateUserHandler(LibraryDbContext dbContext, IAuditTrail auditTrail, TimeProvider clock)
{
    public async Task<Result<UserResponse>> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Email.Create(request.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        var created = User.Create(request.Name, email.Value, clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var user = created.Value;
        dbContext.Users.Add(user);
        auditTrail.Record(AuditEntityTypes.User, user.Id, AuditActions.UserCreated, new { Registered = true });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation(UserConfiguration.EmailIndexName))
        {
            return UserErrors.DuplicateEmail;
        }

        return UserResponse.From(user);
    }
}
