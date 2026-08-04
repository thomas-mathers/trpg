using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_creatures_state_id_room_id", table: "creatures");

            migrationBuilder.DropIndex(
                name: "ix_creature_jobs_state_id_room_id",
                table: "creature_jobs"
            );

            migrationBuilder.DropColumn(name: "city_id", table: "creatures");

            migrationBuilder.DropColumn(name: "district_id", table: "creatures");

            migrationBuilder.RenameColumn(
                name: "room_id",
                table: "creatures",
                newName: "location_id"
            );

            migrationBuilder.RenameColumn(
                name: "room_id",
                table: "creature_jobs",
                newName: "location_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_room_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_location_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "rooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "districts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    district_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locations", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_location_id",
                table: "rooms",
                column: "location_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_districts_location_id",
                table: "districts",
                column: "location_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_creatures_location_id",
                table: "creatures",
                column: "location_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_locations_district_id",
                table: "locations",
                column: "district_id",
                unique: true,
                filter: "room_id IS NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_locations_room_id",
                table: "locations",
                column: "room_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_locations_world_id",
                table: "locations",
                column: "world_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "locations");

            migrationBuilder.DropIndex(name: "ix_rooms_location_id", table: "rooms");

            migrationBuilder.DropIndex(name: "ix_districts_location_id", table: "districts");

            migrationBuilder.DropIndex(name: "ix_creatures_location_id", table: "creatures");

            migrationBuilder.DropColumn(name: "location_id", table: "rooms");

            migrationBuilder.DropColumn(name: "location_id", table: "districts");

            migrationBuilder.RenameColumn(
                name: "location_id",
                table: "creatures",
                newName: "room_id"
            );

            migrationBuilder.RenameColumn(
                name: "location_id",
                table: "creature_jobs",
                newName: "room_id"
            );

            migrationBuilder.RenameIndex(
                name: "ix_creature_jobs_location_id",
                table: "creature_jobs",
                newName: "ix_creature_jobs_room_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_id",
                table: "creatures",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "district_id",
                table: "creatures",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_creatures_state_id_room_id",
                table: "creatures",
                columns: new[] { "state_id", "room_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_creature_jobs_state_id_room_id",
                table: "creature_jobs",
                columns: new[] { "state_id", "room_id" }
            );
        }
    }
}
