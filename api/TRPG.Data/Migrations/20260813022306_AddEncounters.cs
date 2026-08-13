using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "encounter_id",
                table: "fights",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "aggression",
                table: "factions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "reputation_sensitivity",
                table: "factions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "risk_aversion",
                table: "factions",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "temperament",
                table: "factions",
                type: "text",
                nullable: false,
                defaultValue: "Authoritative"
            );

            migrationBuilder.CreateTable(
                name: "encounter_group_members",
                columns: table => new
                {
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_encounter_group_members",
                        x => new { x.encounter_group_id, x.creature_id }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "encounter_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounter_groups", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "encounters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    arrival_origin_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    encounter_type = table.Column<string>(
                        type: "character varying(13)",
                        maxLength: 13,
                        nullable: false
                    ),
                    encounter_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encounters", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_fights_encounter_id",
                table: "fights",
                column: "encounter_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounter_group_members_creature_id",
                table: "encounter_group_members",
                column: "creature_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounter_group_members_world_id",
                table: "encounter_group_members",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounter_groups_world_id",
                table: "encounter_groups",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounter_groups_world_id_location_id",
                table: "encounter_groups",
                columns: new[] { "world_id", "location_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounters_player_id_state",
                table: "encounters",
                columns: new[] { "player_id", "state" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_encounters_world_id",
                table: "encounters",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "encounter_group_members");

            migrationBuilder.DropTable(name: "encounter_groups");

            migrationBuilder.DropTable(name: "encounters");

            migrationBuilder.DropIndex(name: "ix_fights_encounter_id", table: "fights");

            migrationBuilder.DropColumn(name: "encounter_id", table: "fights");

            migrationBuilder.DropColumn(name: "aggression", table: "factions");

            migrationBuilder.DropColumn(name: "reputation_sensitivity", table: "factions");

            migrationBuilder.DropColumn(name: "risk_aversion", table: "factions");

            migrationBuilder.DropColumn(name: "temperament", table: "factions");
        }
    }
}
