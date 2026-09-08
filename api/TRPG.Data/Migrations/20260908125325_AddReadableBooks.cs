using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReadableBooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "work_id",
                table: "items",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "book_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_pages", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "book_works",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    subject_name = table.Column<string>(type: "text", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    secret_id = table.Column<Guid>(type: "uuid", nullable: true),
                    secret_page_number = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_works", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "secrets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_secrets", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_book_pages_work_id_page_number",
                table: "book_pages",
                columns: new[] { "work_id", "page_number" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_book_pages_world_id",
                table: "book_pages",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_book_works_world_id",
                table: "book_works",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ux_book_works_world_title",
                table: "book_works",
                columns: new[] { "world_id", "title" },
                unique: true,
                filter: "tier = 'Flavour'"
            );

            migrationBuilder.CreateIndex(
                name: "ix_secrets_world_id",
                table: "secrets",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "book_pages");

            migrationBuilder.DropTable(name: "book_works");

            migrationBuilder.DropTable(name: "secrets");

            migrationBuilder.DropColumn(name: "work_id", table: "items");
        }
    }
}
