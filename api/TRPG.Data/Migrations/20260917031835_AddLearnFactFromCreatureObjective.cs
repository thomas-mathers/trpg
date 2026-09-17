using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnFactFromCreatureObjective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve KillCreatureObjective data after LearnFactFromCreatureObjective claims creature_id.
            migrationBuilder.RenameColumn(
                name: "creature_id",
                table: "quest_objectives",
                newName: "kill_creature_objective_creature_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "creature_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "base_willingness",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "bribe_willingness",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "fact_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "intimidation_willingness",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<List<Guid>>(
                name: "required_supporting_quest_ids",
                table: "quest_objectives",
                type: "uuid[]",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "weighted_supporting_quest_ids",
                table: "quest_objectives",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "fact_disclosure_lockouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approach = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fact_disclosure_lockouts", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_fact_disclosure_lockouts_player_id_npc_id_fact_id_approach",
                table: "fact_disclosure_lockouts",
                columns: new[] { "player_id", "npc_id", "fact_id", "approach" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_fact_disclosure_lockouts_world_id",
                table: "fact_disclosure_lockouts",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fact_disclosure_lockouts");

            migrationBuilder.DropColumn(name: "creature_id", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "base_willingness", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "bribe_willingness", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "fact_id", table: "quest_objectives");

            migrationBuilder.DropColumn(
                name: "intimidation_willingness",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(
                name: "required_supporting_quest_ids",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(
                name: "weighted_supporting_quest_ids",
                table: "quest_objectives"
            );

            migrationBuilder.RenameColumn(
                name: "kill_creature_objective_creature_id",
                table: "quest_objectives",
                newName: "creature_id"
            );
        }
    }
}
