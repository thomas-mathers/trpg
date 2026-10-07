using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureEntryWalk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "entered_at",
                table: "creatures",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "entry_x",
                table: "creatures",
                type: "double precision",
                nullable: true
            );

            migrationBuilder.AddColumn<double>(
                name: "entry_y",
                table: "creatures",
                type: "double precision",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "entered_at", table: "creatures");

            migrationBuilder.DropColumn(name: "entry_x", table: "creatures");

            migrationBuilder.DropColumn(name: "entry_y", table: "creatures");
        }
    }
}
