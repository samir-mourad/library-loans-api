using Library.Api.Infrastructure.Persistence;
using Library.Domain.Common;
using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Features.Users;

public sealed class GetUserHandler(LibraryDbContext dbContext)
{
    public async Task<Result<UserResponse>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

        return user is null ? UserErrors.NotFound : UserResponse.From(user);
    }
}
