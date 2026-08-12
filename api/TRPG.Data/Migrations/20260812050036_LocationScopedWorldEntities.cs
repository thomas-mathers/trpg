using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocationScopedWorldEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_buildings_state_id_name", table: "buildings");

            migrationBuilder.DropColumn(name: "building_id", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "city_id", table: "quest_objectives");

            migrationBuilder.DropColumn(name: "birth_state_id", table: "creatures");

            migrationBuilder.DropColumn(name: "state_id", table: "creature_jobs");

            migrationBuilder.DropColumn(name: "city_id", table: "buildings");

            migrationBuilder.DropColumn(name: "district_id", table: "buildings");

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "world_events",
                newName: "location_id"
            );

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "creatures",
                newName: "birth_location_id"
            );

            migrationBuilder.RenameColumn(
                name: "state_id",
                table: "buildings",
                newName: "exterior_location_id"
            );

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "locations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "locations",
                type: "text",
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.CreateIndex(
                name: "ix_world_events_location_id",
                table: "world_events",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_locations_state_id",
                table: "locations",
                column: "state_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_buildings_exterior_location_id",
                table: "buildings",
                column: "exterior_location_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_world_events_location_id", table: "world_events");

            migrationBuilder.DropIndex(name: "ix_locations_state_id", table: "locations");

            migrationBuilder.DropIndex(
                name: "ix_buildings_exterior_location_id",
                table: "buildings"
            );

            migrationBuilder.DropColumn(name: "description", table: "locations");

            migrationBuilder.DropColumn(name: "kind", table: "locations");

            migrationBuilder.RenameColumn(
                name: "location_id",
                table: "world_events",
                newName: "state_id"
            );

            migrationBuilder.RenameColumn(
                name: "birth_location_id",
                table: "creatures",
                newName: "state_id"
            );

            migrationBuilder.RenameColumn(
                name: "exterior_location_id",
                table: "buildings",
                newName: "state_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "building_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "quest_objectives",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "birth_state_id",
                table: "creatures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "state_id",
                table: "creature_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
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

            migrationBuilder.CreateIndex(
                name: "ix_buildings_state_id_name",
                table: "buildings",
                columns: new[] { "state_id", "name" },
                unique: true
            );
        }
    }
}
