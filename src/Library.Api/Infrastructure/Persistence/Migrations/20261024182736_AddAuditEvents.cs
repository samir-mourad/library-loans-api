using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Library.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_type_entity_id_occurred_at",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_occurred_at",
                table: "audit_events",
                column: "occurred_at");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_audit_event_changes() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only: % is not allowed', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                CREATE TRIGGER audit_events_prevent_update_delete
                    BEFORE UPDATE OR DELETE ON audit_events
                    FOR EACH ROW EXECUTE FUNCTION prevent_audit_event_changes();

                CREATE TRIGGER audit_events_prevent_truncate
                    BEFORE TRUNCATE ON audit_events
                    FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_event_changes();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_events_prevent_truncate ON audit_events;
                DROP TRIGGER IF EXISTS audit_events_prevent_update_delete ON audit_events;
                DROP FUNCTION IF EXISTS prevent_audit_event_changes();
                """);

            migrationBuilder.DropTable(
                name: "audit_events");
        }
    }
}
