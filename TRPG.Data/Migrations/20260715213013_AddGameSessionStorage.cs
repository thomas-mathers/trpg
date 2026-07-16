using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessionStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_session_combatants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_player = table.Column<bool>(type: "boolean", nullable: false),
                    current_hp = table.Column<int>(type: "integer", nullable: false),
                    current_ap = table.Column<int>(type: "integer", nullable: false),
                    current_mp = table.Column<int>(type: "integer", nullable: false),
                    weapon_swing_counts = table.Column<string>(type: "jsonb", nullable: false),
                    active_conditions = table.Column<string>(type: "jsonb", nullable: false),
                    cooldown_remaining_by_ability = table.Column<string>(
                        type: "jsonb",
                        nullable: false
                    ),
                    active_dots = table.Column<string>(type: "jsonb", nullable: false),
                    active_hots = table.Column<string>(type: "jsonb", nullable: false),
                    active_buffs = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_session_combatants", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "game_session_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    message_json = table.Column<string>(type: "json", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_session_messages", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    banked_playtime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    did_move_this_turn = table.Column<bool>(type: "boolean", nullable: false),
                    did_scene_refresh_this_turn = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                    open_conversation_creature_ids_by_name = table.Column<string>(
                        type: "jsonb",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_sessions", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_game_session_combatants_session_id",
                table: "game_session_combatants",
                column: "session_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_game_session_messages_session_id_ordinal",
                table: "game_session_messages",
                columns: new[] { "session_id", "ordinal" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_game_session_messages_session_id_role",
                table: "game_session_messages",
                columns: new[] { "session_id", "role" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_world_id",
                table: "game_sessions",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "game_session_combatants");

            migrationBuilder.DropTable(name: "game_session_messages");

            migrationBuilder.DropTable(name: "game_sessions");
        }
    }
}
