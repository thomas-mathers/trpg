using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureWeaponProficiencyPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "block_chance",
                table: "items",
                type: "real",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.CreateTable(
                name: "creature_weapon_proficiencies",
                columns: table => new
                {
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weapon_type = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proficiency = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_creature_weapon_proficiencies",
                        x => new { x.creature_id, x.weapon_type }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_weapon_proficiencies_world_id",
                table: "creature_weapon_proficiencies",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "creature_weapon_proficiencies");

            migrationBuilder.AlterColumn<int>(
                name: "block_chance",
                table: "items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "real",
                oldNullable: true
            );
        }
    }
}
