using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Library.Api.Infrastructure.Persistence;

internal static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when the failure is a PostgreSQL unique violation on the given constraint or index. Relying on the
    /// constraint instead of checking beforehand keeps uniqueness correct under concurrent inserts.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == constraintName;
}
