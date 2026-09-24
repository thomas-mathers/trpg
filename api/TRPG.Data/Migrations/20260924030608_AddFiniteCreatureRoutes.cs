using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFiniteCreatureRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "route_stops");

            migrationBuilder.DropColumn(name: "linger_hours", table: "routes");

            migrationBuilder.DropColumn(name: "direction", table: "route_travelers");

            migrationBuilder.DropColumn(name: "kind", table: "route_travelers");

            migrationBuilder.RenameColumn(
                name: "phase_offset_hours",
                table: "route_travelers",
                newName: "speed_units_per_hour"
            );

            migrationBuilder.AddColumn<string>(
                name: "traversal",
                table: "routes",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "started_at_playtime",
                table: "route_travelers",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.CreateTable(
                name: "route_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dwell_hours = table.Column<double>(type: "double precision", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_steps", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_connector_id",
                table: "route_steps",
                column: "connector_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id",
                table: "route_steps",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id_sequence_index",
                table: "route_steps",
                columns: new[] { "route_id", "sequence_index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_world_id",
                table: "route_steps",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "route_steps");

            migrationBuilder.DropColumn(name: "traversal", table: "routes");

            migrationBuilder.DropColumn(name: "started_at_playtime", table: "route_travelers");

            migrationBuilder.RenameColumn(
                name: "speed_units_per_hour",
                table: "route_travelers",
                newName: "phase_offset_hours"
            );

            migrationBuilder.AddColumn<double>(
                name: "linger_hours",
                table: "routes",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "route_travelers",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "route_travelers",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateTable(
                name: "route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance_to_next_stop = table.Column<float>(type: "real", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_stops", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_route_id",
                table: "route_stops",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_stops_route_id_sequence_index",
                table: "route_stops",
                columns: new[] { "route_id", "sequence_index" },
                unique: true
            );
        }
    }
}
