using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cities");

            migrationBuilder.RenameColumn(
                name: "origin_city_id",
                table: "roads",
                newName: "origin_region_id"
            );

            migrationBuilder.RenameColumn(
                name: "destination_city_id",
                table: "roads",
                newName: "destination_region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_roads_origin_city_id_destination_city_id",
                table: "roads",
                newName: "ix_roads_origin_region_id_destination_region_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_city_id",
                table: "persons",
                newName: "location_region_id"
            );

            migrationBuilder.RenameColumn(
                name: "birth_city_id",
                table: "persons",
                newName: "birth_region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_persons_location_city_id_location_building_id",
                table: "persons",
                newName: "ix_persons_location_region_id_location_building_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_city_id",
                table: "jobs",
                newName: "location_region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_location_city_id_location_building_id",
                table: "jobs",
                newName: "ix_jobs_location_region_id_location_building_id"
            );

            migrationBuilder.RenameColumn(
                name: "city_id",
                table: "buildings",
                newName: "region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_buildings_city_id_name",
                table: "buildings",
                newName: "ix_buildings_region_id_name"
            );

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    is_capital = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    region_type = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_regions_country_id_name",
                table: "regions",
                columns: new[] { "country_id", "name" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "regions");

            migrationBuilder.RenameColumn(
                name: "origin_region_id",
                table: "roads",
                newName: "origin_city_id"
            );

            migrationBuilder.RenameColumn(
                name: "destination_region_id",
                table: "roads",
                newName: "destination_city_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_roads_origin_region_id_destination_region_id",
                table: "roads",
                newName: "ix_roads_origin_city_id_destination_city_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_region_id",
                table: "persons",
                newName: "location_city_id"
            );

            migrationBuilder.RenameColumn(
                name: "birth_region_id",
                table: "persons",
                newName: "birth_city_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_persons_location_region_id_location_building_id",
                table: "persons",
                newName: "ix_persons_location_city_id_location_building_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_region_id",
                table: "jobs",
                newName: "location_city_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_jobs_location_region_id_location_building_id",
                table: "jobs",
                newName: "ix_jobs_location_city_id_location_building_id"
            );

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "buildings",
                newName: "city_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_buildings_region_id_name",
                table: "buildings",
                newName: "ix_buildings_city_id_name"
            );

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    is_capital = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_cities_country_id_name",
                table: "cities",
                columns: new[] { "country_id", "name" },
                unique: true
            );
        }
    }
}
