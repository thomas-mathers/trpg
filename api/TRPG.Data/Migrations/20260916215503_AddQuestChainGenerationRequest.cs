using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestChainGenerationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quest_chain_generation_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_chain_generation_requests", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_chain_generation_requests_world_id",
                table: "quest_chain_generation_requests",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_chain_generation_requests_world_id_player_id_status",
                table: "quest_chain_generation_requests",
                columns: new[] { "world_id", "player_id", "status" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "quest_chain_generation_requests");
        }
    }
}
