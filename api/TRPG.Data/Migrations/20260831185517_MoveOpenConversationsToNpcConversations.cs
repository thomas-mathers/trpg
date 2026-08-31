using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveOpenConversationsToNpcConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "open_conversation_creature_ids_by_name",
                table: "game_sessions"
            );

            migrationBuilder.CreateTable(
                name: "npc_conversation_session_states",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    open_conversation_creature_ids_by_name = table.Column<string>(
                        type: "jsonb",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_conversation_session_states", x => x.session_id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_conversation_session_states_world_id",
                table: "npc_conversation_session_states",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "npc_conversation_session_states");

            migrationBuilder.AddColumn<string>(
                name: "open_conversation_creature_ids_by_name",
                table: "game_sessions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}"
            );
        }
    }
}
