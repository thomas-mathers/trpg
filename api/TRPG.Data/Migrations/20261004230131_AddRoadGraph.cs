using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "road_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    @class = table.Column<string>(name: "class", type: "text", nullable: false),
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    length = table.Column<double>(type: "double precision", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waypoints = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_road_edges", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "road_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_road_nodes", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_edges_location_id",
                table: "road_edges",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_edges_world_id",
                table: "road_edges",
                column: "world_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_connector_id",
                table: "road_nodes",
                column: "connector_id",
                unique: true,
                filter: "connector_id IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_location_id",
                table: "road_nodes",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_road_nodes_world_id",
                table: "road_nodes",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "road_edges");

            migrationBuilder.DropTable(name: "road_nodes");
        }
    }
}
