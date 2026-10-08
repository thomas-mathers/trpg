using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCreatureRouteSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM route_traveler_members WHERE route_traveler_id IN
                    (SELECT id FROM route_travelers WHERE creature_route_schedule_id IS NOT NULL);
                DELETE FROM route_travelers WHERE creature_route_schedule_id IS NOT NULL;
                DELETE FROM route_steps WHERE route_id IN (SELECT id FROM routes WHERE traversal = 'Finite');
                DELETE FROM routes WHERE traversal = 'Finite';
                """
            );

            migrationBuilder.DropTable(name: "creature_route_schedules");

            migrationBuilder.DropIndex(
                name: "ix_route_travelers_creature_route_schedule_id",
                table: "route_travelers"
            );

            migrationBuilder.DropColumn(
                name: "creature_route_schedule_id",
                table: "route_travelers"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "creature_route_schedule_id",
                table: "route_travelers",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "creature_route_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    departure_day = table.Column<string>(type: "text", nullable: false),
                    departure_hour = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    destination_creature_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    duration_hours = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    origin_creature_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_creature_route_schedules", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_travelers_creature_route_schedule_id",
                table: "route_travelers",
                column: "creature_route_schedule_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_creature_id",
                table: "creature_route_schedules",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_creature_id_destination_creature_j",
                table: "creature_route_schedules",
                columns: new[]
                {
                    "creature_id",
                    "destination_creature_job_id",
                    "departure_day",
                    "departure_hour",
                },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_destination_creature_job_id",
                table: "creature_route_schedules",
                column: "destination_creature_job_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_origin_creature_job_id",
                table: "creature_route_schedules",
                column: "origin_creature_job_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_route_id",
                table: "creature_route_schedules",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_route_schedules_world_id",
                table: "creature_route_schedules",
                column: "world_id"
            );
        }
    }
}
