using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaravans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caravan_route_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caravan_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_index = table.Column<int>(type: "integer", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance_to_next_stop = table.Column<float>(type: "real", nullable: false),
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
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    ticket_fee_gold = table.Column<int>(type: "integer", nullable: false),
                    linger_hours = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_routes", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "caravan_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caravan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_stop_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchased_at_playtime = table.Column<TimeSpan>(
                        type: "interval",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caravan_tickets", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "caravans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caravan_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_offset_hours = table.Column<double>(
                        type: "double precision",
                        nullable: false
                    ),
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
                name: "ix_caravan_tickets_caravan_id",
                table: "caravan_tickets",
                column: "caravan_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_tickets_creature_id",
                table: "caravan_tickets",
                column: "creature_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_caravan_tickets_world_id",
                table: "caravan_tickets",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "caravan_route_stops");

            migrationBuilder.DropTable(name: "caravan_routes");

            migrationBuilder.DropTable(name: "caravan_tickets");

            migrationBuilder.DropTable(name: "caravans");
        }
    }
}
