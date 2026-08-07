using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenameCombatantsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_game_session_combatants",
                table: "game_session_combatants"
            );

            migrationBuilder.RenameTable(name: "game_session_combatants", newName: "combatants");

            migrationBuilder.RenameIndex(
                name: "ix_game_session_combatants_session_id",
                table: "combatants",
                newName: "ix_combatants_session_id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_combatants",
                table: "combatants",
                column: "id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "pk_combatants", table: "combatants");

            migrationBuilder.RenameTable(name: "combatants", newName: "game_session_combatants");

            migrationBuilder.RenameIndex(
                name: "ix_combatants_session_id",
                table: "game_session_combatants",
                newName: "ix_game_session_combatants_session_id"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_game_session_combatants",
                table: "game_session_combatants",
                column: "id"
            );
        }
    }
}
