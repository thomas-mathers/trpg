using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtendGuardEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LocationName moved to the Encounter base so Hostile/Guard share one column, but
            // FightEncounter never sets it — the column must allow null.
            migrationBuilder.AlterColumn<string>(
                name: "location_name",
                table: "encounters",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "city_faction_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "fine_amount",
                table: "encounters",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "guard_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "jail_hours",
                table: "encounters",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "recent_offenses",
                table: "encounters",
                type: "text[]",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "reputation_score",
                table: "encounters",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "city_faction_id", table: "encounters");

            migrationBuilder.DropColumn(name: "fine_amount", table: "encounters");

            migrationBuilder.DropColumn(name: "guard_name", table: "encounters");

            migrationBuilder.DropColumn(name: "jail_hours", table: "encounters");

            migrationBuilder.DropColumn(name: "recent_offenses", table: "encounters");

            migrationBuilder.DropColumn(name: "reputation_score", table: "encounters");

            migrationBuilder.AlterColumn<string>(
                name: "location_name",
                table: "encounters",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true
            );
        }
    }
}
