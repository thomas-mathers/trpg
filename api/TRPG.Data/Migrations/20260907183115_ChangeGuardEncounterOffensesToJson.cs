using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGuardEncounterOffensesToJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // text[] has no cast to jsonb, and an offence list is a rendering of crimes that
            // outlive it, so an in-flight encounter simply loses its charge sheet.
            migrationBuilder.DropColumn(name: "recent_offenses", table: "encounters");

            migrationBuilder.AddColumn<string>(
                name: "recent_offenses",
                table: "encounters",
                type: "jsonb",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "recent_offenses", table: "encounters");

            migrationBuilder.AddColumn<List<string>>(
                name: "recent_offenses",
                table: "encounters",
                type: "text[]",
                nullable: true
            );
        }
    }
}
