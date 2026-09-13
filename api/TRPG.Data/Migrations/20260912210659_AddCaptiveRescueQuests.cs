using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptiveRescueQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "free_creature_objective_creature_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "creature_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "props",
                type: "boolean",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "lock_level",
                table: "props",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "quest_seed_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule = table.Column<string>(type: "text", nullable: false),
                    last_sync_playtime = table.Column<TimeSpan>(type: "interval", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quest_seed_schedules", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_seed_schedules_location_id",
                table: "quest_seed_schedules",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_quest_seed_schedules_world_id",
                table: "quest_seed_schedules",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "quest_seed_schedules");

            migrationBuilder.DropColumn(
                name: "free_creature_objective_creature_id",
                table: "quest_objectives"
            );

            migrationBuilder.DropColumn(name: "creature_id", table: "props");

            migrationBuilder.DropColumn(name: "is_locked", table: "props");

            migrationBuilder.DropColumn(name: "lock_level", table: "props");
        }
    }
}
