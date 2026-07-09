using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyNpcConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "npc_chat_messages");

            migrationBuilder.DropColumn(name: "last_summarized_index", table: "npc_conversations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "last_summarized_index",
                table: "npc_conversations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "npc_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    index = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_npc_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_npc_chat_messages_npc_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "npc_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_npc_chat_messages_conversation_id_index",
                table: "npc_chat_messages",
                columns: new[] { "conversation_id", "index" },
                unique: true
            );
        }
    }
}
