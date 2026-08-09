using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropCreatureAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "creature_abilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creature_abilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ability_name = table.Column<string>(type: "text", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_abilities", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_abilities_creature_id_ability_name",
                table: "creature_abilities",
                columns: new[] { "creature_id", "ability_name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_abilities_world_id",
                table: "creature_abilities",
                column: "world_id"
            );
        }
    }
}
