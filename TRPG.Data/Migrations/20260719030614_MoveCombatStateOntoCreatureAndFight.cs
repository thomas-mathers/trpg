using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveCombatStateOntoCreatureAndFight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "combatants");

            migrationBuilder.AddColumn<string>(
                name: "active_buffs",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "active_conditions",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "active_dots",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "active_hots",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "cooldown_remaining_by_ability",
                table: "creatures",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "fights",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    combatant_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fights", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fights_world_id",
                table: "fights",
                column: "world_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fights");

            migrationBuilder.DropColumn(
                name: "active_buffs",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "active_conditions",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "active_dots",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "active_hots",
                table: "creatures");

            migrationBuilder.DropColumn(
                name: "cooldown_remaining_by_ability",
                table: "creatures");

            migrationBuilder.CreateTable(
                name: "combatants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_buffs = table.Column<string>(type: "jsonb", nullable: false),
                    active_conditions = table.Column<string>(type: "jsonb", nullable: false),
                    active_dots = table.Column<string>(type: "jsonb", nullable: false),
                    active_hots = table.Column<string>(type: "jsonb", nullable: false),
                    cooldown_remaining_by_ability = table.Column<string>(type: "jsonb", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_ap = table.Column<int>(type: "integer", nullable: false),
                    current_hp = table.Column<int>(type: "integer", nullable: false),
                    current_mp = table.Column<int>(type: "integer", nullable: false),
                    is_player = table.Column<bool>(type: "boolean", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weapon_swing_counts = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_combatants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_combatants_session_id",
                table: "combatants",
                column: "session_id");
        }
    }
}
