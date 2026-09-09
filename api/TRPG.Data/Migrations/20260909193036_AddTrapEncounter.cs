using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrapEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "target_location_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "target_location_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "trap_kind",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "trigger_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "target_location_id", table: "encounters");

            migrationBuilder.DropColumn(name: "target_location_name", table: "encounters");

            migrationBuilder.DropColumn(name: "trap_kind", table: "encounters");

            migrationBuilder.DropColumn(name: "trigger_id", table: "encounters");
        }
    }
}
