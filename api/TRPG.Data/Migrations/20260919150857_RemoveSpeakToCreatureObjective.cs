using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSpeakToCreatureObjective : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM creature_quest_objectives AS progress
                USING quest_objectives AS objective
                WHERE progress.objective_id = objective.id
                    AND objective.objective_kind = 'SpeakToCreature';

                DELETE FROM quest_objectives
                WHERE objective_kind = 'SpeakToCreature';

                DELETE FROM creature_quests AS creature_quest
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM quest_objectives AS objective
                    WHERE objective.quest_id = creature_quest.quest_id
                );

                DELETE FROM quests AS quest
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM quest_objectives AS objective
                    WHERE objective.quest_id = quest.id
                );

                UPDATE quests AS quest
                SET prerequisite_quest_ids = ARRAY(
                    SELECT prerequisite_quest_id
                    FROM unnest(quest.prerequisite_quest_ids) AS prerequisite_quest_id
                    WHERE EXISTS (
                        SELECT 1
                        FROM quests AS existing_quest
                        WHERE existing_quest.id = prerequisite_quest_id
                    )
                );
                """
            );

            migrationBuilder.DropColumn(
                name: "speak_to_creature_objective_creature_id",
                table: "quest_objectives"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "speak_to_creature_objective_creature_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );
        }
    }
}
