using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcDurableFactsAndOpenThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "durable_facts",
                table: "npc_conversation_histories",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "open_threads",
                table: "npc_conversation_histories",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "durable_facts", table: "npc_conversation_histories");

            migrationBuilder.DropColumn(name: "open_threads", table: "npc_conversation_histories");
        }
    }
}
