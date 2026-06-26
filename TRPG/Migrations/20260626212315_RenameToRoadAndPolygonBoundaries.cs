using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenameToRoadAndPolygonBoundaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "travel_routes");

            migrationBuilder.AlterColumn<double>(
                name: "location_coordinates_y",
                table: "persons",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<double>(
                name: "location_coordinates_x",
                table: "persons",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<double>(
                name: "location_coordinates_y",
                table: "jobs",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<double>(
                name: "location_coordinates_x",
                table: "jobs",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "roads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    danger_level = table.Column<float>(type: "real", nullable: false),
                    destination_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance = table.Column<float>(type: "real", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_time = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roads", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roads_origin_city_id_destination_city_id",
                table: "roads",
                columns: new[] { "origin_city_id", "destination_city_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roads");

            migrationBuilder.AlterColumn<int>(
                name: "location_coordinates_y",
                table: "persons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<int>(
                name: "location_coordinates_x",
                table: "persons",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<int>(
                name: "location_coordinates_y",
                table: "jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AlterColumn<int>(
                name: "location_coordinates_x",
                table: "jobs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateTable(
                name: "travel_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    danger_level = table.Column<float>(type: "real", nullable: false),
                    destination_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    distance = table.Column<float>(type: "real", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin_city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    travel_time = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_travel_routes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_travel_routes_origin_city_id_destination_city_id",
                table: "travel_routes",
                columns: new[] { "origin_city_id", "destination_city_id" },
                unique: true);
        }
    }
}
