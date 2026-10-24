using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    loaned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loans", x => x.id);
                    table.CheckConstraint("ck_loans_cancelled_at", "(status = 'Cancelled') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("ck_loans_due_after_loan", "due_at > loaned_at");
                    table.CheckConstraint("ck_loans_returned_at", "(status = 'Returned') = (returned_at IS NOT NULL)");
                    table.CheckConstraint("ck_loans_status", "status IN ('Active', 'Returned', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_loans_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_loans_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loans_book_id_loaned_at",
                table: "loans",
                columns: new[] { "book_id", "loaned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_user_id_loaned_at",
                table: "loans",
                columns: new[] { "user_id", "loaned_at" });

            migrationBuilder.CreateIndex(
                name: "ux_loans_active_book_user",
                table: "loans",
                columns: new[] { "book_id", "user_id" },
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loans");
        }
    }
}
