using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Api.Features.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string EmailIndexName = "ix_users_email";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Name).HasMaxLength(User.NameMaxLength);
        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => Email.Create(value).Value)
            .HasMaxLength(Email.MaxLength);

        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}
