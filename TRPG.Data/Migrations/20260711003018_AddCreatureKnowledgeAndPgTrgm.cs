using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureKnowledgeAndPgTrgm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "creature_knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    knower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_knowledge", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creature_knowledge_knower_id_subject_type",
                table: "creature_knowledge",
                columns: new[] { "knower_id", "subject_type" });

            migrationBuilder.CreateIndex(
                name: "ix_creature_knowledge_world_id",
                table: "creature_knowledge",
                column: "world_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creature_knowledge");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
