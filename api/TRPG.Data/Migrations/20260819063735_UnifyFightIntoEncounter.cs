using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyFightIntoEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fights");

            migrationBuilder.RenameColumn(
                name: "encounter_group_id",
                table: "encounters",
                newName: "faction_id"
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "combatant_ids",
                table: "encounters",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "faction_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "location_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "members",
                table: "encounters",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "outcome",
                table: "encounters",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "combatant_ids", table: "encounters");

            migrationBuilder.DropColumn(name: "faction_name", table: "encounters");

            migrationBuilder.DropColumn(name: "location_name", table: "encounters");

            migrationBuilder.DropColumn(name: "members", table: "encounters");

            migrationBuilder.DropColumn(name: "outcome", table: "encounters");

            migrationBuilder.RenameColumn(
                name: "faction_id",
                table: "encounters",
                newName: "encounter_group_id"
            );

            migrationBuilder.CreateTable(
                name: "fights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    combatant_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    completed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    encounter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fights", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_fights_encounter_id",
                table: "fights",
                column: "encounter_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_fights_player_id",
                table: "fights",
                column: "player_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_fights_world_id",
                table: "fights",
                column: "world_id"
            );
        }
    }
}
