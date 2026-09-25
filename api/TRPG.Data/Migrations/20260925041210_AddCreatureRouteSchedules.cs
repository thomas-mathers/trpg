using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureRouteSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_creature_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_creature_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    departure_day = table.Column<string>(type: "text", nullable: false),
                    departure_hour = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    duration_hours = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    purpose = table.Column<string>(type: "text", nullable: false),
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
                name: "ix_route_steps_world_id_location_id_route_id",
                table: "route_steps",
                columns: new[] { "world_id", "location_id", "route_id" }
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "creature_route_schedules");

            migrationBuilder.DropIndex(
                name: "ix_route_travelers_creature_route_schedule_id",
                table: "route_travelers"
            );

            migrationBuilder.DropIndex(
                name: "ix_route_steps_world_id_location_id_route_id",
                table: "route_steps"
            );

            migrationBuilder.DropColumn(
                name: "creature_route_schedule_id",
                table: "route_travelers"
            );
        }
    }
}
