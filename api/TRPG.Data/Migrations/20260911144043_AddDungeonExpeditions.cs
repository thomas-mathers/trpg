using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDungeonExpeditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dungeon_expeditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survivor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    companion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entrance_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    companion_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discovery_secret_id = table.Column<Guid>(type: "uuid", nullable: false),
                    survivor_name = table.Column<string>(type: "text", nullable: false),
                    companion_name = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    separation = table.Column<string>(type: "text", nullable: false),
                    final_experience = table.Column<string>(type: "text", nullable: false),
                    discovery = table.Column<string>(type: "text", nullable: false),
                    known_route_location_ids = table.Column<List<Guid>>(
                        type: "uuid[]",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dungeon_expeditions", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_expeditions_building_id",
                table: "dungeon_expeditions",
                column: "building_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_expeditions_journal_work_id",
                table: "dungeon_expeditions",
                column: "journal_work_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_dungeon_expeditions_world_id",
                table: "dungeon_expeditions",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "dungeon_expeditions");
        }
    }
}
