using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestCasting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "chain_antagonist_faction_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "chain_giver_faction_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "is_chain_terminal",
                table: "quests",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<Guid>(
                name: "membership_reward_faction_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "required_faction_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "factions",
                type: "text",
                nullable: false,
                defaultValue: "Unclassified"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "faction_id",
                table: "creature_spawners",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "faction_standings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    other_faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faction_standings", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_factions_world_id_kind",
                table: "factions",
                columns: new[] { "world_id", "kind" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_faction_standings_faction_id_other_faction_id",
                table: "faction_standings",
                columns: new[] { "faction_id", "other_faction_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_faction_standings_world_id",
                table: "faction_standings",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "faction_standings");

            migrationBuilder.DropIndex(name: "ix_factions_world_id_kind", table: "factions");

            migrationBuilder.DropColumn(name: "chain_antagonist_faction_id", table: "quests");

            migrationBuilder.DropColumn(name: "chain_giver_faction_id", table: "quests");

            migrationBuilder.DropColumn(name: "is_chain_terminal", table: "quests");

            migrationBuilder.DropColumn(name: "membership_reward_faction_id", table: "quests");

            migrationBuilder.DropColumn(name: "required_faction_id", table: "quests");

            migrationBuilder.DropColumn(name: "kind", table: "factions");

            migrationBuilder.DropColumn(name: "faction_id", table: "creature_spawners");
        }
    }
}
