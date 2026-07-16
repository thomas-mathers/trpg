using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePlaytimeAndDropTurnFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "did_move_this_turn", table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "did_scene_refresh_this_turn",
                table: "game_sessions"
            );

            migrationBuilder.RenameColumn(
                name: "banked_playtime",
                table: "game_sessions",
                newName: "playtime"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "playtime",
                table: "game_sessions",
                newName: "banked_playtime"
            );

            migrationBuilder.AddColumn<bool>(
                name: "did_move_this_turn",
                table: "game_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "did_scene_refresh_this_turn",
                table: "game_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
