using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspicionEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cause",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "suspicion_encounter_city_faction_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "suspicion_encounter_guard_creature_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "suspicion_encounter_guard_name",
                table: "encounters",
                type: "text",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "cause", table: "encounters");

            migrationBuilder.DropColumn(
                name: "suspicion_encounter_city_faction_id",
                table: "encounters"
            );

            migrationBuilder.DropColumn(
                name: "suspicion_encounter_guard_creature_id",
                table: "encounters"
            );

            migrationBuilder.DropColumn(
                name: "suspicion_encounter_guard_name",
                table: "encounters"
            );
        }
    }
}
