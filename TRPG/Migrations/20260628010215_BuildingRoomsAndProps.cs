using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Migrations
{
    /// <inheritdoc />
    public partial class BuildingRoomsAndProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "boundary",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "boundary",
                table: "props");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "rooms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "faction_id",
                table: "buildings",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "description",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "faction_id",
                table: "buildings");

            migrationBuilder.AddColumn<string>(
                name: "boundary",
                table: "rooms",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "boundary",
                table: "props",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
