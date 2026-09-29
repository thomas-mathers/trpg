using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class TimeBasedCombatEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "cooldown_remaining_by_ability",
                table: "creatures",
                newName: "cooldown_ready_at_by_ability"
            );

            migrationBuilder.Sql(
                """
                UPDATE creatures
                SET active_conditions = '{}'::jsonb,
                    cooldown_ready_at_by_ability = '{}'::jsonb,
                    active_dots = '[]'::jsonb,
                    active_hots = '[]'::jsonb,
                    active_buffs = '[]'::jsonb
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "cooldown_ready_at_by_ability",
                table: "creatures",
                newName: "cooldown_remaining_by_ability"
            );
        }
    }
}
