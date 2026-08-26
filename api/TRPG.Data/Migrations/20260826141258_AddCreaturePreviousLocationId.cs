using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreaturePreviousLocationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "arrival_origin_location_id", table: "encounters");

            migrationBuilder.AddColumn<Guid>(
                name: "previous_location_id",
                table: "creatures",
                type: "uuid",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "previous_location_id", table: "creatures");

            migrationBuilder.AddColumn<Guid>(
                name: "arrival_origin_location_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );
        }
    }
}
