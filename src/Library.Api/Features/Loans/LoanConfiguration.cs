using Library.Domain.Books;
using Library.Domain.Loans;
using Library.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Api.Features.Loans;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public const string ActiveLoanIndexName = "ux_loans_active_book_user";

    private const int StatusMaxLength = 20;

    /// <summary>
    /// Foreign keys restrict deletes so loan history can never disappear; check constraints keep status and timestamps
    /// consistent; the partial unique index allows a single active loan per user and book, even under concurrency.
    /// </summary>
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans", table =>
        {
            table.HasCheckConstraint("ck_loans_status", "status IN ('Active', 'Returned', 'Cancelled')");
            table.HasCheckConstraint("ck_loans_returned_at", "(status = 'Returned') = (returned_at IS NOT NULL)");
            table.HasCheckConstraint("ck_loans_cancelled_at", "(status = 'Cancelled') = (cancelled_at IS NOT NULL)");
            table.HasCheckConstraint("ck_loans_due_after_loan", "due_at > loaned_at");
        });

        builder.HasKey(loan => loan.Id);
        builder.Property(loan => loan.Id).ValueGeneratedNever();
        builder.Property(loan => loan.Status).HasMaxLength(StatusMaxLength);
        builder.Property(loan => loan.CancellationReason).HasMaxLength(Loan.CancellationReasonMaxLength);

        builder.HasOne<Book>().WithMany().HasForeignKey(loan => loan.BookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(loan => loan.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(loan => new { loan.BookId, loan.LoanedAt });
        builder.HasIndex(loan => new { loan.UserId, loan.LoanedAt });
        builder.HasIndex(loan => new { loan.BookId, loan.UserId })
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName(ActiveLoanIndexName);
    }
}
