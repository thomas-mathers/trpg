using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveQuestObjectiveRequiredAmountToBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "required_amount",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE quest_objectives
                SET required_amount = CASE
                    WHEN objective_kind = 'CollectItem' THEN COALESCE(collect_item_required_amount, 1)
                    WHEN objective_kind = 'KillCreatureType' THEN COALESCE(kill_creature_type_required_amount, 1)
                    ELSE 1
                END;
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "required_amount",
                table: "quest_objectives",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.DropColumn(
                name: "collect_item_required_amount",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(
                name: "kill_creature_type_required_amount",
                table: "quest_objectives"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "collect_item_required_amount",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "kill_creature_type_required_amount",
                table: "quest_objectives",
                type: "integer",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE quest_objectives
                SET collect_item_required_amount = CASE
                        WHEN objective_kind = 'CollectItem' THEN required_amount
                    END,
                    kill_creature_type_required_amount = CASE
                        WHEN objective_kind = 'KillCreatureType' THEN required_amount
                    END;
                """
            );

            migrationBuilder.DropColumn(name: "required_amount", table: "quest_objectives");
        }
    }
}
