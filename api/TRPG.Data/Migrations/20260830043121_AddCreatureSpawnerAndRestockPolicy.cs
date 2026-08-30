using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureSpawnerAndRestockPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "spawner_id",
                table: "creatures",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "creature_spawners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archetype_creature_types = table.Column<string>(type: "jsonb", nullable: false),
                    max_population = table.Column<int>(type: "integer", nullable: false),
                    trigger_hour = table.Column<int>(type: "integer", nullable: false),
                    specific_day = table.Column<string>(type: "text", nullable: true),
                    last_sync_playtime = table.Column<TimeSpan>(type: "interval", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_spawners", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "restock_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workstation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_hour = table.Column<int>(type: "integer", nullable: false),
                    specific_day = table.Column<string>(type: "text", nullable: true),
                    last_sync_playtime = table.Column<TimeSpan>(type: "interval", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_restock_policies", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creatures_spawner_id",
                table: "creatures",
                column: "spawner_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_spawners_location_id",
                table: "creature_spawners",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_spawners_world_id",
                table: "creature_spawners",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_restock_policies_workstation_id",
                table: "restock_policies",
                column: "workstation_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_restock_policies_world_id",
                table: "restock_policies",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "creature_spawners");

            migrationBuilder.DropTable(name: "restock_policies");

            migrationBuilder.DropIndex(name: "ix_creatures_spawner_id", table: "creatures");

            migrationBuilder.DropColumn(name: "spawner_id", table: "creatures");
        }
    }
}
