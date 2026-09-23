using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TRPG.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShakedownEncounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "shakedown_encounter_faction_id",
                table: "encounters",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "shakedown_encounter_faction_name",
                table: "encounters",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "shakedown_encounter_members",
                table: "encounters",
                type: "jsonb",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "toll_amount",
                table: "encounters",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "shakedown_encounter_faction_id",
                table: "encounters"
            );

            migrationBuilder.DropColumn(
                name: "shakedown_encounter_faction_name",
                table: "encounters"
            );

            migrationBuilder.DropColumn(name: "shakedown_encounter_members", table: "encounters");

            migrationBuilder.DropColumn(name: "toll_amount", table: "encounters");
        }
    }
}
