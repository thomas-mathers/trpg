using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class BuildingRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "building_id", table: "props", newName: "room_id");

            migrationBuilder.RenameIndex(
                name: "ix_props_building_id",
                table: "props",
                newName: "ix_props_room_id"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "chest_key_item_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "destination_room_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "staircase_destination_room_id",
                table: "props",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "building_rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_building_rooms", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_building_rooms_building_id",
                table: "building_rooms",
                column: "building_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "building_rooms");

            migrationBuilder.DropColumn(name: "chest_key_item_id", table: "props");

            migrationBuilder.DropColumn(name: "destination_room_id", table: "props");

            migrationBuilder.DropColumn(name: "staircase_destination_room_id", table: "props");

            migrationBuilder.RenameColumn(name: "room_id", table: "props", newName: "building_id");

            migrationBuilder.RenameIndex(
                name: "ix_props_room_id",
                table: "props",
                newName: "ix_props_building_id"
            );
        }
    }
}
