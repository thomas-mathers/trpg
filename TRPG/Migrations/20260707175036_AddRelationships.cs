using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relative_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relationships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_relationships_relative_id",
                table: "relationships",
                column: "relative_id");

            migrationBuilder.CreateIndex(
                name: "ix_relationships_subject_id_relative_id_relationship_type",
                table: "relationships",
                columns: new[] { "subject_id", "relative_id", "relationship_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_relationships_world_id",
                table: "relationships",
                column: "world_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relationships");
        }
    }
}
