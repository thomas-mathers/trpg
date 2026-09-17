using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFactDisclosureReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "required_fact_id",
                table: "quests",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "reason_fact_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "fact_disclosure_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fact_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fact_disclosure_attempts", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_fact_disclosure_attempts_player_id_npc_id_fact_id",
                table: "fact_disclosure_attempts",
                columns: new[] { "player_id", "npc_id", "fact_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_fact_disclosure_attempts_world_id",
                table: "fact_disclosure_attempts",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "fact_disclosure_attempts");

            migrationBuilder.DropColumn(name: "required_fact_id", table: "quests");

            migrationBuilder.DropColumn(name: "reason_fact_id", table: "quest_objectives");
        }
    }
}
