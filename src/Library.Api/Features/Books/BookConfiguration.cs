using Library.Domain.Books;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Api.Features.Books;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public const string IsbnIndexName = "ix_books_isbn";

    private const int IsbnLength = 13;

    /// <summary>
    /// Check constraints mirror the domain invariants as a last line of defense against concurrent or out-of-band writes.
    /// </summary>
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books", table =>
        {
            table.HasCheckConstraint("ck_books_available_copies_range", "available_copies BETWEEN 0 AND total_copies");
            table.HasCheckConstraint("ck_books_total_copies_range", $"total_copies BETWEEN {Book.MinCopies} AND {Book.MaxCopies}");
        });

        builder.HasKey(book => book.Id);
        builder.Property(book => book.Id).ValueGeneratedNever();
        builder.Property(book => book.Title).HasMaxLength(Book.TitleMaxLength);
        builder.Property(book => book.Author).HasMaxLength(Book.AuthorMaxLength);
        builder.Property(book => book.Isbn)
            .HasConversion(isbn => isbn.Value, value => Isbn.Create(value).Value)
            .HasMaxLength(IsbnLength)
            .IsFixedLength();
        builder.Ignore(book => book.LoanedCopies);

        builder.HasIndex(book => book.Isbn).IsUnique().HasDatabaseName(IsbnIndexName);
        builder.HasIndex(book => book.Title);
    }
}
