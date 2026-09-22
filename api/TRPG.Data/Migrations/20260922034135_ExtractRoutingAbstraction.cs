using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtractRoutingAbstraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "caravan_route_stops");

            migrationBuilder.DropTable(name: "caravan_routes");

            migrationBuilder.DropTable(name: "caravans");

            migrationBuilder.RenameColumn(
                name: "caravan_id",
                table: "caravan_tickets",
                newName: "route_traveler_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_caravan_tickets_caravan_id",
                table: "caravan_tickets",
                newName: "ix_caravan_tickets_route_traveler_id"
            );

            migrationBuilder.CreateTable(
                name: "caravan_fares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_fee_gold = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_fares", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance_to_next_stop = table.Column<float>(type: "real", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_stops", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "route_travelers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_offset_hours = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    direction = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_travelers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    linger_hours = table.Column<double>(type: "double precision", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_routes", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_fares_route_id",
                table: "caravan_fares",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_fares_world_id",
                table: "caravan_fares",
                column: "world_id"
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

            migrationBuilder.CreateIndex(
                name: "ix_route_travelers_route_id",
                table: "route_travelers",
                column: "route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_route_travelers_world_id",
                table: "route_travelers",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_routes_world_id",
                table: "routes",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "caravan_fares");

            migrationBuilder.DropTable(name: "route_stops");

            migrationBuilder.DropTable(name: "route_travelers");

            migrationBuilder.DropTable(name: "routes");

            migrationBuilder.RenameColumn(
                name: "route_traveler_id",
                table: "caravan_tickets",
                newName: "caravan_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_caravan_tickets_route_traveler_id",
                table: "caravan_tickets",
                newName: "ix_caravan_tickets_caravan_id"
            );

            migrationBuilder.CreateTable(
                name: "caravan_route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caravan_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance_to_next_stop = table.Column<float>(type: "real", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_route_stops", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "caravan_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    linger_hours = table.Column<double>(type: "double precision", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    ticket_fee_gold = table.Column<int>(type: "integer", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_routes", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "caravans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caravan_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "text", nullable: false),
                    phase_offset_hours = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravans", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_route_stops_caravan_route_id",
                table: "caravan_route_stops",
                column: "caravan_route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_route_stops_caravan_route_id_sequence_index",
                table: "caravan_route_stops",
                columns: new[] { "caravan_route_id", "sequence_index" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_routes_world_id",
                table: "caravan_routes",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravans_caravan_route_id",
                table: "caravans",
                column: "caravan_route_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravans_world_id",
                table: "caravans",
                column: "world_id"
            );
        }
    }
}
