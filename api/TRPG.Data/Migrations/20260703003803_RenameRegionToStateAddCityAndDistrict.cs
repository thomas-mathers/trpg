using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class RenameRegionToStateAddCityAndDistrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "regions");

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "world_events",
                newName: "state_id"
            );

            migrationBuilder.RenameColumn(
                name: "origin_region_id",
                table: "roads",
                newName: "origin_state_id"
            );

            migrationBuilder.RenameColumn(
                name: "destination_region_id",
                table: "roads",
                newName: "destination_state_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_roads_origin_region_id_destination_region_id",
                table: "roads",
                newName: "ix_roads_origin_state_id_destination_state_id"
            );

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "quest_objectives",
                newName: "state_id"
            );

            migrationBuilder.RenameColumn(name: "region_id", table: "persons", newName: "state_id");

            migrationBuilder.RenameColumn(
                name: "birth_region_id",
                table: "persons",
                newName: "birth_state_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_persons_region_id_room_id",
                table: "persons",
                newName: "ix_persons_state_id_room_id"
            );

            migrationBuilder.RenameColumn(name: "region_id", table: "jobs", newName: "state_id");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_region_id_room_id",
                table: "jobs",
                newName: "ix_jobs_state_id_room_id"
            );

            migrationBuilder.RenameColumn(
                name: "region_id",
                table: "buildings",
                newName: "state_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_buildings_region_id_name",
                table: "buildings",
                newName: "ix_buildings_state_id_name"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "persons",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "persons",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "buildings",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "buildings",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    is_capital = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    district_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_districts", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                    center = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_states", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_cities_country_id_name",
                table: "cities",
                columns: new[] { "country_id", "name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_cities_state_id",
                table: "cities",
                column: "state_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_districts_city_id",
                table: "districts",
                column: "city_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_districts_city_id_district_type",
                table: "districts",
                columns: new[] { "city_id", "district_type" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_states_country_id_name",
                table: "states",
                columns: new[] { "country_id", "name" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "cities");

            migrationBuilder.DropTable(name: "districts");

            migrationBuilder.DropTable(name: "states");

            migrationBuilder.DropColumn(name: "city_id", table: "persons");

            migrationBuilder.DropColumn(name: "district_id", table: "persons");

            migrationBuilder.DropColumn(name: "city_id", table: "buildings");

            migrationBuilder.DropColumn(name: "district_id", table: "buildings");

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "world_events",
                newName: "region_id"
            );

            migrationBuilder.RenameColumn(
                name: "origin_state_id",
                table: "roads",
                newName: "origin_region_id"
            );

            migrationBuilder.RenameColumn(
                name: "destination_state_id",
                table: "roads",
                newName: "destination_region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_roads_origin_state_id_destination_state_id",
                table: "roads",
                newName: "ix_roads_origin_region_id_destination_region_id"
            );

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "quest_objectives",
                newName: "region_id"
            );

            migrationBuilder.RenameColumn(name: "state_id", table: "persons", newName: "region_id");

            migrationBuilder.RenameColumn(
                name: "birth_state_id",
                table: "persons",
                newName: "birth_region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_persons_state_id_room_id",
                table: "persons",
                newName: "ix_persons_region_id_room_id"
            );

            migrationBuilder.RenameColumn(name: "state_id", table: "jobs", newName: "region_id");

            migrationBuilder.RenameIndex(
                name: "ix_jobs_state_id_room_id",
                table: "jobs",
                newName: "ix_jobs_region_id_room_id"
            );

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "buildings",
                newName: "region_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_buildings_state_id_name",
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
    }
}
