using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSecretToFact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "secrets", newName: "facts");

            migrationBuilder.RenameColumn(
                name: "discovery_secret_id",
                table: "dungeon_expeditions",
                newName: "discovery_fact_id"
            );

            migrationBuilder.RenameColumn(
                name: "secret_page_number",
                table: "book_works",
                newName: "fact_page_number"
            );

            migrationBuilder.RenameColumn(
                name: "secret_id",
                table: "book_works",
                newName: "fact_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_secrets_world_id",
                table: "facts",
                newName: "ix_facts_world_id"
            );

            migrationBuilder.Sql("ALTER TABLE facts RENAME CONSTRAINT pk_secrets TO pk_facts;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE facts RENAME CONSTRAINT pk_facts TO pk_secrets;");

            migrationBuilder.RenameIndex(
                name: "ix_facts_world_id",
                table: "facts",
                newName: "ix_secrets_world_id"
            );

            migrationBuilder.RenameColumn(
                name: "discovery_fact_id",
                table: "dungeon_expeditions",
                newName: "discovery_secret_id"
            );

            migrationBuilder.RenameColumn(
                name: "fact_page_number",
                table: "book_works",
                newName: "secret_page_number"
            );

            migrationBuilder.RenameColumn(
                name: "fact_id",
                table: "book_works",
                newName: "secret_id"
            );

            migrationBuilder.RenameTable(name: "facts", newName: "secrets");
        }
    }
}
