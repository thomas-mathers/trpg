using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenameChatMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_game_session_messages",
                table: "game_session_messages"
            );

            migrationBuilder.RenameTable(name: "game_session_messages", newName: "chat_messages");

            migrationBuilder.RenameIndex(
                name: "ix_game_session_messages_session_id_role",
                table: "chat_messages",
                newName: "ix_chat_messages_session_id_role"
            );

            migrationBuilder.RenameIndex(
                name: "ix_game_session_messages_session_id_ordinal",
                table: "chat_messages",
                newName: "ix_chat_messages_session_id_ordinal"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_chat_messages",
                table: "chat_messages",
                column: "id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(name: "pk_chat_messages", table: "chat_messages");

            migrationBuilder.RenameTable(name: "chat_messages", newName: "game_session_messages");

            migrationBuilder.RenameIndex(
                name: "ix_chat_messages_session_id_role",
                table: "game_session_messages",
                newName: "ix_game_session_messages_session_id_role"
            );

            migrationBuilder.RenameIndex(
                name: "ix_chat_messages_session_id_ordinal",
                table: "game_session_messages",
                newName: "ix_game_session_messages_session_id_ordinal"
            );

            migrationBuilder.AddPrimaryKey(
                name: "pk_game_session_messages",
                table: "game_session_messages",
                column: "id"
            );
        }
    }
}
