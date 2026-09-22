using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardPatrols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guard_patrol_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_traveler_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_guard_patrol_members", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_guard_patrol_members_creature_id",
                table: "guard_patrol_members",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guard_patrol_members_route_traveler_id",
                table: "guard_patrol_members",
                column: "route_traveler_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_guard_patrol_members_world_id",
                table: "guard_patrol_members",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "guard_patrol_members");
        }
    }
}
